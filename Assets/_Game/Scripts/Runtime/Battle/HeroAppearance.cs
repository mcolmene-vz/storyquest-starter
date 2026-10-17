using System;
using System.Collections.Generic;
using Game.Core;
using Game.Core.Data;
using UnityEngine;

namespace Game.Runtime
{
    /// A hero's visible gear (ARCHITECTURE §15.4, §15.15.2). The generated prefab already carries every candidate: the
    /// outfit, the class armor's parts (inactive) and two role-named weapon children under the weapon socket. Apply only
    /// switches them with SetActive, once at spawn, before CombatantActor caches the renderers the flash touches.
    public static class HeroAppearance
    {
        /// The starter weapon child's name; the other child is named after its item id. It must match
        /// HeroGenerator.StarterWeapon, which Runtime cannot reference; a mismatch fails Apply by name.
        public const string StarterWeapon = "starter";

        public static void Apply(Transform model, ClassDefinition cls, string body, Equipment equipment, GameTables tables)
        {
            ApplyWeapon(model, cls, equipment.Weapon);
            ApplyArmor(model, cls, body, equipment.Armor, tables);
        }

        /// The child named after the equipped weapon's item id, or the starter, is active; every other child is not.
        static void ApplyWeapon(Transform model, ClassDefinition cls, string? weapon)
        {
            var socket = FindDescendant(model, cls.WeaponSocket)
                         ?? throw new InvalidOperationException($"{model.name} has no {cls.WeaponSocket}; regenerate the prefab");
            string role = weapon ?? StarterWeapon;
            bool found = false;
            for (int i = 0; i < socket.childCount; i++)
            {
                var child = socket.GetChild(i);
                bool on = child.name == role;
                child.gameObject.SetActive(on);
                found |= on;
            }
            if (!found)
                throw new InvalidOperationException($"{model.name} has no weapon '{role}' under {cls.WeaponSocket}; regenerate the prefab");
        }

        /// §15.4's part-group rule: the worn armor's parts for this body are on and every outfit part in one of their
        /// groups is off (a helm over the coif); the class armor's other parts are off and the outfit is whole. A part
        /// whose group has no outfit part (a cape, shoulder pads) just switches.
        static void ApplyArmor(Transform model, ClassDefinition cls, string body, string? armor, GameTables tables)
        {
            if (!cls.Variants.TryGetValue(body, out var variant))
                throw new InvalidOperationException($"{model.name}: class '{cls.Id}' has no '{body}' variant");

            var shown = new Dictionary<string, bool>();
            foreach (var item in tables.Items)
                if (item.Kind == ItemKinds.Armor && item.ClassId == cls.Id)
                    foreach (var part in PartsFor(item, body))
                        shown[part] = false;

            IReadOnlyList<string> worn = armor == null ? Array.Empty<string>() : PartsFor(tables.Item(armor), body);
            var covered = new HashSet<string>();
            foreach (var part in worn)
                covered.Add(Group(part));
            foreach (var part in variant.Parts)
                shown[part] = !covered.Contains(Group(part));
            foreach (var part in worn)
                shown[part] = true;

            int matched = 0;
            foreach (var renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (!shown.TryGetValue(renderer.name, out bool on))
                    continue;
                renderer.gameObject.SetActive(on);
                matched++;
            }
            if (matched != shown.Count)
                throw new InvalidOperationException($"{model.name} carries {matched} of the {shown.Count} outfit and armor parts classes.json and " +
                                                    "items.json list for it; regenerate the prefab");
        }

        static List<string> PartsFor(ItemDefinition item, string body) =>
            item.Parts.TryGetValue(body, out var parts)
                ? parts
                : throw new InvalidOperationException($"items.json '{item.Id}' has no parts for '{body}'");

        /// §15.4: a part name minus its trailing _NN index, so Chr_HeadCoverings_No_Hair_11 and _12 share a group.
        static string Group(string part)
        {
            int cut = part.LastIndexOf('_');
            if (cut <= 0 || cut == part.Length - 1)
                return part;
            for (int i = cut + 1; i < part.Length; i++)
                if (part[i] < '0' || part[i] > '9')
                    return part;
            return part.Substring(0, cut);
        }

        static Transform? FindDescendant(Transform parent, string name)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child.name == name)
                    return child;
                var found = FindDescendant(child, name);
                if (found != null)
                    return found;
            }
            return null;
        }
    }
}
