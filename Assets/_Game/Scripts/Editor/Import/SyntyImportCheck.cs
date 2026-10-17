using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Game.Editor.Import
{
    /// -executeMethod report for the Synty import (PLAN-M2 D5). Prints ARCHITECTURE §15.1/§15.15.2 facts (a)–(g) and the
    /// modular part names, and exits 1 if a SyntyPostprocessor rule is missing from a file, an avatar is not a valid
    /// Humanoid, or a §15.15.5 clip is not on disk.
    /// Run without -nographics: fact (b)'s swatch-grid measurement blits the atlas, which needs a graphics device.
    /// Fact (b) and the package lists come from Builds/synty-extract (Tools/synty/extract.sh); the check prints names,
    /// sizes, counts and texture slot assignments only, never file contents.
    public static class SyntyImportCheck
    {
        const string Tag = "[SyntyImportCheck] ";
        const string PacksFile = "Tools/synty/packs.txt";
        const string ScratchFolder = "Builds/synty-extract/";
        const string HeroPack = "POLYGON - Modular Fantasy Hero Characters";
        const string BowPack = "ANIMATION - Bow Combat";
        const int GridTolerance = 10; // 0–255 per channel: ASTC 6×6 noise inside a flat swatch stays under this

        // Materials whose texture slots answer fact (b): the pack's character material (Synty's mask shader), its
        // plain-albedo twin, and the Bow Combat bow material.
        static readonly (string Pack, string Path)[] SlotMaterials =
        {
            (HeroPack, "Assets/Synty/PolygonFantasyHeroCharacters/Materials/FantasyHero.mat"),
            (HeroPack, "Assets/Synty/PolygonFantasyHeroCharacters/Materials/Standard/PolygonFantasyHero_Texture_01_A.mat"),
            (BowPack, "Assets/Synty/AnimationBowCombat/Materials/BowCombat_Texture_01.mat"),
        };

        // §15.15.5's state table as of D5, {g} expanded. D6 moves the names into classes.json, where the generator checks them.
        static readonly string[] TableClips =
        {
            "A_Idle_Base_Sword",
            "A_POLY_BOW_Stand_Idle_Base_WithBow_Masc", "A_POLY_BOW_Stand_Idle_Base_WithBow_Femn",
            "A_Attack_LightCombo01A_ReturnToIdle_Sword", "A_Attack_HeavyStab01_ReturnToIdle_Sword",
            "A_Attack_HeavyCombo01A_ReturnToIdle_Sword", "A_Attack_HeavyFlourish01_ReturnToIdle_Sword",
            "A_Attack_LightLeaping01_ReturnToIdle_Sword",
            "A_POLY_EMOT_Happy_ArmsRaised_Masc", "A_POLY_EMOT_Happy_ArmsRaised_Femn",
            "A_Hit_F_React_Sword", "A_Hit_F_Stagger_Sword", "A_Block_Loop_Sword", "A_Death_B_01_Sword",
            "A_POLY_EMOT_Happy_FistPump_Masc", "A_POLY_EMOT_Happy_FistPump_Femn",
        };

        // The table leaves the Archer's Attack and Skill to the import (fact f); these are the kept candidates.
        static readonly string[] ArcherCandidates = { "A_POLY_BOW_Stand_Shoot_ToBowDown_Neut", "A_POLY_BOW_Stand_Shoot_Leaping_Neut" };

        static readonly HumanBodyBones[] ReportedBones =
        {
            HumanBodyBones.Hips, HumanBodyBones.Chest, HumanBodyBones.UpperChest, HumanBodyBones.Head,
            HumanBodyBones.LeftHand, HumanBodyBones.RightHand,
        };

        public static void Run()
        {
            if (Environment.GetEnvironmentVariable("SQ_REIMPORT") == "1")
            {
                Log("SQ_REIMPORT=1: forcing a recursive reimport of " + SyntyPostprocessor.Root);
                AssetDatabase.ImportAsset(SyntyPostprocessor.Root.TrimEnd('/'),
                    ImportAssetOptions.ForceUpdate | ImportAssetOptions.ImportRecursive);
            }

            var failures = new List<string>();
            var packs = LoadPacks();
            ReportCharacters(failures);
            ReportColour(failures, packs);
            ReportWeapons(failures, packs);
            ReportAnimations(failures);

            if (failures.Count == 0)
            {
                Log("ok");
                return;
            }

            string message = Tag + failures.Count + " failure(s):\n  " + string.Join("\n  ", failures);
            Debug.LogError(message);
            if (Application.isBatchMode)
                EditorApplication.Exit(1);
            else
                throw new InvalidOperationException(message);
        }

        // ---- (a), (d) and the part names ------------------------------------------------------------------------

        static void ReportCharacters(List<string> failures)
        {
            var partSets = new List<(string File, HashSet<string> Parts)>();
            foreach (string path in ModelPaths(SyntyPostprocessor.CharactersFolder))
            {
                var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                CheckModelRules(failures, path, importer);
                if (importer.animationType != ModelImporterAnimationType.Human || importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel)
                    failures.Add($"{path}: rig {importer.animationType}/{importer.avatarSetup}, expected Human/CreateFromThisModel");
                if (importer.importAnimation)
                    failures.Add($"{path}: importAnimation is on");
                var avatar = CheckAvatar(failures, path);

                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var instance = Object.Instantiate(model);
                try
                {
                    var renderers = instance.GetComponentsInChildren<Renderer>(true);
                    var bounds = Encapsulate(renderers);
                    Log($"(a) {path}: fileScale {F(importer.fileScale)}, globalScale {F(importer.globalScale)}, {renderers.Length} renderers " +
                        $"({renderers.OfType<SkinnedMeshRenderer>().Count()} skinned), {Triangles(renderers)} triangles in all, " +
                        $"avatar isHuman {avatar != null && avatar.isHuman} isValid {avatar != null && avatar.isValid}");

                    var animator = instance.GetComponent<Animator>();
                    if (animator != null && animator.avatar == null)
                        animator.avatar = avatar;
                    if (animator != null && animator.isHuman)
                    {
                        foreach (var bone in ReportedBones)
                        {
                            var t = animator.GetBoneTransform(bone);
                            Log($"(a)   {bone} -> " + (t == null ? "none" : $"{t.name}, lossyScale {V(t.lossyScale)}, world y {F(t.position.y)}"));
                        }
                    }
                    else
                    {
                        Log("(a)   no Humanoid animator on the instance; bones not reported");
                    }

                    Log($"(d) {Path.GetFileName(path)}, all parts at once: height {F(bounds.size.y)} m at globalScale {F(SyntyPostprocessor.GlobalScale)}, " +
                        $"{F(bounds.size.y / SyntyPostprocessor.GlobalScale)} m at globalScale 1 (y {F(bounds.min.y)}..{F(bounds.max.y)})");

                    var parts = new HashSet<string>(renderers.Select(r => r.name));
                    partSets.Add((path, parts));
                    if (partSets.Count == 1)
                    {
                        ReportSkeleton(instance.transform, renderers);
                        ReportParts(instance.transform, renderers);
                    }
                }
                finally
                {
                    Object.DestroyImmediate(instance);
                }
            }

            if (partSets.Count == 0)
                failures.Add("no character model under " + SyntyPostprocessor.CharactersFolder);
            for (int i = 1; i < partSets.Count; i++)
            {
                var a = partSets[0];
                var b = partSets[i];
                var onlyA = a.Parts.Except(b.Parts).OrderBy(n => n, StringComparer.Ordinal).ToList();
                var onlyB = b.Parts.Except(a.Parts).OrderBy(n => n, StringComparer.Ordinal).ToList();
                Log($"(a) parts: {a.File} has {a.Parts.Count}, {b.File} has {b.Parts.Count}; only in the first: {onlyA.Count}" +
                    (onlyA.Count == 0 ? "" : " [" + string.Join(", ", onlyA) + "]") + $"; only in the second: {onlyB.Count}" +
                    (onlyB.Count == 0 ? "" : " [" + string.Join(", ", onlyB) + "]"));
            }
        }

        /// Every transform a part is skinned to, with its parent, and any name used twice: a Humanoid bone map
        /// addresses bones by name, so a duplicated name cannot be mapped (§15.7's explicit humanDescription rule).
        static void ReportSkeleton(Transform root, Renderer[] renderers)
        {
            var bones = new HashSet<Transform>();
            foreach (var smr in renderers.OfType<SkinnedMeshRenderer>())
            foreach (var bone in smr.bones)
                for (var t = bone; t != null && t != root; t = t.parent)
                    bones.Add(t);
            var ordered = bones.OrderBy(b => RelativePath(root, b), StringComparer.Ordinal).ToList();
            var duplicates = ordered.GroupBy(b => b.name).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            Log($"(a) skeleton: {ordered.Count} transforms; names used more than once: " + (duplicates.Count == 0 ? "none" : string.Join(", ", duplicates)));
            Log("(a) skeleton (name<parent): " + string.Join(", ", ordered.Select(b => b.name + "<" + (b.parent == root ? "(root)" : b.parent.name))));
        }

        /// One line per parent group (body and category), each part with its triangles and top height, then per-body
        /// base-outfit sums: every group's "_00" part (the pack's starting piece), the input to the §15.7 budget.
        static void ReportParts(Transform root, Renderer[] renderers)
        {
            var groups = renderers
                .GroupBy(r => RelativePath(root, r.transform.parent))
                .OrderBy(g => g.Key, StringComparer.Ordinal);
            foreach (var group in groups)
            {
                var items = group.OrderBy(r => r.name, StringComparer.Ordinal)
                    .Select(r => $"{r.name} [{Triangles(new[] { r })}t, top {F(r.bounds.max.y)}]");
                Log($"(parts) {group.Key} ({group.Count()}): {string.Join(", ", items)}");
            }

            var byTop = renderers.GroupBy(r => BodyGroup(root, r.transform)).OrderBy(g => g.Key, StringComparer.Ordinal);
            foreach (var top in byTop)
            {
                var categories = top.GroupBy(r => RelativePath(root, r.transform.parent)).ToList();
                var tris = categories.Select(c => c.Select(r => Triangles(new[] { r })).ToList()).ToList();
                var starters = top.Where(r => r.name.EndsWith("_00", StringComparison.Ordinal)).ToList();
                var startersBounds = Encapsulate(starters.ToArray());
                Log($"(d) {top.Key}: {top.Count()} parts in {categories.Count} groups; triangles per part min {tris.SelectMany(t => t).DefaultIfEmpty().Min()} " +
                    $"max {tris.SelectMany(t => t).DefaultIfEmpty().Max()}, one average part per group sums to {tris.Sum(t => t.Count == 0 ? 0 : t.Average()):F0}; " +
                    $"the {starters.Count} \"_00\" parts sum to {Triangles(starters.ToArray())} triangles" +
                    (starters.Count == 0 ? "" : $", height {F(startersBounds.size.y)} m at globalScale {F(SyntyPostprocessor.GlobalScale)} " +
                        $"({F(startersBounds.size.y / SyntyPostprocessor.GlobalScale)} m at 1; y {F(startersBounds.min.y)}..{F(startersBounds.max.y)})"));
            }
        }

        // ---- (b) ------------------------------------------------------------------------------------------------

        static void ReportColour(List<string> failures, Dictionary<string, string> packs)
        {
            foreach (var (pack, matPath) in SlotMaterials)
            {
                var index = LoadIndex(packs, pack);
                if (index == null)
                {
                    Log($"(b) {matPath}: skipped, {pack} is not extracted (run Tools/synty/extract.sh)");
                    continue;
                }
                var guid = index.FirstOrDefault(p => p.Value == matPath).Key;
                string file = guid == null ? "" : ScratchFolder + Stem(packs[pack]) + "/" + guid + "/asset";
                if (guid == null || !File.Exists(file))
                {
                    Log($"(b) {matPath}: not in the extract");
                    continue;
                }
                var (shader, slots) = ReadMaterial(file);
                string shaderName = shader == null ? "built-in" : index.TryGetValue(shader, out var sp) ? sp : AnyPackPath(packs, shader) ?? shader;
                Log($"(b) {matPath}: shader {shaderName}");
                foreach (var (slot, tex) in slots)
                {
                    if (tex == null)
                        continue;
                    Log($"(b)   {slot} = {(index.TryGetValue(tex, out var tp) ? tp : tex + " (not in the package)")}");
                }
            }

            foreach (string path in AssetPaths("t:Texture2D", SyntyPostprocessor.TexturesFolder))
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                CheckTextureRules(failures, path, importer);
                importer.GetSourceTextureWidthAndHeight(out int sw, out int sh);
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                Log($"(b) {path}: source {sw}×{sh}, imported {tex.width}×{tex.height} {tex.format}, mips {tex.mipmapCount}");
                MeasureGrid(path, tex);
            }
        }

        /// A swatch atlas is a grid of flat cells. For each candidate grid, the share of cells whose interior is one
        /// colour: the largest grid near 100 % gives the cell size. Prints counts only.
        static void MeasureGrid(string path, Texture2D tex)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                Log($"(b)   {Path.GetFileName(path)}: grid not measured, no graphics device (run without -nographics)");
                return;
            }

            int w = tex.width, h = tex.height;
            var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var previous = RenderTexture.active;
            Graphics.Blit(tex, rt);
            RenderTexture.active = rt;
            var copy = new Texture2D(w, h, TextureFormat.RGBA32, false, false);
            copy.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            copy.Apply(false);
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
            var px = copy.GetPixels32();
            Object.DestroyImmediate(copy);

            var lines = new List<string>();
            foreach (int cells in new[] { 4, 8, 16, 32, 64, 128 })
            {
                int s = w / cells;
                int rows = h / s;
                if (s < 4 || w % cells != 0)
                    continue;
                int inset = Math.Max(1, s / 4);
                int uniform = 0;
                var colours = new HashSet<int>();
                for (int cy = 0; cy < rows; cy++)
                for (int cx = 0; cx < cells; cx++)
                {
                    var c = px[(cy * s + s / 2) * w + cx * s + s / 2];
                    bool flat = true;
                    for (int y = cy * s + inset; flat && y < (cy + 1) * s - inset; y++)
                    for (int x = cx * s + inset; x < (cx + 1) * s - inset; x++)
                    {
                        var p = px[y * w + x];
                        if (Math.Abs(p.r - c.r) > GridTolerance || Math.Abs(p.g - c.g) > GridTolerance || Math.Abs(p.b - c.b) > GridTolerance)
                        {
                            flat = false;
                            break;
                        }
                    }
                    if (!flat)
                        continue;
                    uniform++;
                    colours.Add((c.r >> 3) << 10 | (c.g >> 3) << 5 | (c.b >> 3));
                }
                lines.Add($"{cells}×{rows} of {s}px {100f * uniform / (cells * rows):F0}% flat ({colours.Count} distinct)");
            }
            Log($"(b)   {Path.GetFileName(path)} grid: " + string.Join("; ", lines));
        }

        // ---- (c), (e) --------------------------------------------------------------------------------------------

        static void ReportWeapons(List<string> failures, Dictionary<string, string> packs)
        {
            var heroIndex = LoadIndex(packs, HeroPack);
            if (heroIndex != null)
            {
                var wep = heroIndex.Values.Where(p => p.EndsWith(".fbx", StringComparison.Ordinal) && Path.GetFileName(p).StartsWith("SM_Wep_", StringComparison.Ordinal))
                    .Select(p => Path.GetFileNameWithoutExtension(p)).OrderBy(n => n, StringComparer.Ordinal).ToList();
                Log($"(c) {HeroPack}: {wep.Count} SM_Wep_* files: {string.Join(", ", wep)}");
                var bows = heroIndex.Values.Where(p => Path.GetFileName(p).Contains("Bow") && p.EndsWith(".fbx", StringComparison.Ordinal)).ToList();
                Log($"(c) {HeroPack}: bow FBX: " + (bows.Count == 0 ? "none" : string.Join(", ", bows)));
            }
            var bowIndex = LoadIndex(packs, BowPack);
            if (bowIndex != null)
            {
                var bows = bowIndex.Values.Where(p => p.Contains("/Models/Bows/") && p.EndsWith(".fbx", StringComparison.Ordinal))
                    .OrderBy(p => p, StringComparer.Ordinal).ToList();
                var prefabs = bowIndex.Values.Count(p => p.Contains("/Prefabs/Bows/") && p.EndsWith(".prefab", StringComparison.Ordinal));
                Log($"(e) {BowPack}: {bows.Count} bow FBX ({prefabs} bow prefabs, not kept): {string.Join(", ", bows.Select(Path.GetFileName))}");
            }

            foreach (string path in ModelPaths(SyntyPostprocessor.WeaponsFolder))
            {
                var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                CheckModelRules(failures, path, importer);
                if (importer.animationType != ModelImporterAnimationType.None)
                    failures.Add($"{path}: rig {importer.animationType}, expected None");

                var instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
                try
                {
                    var renderers = instance.GetComponentsInChildren<Renderer>(true);
                    var size = Encapsulate(renderers).size;
                    Log($"(c/e) {Path.GetFileName(path)}: {renderers.Length} renderer(s) ({string.Join(", ", renderers.Select(r => r.GetType().Name).Distinct())}), " +
                        $"{Triangles(renderers)} triangles, size {V(size)} m, longest {F(Mathf.Max(size.x, Mathf.Max(size.y, size.z)))} m");
                }
                finally
                {
                    Object.DestroyImmediate(instance);
                }
            }
        }

        // ---- (f), (g) --------------------------------------------------------------------------------------------

        static void ReportAnimations(List<string> failures)
        {
            var found = new Dictionary<string, AnimationClip>();
            foreach (string path in ModelPaths(SyntyPostprocessor.AnimationsFolder))
            {
                var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                CheckModelRules(failures, path, importer);
                if (importer.animationType != ModelImporterAnimationType.Human || importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel)
                    failures.Add($"{path}: rig {importer.animationType}/{importer.avatarSetup}, expected Human/CreateFromThisModel");
                if (!importer.importAnimation)
                    failures.Add($"{path}: importAnimation is off");
                var avatar = CheckAvatar(failures, path);

                ReportRig(path, importer);

                string fileName = Path.GetFileNameWithoutExtension(path);
                var settings = importer.clipAnimations;
                foreach (var s in settings)
                {
                    if (!(s.lockRootRotation && s.lockRootHeightY && s.lockRootPositionXZ && s.keepOriginalOrientation && s.keepOriginalPositionY && s.keepOriginalPositionXZ))
                        failures.Add($"{path}: clip {s.name} does not bake root rotation, height and XZ based on Original");
                }

                var clips = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                    .Where(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal)).OrderBy(c => c.name, StringComparer.Ordinal).ToList();
                if (clips.Count != 1 || clips[0].name != fileName)
                    failures.Add($"{path}: expected one clip named {fileName}, found [{string.Join(", ", clips.Select(c => c.name))}]");
                foreach (var clip in clips)
                {
                    found[clip.name] = clip;
                    var events = AnimationUtility.GetAnimationEvents(clip);
                    Log($"(g) {Path.GetFileName(path)}: avatar isHuman {avatar != null && avatar.isHuman} isValid {avatar != null && avatar.isValid}; " +
                        $"clip {clip.name} {F(clip.length)}s {F(clip.frameRate)}fps loop={clip.isLooping} events={events.Length} humanMotion={clip.humanMotion}");
                }
            }

            foreach (string name in TableClips)
            {
                if (!found.ContainsKey(name))
                    failures.Add("§15.15.5 clip not found under " + SyntyPostprocessor.AnimationsFolder + ": " + name);
            }
            Log($"(f) §15.15.5 table: {TableClips.Count(found.ContainsKey)}/{TableClips.Length} named clips found");
            foreach (string name in ArcherCandidates)
                Log($"(f) Archer candidate {name}: " + (found.TryGetValue(name, out var c) ? $"{F(c.length)}s" : "not found"));
        }

        /// Whether an animation file's automatic Humanoid map puts the body on the same transform names as the
        /// character's explicit map (the same POLYGON rig), and how many finger bones it maps.
        static void ReportRig(string path, ModelImporter importer)
        {
            var human = importer.humanDescription.human.ToDictionary(h => h.humanName, h => h.boneName);
            var differences = SyntyPostprocessor.CharacterBoneMap
                .Where(m => !human.TryGetValue(HumanTrait.BoneName[(int)m.Bone], out var b) || b != m.Transform)
                .Select(m => $"{m.Bone}: {(human.TryGetValue(HumanTrait.BoneName[(int)m.Bone], out var b) ? b : "unmapped")} vs {m.Transform}")
                .ToList();
            var fingers = human.Where(h => FingerWords.Any(w => h.Key.Contains(w))).OrderBy(h => h.Key, StringComparer.Ordinal).ToList();
            Log($"(g) {Path.GetFileName(path)}: {human.Count} bones mapped; body " +
                (differences.Count == 0 ? "on the same transforms as the character map" : "differs: " + string.Join("; ", differences)) +
                $"; {fingers.Count} finger bones" + (fingers.Count == 0 ? "" : $" (e.g. {fingers[0].Key} -> {fingers[0].Value})"));
        }

        static readonly string[] FingerWords = { "Thumb", "Index", "Middle", "Ring", "Little" };

        // ---- shared ----------------------------------------------------------------------------------------------

        static void CheckModelRules(List<string> failures, string path, ModelImporter importer)
        {
            var wrong = new List<string>();
            if (importer.materialImportMode != ModelImporterMaterialImportMode.None) wrong.Add("materialImportMode " + importer.materialImportMode);
            if (importer.isReadable) wrong.Add("isReadable");
            if (importer.importBlendShapes) wrong.Add("importBlendShapes");
            if (importer.importCameras) wrong.Add("importCameras");
            if (importer.importLights) wrong.Add("importLights");
            if (!importer.useFileScale) wrong.Add("useFileScale off");
            if (!Mathf.Approximately(importer.globalScale, SyntyPostprocessor.GlobalScale)) wrong.Add("globalScale " + F(importer.globalScale));
            if (importer.meshCompression != ModelImporterMeshCompression.Off) wrong.Add("meshCompression " + importer.meshCompression);
            if (importer.optimizeGameObjects) wrong.Add("optimizeGameObjects");
            if (wrong.Count > 0)
                failures.Add($"{path}: {string.Join(", ", wrong)} (postprocessor did not run? try SQ_REIMPORT=1)");
        }

        static void CheckTextureRules(List<string> failures, string path, TextureImporter importer)
        {
            var ios = importer.GetPlatformTextureSettings("iPhone");
            var wrong = new List<string>();
            if (!importer.sRGBTexture) wrong.Add("not sRGB");
            if (importer.maxTextureSize != SyntyPostprocessor.MaxTextureSize) wrong.Add("maxTextureSize " + importer.maxTextureSize);
            if (!importer.mipmapEnabled) wrong.Add("no mipmaps");
            if (importer.isReadable) wrong.Add("isReadable");
            if (!ios.overridden || ios.format != TextureImporterFormat.ASTC_6x6 || ios.maxTextureSize != SyntyPostprocessor.MaxTextureSize)
                wrong.Add($"iOS {(ios.overridden ? "" : "not overridden ")}{ios.format} {ios.maxTextureSize}");
            if (wrong.Count > 0)
                failures.Add($"{path}: {string.Join(", ", wrong)} (postprocessor did not run? try SQ_REIMPORT=1)");
        }

        static Avatar? CheckAvatar(List<string> failures, string path)
        {
            var avatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
            if (avatar == null || !avatar.isHuman || !avatar.isValid)
                failures.Add($"{path}: avatar " + (avatar == null ? "missing" : $"isHuman {avatar.isHuman} isValid {avatar.isValid}"));
            return avatar;
        }

        /// packs.txt store_name -> file, so the check finds each pack's scratch extract the way extract.sh does.
        static Dictionary<string, string> LoadPacks()
        {
            var packs = new Dictionary<string, string>();
            if (!File.Exists(PacksFile))
                return packs;
            foreach (string line in File.ReadAllLines(PacksFile).Skip(1))
            {
                var cols = line.Split('\t');
                if (cols.Length >= 7 && cols[0].Length > 0)
                    packs[cols[0]] = cols[6];
            }
            return packs;
        }

        /// guid -> pathname, from the index extract.sh writes next to each pack's scratch folder.
        static Dictionary<string, string>? LoadIndex(Dictionary<string, string> packs, string pack)
        {
            if (!packs.TryGetValue(pack, out var file))
                return null;
            string index = ScratchFolder + Stem(file) + ".index";
            if (!File.Exists(index))
                return null;
            var map = new Dictionary<string, string>();
            foreach (string line in File.ReadAllLines(index))
            {
                int tab = line.IndexOf('\t');
                if (tab > 0)
                    map[line.Substring(0, tab)] = line.Substring(tab + 1);
            }
            return map;
        }

        static string? AnyPackPath(Dictionary<string, string> packs, string guid)
        {
            foreach (string pack in packs.Keys)
            {
                var index = LoadIndex(packs, pack);
                if (index != null && index.TryGetValue(guid, out var path))
                    return path;
            }
            return null;
        }

        static string Stem(string file) => file.EndsWith(".unitypackage", StringComparison.Ordinal) ? file.Substring(0, file.Length - ".unitypackage".Length) : file;

        static readonly Regex SlotLine = new Regex(@"^\s+- (\S+):\s*$");
        static readonly Regex TextureRef = new Regex(@"m_Texture: \{fileID: -?\d+(?:, guid: ([0-9a-f]{32}))?");
        static readonly Regex ShaderRef = new Regex(@"m_Shader: \{fileID: -?\d+, guid: ([0-9a-f]{32})");
        const string BuiltinGuid = "0000000000000000f000000000000000";

        /// The shader guid and the m_TexEnvs slot -> texture guid pairs of a material's YAML; nothing else is read.
        static (string? Shader, List<(string Slot, string? Texture)> Slots) ReadMaterial(string file)
        {
            string? shader = null;
            var slots = new List<(string, string?)>();
            bool inTextures = false;
            string? slot = null;
            foreach (string line in File.ReadLines(file))
            {
                var sm = ShaderRef.Match(line);
                if (sm.Success && shader == null)
                    shader = sm.Groups[1].Value == BuiltinGuid ? null : sm.Groups[1].Value;
                string trimmed = line.Trim();
                if (trimmed == "m_TexEnvs:") { inTextures = true; continue; }
                if (trimmed == "m_Ints:" || trimmed == "m_Floats:" || trimmed == "m_Colors:") inTextures = false;
                if (!inTextures)
                    continue;
                var slotMatch = SlotLine.Match(line);
                if (slotMatch.Success) { slot = slotMatch.Groups[1].Value; continue; }
                var tm = TextureRef.Match(line);
                if (tm.Success && slot != null)
                {
                    slots.Add((slot, tm.Groups[1].Success ? tm.Groups[1].Value : null));
                    slot = null;
                }
            }
            return (shader, slots);
        }

        static Bounds Encapsulate(Renderer[] renderers)
        {
            var bounds = new Bounds();
            for (int i = 0; i < renderers.Length; i++)
            {
                if (i == 0) bounds = renderers[i].bounds;
                else bounds.Encapsulate(renderers[i].bounds);
            }
            return bounds;
        }

        static long Triangles(Renderer[] renderers)
        {
            long total = 0;
            foreach (var r in renderers)
            {
                var mesh = r is SkinnedMeshRenderer s ? s.sharedMesh : r.GetComponent<MeshFilter>()?.sharedMesh;
                if (mesh == null)
                    continue;
                for (int i = 0; i < mesh.subMeshCount; i++)
                    total += mesh.GetIndexCount(i) / 3;
            }
            return total;
        }

        static string RelativePath(Transform root, Transform? t)
        {
            var names = new List<string>();
            for (; t != null && t != root; t = t.parent)
                names.Add(t.name);
            names.Reverse();
            return names.Count == 0 ? "(root)" : string.Join("/", names);
        }

        /// The second level under the model root: Male_Parts, Female_Parts or All_Gender_Parts in this pack.
        static string BodyGroup(Transform root, Transform t)
        {
            var segments = RelativePath(root, t).Split('/');
            return segments.Length > 1 ? segments[1] : segments[0];
        }

        static List<string> ModelPaths(string folder) => AssetPaths("t:Model", folder);

        static List<string> AssetPaths(string filter, string folder) =>
            AssetDatabase.IsValidFolder(folder.TrimEnd('/'))
                ? AssetDatabase.FindAssets(filter, new[] { folder.TrimEnd('/') }).Select(AssetDatabase.GUIDToAssetPath).Distinct()
                    .OrderBy(p => p, StringComparer.Ordinal).ToList()
                : new List<string>();

        static string F(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);
        static string V(Vector3 v) => $"({F(v.x)}, {F(v.y)}, {F(v.z)})";

        static void Log(string message) => Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, "{0}", Tag + message);
    }
}
