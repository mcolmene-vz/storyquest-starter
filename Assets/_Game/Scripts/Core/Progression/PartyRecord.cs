using System.Collections.Generic;
using Game.Core.Data;

namespace Game.Core
{
    /// Equipped item ids per slot; null is the class's starter weapon, the outfit's own parts, or no charm.
    public sealed record Equipment(string? Weapon, string? Armor, string? Charm)
    {
        public static readonly Equipment None = new Equipment(null, null, null);
    }

    /// One kid's hero. Coins are per hero; owned lists equipment ids only (consumables are the party pool).
    public sealed class HeroRecord
    {
        public string Name { get; set; } = "";
        public string ClassId { get; set; } = "";
        public string Body { get; set; } = Bodies.Boy;
        public int Xp { get; set; }
        public int Coins { get; set; }
        public Equipment Equipped { get; set; } = Equipment.None;
        public List<string> Owned { get; set; } = new List<string>();
    }

    /// The party between battles, held in memory in M2 and serialized by SaveService from M3 (ARCHITECTURE §15.5).
    public sealed class PartyRecord
    {
        public List<HeroRecord> Heroes { get; set; } = new List<HeroRecord>();
        public Dictionary<string, int> Consumables { get; set; } = new Dictionary<string, int>();
    }
}
