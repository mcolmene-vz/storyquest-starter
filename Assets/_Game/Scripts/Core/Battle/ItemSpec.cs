namespace Game.Core.Battle
{
    public enum ItemEffect
    {
        Heal,
        Revive,
    }

    /// What the resolver needs of a consumable: Heal.Amount is flat HP, Revive.Amount is a fraction of max HP.
    public sealed record ItemSpec(string Id, ItemEffect Effect, double Amount);
}
