using System.Collections.Generic;
using Game.Core;
using Game.Core.Data;

namespace Game.Runtime
{
    /// The M2 party (ARCHITECTURE §15.3, §15.5): the four heroes at 0 XP with 60 coins each, and a pool of two potions
    /// and a revive. Held in memory by Services.Party and lost on quit. Deleted in M3 with the Body toggle, when profiles
    /// pick the bodies and SaveService loads the record.
    public static class DebugParty
    {
        public static PartyRecord CreateParty() => new PartyRecord
        {
            Heroes = new List<HeroRecord>
            {
                Hero("Swordsman", "swordsman", Bodies.Girl),
                Hero("Archer", "archer", Bodies.Boy),
                Hero("Mage", "mage", Bodies.Girl),
                Hero("Fighter", "fighter", Bodies.Boy),
            },
            Consumables = new Dictionary<string, int> { ["potion"] = 2, ["revive"] = 1 },
        };

        static HeroRecord Hero(string name, string classId, string body) => new HeroRecord
        {
            Name = name,
            ClassId = classId,
            Body = body,
            Xp = 0,
            Coins = 60,
        };
    }
}
