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

        // Equipment (M2 shop); Core carries the fields, nothing reads them in M1.
        public string? ClassId { get; set; }
        public string? Stat { get; set; }
        public int Bonus { get; set; }
        public string? Mesh { get; set; }
        public string? Socket { get; set; }
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
        public static readonly string[] All = { "hp", "attack", "defence", "speed" };
    }

    public sealed class ItemsTable : IVersionedTable
    {
        public int Version { get; set; }
        public List<ItemDefinition> Items { get; set; } = new List<ItemDefinition>();
    }
}
