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

        // Presentation fields: Core carries them and never reads them (ARCHITECTURE §15.16.2, §15.16.8).
        // The character file under the Synty Characters folder, without extension.
        public string Model { get; set; } = "";
        // The renderers kept from the model; every other part is stripped (the hero keep-and-strip rule).
        public List<string> Parts { get; set; } = new List<string>();
        // The atlas for every renderer of the enemy, the weapon included, overriding the weapon mesh's default texture.
        public string Texture { get; set; } = "";
        // A key of classes.json weaponMeshes, held in the socket named by WeaponSocket.
        public string WeaponMesh { get; set; } = "";
        public string WeaponSocket { get; set; } = "";
        public double Scale { get; set; } = 1.0;
        public Dictionary<string, string> Clips { get; set; } = new Dictionary<string, string>();
    }

    public sealed class EnemiesTable : IVersionedTable
    {
        public int Version { get; set; }
        public List<EnemyDefinition> Enemies { get; set; } = new List<EnemyDefinition>();
    }
}
