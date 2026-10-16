using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core.Battle
{
    /// The battle rules of ARCHITECTURE §14.3. Pure: every call clones the state it is given and returns the
    /// new state with the events that explain it; the caller's state is never mutated.
    public static class BattleResolver
    {
        // Guards floor() against products like 10 x 1.7 landing a hair under the integer they mean.
        const double Epsilon = 1e-9;

        public static BattleStep Start(BattleState state, IRandom random)
        {
            if (state.Phase != BattlePhase.Active)
                throw new InvalidOperationException("the battle is already over");
            if (state.Round != 0 || state.ActiveId != -1)
                throw new InvalidOperationException("the battle has already started");

            var next = state.Clone();
            var events = new List<BattleEvent> { new BattleStarted() };
            BeginRound(next, events);
            return new BattleStep(next, events);
        }

        public static BattleStep Resolve(BattleState state, Command command, IRandom random)
        {
            if (state.Phase != BattlePhase.Active)
                throw new InvalidOperationException("the battle is over; nothing more can be resolved");
            if (state.ActiveId < 0)
                throw new InvalidOperationException("call Start before Resolve");

            var next = state.Clone();
            var events = new List<BattleEvent>();
            var actor = next.Get(command.ActorId);
            if (actor.Id != next.ActiveId)
                throw new ArgumentException($"combatant {actor.Id} is not the active combatant ({next.ActiveId})", nameof(command));
            if (!actor.IsAlive)
                throw new ArgumentException($"combatant {actor.Id} is knocked down", nameof(command));

            switch (command.Kind)
            {
                case CommandKind.Attack:
                    Strike(next, actor, EnemyTarget(next, actor, command.TargetId), actor.AttackType, actor.AttackPower, null, events);
                    break;
                case CommandKind.Skill:
                    var skill = actor.Skill ?? throw new ArgumentException($"combatant {actor.Id} has no skill", nameof(command));
                    if (actor.SkillCooldown > 0)
                        throw new ArgumentException($"skill '{skill.Id}' is on cooldown for {actor.SkillCooldown} more turn(s)", nameof(command));
                    var skillTarget = EnemyTarget(next, actor, command.TargetId);
                    actor.SkillCooldown = skill.CooldownTurns;
                    Strike(next, actor, skillTarget, skill.DamageType, skill.Power, skill.Id, events);
                    break;
                case CommandKind.Item:
                    UseItem(next, actor, command, events);
                    break;
                case CommandKind.Defend:
                    actor.Defending = true;
                    events.Add(new Defended(actor.Id));
                    break;
                default:
                    throw new ArgumentException($"unknown command kind {command.Kind}", nameof(command));
            }

            if (!CheckEnd(next, events))
                NextTurn(next, events);
            return new BattleStep(next, events);
        }

        public static Command EnemyCommand(BattleState state, IRandom random)
        {
            if (state.Phase != BattlePhase.Active)
                throw new InvalidOperationException("the battle is over");
            var actor = state.Get(state.ActiveId);
            if (actor.Team != Team.Enemy)
                throw new InvalidOperationException($"active combatant {actor.Id} is a hero; EnemyCommand is for enemy turns");
            return EnemyBrain.Choose(state, actor, random);
        }

        static Combatant EnemyTarget(BattleState state, Combatant actor, int targetId)
        {
            var target = state.Get(targetId);
            if (target.Team == actor.Team)
                throw new ArgumentException($"target {targetId} is on the attacker's own team", nameof(targetId));
            if (!target.IsAlive)
                throw new ArgumentException($"target {targetId} is already knocked down", nameof(targetId));
            return target;
        }

        static void Strike(BattleState state, Combatant attacker, Combatant target, DamageType type, double power,
            string? skillId, List<BattleEvent> events)
        {
            int damage = Math.Max(1, (int)Math.Floor(attacker.Stats.Attack * power + Epsilon) - target.Stats.Defence);
            bool isWeakness = target.Weakness == type;
            if (isWeakness)
                damage *= 2;
            if (target.Defending)
                damage = Math.Max(1, damage / 2);

            target.Hp = Math.Max(0, target.Hp - damage);
            events.Add(new Attacked(attacker.Id, target.Id, damage, isWeakness, false, skillId));
            if (!target.IsAlive)
            {
                events.Add(new Defeated(target.Id));
                // Knocked down mid-round means skipped; a later revive joins the next round, not this queue.
                state.TurnQueue.Remove(target.Id);
            }
        }

        static void UseItem(BattleState state, Combatant actor, Command command, List<BattleEvent> events)
        {
            if (actor.Team != Team.Hero)
                throw new ArgumentException("items belong to the party; an enemy cannot use them", nameof(command));
            var itemId = command.ItemId ?? throw new ArgumentException("an Item command needs an item id", nameof(command));
            if (!state.Items.TryGetValue(itemId, out var item))
                throw new ArgumentException($"'{itemId}' is not a usable item in this battle", nameof(command));
            state.Inventory.TryGetValue(itemId, out int count);
            if (count <= 0)
                throw new ArgumentException($"no '{itemId}' left in the inventory", nameof(command));
            var target = state.Get(command.TargetId);
            if (target.Team != Team.Hero)
                throw new ArgumentException($"items target heroes; {target.Id} is an enemy", nameof(command));
            if (item.Effect == ItemEffect.Heal && !target.IsAlive)
                throw new ArgumentException($"'{itemId}' heals a living hero; {target.Id} is knocked down", nameof(command));
            if (item.Effect == ItemEffect.Revive && target.IsAlive)
                throw new ArgumentException($"'{itemId}' revives a knocked-down hero; {target.Id} is standing", nameof(command));

            state.Inventory[itemId] = count - 1;
            events.Add(new ItemUsed(actor.Id, itemId, count - 1));
            if (item.Effect == ItemEffect.Heal)
            {
                int before = target.Hp;
                target.Hp = Math.Min(target.Stats.MaxHp, target.Hp + (int)item.Amount);
                events.Add(new Healed(actor.Id, target.Id, target.Hp - before, itemId));
            }
            else
            {
                target.Hp = Math.Max(1, (int)Math.Floor(target.Stats.MaxHp * item.Amount + Epsilon));
                events.Add(new Revived(actor.Id, target.Id, target.Hp, itemId));
            }
        }

        static bool CheckEnd(BattleState state, List<BattleEvent> events)
        {
            if (!state.Living(Team.Enemy).Any())
            {
                state.Phase = BattlePhase.Won;
                state.XpEarned = state.Combatants.Where(c => c.Team == Team.Enemy).Sum(c => c.XpReward);
                state.CoinsEarned = state.Combatants.Where(c => c.Team == Team.Enemy).Sum(c => c.CoinReward);
                events.Add(new Victory(state.XpEarned, state.CoinsEarned));
                return true;
            }
            if (!state.Living(Team.Hero).Any())
            {
                state.Phase = BattlePhase.Lost;
                events.Add(new PartyDefeated());
                return true;
            }
            return false;
        }

        static void BeginRound(BattleState state, List<BattleEvent> events)
        {
            state.Round++;
            var order = state.Combatants.Where(c => c.IsAlive)
                .OrderByDescending(c => c.Stats.Speed)
                .ThenBy(c => c.Team == Team.Hero ? 0 : 1)
                .ThenBy(c => c.Slot)
                .Select(c => c.Id)
                .ToArray();
            if (order.Length == 0)
                throw new InvalidOperationException("no living combatant to start a round");

            state.TurnQueue.Clear();
            state.TurnQueue.AddRange(order);
            events.Add(new RoundStarted(state.Round, order));
            NextTurn(state, events);
        }

        static void NextTurn(BattleState state, List<BattleEvent> events)
        {
            while (state.TurnQueue.Count > 0)
            {
                var combatant = state.Get(state.TurnQueue[0]);
                state.TurnQueue.RemoveAt(0);
                if (!combatant.IsAlive)
                    continue;

                state.ActiveId = combatant.Id;
                combatant.Defending = false;
                combatant.SkillCooldown = Math.Max(0, combatant.SkillCooldown - 1);
                events.Add(new TurnStarted(combatant.Id));
                return;
            }
            BeginRound(state, events);
        }
    }
}
