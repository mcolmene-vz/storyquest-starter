using System.Collections.Generic;

namespace Game.Core.Data
{
    public sealed class ItemDefinition
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Kind { get; set; } = "";
        public int Price { get; set; }

        // Consumables: heal.amount is flat HP, revive.amount is a fraction of max HP.
        public string? Effect { get; set; }
        public double Amount { get; set; }

        // Equipment: Progression reads the class, stat and bonus.
        public string? ClassId { get; set; }
        public string? Stat { get; set; }
        public int Bonus { get; set; }

        // Presentation fields: a weapon's mesh goes under its class's weaponSocket; armor parts are keyed by body.
        // Core carries them for the generator and HeroAppearance and never reads them.
        public string? Mesh { get; set; }
        public Dictionary<string, List<string>> Parts { get; set; } = new Dictionary<string, List<string>>();
        public string? Tint { get; set; }
    }

    public static class ItemKinds
    {
        public const string Consumable = "consumable";
        public const string Weapon = "weapon";
        public const string Armor = "armor";
        public const string Charm = "charm";
        public static readonly string[] All = { Consumable, Weapon, Armor, Charm };
    }

    public static class ItemEffects
    {
        public const string Heal = "heal";
        public const string Revive = "revive";
        public static readonly string[] All = { Heal, Revive };
    }

    public static class StatNames
    {
        public const string Hp = "hp";
        public const string Attack = "attack";
        public const string Defence = "defence";
        public const string Speed = "speed";
        public static readonly string[] All = { Hp, Attack, Defence, Speed };
    }

    public static class Bodies
    {
        public const string Boy = "boy";
        public const string Girl = "girl";
        public static readonly string[] All = { Boy, Girl };
    }

    public sealed class ItemsTable : IVersionedTable
    {
        public int Version { get; set; }
        public List<ItemDefinition> Items { get; set; } = new List<ItemDefinition>();
    }
}
