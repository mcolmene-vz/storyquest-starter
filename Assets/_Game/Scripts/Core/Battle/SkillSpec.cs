namespace Game.Core.Battle
{
    /// What the resolver needs of a class skill, copied from the table by BattleFactory so Resolve takes no tables.
    public sealed record SkillSpec(string Id, DamageType DamageType, double Power, int CooldownTurns);
}
