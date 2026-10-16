using System;
using System.Collections.Generic;
using Game.Core.Battle;
using Game.Core.Data;

namespace Game.Core
{
    /// Level thresholds and per-level stats from levels.json. The level cap is the length of xpToReach (ten);
    /// surplus XP past the cap is kept and does nothing.
    public static class Progression
    {
        public static int LevelFor(int totalXp, IReadOnlyList<int> xpToReach)
        {
            int level = 1;
            for (int i = 1; i < xpToReach.Count; i++)
            {
                if (totalXp < xpToReach[i])
                    break;
                level = i + 1;
            }
            return level;
        }

        public static Stats StatsFor(string classId, int level, GameTables tables)
        {
            if (!tables.Levels.Stats.TryGetValue(classId, out var rows))
                throw new ArgumentException($"no level rows for class '{classId}'", nameof(classId));
            if (level < 1 || level > rows.Count)
                throw new ArgumentOutOfRangeException(nameof(level), level, $"class '{classId}' has levels 1..{rows.Count}");
            var row = rows[level - 1];
            return new Stats(row.Hp, row.Attack, row.Defence, row.Speed);
        }

        public static (int NewTotal, int OldLevel, int NewLevel) ApplyXp(int totalXp, int gained, IReadOnlyList<int> xpToReach)
        {
            if (gained < 0)
                throw new ArgumentOutOfRangeException(nameof(gained), gained, "XP gained cannot be negative");
            int newTotal = totalXp + gained;
            return (newTotal, LevelFor(totalXp, xpToReach), LevelFor(newTotal, xpToReach));
        }
    }
}
