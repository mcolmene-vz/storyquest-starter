using System;
using System.Collections.Generic;
using Game.Core.Battle;

namespace Game.Runtime
{
    /// The M1 party (ARCHITECTURE §14.4). Deleted in M3 when StoryService builds the setup from the `# battle:` tag.
    public static class DebugParty
    {
        public static BattleSetup Create() => new BattleSetup(
            new[]
            {
                new HeroSpec("swordsman", 1, "Swordsman"),
                new HeroSpec("archer", 1, "Archer"),
                new HeroSpec("mage", 1, "Mage"),
                new HeroSpec("fighter", 1, "Fighter"),
            },
            new[] { "troll_grunt" },
            new Dictionary<string, int> { ["potion"] = 2, ["revive"] = 1 },
            (int)(DateTime.UtcNow.Ticks & 0x7fffffff));
    }
}
