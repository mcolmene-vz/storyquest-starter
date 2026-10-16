using System.Collections.Generic;

namespace Game.Core.Data
{
    public sealed class ClassDefinition
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string DamageType { get; set; } = "";
        public double AttackPower { get; set; }
        public SkillDefinition? Skill { get; set; }

        // Presentation fields: Core carries them for the generator and the presenter and never reads them.
        public string WeaponSocket { get; set; } = "";
        public string WeaponMesh { get; set; } = "";
        public string Model { get; set; } = "";
        public Dictionary<string, string> Clips { get; set; } = new Dictionary<string, string>();
    }

    public sealed class SkillDefinition
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string DamageType { get; set; } = "";
        public double Power { get; set; }
        public int CooldownTurns { get; set; }
    }

    public sealed class ClassesTable : IVersionedTable
    {
        public int Version { get; set; }
        public List<ClassDefinition> Classes { get; set; } = new List<ClassDefinition>();
    }
}
