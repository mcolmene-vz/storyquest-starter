using System;
using System.Collections.Generic;
using Game.Core.Battle;

namespace Game.Runtime
{
    /// D5's stand-in for BattlePresenter: applies a batch to the HUD immediately, in event order, then reconciles
    /// with the end-of-batch snapshot, opens the command panel on a hero's TurnStarted and reports the outcome.
    /// D6 replaces this with the sequential presenter of ARCHITECTURE §14.4.
    public sealed class EventApplier
    {
        readonly BattleHud hud;
        readonly CommandPanel panel;
        readonly Action<BattleOutcome> onOutcome;

        public EventApplier(BattleHud hud, CommandPanel panel, Action<BattleOutcome> onOutcome)
        {
            this.hud = hud;
            this.panel = panel;
            this.onOutcome = onOutcome;
        }

        public void Apply(IReadOnlyList<BattleEvent> events)
        {
            var battle = Services.Battle;
            var state = battle.State;
            for (int i = 0; i < events.Count; i++)
            {
                switch (events[i])
                {
                    case TurnStarted turn:
                        hud.SetActive(turn.Actor);
                        hud.SetDefending(turn.Actor, false);
                        hud.DecrementCooldown(turn.Actor);
                        break;
                    case Attacked attacked:
                        hud.Damage(attacked.Target, attacked.Damage);
                        if (attacked.SkillId != null)
                            hud.SetCooldown(attacked.Attacker, state.Get(attacked.Attacker).Skill?.CooldownTurns ?? 0);
                        break;
                    case Healed healed:
                        hud.Heal(healed.Target, healed.Amount);
                        break;
                    case Revived revived:
                        hud.SetHp(revived.Target, revived.Hp);
                        hud.SetKnockedDown(revived.Target, false);
                        break;
                    case Defended defended:
                        hud.SetDefending(defended.Actor, true);
                        break;
                    case Defeated defeated:
                        hud.SetKnockedDown(defeated.Target, true);
                        break;
                }
            }

            // The snapshot is only read once the batch has been applied (§14.4).
            hud.Reconcile(state);

            if (events.Count > 0 && events[events.Count - 1] is TurnStarted last && state.Get(last.Actor).Team == Team.Hero)
                panel.ShowFor(last.Actor, state);
            else if (battle.Outcome is BattleOutcome outcome)
                onOutcome(outcome);
        }
    }
}
