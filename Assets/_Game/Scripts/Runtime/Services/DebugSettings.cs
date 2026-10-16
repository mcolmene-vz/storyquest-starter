namespace Game.Runtime
{
    /// Stand-in for SettingsService until M3 (ARCHITECTURE §14.5). Always Win is the default because the youngest
    /// age band plays first and "nothing is lost" is the safer failure.
    public static class DebugSettings
    {
        public enum Difficulty
        {
            AlwaysWin,
            Normal,
        }

        public static Difficulty Current { get; set; } = Difficulty.AlwaysWin;
    }
}
