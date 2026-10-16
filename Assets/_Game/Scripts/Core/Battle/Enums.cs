namespace Game.Core.Battle
{
    public enum Team
    {
        Hero,
        Enemy,
    }

    public enum CommandKind
    {
        Attack,
        Skill,
        Item,
        Defend,
    }

    public enum BattlePhase
    {
        Active,
        Won,
        Lost,
    }
}
