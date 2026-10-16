using System.Linq;
using System.Text;

namespace Game.Core.Battle
{
    /// Ids are Combatant.Id. Sealed records so two battles' event lists compare by value.
    public abstract record BattleEvent;

    public sealed record BattleStarted : BattleEvent;

    public sealed record RoundStarted(int Round, int[] Order) : BattleEvent
    {
        // Arrays compare by reference; a replayed battle must still produce equal events.
        public bool Equals(RoundStarted? other) =>
            other is not null && Round == other.Round && Order.SequenceEqual(other.Order);

        public override int GetHashCode()
        {
            int hash = Round;
            foreach (var id in Order)
                hash = hash * 31 + id;
            return hash;
        }

        protected override bool PrintMembers(StringBuilder builder)
        {
            builder.Append("Round = ").Append(Round).Append(", Order = [").Append(string.Join(", ", Order)).Append(']');
            return true;
        }
    }

    public sealed record TurnStarted(int Actor) : BattleEvent;

    /// Missed is always false in V1 (V1-SCOPE §5) and stays so the schema does not churn if accuracy is ever added.
    public sealed record Attacked(int Attacker, int Target, int Damage, bool IsWeakness, bool Missed, string? SkillId) : BattleEvent;

    public sealed record Healed(int Actor, int Target, int Amount, string ItemId) : BattleEvent;

    public sealed record Revived(int Actor, int Target, int Hp, string ItemId) : BattleEvent;

    public sealed record ItemUsed(int Actor, string ItemId, int Remaining) : BattleEvent;

    public sealed record Defended(int Actor) : BattleEvent;

    public sealed record Defeated(int Target) : BattleEvent;

    public sealed record Victory(int Xp, int Coins) : BattleEvent;

    public sealed record PartyDefeated : BattleEvent;
}
