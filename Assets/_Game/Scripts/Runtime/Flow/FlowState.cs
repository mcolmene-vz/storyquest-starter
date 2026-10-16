namespace Game.Runtime
{
    /// The whole V1 flow laid out now (ARCHITECTURE §14.4); M1 drives Boot → Battle.
    public enum FlowState
    {
        Boot,
        Title,
        Profiles,
        Camp,
        Battle,
        Victory,
        Defeat,
        Ending,
    }
}
