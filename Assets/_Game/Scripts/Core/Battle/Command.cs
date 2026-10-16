namespace Game.Core.Battle
{
    /// TargetId is the enemy for Attack/Skill, the hero for Item, and the actor itself for Defend.
    public sealed record Command(CommandKind Kind, int ActorId, int TargetId, string? ItemId)
    {
        public static Command Attack(int actorId, int targetId) => new Command(CommandKind.Attack, actorId, targetId, null);
        public static Command Skill(int actorId, int targetId) => new Command(CommandKind.Skill, actorId, targetId, null);
        public static Command Item(int actorId, int targetId, string itemId) => new Command(CommandKind.Item, actorId, targetId, itemId);
        public static Command Defend(int actorId) => new Command(CommandKind.Defend, actorId, actorId, null);
    }
}
