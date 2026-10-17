using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Game.Core.Data;
using Game.Editor.Import;
using Game.Runtime;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Editor
{
    /// The eight Synty hero variants (ARCHITECTURE §15.3, §15.15, §15.18): one prefab and one override controller per
    /// (class, body), one hero material per skin tone in use, and the weapon materials. Called from
    /// CombatantGenerator.Generate with the same determinism rules: assets are loaded and modified in place, and a run on
    /// unchanged inputs writes every file byte-identically. Fails, naming the problem, when a listed part or mesh is
    /// missing, an avatar maps too few bones, a height leaves the band or the party exceeds the renderer budget.
    public static class HeroGenerator
    {
        public const string GenderToken = "{g}";
        public const string StarterWeapon = "starter";

        const string Tag = "[HeroGenerator] ";
        const string Derived = "[Synty-derived] ";
        // §15.18.1: 22 body bones and 18 finger bones. A file imported once with a brand-new .meta maps fewer (DEVICE-TESTING §2c).
        const int MinMappedBones = 40;
        // §15.18.3: catches a weapon whose scene offset survived; the hand must touch the weapon's bounds.
        const float MaxWeaponDistance = 0.3f;
        // §15.7: the four on-screen variants plus the enemy.
        const int MaxPartyRenderers = 90;

        const string HeroAtlas = SyntyPostprocessor.TexturesFolder + "PolygonFantasyHero_Texture_01_A.png";

        static readonly (string Name, HumanBodyBones Bone, HumanBodyBones Fallback)[] Sockets =
        {
            (SocketNames.HandR, HumanBodyBones.RightHand, HumanBodyBones.RightHand),
            (SocketNames.HandL, HumanBodyBones.LeftHand, HumanBodyBones.LeftHand),
            (SocketNames.Back, HumanBodyBones.UpperChest, HumanBodyBones.Chest),
            (SocketNames.Head, HumanBodyBones.Head, HumanBodyBones.Head),
        };

        public static string VariantId(string classId, string body) => classId + "_" + body;

        /// §15.15.5: {g} is Masc for the boy and Femn for the girl.
        public static string ResolveClip(string clip, string body) =>
            clip.Replace(GenderToken, body == Bodies.Girl ? "Femn" : "Masc");

        public static Dictionary<string, string> ResolveClips(Dictionary<string, string> clips, string body) =>
            clips.ToDictionary(p => p.Key, p => ResolveClip(p.Value, body));

        /// Returns, per class, the most renderers either body shows with its armor on and its starter weapon.
        public static Dictionary<string, int> Generate(GameTables tables, AnimatorController controller,
            Dictionary<string, AnimationClip> placeholders, Dictionary<string, AnimationClip> clips, Shader toon)
        {
            var heroModel = CombatantGenerator.LoadModel(SyntyPostprocessor.HeroModel);
            CheckAvatar(SyntyPostprocessor.HeroModel);

            var toneMaterials = GenerateHeroMaterials(tables, toon);
            var weaponMeshes = tables.Heroes.WeaponMeshes;
            var weaponMaterials = new Dictionary<string, Material>();

            var renderers = new Dictionary<string, int>();
            foreach (var cls in tables.Classes)
            {
                var weaponItem = tables.Items.Single(i => i.Kind == ItemKinds.Weapon && i.ClassId == cls.Id);
                var armorItem = tables.Items.Single(i => i.Kind == ItemKinds.Armor && i.ClassId == cls.Id);
                var starterMaterial = WeaponMaterial(cls.WeaponMesh, null, weaponMeshes, toon, weaponMaterials);
                var shopMaterial = WeaponMaterial(weaponItem.Mesh!, weaponItem, weaponMeshes, toon, weaponMaterials);

                foreach (var body in Bodies.All)
                {
                    string id = VariantId(cls.Id, body);
                    var clipMap = ResolveClips(cls.Clips, body);
                    foreach (var clip in clipMap.Values.Distinct())
                        CheckAvatar(AssetDatabase.GetAssetPath(clips[clip]));
                    var overrides = CombatantGenerator.GenerateOverrideController(id, controller, placeholders, clipMap, clips);
                    LogDerived(CombatantGenerator.AnimatorsFolder + "/" + id + ".overrideController",
                        clipMap.Values.Select(c => AssetDatabase.GetAssetPath(clips[c])));

                    var variant = cls.Variants[body];
                    int count = GeneratePrefab(id, heroModel, variant, armorItem.Parts[body], toneMaterials[variant.Tone],
                        cls, weaponItem, starterMaterial, shopMaterial, weaponMeshes, overrides);
                    renderers[cls.Id] = Math.Max(renderers.TryGetValue(cls.Id, out int n) ? n : 0, count);
                }
            }
            return renderers;
        }

        /// §15.7's budget: the worst-case party (every class at its larger body, armor on) plus the largest enemy.
        public static void CheckPartyBudget(Dictionary<string, int> perClass, int enemyRenderers)
        {
            int total = perClass.Values.Sum() + enemyRenderers;
            Debug.Log(Tag + "worst-case party renderers: " + string.Join(" + ", perClass.Select(p => $"{p.Key} {p.Value}")) +
                      $" + enemy {enemyRenderers} = {total} (budget {MaxPartyRenderers})");
            if (total > MaxPartyRenderers)
                throw new InvalidOperationException($"{Tag}the worst-case party draws {total} renderers, over §15.7's {MaxPartyRenderers}; " +
                                                    "use §15.7 lever 1 or drop a cosmetic part in classes.json");
        }

        // ----- materials -----

        /// §15.18.2, §15.18.8: one material per tone in use, sharing 01_A and the skin mask; the tone is the only difference.
        /// It multiplies the skin and the stubble swatch, so the male chin takes the tone too.
        static Dictionary<string, Material> GenerateHeroMaterials(GameTables tables, Shader toon)
        {
            var heroes = tables.Heroes;
            var atlas = LoadTexture(HeroAtlas);
            var mask = LoadTexture(SyntyPostprocessor.SkinMask);
            var reference = ParseColour(heroes.SkinReference, "skinReference").linear;
            var stubbleReference = ParseColour(heroes.StubbleReference, "stubbleReference").linear;
            var used = new HashSet<string>(tables.Classes.SelectMany(c => c.Variants.Values.Select(v => v.Tone)));

            var materials = new Dictionary<string, Material>();
            foreach (var tone in SkinToneNames.All.Where(used.Contains))
            {
                var colour = ParseColour(heroes.SkinTones[tone], "skinTones." + tone).linear;
                string path = CombatantGenerator.MaterialsFolder + "/hero_" + tone + ".mat";
                var before = CombatantGenerator.ReadBytes(path);
                var material = CombatantGenerator.LoadOrCreate(path, () => new Material(toon));
                if (material.shader != toon)
                    material.shader = toon;
                material.SetTexture("_BaseMap", atlas);
                material.SetColor("_BaseColor", Color.white);
                material.SetTexture("_SkinMask", mask);
                // The same per-channel multiplier as §15.14.3: light (the reference) multiplies by exactly 1.
                material.SetVector("_SkinTint", new Vector4(colour.r / reference.r, colour.g / reference.g, colour.b / reference.b, 1f));
                material.SetVector("_StubbleTint", new Vector4(colour.r / stubbleReference.r, colour.g / stubbleReference.g, colour.b / stubbleReference.b, 1f));
                material.EnableKeyword("_SKIN_TINT");
                CombatantGenerator.Save(material, path, before);
                LogDerived(path, new[] { HeroAtlas, SyntyPostprocessor.SkinMask });
                materials[tone] = material;
            }
            return materials;
        }

        /// A weapon's material, one per texture (§15.18.8): the mesh's default atlas from classes.json weaponMeshes, or the
        /// item's own texture (bow_2). Every weapon on one texture shares its material.
        static Material WeaponMaterial(string mesh, ItemDefinition? item, Dictionary<string, WeaponMeshDefinition> meshes, Shader toon,
            Dictionary<string, Material> cache)
        {
            if (!meshes.TryGetValue(mesh, out var entry))
                throw new InvalidOperationException($"{Tag}no weaponMeshes entry for '{mesh}' in classes.json");
            string texture = item != null && !string.IsNullOrEmpty(item.Texture) ? item.Texture! : entry.Texture;
            if (cache.TryGetValue(texture, out var cached))
                return cached;

            string texturePath = SyntyPostprocessor.TexturesFolder + texture + ".png";
            string path = CombatantGenerator.MaterialsFolder + "/weapon_" + texture + ".mat";
            var material = CombatantGenerator.GenerateToonMaterial(path, toon, LoadTexture(texturePath), Color.white);
            LogDerived(path, new[] { texturePath });
            cache[texture] = material;
            return material;
        }

        // ----- prefabs -----

        static int GeneratePrefab(string id, GameObject heroModel, VariantDefinition variant, List<string> armorParts,
            Material bodyMaterial, ClassDefinition cls, ItemDefinition weaponItem, Material starterMaterial, Material shopMaterial,
            Dictionary<string, WeaponMeshDefinition> meshes, AnimatorOverrideController overrides)
        {
            string path = CombatantGenerator.PrefabsFolder + "/" + id + ".prefab";
            var before = CombatantGenerator.ReadBytes(path);

            var root = new GameObject(id);
            try
            {
                var model = Object.Instantiate(heroModel);
                model.name = heroModel.name;
                model.transform.SetParent(root.transform, false);
                var animator = model.GetComponent<Animator>();
                if (animator == null || !animator.isHuman)
                    throw new InvalidOperationException($"{Tag}{id}: {SyntyPostprocessor.HeroModel} has no Humanoid Animator");

                var bodyRenderers = KeepParts(id, model, SyntyPostprocessor.HeroModel, variant.Parts, armorParts, out var armorRenderers);
                foreach (var renderer in bodyRenderers.Concat(armorRenderers))
                    CombatantGenerator.ApplyMaterial(renderer, bodyMaterial);

                // §15.3: body renderers only (the weapons come later), without and with the armor.
                foreach (var renderer in armorRenderers)
                    renderer.gameObject.SetActive(false);
                float height = ActiveHeight(model);
                foreach (var renderer in armorRenderers)
                    renderer.gameObject.SetActive(true);
                float armoredHeight = ActiveHeight(model);
                foreach (var h in new[] { height, armoredHeight })
                    if (h < CombatantGenerator.MinHeight || h > CombatantGenerator.MaxHeight)
                        throw new InvalidOperationException(
                            $"{Tag}{id} stands {height:F2} m ({armoredHeight:F2} m with armor), outside {CombatantGenerator.MinHeight}–{CombatantGenerator.MaxHeight} m; " +
                            "fix SyntyPostprocessor.GlobalScale, not the data");
                foreach (var renderer in armorRenderers)
                    renderer.gameObject.SetActive(false);

                var sockets = AddSockets(id, animator);
                if (!sockets.TryGetValue(cls.WeaponSocket, out var socket))
                    throw new InvalidOperationException($"{Tag}{id}: weaponSocket '{cls.WeaponSocket}' is not one of {string.Join(", ", sockets.Keys)}");
                var starter = AddWeapon(id, socket, cls.WeaponMesh, StarterWeapon, starterMaterial, meshes);
                var shop = AddWeapon(id, socket, weaponItem.Mesh!, weaponItem.Id, shopMaterial, meshes);
                shop.SetActive(false);

                animator.runtimeAnimatorController = overrides;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.updateMode = AnimatorUpdateMode.Normal;
                if (model.GetComponent<AnimationEventRelay>() == null)
                    model.AddComponent<AnimationEventRelay>();

                // Animation-driven combat, no physics on characters (§5): strip anything the FBX brought.
                foreach (var collider in root.GetComponentsInChildren<Collider>(true))
                    Object.DestroyImmediate(collider);
                foreach (var rigidbody in root.GetComponentsInChildren<Rigidbody>(true))
                    Object.DestroyImmediate(rigidbody);

                PrefabUtility.SaveAsPrefabAsset(root, path, out bool success);
                if (!success)
                    throw new InvalidOperationException(Tag + "could not save " + path);
                CombatantGenerator.Report(path, before);

                int skinned = bodyRenderers.Count;
                int armor = armorRenderers.Count;
                int weapon = starter.GetComponentsInChildren<Renderer>().Length;
                Debug.Log(Tag + string.Format(CultureInfo.InvariantCulture,
                    "{0}: {1:F2} m, {2:F2} m with armor; renderers {3} skinned + {4} armor + {5} weapon = {6}; {7} under {8}, {9} inactive",
                    id, height, armoredHeight, skinned, armor, weapon, skinned + armor + weapon, cls.WeaponMesh, cls.WeaponSocket, weaponItem.Mesh));
                LogDerived(path, new[] { SyntyPostprocessor.HeroModel, WeaponPath(cls.WeaponMesh), WeaponPath(weaponItem.Mesh!) }.Distinct());
                return skinned + armor + weapon;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// Keeps exactly the variant's parts and the class armor's parts for this body (§15.3), destroys every other part,
        /// then the part groups left empty. The skeleton stays whole. Enemies keep their parts the same way (§15.16.2).
        internal static List<Renderer> KeepParts(string id, GameObject model, string source, List<string> parts, List<string> armorParts,
            out List<Renderer> armor)
        {
            var byName = model.GetComponentsInChildren<SkinnedMeshRenderer>(true).GroupBy(r => r.name).ToDictionary(g => g.Key, g => g.ToList());
            var overlap = parts.Intersect(armorParts).ToList();
            if (overlap.Count > 0)
                throw new InvalidOperationException($"{Tag}{id}: armor parts are also outfit parts: {string.Join(", ", overlap)}");

            List<Renderer> Find(IEnumerable<string> names)
            {
                var found = new List<Renderer>();
                foreach (var name in names)
                {
                    if (!byName.TryGetValue(name, out var list))
                        throw new InvalidOperationException($"{Tag}{id}: part '{name}' is not in {source}");
                    if (list.Count > 1)
                        throw new InvalidOperationException($"{Tag}{id}: part name '{name}' is used by {list.Count} renderers in {source}");
                    found.Add(list[0]);
                }
                return found;
            }

            var kept = Find(parts);
            armor = Find(armorParts);
            var keep = new HashSet<Renderer>(kept.Concat(armor));
            foreach (var renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (!keep.Contains(renderer))
                    Object.DestroyImmediate(renderer.gameObject);

            var animator = model.GetComponent<Animator>();
            var skeleton = animator.GetBoneTransform(HumanBodyBones.Hips);
            while (skeleton.parent != model.transform)
                skeleton = skeleton.parent;
            for (int i = model.transform.childCount - 1; i >= 0; i--)
            {
                var child = model.transform.GetChild(i);
                if (child != skeleton)
                    PruneEmpty(child);
            }
            return kept;
        }

        static void PruneEmpty(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--)
                PruneEmpty(t.GetChild(i));
            if (t.childCount == 0 && t.GetComponent<Renderer>() == null)
                Object.DestroyImmediate(t.gameObject);
        }

        static float ActiveHeight(GameObject model)
        {
            var renderers = model.GetComponentsInChildren<Renderer>();
            var all = renderers[0].bounds;
            foreach (var r in renderers)
                all.Encapsulate(r.bounds);
            return all.size.y;
        }

        /// §15.3 and §15.18.3: empty children of the avatar's bones at identity, scaled so their world scale is 1; the
        /// per-mesh grip carries the whole alignment.
        internal static Dictionary<string, Transform> AddSockets(string id, Animator animator)
        {
            var sockets = new Dictionary<string, Transform>();
            foreach (var (name, bone, fallback) in Sockets)
            {
                var parent = animator.GetBoneTransform(bone) ?? animator.GetBoneTransform(fallback);
                if (parent == null)
                    throw new InvalidOperationException($"{Tag}{id}: the avatar maps neither {bone} nor {fallback} for {name}");
                var socket = new GameObject(name).transform;
                socket.SetParent(parent, false);
                var s = parent.lossyScale;
                socket.localScale = new Vector3(1f / s.x, 1f / s.y, 1f / s.z);
                sockets[name] = socket;
            }
            return sockets;
        }

        /// The weapon under the socket, named by role (§15.15.2: "starter" or the item id), posed by the mesh's grip from
        /// data. It is rebuilt from the FBX's meshes rather than instantiated, so the FBX root's scene offset (§15.18.3) and
        /// its empty nodes stay behind. Only the mesh entry's parts are kept.
        internal static GameObject AddWeapon(string id, Transform socket, string mesh, string role, Material material,
            Dictionary<string, WeaponMeshDefinition> meshes)
        {
            if (!meshes.TryGetValue(mesh, out var grip))
                throw new InvalidOperationException($"{Tag}{id}: no weaponMeshes entry for '{mesh}' in classes.json");
            var source = CombatantGenerator.LoadModel(WeaponPath(mesh));
            var pieces = source.GetComponentsInChildren<MeshRenderer>(true).ToList();
            foreach (var part in grip.Parts)
                if (!pieces.Any(r => r.name == part))
                    throw new InvalidOperationException($"{Tag}{id}: weaponMeshes.{mesh}.parts names '{part}', which is not a renderer in {WeaponPath(mesh)}");
            if (grip.Parts.Count > 0)
                pieces = pieces.Where(r => grip.Parts.Contains(r.name)).ToList();

            var weapon = new GameObject(role);
            weapon.transform.SetParent(socket, false);
            weapon.transform.localPosition = new Vector3((float)grip.Pos[0], (float)grip.Pos[1], (float)grip.Pos[2]);
            weapon.transform.localEulerAngles = new Vector3((float)grip.Euler[0], (float)grip.Euler[1], (float)grip.Euler[2]);
            weapon.transform.localScale = Vector3.one;

            var renderers = new List<Renderer>();
            foreach (var piece in pieces)
            {
                var filter = piece.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null)
                    throw new InvalidOperationException($"{Tag}{id}: {WeaponPath(mesh)} renderer '{piece.name}' has no mesh");
                // The piece's pose relative to the FBX root, so the root's scene offset is dropped.
                var pose = source.transform.worldToLocalMatrix * piece.transform.localToWorldMatrix;
                // Prefixed by role so no name repeats in the prefab: the bow and bow_2 share a mesh, and Unity reissues
                // the fileIDs of same-named objects on every save.
                var go = new GameObject(role + "_" + piece.name);
                go.transform.SetParent(weapon.transform, false);
                go.transform.localPosition = pose.GetPosition();
                go.transform.localRotation = pose.rotation;
                go.transform.localScale = pose.lossyScale;
                go.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                var renderer = go.AddComponent<MeshRenderer>();
                CombatantGenerator.ApplyMaterial(renderer, material);
                renderers.Add(renderer);
            }

            var bounds = renderers[0].bounds;
            foreach (var r in renderers)
                bounds.Encapsulate(r.bounds);
            float distance = Mathf.Sqrt(bounds.SqrDistance(socket.position));
            if (distance > MaxWeaponDistance)
                throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture,
                    "{0}{1}: {2} ({3}) lies {4:F2} m from {5}, over {6} m; a scene offset survived or the grip is wrong",
                    Tag, id, mesh, role, distance, socket.name, MaxWeaponDistance));
            Debug.Log(string.Format(CultureInfo.InvariantCulture, "{0}{1}: {2} as {3}, bounds centre {4:F2} m from {5}",
                Tag, id, mesh, role, Vector3.Distance(bounds.center, socket.position), socket.name));
            return weapon;
        }

        // ----- inputs -----

        internal static string WeaponPath(string mesh) => SyntyPostprocessor.WeaponsFolder + mesh + ".fbx";

        internal static Texture2D LoadTexture(string path)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null)
                throw new InvalidOperationException(Tag + "texture not imported: " + path + " (is it on Tools/synty/keep.txt?)");
            return texture;
        }

        static Color ParseColour(string hex, string what)
        {
            if (ColorUtility.TryParseHtmlString(hex, out var colour))
                return colour;
            throw new InvalidOperationException($"{Tag}classes.json {what} '{hex}' is not a #RRGGBB colour");
        }

        /// §15.18.1: every Synty avatar the heroes and enemies use maps the shared 40 bones; a missed SQ_REIMPORT pass cannot go unnoticed.
        internal static void CheckAvatar(string path)
        {
            if (!path.StartsWith(SyntyPostprocessor.Root, StringComparison.Ordinal))
                return;
            var avatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
            int mapped = avatar == null ? 0 : avatar.humanDescription.human.Length;
            if (avatar == null || !avatar.isHuman || !avatar.isValid || mapped < MinMappedBones)
                throw new InvalidOperationException($"{Tag}{path}: avatar " + (avatar == null ? "missing" : $"isHuman {avatar.isHuman} isValid {avatar.isValid}, {mapped} bones mapped") +
                                                    $"; expected a valid Humanoid with at least {MinMappedBones} (re-run the import with SQ_REIMPORT=1, DEVICE-TESTING §2c)");
        }

        /// §15.15.6: one line per generated asset that derives from a Synty file, so the sign-off log names them all.
        internal static void LogDerived(string asset, IEnumerable<string> sources) =>
            Debug.Log(Derived + asset + " <- " + string.Join(", ", sources.Distinct().OrderBy(s => s, StringComparer.Ordinal)));
    }
}
