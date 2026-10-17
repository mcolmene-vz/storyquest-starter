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
        // A clip name may hold {g}: the generator writes Masc for the boy and Femn for the girl (ARCHITECTURE §15.15.5).
        public Dictionary<string, string> Clips { get; set; } = new Dictionary<string, string>();
        // Keyed by body, "boy" and "girl" (§15.3).
        public Dictionary<string, VariantDefinition> Variants { get; set; } = new Dictionary<string, VariantDefinition>();
    }

    /// One hero variant (§15.3): the complete list of modular parts shown, and the skin tone, a key of skinTones.
    public sealed class VariantDefinition
    {
        public List<string> Parts { get; set; } = new List<string>();
        public string Tone { get; set; } = "";
    }

    /// One weapon mesh (§15.18.3, §15.18.8): how it sits in its socket, in metres and degrees in the socket's local
    /// space, which of its file's renderers make the weapon, and its default atlas.
    public sealed class WeaponMeshDefinition
    {
        public List<double> Pos { get; set; } = new List<double>();
        public List<double> Euler { get; set; } = new List<double>();
        // The renderers of the mesh's file that make the weapon, when the file holds alternatives on top of each other
        // (the Long bow file carries three bow bodies and one string); empty means every renderer.
        public List<string> Parts { get; set; } = new List<string>();
        // The texture file name, without extension, under the Synty Textures folder. A weapon item's own texture wins.
        public string Texture { get; set; } = "";
    }

    /// The classes.json fields that are not per class. Core carries them for the generator and never reads them.
    public sealed class HeroPresentation
    {
        public static readonly HeroPresentation None = new HeroPresentation();

        public Dictionary<string, string> SkinTones { get; set; } = new Dictionary<string, string>();
        public string SkinReference { get; set; } = "";
        public string StubbleReference { get; set; } = "";
        public Dictionary<string, WeaponMeshDefinition> WeaponMeshes { get; set; } = new Dictionary<string, WeaponMeshDefinition>();
    }

    public static class SocketNames
    {
        public const string HandR = "Socket_HandR";
        public const string HandL = "Socket_HandL";
        public const string Back = "Socket_Back";
        public const string Head = "Socket_Head";
        public static readonly string[] All = { HandR, HandL, Back, Head };
    }

    public static class SkinToneNames
    {
        public const string Light = "light";
        public const string Medium = "medium";
        public const string Dark = "dark";
        // Lightest first: the order TableLoader's luma check expects.
        public static readonly string[] All = { Light, Medium, Dark };
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
        public Dictionary<string, string> SkinTones { get; set; } = new Dictionary<string, string>();
        public string SkinReference { get; set; } = "";
        public string StubbleReference { get; set; } = "";
        public Dictionary<string, WeaponMeshDefinition> WeaponMeshes { get; set; } = new Dictionary<string, WeaponMeshDefinition>();
    }
}
