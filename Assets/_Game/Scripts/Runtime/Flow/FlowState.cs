namespace Game.Runtime
{
    /// The whole V1 flow laid out now (ARCHITECTURE §14.4); M2 drives Boot → Camp → Battle → Camp (§15.5).
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
