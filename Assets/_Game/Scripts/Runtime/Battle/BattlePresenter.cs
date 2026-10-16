using System;
using System.Collections;
using System.Collections.Generic;
using Game.Core.Battle;
using UnityEngine;

namespace Game.Runtime
{
    /// Plays each event batch from BattleService on the stage, in order, in one coroutine on the flow host
    /// (ARCHITECTURE §14.4): the hit recipe for every Attacked, the heal, defend, victory and defeat presentations,
    /// and every HUD value at the moment its event plays. At a hero's TurnStarted it opens the command panel and waits
    /// for the next batch; an enemy's TurnStarted gets a beat so kids see whose turn it is. Plain C#, no serialized
    /// fields, no physics; Core's state is only read as the end-of-batch snapshot.
    public sealed class BattlePresenter
    {
        const float CrossFadeSeconds = 0.1f;
        const float DefendExitSeconds = 0.2f;
        const float FreezeSeconds = 0.08f;
        const float WeaknessFreezeSeconds = 0.10f;
        const float KnockbackMetres = 0.35f;
        const float WeaknessKnockbackMetres = 0.5f;
        const float KnockbackOutSeconds = 0.25f;
        const float KnockbackBackSeconds = 0.35f;
        const int FlashFrames = 2;
        const float ImpulseForce = 1f;
        const float WeaknessImpulseForce = 1.6f;
        const float HoldAfterImpactSeconds = 0.7f;
        const float EnemyTurnBeatSeconds = 0.6f;
        const float VictoryCheerSeconds = 1.2f;
        // Used only if a prefab's controller is not the generated override controller and no Defeat length is known.
        const float FallbackClipSeconds = 1.0f;
        const string Tag = "[BattlePresenter] ";

        static readonly int IdleHash = Animator.StringToHash("Idle");
        static readonly int AttackHash = Animator.StringToHash("Attack");
        static readonly int SkillHash = Animator.StringToHash("Skill");
        static readonly int HitHash = Animator.StringToHash("Hit");
        static readonly int BigStaggerHash = Animator.StringToHash("BigStagger");
        static readonly int DefendHash = Animator.StringToHash("Defend");
        static readonly int DefeatHash = Animator.StringToHash("Defeat");
        static readonly int VictoryHash = Animator.StringToHash("Victory");
        static readonly int FlashAmountId = Shader.PropertyToID("_FlashAmount");

        sealed class ActorRecord
        {
            public CombatantActor Actor = null!;
            public Team Team;
            public DamageType AttackType;
            public DamageType SkillType;
            public int SkillCooldownTurns;
            public string AttackClip = "";
            public string SkillClip = "";
            public float AttackLength = FallbackClipSeconds;
            public float SkillLength = FallbackClipSeconds;
            public float DefeatLength = FallbackClipSeconds;
            public bool Defending;
            public bool Down;
            public Coroutine? Knockback;
        }

        readonly MonoBehaviour host;
        readonly BattleHud hud;
        readonly CommandPanel panel;
        readonly BattleCameras cameras;
        readonly HitFx fx;
        readonly DamageNumberPool numbers;
        readonly Action<BattleOutcome> onOutcome;
        readonly Dictionary<int, ActorRecord> records = new Dictionary<int, ActorRecord>();
        readonly List<ActorRecord> recordList = new List<ActorRecord>();
        readonly Queue<IReadOnlyList<BattleEvent>> pending = new Queue<IReadOnlyList<BattleEvent>>();
        readonly MaterialPropertyBlock flashBlock = new MaterialPropertyBlock();
        readonly WaitForSecondsRealtime freeze = new WaitForSecondsRealtime(FreezeSeconds);
        readonly WaitForSecondsRealtime weaknessFreeze = new WaitForSecondsRealtime(WeaknessFreezeSeconds);
        readonly WaitForSeconds enemyBeat = new WaitForSeconds(EnemyTurnBeatSeconds);
        readonly WaitForSeconds victoryCheer = new WaitForSeconds(VictoryCheerSeconds);
        readonly Action onImpact;
        Coroutine? runner;
        CombatantActor? flashed;
        bool impactFired;
        float lastDefeatEnds;

        public BattlePresenter(MonoBehaviour host, BattleHud hud, CommandPanel panel, BattleCameras cameras,
            IReadOnlyList<CombatantActor> actors, HitFx fx, DamageNumberPool numbers, BattleState start,
            Action<BattleOutcome> onOutcome)
        {
            this.host = host;
            this.hud = hud;
            this.panel = panel;
            this.cameras = cameras;
            this.fx = fx;
            this.numbers = numbers;
            this.onOutcome = onOutcome;
            onImpact = () => impactFired = true;
            flashBlock.SetFloat(FlashAmountId, 1f);

            var scratch = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            foreach (var actor in actors)
            {
                var combatant = start.Get(actor.Id);
                var record = new ActorRecord
                {
                    Actor = actor,
                    Team = combatant.Team,
                    AttackType = combatant.AttackType,
                    SkillType = combatant.Skill?.DamageType ?? combatant.AttackType,
                    SkillCooldownTurns = combatant.Skill?.CooldownTurns ?? 0,
                    Down = !combatant.IsAlive,
                };
                ReadClips(record, scratch);
                records[actor.Id] = record;
                recordList.Add(record);
            }
        }

        /// One batch from BattleService; batches queue and play in order.
        public void Play(IReadOnlyList<BattleEvent> events)
        {
            pending.Enqueue(events);
            if (runner == null)
                runner = host.StartCoroutine(Run());
        }

        /// Stops playback and leaves nothing behind: time scale, flash block, subscriptions, knockbacks, camera.
        public void Stop()
        {
            if (runner != null)
                host.StopCoroutine(runner);
            runner = null;
            pending.Clear();
            foreach (var record in recordList)
            {
                if (record.Knockback != null)
                    host.StopCoroutine(record.Knockback);
                record.Knockback = null;
                record.Actor.Relay.Impact -= onImpact;
            }
            if (flashed != null)
                SetFlash(flashed, false);
            Time.timeScale = 1f;
            cameras.SetActionActive(false);
        }

        IEnumerator Run()
        {
            while (true)
            {
                while (pending.Count == 0)
                    yield return null;
                var batch = pending.Dequeue();
                panel.Hide();

                for (int i = 0; i < batch.Count; i++)
                {
                    switch (batch[i])
                    {
                        case TurnStarted turn:
                            PlayTurnStarted(turn);
                            if (records[turn.Actor].Team == Team.Enemy)
                                yield return enemyBeat;
                            break;
                        case Attacked attacked:
                            yield return PlayAttack(attacked, batch, i);
                            break;
                        case Healed healed:
                            yield return PlayHeal(healed.Actor, healed.Target, healed.Amount, false);
                            break;
                        case Revived revived:
                            yield return PlayHeal(revived.Actor, revived.Target, revived.Hp, true);
                            break;
                        case Defended defended:
                            PlayDefend(defended);
                            break;
                        case Defeated defeated:
                            records[defeated.Target].Down = true;
                            hud.SetKnockedDown(defeated.Target, true);
                            break;
                        case Victory:
                            yield return PlayVictory();
                            break;
                        case PartyDefeated:
                            while (Time.time < lastDefeatEnds)
                                yield return null;
                            break;
                    }
                }

                FinishBatch(batch);
            }
        }

        void FinishBatch(IReadOnlyList<BattleEvent> batch)
        {
            var battle = Services.Battle;
            var state = battle.State;
            // §14.4: the snapshot is read only once every event in the batch has played.
            hud.Reconcile(state);
            if (batch.Count > 0 && batch[batch.Count - 1] is TurnStarted last && state.Get(last.Actor).Team == Team.Hero)
                panel.ShowFor(last.Actor, state);
            else if (battle.Outcome is BattleOutcome outcome)
                onOutcome(outcome);
        }

        void PlayTurnStarted(TurnStarted turn)
        {
            var record = records[turn.Actor];
            hud.SetActive(turn.Actor);
            hud.SetDefending(turn.Actor, false);
            hud.DecrementCooldown(turn.Actor);
            if (record.Defending)
            {
                record.Defending = false;
                record.Actor.Animator.CrossFadeInFixedTime(IdleHash, DefendExitSeconds);
            }
        }

        /// The hit recipe of §14.4, steps 1–11 in order; the numbers are the spec.
        IEnumerator PlayAttack(Attacked hit, IReadOnlyList<BattleEvent> batch, int index)
        {
            var attacker = records[hit.Attacker];
            var target = records[hit.Target];
            bool skill = hit.SkillId != null;
            bool weakness = hit.IsWeakness;

            // 1. cut to the action camera
            cameras.PoseOverShoulder(attacker.Actor, target.Actor);
            cameras.SetActionActive(true);

            // 2. attack clip
            float length = skill ? attacker.SkillLength : attacker.AttackLength;
            string clipName = skill ? attacker.SkillClip : attacker.AttackClip;
            float start = Time.time;
            yield return PlayClipToImpact(attacker, skill ? SkillHash : AttackHash, length, clipName);
            float impactTime = Time.time;

            // 4. freeze on unscaled time; the finally keeps an exception from leaving the game frozen
            Time.timeScale = 0f;
            try
            {
                yield return weakness ? weaknessFreeze : freeze;
            }
            finally
            {
                Time.timeScale = 1f;
            }

            // 5. reaction
            if (DefeatedInBatch(batch, index, hit.Target))
            {
                target.Actor.Animator.CrossFadeInFixedTime(DefeatHash, CrossFadeSeconds);
                lastDefeatEnds = Time.time + target.DefeatLength;
            }
            else
            {
                target.Actor.Animator.CrossFadeInFixedTime(weakness ? BigStaggerHash : HitHash, CrossFadeSeconds);
            }

            // 6. knockback of the root, alongside the rest
            StartKnockback(attacker, target, weakness ? WeaknessKnockbackMetres : KnockbackMetres);

            // 7. flash on; 8. burst; 9. impulse; 10. number and the HUD value, all on the same frame
            SetFlash(target.Actor, true);
            var point = target.Actor.Chest.position;
            fx.Burst(point, HitFx.KindOf(skill ? attacker.SkillType : attacker.AttackType));
            cameras.Impulse.GenerateImpulseWithForce(weakness ? WeaknessImpulseForce : ImpulseForce);
            numbers.Show(point, hit.Damage, weakness);
            hud.Damage(hit.Target, hit.Damage);
            if (skill)
                hud.SetCooldown(hit.Attacker, attacker.SkillCooldownTurns);

            for (int frame = 0; frame < FlashFrames; frame++)
                yield return null;
            SetFlash(target.Actor, false);

            // 11. hold the action shot until the clip ends or 0.7 s after impact, whichever is later
            float release = Mathf.Max(start + length, impactTime + HoldAfterImpactSeconds);
            while (Time.time < release)
                yield return null;
            cameras.SetActionActive(false);
        }

        /// Steps 2–3: cross-fade the clip and wait for its OnImpact event. If the clip ends without one, log and go on.
        IEnumerator PlayClipToImpact(ActorRecord actor, int stateHash, float length, string clipName)
        {
            var relay = actor.Actor.Relay;
            impactFired = false;
            relay.Impact += onImpact;
            float deadline = Time.time + length;
            actor.Actor.Animator.CrossFadeInFixedTime(stateHash, CrossFadeSeconds);
            while (!impactFired && Time.time < deadline)
                yield return null;
            relay.Impact -= onImpact;
            if (!impactFired)
                Debug.LogError(Tag + actor.Actor.Root.name + ": clip '" + clipName + "' ended without an OnImpact event; check clips.json and the KayKit import");
        }

        /// Healed and Revived (§14.4): the actor casts, the target gets the green burst and number; no cut, no freeze, no knockback.
        IEnumerator PlayHeal(int actorId, int targetId, int amount, bool revive)
        {
            var actor = records[actorId];
            var target = records[targetId];
            float start = Time.time;
            yield return PlayClipToImpact(actor, SkillHash, actor.SkillLength, actor.SkillClip);

            if (revive)
            {
                target.Down = false;
                // Defeat has no exit in the generated controller; the revived hero stands up through Idle.
                target.Actor.Animator.CrossFadeInFixedTime(IdleHash, CrossFadeSeconds);
                hud.SetHp(targetId, amount);
                hud.SetKnockedDown(targetId, false);
            }
            else
            {
                hud.Heal(targetId, amount);
            }
            var point = target.Actor.Chest.position;
            fx.Burst(point, HitFxKind.Heal);
            numbers.ShowHeal(point, amount);

            float end = start + actor.SkillLength;
            while (Time.time < end)
                yield return null;
        }

        void PlayDefend(Defended defended)
        {
            var record = records[defended.Actor];
            record.Defending = true;
            record.Actor.Animator.CrossFadeInFixedTime(DefendHash, CrossFadeSeconds);
            fx.Burst(record.Actor.Chest.position, HitFxKind.Defend, HitFx.DefendBurstCount);
            hud.SetDefending(defended.Actor, true);
        }

        IEnumerator PlayVictory()
        {
            foreach (var record in recordList)
                if (record.Team == Team.Hero && !record.Down)
                    record.Actor.Animator.CrossFadeInFixedTime(VictoryHash, CrossFadeSeconds);
            yield return victoryCheer;
        }

        void StartKnockback(ActorRecord attacker, ActorRecord target, float metres)
        {
            if (target.Knockback != null)
                host.StopCoroutine(target.Knockback);
            var direction = target.Actor.SlotPosition - attacker.Actor.SlotPosition;
            direction.y = 0f;
            target.Knockback = host.StartCoroutine(Knockback(target, direction.normalized, metres));
        }

        // Cubic ease-out away over 0.25 s, linear back to the slot over 0.35 s; the root moves, nothing else (§5).
        IEnumerator Knockback(ActorRecord target, Vector3 direction, float metres)
        {
            var root = target.Actor.Root;
            var slot = target.Actor.SlotPosition;
            float t = 0f;
            while (t < KnockbackOutSeconds)
            {
                t += Time.deltaTime;
                float u = 1f - Mathf.Clamp01(t / KnockbackOutSeconds);
                root.position = slot + direction * (metres * (1f - u * u * u));
                yield return null;
            }
            t = 0f;
            while (t < KnockbackBackSeconds)
            {
                t += Time.deltaTime;
                root.position = slot + direction * (metres * (1f - Mathf.Clamp01(t / KnockbackBackSeconds)));
                yield return null;
            }
            root.position = slot;
            target.Knockback = null;
        }

        // One shared block for the two-frame flash; SetPropertyBlock(null) puts the renderers back in the SRP Batcher.
        void SetFlash(CombatantActor actor, bool on)
        {
            var renderers = actor.Renderers;
            for (int i = 0; i < renderers.Length; i++)
                renderers[i].SetPropertyBlock(on ? flashBlock : null);
            flashed = on ? actor : null;
        }

        static bool DefeatedInBatch(IReadOnlyList<BattleEvent> batch, int index, int target)
        {
            for (int i = index + 1; i < batch.Count; i++)
            {
                switch (batch[i])
                {
                    case Defeated defeated when defeated.Target == target:
                        return true;
                    case Attacked:
                    case TurnStarted:
                        return false;
                }
            }
            return false;
        }

        /// Clip names and lengths per state from the generated override controller, read once at build time.
        static void ReadClips(ActorRecord record, List<KeyValuePair<AnimationClip, AnimationClip>> scratch)
        {
            if (!(record.Actor.Animator.runtimeAnimatorController is AnimatorOverrideController overrides))
            {
                Debug.LogError(Tag + record.Actor.Root.name + " has no override controller; clip timings fall back to " + FallbackClipSeconds + " s");
                return;
            }
            scratch.Clear();
            overrides.GetOverrides(scratch);
            foreach (var pair in scratch)
            {
                var clip = pair.Value;
                if (clip == null)
                    continue;
                switch (pair.Key.name)
                {
                    case "Attack":
                        record.AttackClip = clip.name;
                        record.AttackLength = clip.length;
                        break;
                    case "Skill":
                        record.SkillClip = clip.name;
                        record.SkillLength = clip.length;
                        break;
                    case "Defeat":
                        record.DefeatLength = clip.length;
                        break;
                }
            }
        }
    }
}
