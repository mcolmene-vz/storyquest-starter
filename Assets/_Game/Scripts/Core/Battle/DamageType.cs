using System;

namespace Game.Core.Battle
{
    public enum DamageType
    {
        Slash,
        Pierce,
        Magic,
        Blunt,
    }

    /// The table spelling of each damage type; tables keep strings, Core keeps the enum.
    public static class DamageTypes
    {
        public const string Names = "slash/pierce/magic/blunt";

        public static bool TryParse(string? name, out DamageType type)
        {
            switch (name)
            {
                case "slash": type = DamageType.Slash; return true;
                case "pierce": type = DamageType.Pierce; return true;
                case "magic": type = DamageType.Magic; return true;
                case "blunt": type = DamageType.Blunt; return true;
                default: type = default; return false;
            }
        }

        public static DamageType Parse(string? name)
        {
            if (TryParse(name, out var type))
                return type;
            throw new ArgumentException($"unknown damage type '{name}' (expected {Names})", nameof(name));
        }
    }
}
