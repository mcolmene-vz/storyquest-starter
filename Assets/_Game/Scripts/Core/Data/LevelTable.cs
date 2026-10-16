using System.Collections.Generic;

namespace Game.Core.Data
{
    /// xpToReach[i] is the cumulative XP for level i + 1; stats[classId][i] are the stats at level i + 1.
    public sealed class LevelTable : IVersionedTable
    {
        public int Version { get; set; }
        public List<int> XpToReach { get; set; } = new List<int>();
        public Dictionary<string, List<LevelRow>> Stats { get; set; } = new Dictionary<string, List<LevelRow>>();
    }

    public sealed class LevelRow
    {
        public int Hp { get; set; }
        public int Attack { get; set; }
        public int Defence { get; set; }
        public int Speed { get; set; }
    }
}
