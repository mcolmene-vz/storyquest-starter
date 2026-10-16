using System.Collections.Generic;

namespace Game.Core.Data
{
    public sealed class EnemyDefinition
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public int Hp { get; set; }
        public int Attack { get; set; }
        public int Defence { get; set; }
        public int Speed { get; set; }
        public string DamageType { get; set; } = "";
        public double AttackPower { get; set; }
        public string? Weakness { get; set; }
        public string Behaviour { get; set; } = "";
        public int Xp { get; set; }
        public int Coins { get; set; }

        // Presentation fields: Core carries them and never reads them.
        public string Model { get; set; } = "";
        public double Scale { get; set; } = 1.0;
        public string Tint { get; set; } = "";
        public Dictionary<string, string> Clips { get; set; } = new Dictionary<string, string>();
    }

    public sealed class EnemiesTable : IVersionedTable
    {
        public int Version { get; set; }
        public List<EnemyDefinition> Enemies { get; set; } = new List<EnemyDefinition>();
    }
}
