using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Game.Editor.Import
{
    /// -executeMethod report for the Synty import (PLAN-M2 D5, D6.1). Prints ARCHITECTURE §15.1/§15.15.2 facts (a)–(g),
    /// the modular part names and the §15.18.2 skin measurement, and exits 1 if a SyntyPostprocessor rule is missing from
    /// a file, an avatar is not a valid Humanoid on the shared bone map, a §15.15.5 clip is not on disk, a material
    /// references EditorOnly/, or the skin mask channel is not skin-coloured.
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
            HumanBodyBones.LeftThumbProximal, HumanBodyBones.RightIndexProximal, HumanBodyBones.RightMiddleDistal,
        };

        // §15.18.2. Synty's POLYGON_CustomCharacters.shadergraph lerps _Color_Skin in with T = Mask_03's red channel, the
        // colour on the Lerp's A side, so skin is where red is low (read from the scratch extract on 2026-10-17). Every
        // Synty mask marks its regions black on white. ReportSkin confirms both from the pixels.
        const string ReferenceAtlas = "PolygonFantasyHero_Texture_01_A";
        const string SkinMask = "PolygonFantasyHero_Texture_Mask_03";
        const int SkinChannel = 0;
        const float SameClothesTolerance = 3f; // §15.18.2: at most 3/255 mean absolute difference outside the skin
        // Our reading of "clearly lighter or darker": a skin luma change of at least a sixth of the light–dark gap below.
        const float ClearlyDifferentLuma = 16f;

        // Which colour each mask channel gates in Synty's shader graph (its Lerp chain), so an outside-skin difference can be
        // placed. Mask_06 is not in the graph.
        static readonly (string Mask, int Channel, string Region)[] MaskRegions =
        {
            ("PolygonFantasyHero_Texture_Mask_01", 0, "primary"), ("PolygonFantasyHero_Texture_Mask_01", 1, "secondary"),
            ("PolygonFantasyHero_Texture_Mask_01", 2, "body art"), ("PolygonFantasyHero_Texture_Mask_02", 0, "metal primary"),
            ("PolygonFantasyHero_Texture_Mask_02", 1, "metal secondary"), ("PolygonFantasyHero_Texture_Mask_02", 2, "metal dark"),
            ("PolygonFantasyHero_Texture_Mask_03", 1, "scar"), ("PolygonFantasyHero_Texture_Mask_03", 2, "stubble"),
            ("PolygonFantasyHero_Texture_Mask_04", 0, "leather primary"), ("PolygonFantasyHero_Texture_Mask_04", 1, "leather secondary"),
            ("PolygonFantasyHero_Texture_Mask_04", 2, "hair"), ("PolygonFantasyHero_Texture_Mask_05", 0, "eyes"),
        };

        // §15.14.3's skinTones, the classes a measured skin colour is sorted into (nearest by luma).
        static readonly (string Tone, Color32 Colour)[] ReferenceTones =
        {
            ("light", new Color32(0xF6, 0xC0, 0x9C, 0xFF)),
            ("medium", new Color32(0xC6, 0x86, 0x42, 0xFF)),
            ("dark", new Color32(0x8D, 0x55, 0x24, 0xFF)),
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
            ReportSkin(failures);
            ReportStubble(failures);
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
                if (SyntyPostprocessor.UsesSharedMap(path))
                    CheckBoneMap(failures, path, avatar);

                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var instance = Object.Instantiate(model);
                try
                {
                    var renderers = instance.GetComponentsInChildren<Renderer>(true);
                    var bounds = Encapsulate(renderers);
                    Log($"(a) {path}: fileScale {F(importer.fileScale)}, globalScale {F(importer.globalScale)}, {renderers.Length} renderers " +
                        $"({renderers.OfType<SkinnedMeshRenderer>().Count()} skinned), {Triangles(renderers)} triangles in all, " +
                        $"avatar isHuman {avatar != null && avatar.isHuman} isValid {avatar != null && avatar.isValid}, {MappedBones(avatar)}");

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

        // ---- §15.18.2 skin tones ---------------------------------------------------------------------------------

        /// Which mask channel is skin, then each alternative hero atlas against 01_A: the mean colour inside the skin
        /// mask and the mean absolute difference outside it. Prints means and counts only.
        static void ReportSkin(List<string> failures)
        {
            var paths = AssetPaths("t:Texture2D", SyntyPostprocessor.EditorOnlyFolder);
            foreach (string path in paths)
                CheckEditorOnlyRules(failures, path, (TextureImporter)AssetImporter.GetAtPath(path));
            CheckNoEditorOnlyReferences(failures);
            if (paths.Count == 0)
            {
                // D6.2 recorded §15.18.2 step 2 (the shader skin mask) and took the measuring set off keep.txt.
                Log($"(skin) no measuring inputs under {SyntyPostprocessor.EditorOnlyFolder}: the step-2 decision is recorded (PLAN-M2 D6.1); " +
                    $"the hero materials sample {SyntyPostprocessor.SkinMask}");
                return;
            }

            var textures = paths.ToDictionary(p => Path.GetFileNameWithoutExtension(p), p => AssetDatabase.LoadAssetAtPath<Texture2D>(p));
            if (!textures.TryGetValue(ReferenceAtlas, out var reference) || !textures.TryGetValue(SkinMask, out var skinMask) ||
                !reference.isReadable || !skinMask.isReadable)
            {
                failures.Add($"§15.18.2: {ReferenceAtlas} or {SkinMask} is missing or not readable under {SyntyPostprocessor.EditorOnlyFolder}");
                return;
            }
            int w = reference.width, h = reference.height;
            var refPx = reference.GetPixels32();

            foreach (var mask in textures.Where(t => t.Key.Contains("_Mask_")).OrderBy(t => t.Key, StringComparer.Ordinal))
            {
                var m = Resample(mask.Value, w, h);
                var cells = new List<string>();
                for (int c = 0; c < 3; c++)
                {
                    var inside = Inside(m, c);
                    int count = inside.Count(x => x);
                    var mean = Mean(refPx, inside);
                    cells.Add($"{"RGB"[c]} {100f * count / inside.Length:F1}%" + (count == 0 ? "" : $" {Hex(mean)}{(IsSkinColour(mean) ? " skin-like" : "")}"));
                }
                Log($"(skin) {mask.Key}: share of texels where the channel < 0.5, and the 01_A mean there: {string.Join("; ", cells)}");
            }

            var skin = Inside(Resample(skinMask, w, h), SkinChannel);
            var skinMean = Mean(refPx, skin);
            bool confirmed = IsSkinColour(skinMean);
            Log($"(skin) skin = {SkinMask} channel {"RGB"[SkinChannel]} < 0.5 (Synty's shader graph), {100f * skin.Count(x => x) / skin.Length:F1}% of texels; " +
                $"01_A mean there {Hex(skinMean)}, " + (confirmed ? "skin-coloured: confirmed" : "NOT skin-coloured"));
            if (!confirmed)
                failures.Add($"§15.18.2: 01_A where {SkinMask}.{"RGB"[SkinChannel]} < 0.5 averages {Hex(skinMean)}, not a skin colour");
            foreach (var old in textures.Where(t => t.Key.Contains("_Mask_Skin_")).OrderBy(t => t.Key, StringComparer.Ordinal))
            {
                var o = Inside(Resample(old.Value, w, h), 0);
                int both = 0, either = 0;
                for (int i = 0; i < o.Length; i++)
                {
                    if (o[i] && skin[i]) both++;
                    if (o[i] || skin[i]) either++;
                }
                Log($"(skin) {old.Key} (Old/): red < 0.5 overlaps the skin region by {(either == 0 ? 0 : 100f * both / either):F1}% (intersection over union)");
            }

            float refLuma = Luma(skinMean);
            var tones = new Dictionary<string, List<string>>();
            var regionDiffs = new List<string>();
            Log("(skin) atlas | skin mean | skin luma (vs 01_A) | outside-skin mean abs diff /255 | qualifies | tone");
            foreach (var atlas in textures.Where(t => t.Key.StartsWith("PolygonFantasyHero_Texture_0", StringComparison.Ordinal))
                         .OrderBy(t => t.Key, StringComparer.Ordinal))
            {
                string shortName = atlas.Key.Substring("PolygonFantasyHero_Texture_".Length);
                if (atlas.Value.width != w || atlas.Value.height != h || !atlas.Value.isReadable)
                {
                    failures.Add($"§15.18.2: {atlas.Key} is {atlas.Value.width}×{atlas.Value.height}, readable {atlas.Value.isReadable}; expected {w}×{h} readable");
                    continue;
                }
                var px = atlas.Value.GetPixels32();
                var mean = Mean(px, skin);
                double diff = 0;
                int outside = 0;
                for (int i = 0; i < px.Length; i++)
                {
                    if (skin[i])
                        continue;
                    diff += (Math.Abs(px[i].r - refPx[i].r) + Math.Abs(px[i].g - refPx[i].g) + Math.Abs(px[i].b - refPx[i].b)) / 3.0;
                    outside++;
                }
                diff = outside == 0 ? 0 : diff / outside;
                float dLuma = Luma(mean) - refLuma;
                bool isReference = atlas.Key == ReferenceAtlas;
                bool qualifies = isReference || (diff <= SameClothesTolerance && Math.Abs(dLuma) >= ClearlyDifferentLuma);
                string tone = ToneClass(mean);
                if (qualifies)
                {
                    if (!tones.TryGetValue(tone, out var list))
                        tones[tone] = list = new List<string>();
                    list.Add(shortName);
                }
                Log($"(skin) {shortName} | {Hex(mean)} | {Luma(mean):F1} ({dLuma:+0.0;-0.0;0.0}) | {diff:F2} | " +
                    $"{(isReference ? "reference" : qualifies ? "yes" : "no")} | {tone}");
                if (!qualifies && Math.Abs(dLuma) >= ClearlyDifferentLuma)
                    regionDiffs.Add($"(skin) {shortName} outside the skin, mean abs diff /255 by mask region: " + RegionDiffs(textures, px, refPx, skin, w, h));
            }
            foreach (string line in regionDiffs)
                Log(line);
            bool allThree = ReferenceTones.All(t => tones.ContainsKey(t.Tone));
            Log("(skin) qualifying tones: " + string.Join("; ", ReferenceTones.Select(t => $"{t.Tone} [{(tones.TryGetValue(t.Tone, out var l) ? string.Join(", ", l) : "")}]")) +
                $"; light, medium and dark all qualify: {(allThree ? "yes (§15.18.2 step 1)" : "no (§15.18.2 step 2)")}");
        }

        /// Where an atlas differs from 01_A outside the skin: each mask region over 1/255, then the texels in no region.
        static string RegionDiffs(Dictionary<string, Texture2D> textures, Color32[] px, Color32[] refPx, bool[] skin, int w, int h)
        {
            var claimed = new bool[px.Length];
            var parts = new List<string>();
            foreach (var group in MaskRegions.GroupBy(r => r.Mask))
            {
                if (!textures.TryGetValue(group.Key, out var mask))
                    continue;
                var m = Resample(mask, w, h);
                foreach (var (_, channel, region) in group)
                {
                    var inside = Inside(m, channel);
                    for (int i = 0; i < inside.Length; i++)
                    {
                        inside[i] &= !skin[i];
                        claimed[i] |= inside[i];
                    }
                    var (d, n) = MeanDiff(px, refPx, inside);
                    if (d > 1)
                        parts.Add($"{region} ({100f * n / px.Length:F1}% of texels) {d:F1}");
                }
            }
            var rest = new bool[px.Length];
            for (int i = 0; i < rest.Length; i++)
                rest[i] = !skin[i] && !claimed[i];
            var (rd, rn) = MeanDiff(px, refPx, rest);
            parts.Add($"no region ({100f * rn / px.Length:F1}%) {rd:F1}");
            return string.Join("; ", parts);
        }

        static (double Diff, int Count) MeanDiff(Color32[] px, Color32[] refPx, bool[] inside)
        {
            double sum = 0;
            int n = 0;
            for (int i = 0; i < px.Length; i++)
            {
                if (!inside[i])
                    continue;
                sum += (Math.Abs(px[i].r - refPx[i].r) + Math.Abs(px[i].g - refPx[i].g) + Math.Abs(px[i].b - refPx[i].b)) / 3.0;
                n++;
            }
            return (n == 0 ? 0 : sum / n, n);
        }

        static void CheckEditorOnlyRules(List<string> failures, string path, TextureImporter importer)
        {
            var defaults = importer.GetDefaultPlatformTextureSettings();
            importer.GetSourceTextureWidthAndHeight(out int sw, out int sh);
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            var wrong = new List<string>();
            if (!importer.isReadable) wrong.Add("not readable");
            if (importer.mipmapEnabled) wrong.Add("mipmaps");
            if (importer.textureCompression != TextureImporterCompression.Uncompressed || defaults.format != TextureImporterFormat.RGBA32)
                wrong.Add($"{importer.textureCompression} {defaults.format}");
            if (tex == null || tex.width != sw || tex.height != sh) wrong.Add($"imported {(tex == null ? "none" : tex.width + "×" + tex.height)}, source {sw}×{sh}");
            if (wrong.Count > 0)
                failures.Add($"{path}: {string.Join(", ", wrong)} (postprocessor did not run? try SQ_REIMPORT=1)");
        }

        /// EditorOnly/ must never reach a build, so no material may point into it (§15.18.2; the D8 audit repeats this).
        static void CheckNoEditorOnlyReferences(List<string> failures)
        {
            foreach (string path in AssetDatabase.FindAssets("t:Material", new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath).Distinct())
            {
                var hits = AssetDatabase.GetDependencies(path, false).Where(d => d.StartsWith(SyntyPostprocessor.EditorOnlyFolder, StringComparison.Ordinal)).ToList();
                if (hits.Count > 0)
                    failures.Add($"{path} references {string.Join(", ", hits)}; nothing may reference {SyntyPostprocessor.EditorOnlyFolder}");
            }
        }

        /// Nearest-texel resample, so a mask of another size lines up texel for texel with the atlas.
        static Color32[] Resample(Texture2D tex, int w, int h)
        {
            var src = tex.GetPixels32();
            if (tex.width == w && tex.height == h)
                return src;
            var dst = new Color32[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                dst[y * w + x] = src[(y * tex.height / h) * tex.width + x * tex.width / w];
            return dst;
        }

        /// The texels a black-on-white mask channel marks.
        static bool[] Inside(Color32[] mask, int channel)
        {
            var inside = new bool[mask.Length];
            for (int i = 0; i < mask.Length; i++)
                inside[i] = (channel == 0 ? mask[i].r : channel == 1 ? mask[i].g : mask[i].b) < 128;
            return inside;
        }

        static Color32 Mean(Color32[] px, bool[] inside)
        {
            long r = 0, g = 0, b = 0, n = 0;
            for (int i = 0; i < px.Length; i++)
            {
                if (!inside[i])
                    continue;
                r += px[i].r;
                g += px[i].g;
                b += px[i].b;
                n++;
            }
            return n == 0
                ? new Color32(0, 0, 0, 0)
                : new Color32((byte)Math.Round((double)r / n), (byte)Math.Round((double)g / n), (byte)Math.Round((double)b / n), 255);
        }

        /// Kovac, Peer and Solina's RGB skin rule (daylight), on sRGB bytes: enough to tell skin from cloth, hair or metal.
        static bool IsSkinColour(Color32 c) =>
            c.r > 95 && c.g > 40 && c.b > 20 && Math.Max(c.r, Math.Max(c.g, c.b)) - Math.Min(c.r, Math.Min(c.g, c.b)) > 15 &&
            Math.Abs(c.r - c.g) > 15 && c.r > c.g && c.r > c.b;

        static float Luma(Color32 c) => 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;

        static string ToneClass(Color32 c) => ReferenceTones.OrderBy(t => Math.Abs(Luma(t.Colour) - Luma(c))).First().Tone;

        static string Hex(Color32 c) => $"#{c.r:X2}{c.g:X2}{c.b:X2}";

        // ---- §15.18.8 stubble ------------------------------------------------------------------------------------

        const int StubbleChannel = 2; // Mask_03.b gates _Color_Stubble in Synty's shader graph (MaskRegions above)

        /// Mask_03.b's polarity: the share of texels above and below 0.5 and 01_A's mean colour in each, read from the
        /// source PNGs in Textures/ (the shipped imports are not readable, and the imageconversion module is out, §13.2).
        /// Then every 4-connected region of the small set, the stubble set, with the hero parts whose triangles sample it,
        /// so a region outside the male lower face shows up before the shader relies on the channel.
        static void ReportStubble(List<string> failures)
        {
            string atlasPath = SyntyPostprocessor.TexturesFolder + ReferenceAtlas + ".png";
            if (!TryReadPng(atlasPath, out var atlas, out int w, out int h, out string error) ||
                !TryReadPng(SyntyPostprocessor.SkinMask, out var mask, out int mw, out int mh, out error))
            {
                failures.Add("§15.18.8: " + error);
                return;
            }
            if (mw != w || mh != h)
            {
                failures.Add($"§15.18.8: {SyntyPostprocessor.SkinMask} is {mw}×{mh}, {atlasPath} {w}×{h}; the stubble report needs them texel for texel");
                return;
            }

            var below = Inside(mask, StubbleChannel);
            var above = below.Select(x => !x).ToArray();
            int nBelow = below.Count(x => x), nAbove = above.Length - nBelow;
            Log($"(stubble) {SkinMask}.B below 0.5: {100f * nBelow / below.Length:F2}% of texels, 01_A mean {Hex(Mean(atlas, below))}; " +
                $"above 0.5: {100f * nAbove / above.Length:F2}%, 01_A mean {Hex(Mean(atlas, above))}");
            bool blackOnWhite = nBelow <= nAbove;
            var stubble = blackOnWhite ? below : above;
            Log("(stubble) the stubble set is the small one, B " + (blackOnWhite ? "< 0.5: black on white, stubble = 1 − b" : ">= 0.5: white on black, stubble = b"));

            CheckImportedStubble(failures, below, w, h);

            var regions = Regions(stubble, w, h, out int[] label);
            var parts = PartsPerRegion(label, regions.Count, w, h);
            for (int i = 0; i < regions.Count; i++)
            {
                var (count, x0, y0, x1, y1, mean) = Region(regions[i], atlas, w);
                var users = parts[i].OrderBy(p => p.Key, StringComparer.Ordinal)
                    .Select(p => $"{p.Key} ({p.Value.Triangles} tri at {100f * p.Value.Height:F0}%)").ToList();
                Log($"(stubble) region {i + 1}: {count} texels, x {x0}..{x1} y {y0}..{y1} (uv {F((float)x0 / w)}..{F((x1 + 1f) / w)}, " +
                    $"{F((float)y0 / h)}..{F((y1 + 1f) / h)}), 01_A mean {Hex(mean)}, sampled by {users.Count} part(s), each with the mean " +
                    "height of those triangles in the part's bounds (0% bottom, 100% top)" +
                    (users.Count == 0 ? "" : ": " + string.Join(", ", users)));
            }
        }

        /// The shader reads stubble from the imported mask's green (§15.18.8), so the import must carry the source's blue
        /// there texel for texel: a blit of mip 0 at full size, its green below 0.5 compared with the source's blue below 0.5.
        static void CheckImportedStubble(List<string> failures, bool[] sourceBelow, int w, int h)
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(SyntyPostprocessor.SkinMask);
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null || tex == null)
            {
                Log("(stubble) imported mask not compared: " + (tex == null ? "not imported" : "no graphics device (run without -nographics)"));
                return;
            }
            if (tex.width != w || tex.height != h)
            {
                failures.Add($"§15.18.8: {SyntyPostprocessor.SkinMask} imported {tex.width}×{tex.height}, source {w}×{h}");
                return;
            }
            var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var previous = RenderTexture.active;
            Graphics.Blit(tex, rt);
            RenderTexture.active = rt;
            var copy = new Texture2D(w, h, TextureFormat.RGBA32, false, true);
            copy.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            copy.Apply(false);
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
            var px = copy.GetPixels32();
            Object.DestroyImmediate(copy);

            var imported = Inside(px, 1);
            int mismatches = 0;
            for (int i = 0; i < imported.Length; i++)
                if (imported[i] != sourceBelow[i])
                    mismatches++;
            Log($"(stubble) imported {tex.format}: green < 0.5 on {100f * imported.Count(x => x) / imported.Length:F2}% of texels, " +
                $"{mismatches} texel(s) differ from the source's blue below 0.5");
            if (mismatches > 0)
                failures.Add($"§15.18.8: {SyntyPostprocessor.SkinMask} imported green differs from the source's blue on {mismatches} texel(s)");
        }

        /// 4-connected regions of a set, each as its texel indices; label holds region index + 1 per texel, 0 outside.
        static List<List<int>> Regions(bool[] set, int w, int h, out int[] label)
        {
            label = new int[set.Length];
            var regions = new List<List<int>>();
            var stack = new Stack<int>();
            for (int start = 0; start < set.Length; start++)
            {
                if (!set[start] || label[start] != 0)
                    continue;
                var region = new List<int>();
                regions.Add(region);
                label[start] = regions.Count;
                stack.Push(start);
                while (stack.Count > 0)
                {
                    int i = stack.Pop();
                    region.Add(i);
                    int x = i % w, y = i / w;
                    foreach (int n in new[] { x > 0 ? i - 1 : -1, x < w - 1 ? i + 1 : -1, y > 0 ? i - w : -1, y < h - 1 ? i + w : -1 })
                    {
                        if (n < 0 || !set[n] || label[n] != 0)
                            continue;
                        label[n] = regions.Count;
                        stack.Push(n);
                    }
                }
            }
            return regions;
        }

        static (int Count, int X0, int Y0, int X1, int Y1, Color32 Mean) Region(List<int> texels, Color32[] atlas, int w)
        {
            int x0 = int.MaxValue, y0 = int.MaxValue, x1 = -1, y1 = -1;
            var inside = new bool[atlas.Length];
            foreach (int i in texels)
            {
                inside[i] = true;
                x0 = Math.Min(x0, i % w);
                x1 = Math.Max(x1, i % w);
                y0 = Math.Min(y0, i / w);
                y1 = Math.Max(y1, i / w);
            }
            return (texels.Count, x0, y0, x1, y1, Mean(atlas, inside));
        }

        /// Per region, every hero part with the number of its triangles that sample it and where they sit in the part's
        /// height, so a head's lower face reads as a low share. Each triangle is sampled at its centroid and at its corners
        /// pulled a tenth of the way in, so a corner on a swatch edge does not count the neighbour.
        static List<Dictionary<string, (int Triangles, float Height)>> PartsPerRegion(int[] label, int regionCount, int w, int h)
        {
            var result = Enumerable.Range(0, regionCount).Select(_ => new Dictionary<string, (int Triangles, float Height)>()).ToList();
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(SyntyPostprocessor.HeroModel);
            if (model == null)
                return result;
            foreach (var renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mesh = renderer.sharedMesh;
                if (mesh == null)
                    continue;
                var uv = mesh.uv;
                var vertices = mesh.vertices;
                var triangles = mesh.triangles;
                var bounds = mesh.bounds;
                var hit = new HashSet<int>();
                for (int t = 0; t + 2 < triangles.Length; t += 3)
                {
                    var a = uv[triangles[t]];
                    var b = uv[triangles[t + 1]];
                    var c = uv[triangles[t + 2]];
                    var centre = (a + b + c) / 3f;
                    hit.Clear();
                    foreach (var p in new[] { centre, Vector2.Lerp(a, centre, 0.1f), Vector2.Lerp(b, centre, 0.1f), Vector2.Lerp(c, centre, 0.1f) })
                    {
                        int x = Mathf.Clamp((int)(Mathf.Clamp01(p.x) * w), 0, w - 1);
                        int y = Mathf.Clamp((int)(Mathf.Clamp01(p.y) * h), 0, h - 1);
                        int region = label[y * w + x];
                        if (region > 0)
                            hit.Add(region - 1);
                    }
                    float centreY = (vertices[triangles[t]].y + vertices[triangles[t + 1]].y + vertices[triangles[t + 2]].y) / 3f;
                    float height = bounds.size.y > 0f ? (centreY - bounds.min.y) / bounds.size.y : 0f;
                    foreach (int region in hit)
                    {
                        result[region].TryGetValue(renderer.name, out var sum);
                        result[region][renderer.name] = (sum.Triangles + 1, sum.Height + height);
                    }
                }
            }
            foreach (var parts in result)
                foreach (var name in parts.Keys.ToList())
                    parts[name] = (parts[name].Triangles, parts[name].Height / parts[name].Triangles);
            return result;
        }

        /// An 8-bit, non-interlaced RGB or RGBA PNG as Color32, bottom row first like Texture2D.GetPixels32. Enough for
        /// Synty's atlases and masks; anything else is reported, not guessed.
        static bool TryReadPng(string path, out Color32[] pixels, out int width, out int height, out string error)
        {
            pixels = Array.Empty<Color32>();
            width = height = 0;
            error = "";
            if (!File.Exists(path))
            {
                error = path + " not found (is it on Tools/synty/keep.txt?)";
                return false;
            }
            var bytes = File.ReadAllBytes(path);
            var signature = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 };
            if (bytes.Length < 8 || !bytes.Take(8).SequenceEqual(signature))
            {
                error = path + " is not a PNG";
                return false;
            }
            int channels = 0;
            var idat = new MemoryStream();
            for (int pos = 8; pos + 8 <= bytes.Length;)
            {
                int length = bytes[pos] << 24 | bytes[pos + 1] << 16 | bytes[pos + 2] << 8 | bytes[pos + 3];
                string type = System.Text.Encoding.ASCII.GetString(bytes, pos + 4, 4);
                int data = pos + 8;
                if (type == "IHDR")
                {
                    width = bytes[data] << 24 | bytes[data + 1] << 16 | bytes[data + 2] << 8 | bytes[data + 3];
                    height = bytes[data + 4] << 24 | bytes[data + 5] << 16 | bytes[data + 6] << 8 | bytes[data + 7];
                    int depth = bytes[data + 8], colourType = bytes[data + 9], interlace = bytes[data + 12];
                    channels = colourType == 2 ? 3 : colourType == 6 ? 4 : 0;
                    if (depth != 8 || channels == 0 || interlace != 0)
                    {
                        error = $"{path}: bit depth {depth}, colour type {colourType}, interlace {interlace}; only 8-bit non-interlaced RGB or RGBA is read";
                        return false;
                    }
                }
                else if (type == "IDAT")
                    idat.Write(bytes, data, length);
                else if (type == "IEND")
                    break;
                pos = data + length + 4;
            }
            if (channels == 0 || idat.Length < 2)
            {
                error = path + ": no IHDR or IDAT";
                return false;
            }

            int stride = width * channels;
            var raw = new byte[(stride + 1) * height];
            idat.Position = 2; // the zlib header; DeflateStream reads the raw deflate data after it
            using (var inflate = new DeflateStream(idat, CompressionMode.Decompress))
            {
                int read = 0;
                while (read < raw.Length)
                {
                    int n = inflate.Read(raw, read, raw.Length - read);
                    if (n == 0)
                        break;
                    read += n;
                }
                if (read != raw.Length)
                {
                    error = $"{path}: {read} bytes inflated, expected {raw.Length}";
                    return false;
                }
            }

            var previous = new byte[stride];
            var row = new byte[stride];
            pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                int filter = raw[y * (stride + 1)];
                Array.Copy(raw, y * (stride + 1) + 1, row, 0, stride);
                for (int i = 0; i < stride; i++)
                {
                    int left = i >= channels ? row[i - channels] : 0;
                    int up = previous[i];
                    int upLeft = i >= channels ? previous[i - channels] : 0;
                    int predictor = filter switch
                    {
                        0 => 0,
                        1 => left,
                        2 => up,
                        3 => (left + up) / 2,
                        4 => Paeth(left, up, upLeft),
                        _ => -1,
                    };
                    if (predictor < 0)
                    {
                        error = $"{path}: row {y} has filter type {filter}";
                        return false;
                    }
                    row[i] = (byte)(row[i] + predictor);
                }
                int dst = (height - 1 - y) * width;
                for (int x = 0; x < width; x++)
                {
                    int s = x * channels;
                    pixels[dst + x] = new Color32(row[s], row[s + 1], row[s + 2], channels == 4 ? row[s + 3] : (byte)255);
                }
                (previous, row) = (row, previous);
            }
            return true;
        }

        static int Paeth(int a, int b, int c)
        {
            int p = a + b - c;
            int pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
            return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
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
                CheckBoneMap(failures, path, avatar);

                ReportRig(path, avatar);

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
                    Log($"(g) {Path.GetFileName(path)}: avatar isHuman {avatar != null && avatar.isHuman} isValid {avatar != null && avatar.isValid}, {MappedBones(avatar)}; " +
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

        /// The finger bones an animation file's avatar maps, so the log shows the renamed L_/R_ transforms are reached.
        static void ReportRig(string path, Avatar? avatar)
        {
            if (avatar == null)
                return;
            var fingers = avatar.humanDescription.human.Where(h => IsFinger(h.humanName)).OrderBy(h => h.humanName, StringComparer.Ordinal).ToList();
            Log($"(g) {Path.GetFileName(path)}: {fingers.Count} finger bones" +
                (fingers.Count == 0 ? "" : $" (e.g. {fingers[0].humanName} -> {fingers[0].boneName}, {fingers[fingers.Count - 1].humanName} -> {fingers[fingers.Count - 1].boneName})"));
        }

        /// The built avatar must carry exactly SyntyPostprocessor's shared map: same human bones on the same transforms.
        static void CheckBoneMap(List<string> failures, string path, Avatar? avatar)
        {
            if (avatar == null)
                return;
            var human = avatar.humanDescription.human.ToDictionary(h => h.humanName, h => h.boneName);
            var expected = SyntyPostprocessor.CharacterBoneMap.ToDictionary(m => HumanTrait.BoneName[(int)m.Bone], m => m.Transform);
            var differences = expected.Where(e => !human.TryGetValue(e.Key, out var b) || b != e.Value)
                .Select(e => $"{e.Key}: {(human.TryGetValue(e.Key, out var b) ? b : "unmapped")} vs {e.Value}")
                .Concat(human.Keys.Where(k => !expected.ContainsKey(k)).Select(k => $"{k}: {human[k]} not in the shared map"))
                .ToList();
            if (differences.Count > 0)
                failures.Add($"{path}: avatar map differs from SyntyPostprocessor.CharacterBoneMap: {string.Join("; ", differences)}");
        }

        static string MappedBones(Avatar? avatar)
        {
            if (avatar == null)
                return "no avatar";
            var human = avatar.humanDescription.human;
            return $"{human.Length} bones mapped ({human.Count(h => IsFinger(h.humanName))} finger)";
        }

        static readonly string[] FingerWords = { "Thumb", "Index", "Middle", "Ring", "Little" };

        static bool IsFinger(string humanName) => FingerWords.Any(humanName.Contains);

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
            if (path == SyntyPostprocessor.SkinMask)
            {
                // §15.18.8: linear data, RG16 uncompressed on iOS with the source's blue in green, same size, mips and
                // filtering as 01_A.
                if (importer.sRGBTexture) wrong.Add("sRGB");
                if (importer.swizzleR != TextureImporterSwizzle.R || importer.swizzleG != SyntyPostprocessor.StubbleSourceChannel)
                    wrong.Add($"swizzle R={importer.swizzleR} G={importer.swizzleG}");
                if (importer.maxTextureSize != SyntyPostprocessor.MaxTextureSize) wrong.Add("maxTextureSize " + importer.maxTextureSize);
                if (!importer.mipmapEnabled) wrong.Add("no mipmaps");
                if (importer.isReadable) wrong.Add("isReadable");
                if (!ios.overridden || ios.format != SyntyPostprocessor.SkinMaskFormat || ios.maxTextureSize != SyntyPostprocessor.MaxTextureSize)
                    wrong.Add($"iOS {(ios.overridden ? "" : "not overridden ")}{ios.format} {ios.maxTextureSize}");
                if (wrong.Count > 0)
                    failures.Add($"{path}: {string.Join(", ", wrong)} (postprocessor did not run? try SQ_REIMPORT=1)");
                return;
            }
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
