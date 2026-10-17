using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Game.Editor.Import
{
    /// Import rules for the Synty files that Tools/synty/extract.sh copies (ARCHITECTURE §15.7, §15.15.5), keyed on the
    /// folder so no setting lives in a click. Synty's own .meta files are never copied, so every setting comes from here.
    /// Loop flags, OnImpact events and trims come from Resources/Data/clips.json through ClipTable.
    public sealed class SyntyPostprocessor : AssetPostprocessor
    {
        public const string Root = "Assets/ThirdParty/Synty/";
        public const string CharactersFolder = Root + "Characters/";
        public const string WeaponsFolder = Root + "Weapons/";
        public const string AnimationsFolder = Root + "Animations/";
        public const string TexturesFolder = Root + "Textures/";
        public const string EditorOnlyFolder = Root + "EditorOnly/";
        public const string HeroModel = CharactersFolder + "ModularCharacters.fbx";
        // §15.16.2: the troll's file. Synty's Big Rig skeleton uses the hero rig's transform names, finger chains included,
        // and Unity's automatic map fails on it (Hips on Root, duplicate Thumb_01; first import 2026-10-17), so it takes the
        // same explicit map and finger rename as the hero.
        public const string BigRigModel = CharactersFolder + "Characters_BR.fbx";
        // §15.18.2, §15.18.8: the skin and stubble mask the hero materials sample. Linear data, so it gets its own rule below.
        public const string SkinMask = TexturesFolder + "PolygonFantasyHero_Texture_Mask_03.png";
        public const TextureImporterFormat SkinMaskFormat = TextureImporterFormat.RG16;
        public const TextureImporterSwizzle StubbleSourceChannel = TextureImporterSwizzle.B;
        public const int MaxTextureSize = 1024;
        // EditorOnly/ holds measuring and baking inputs (§15.18.2); none may be downscaled.
        public const int EditorOnlyMaxTextureSize = 8192;

        // Measured 2026-10-17 by SyntyImportCheck: FixedScale/ModularCharacters.fbx is authored in centimetres (file scale
        // 0.01, applied by useFileScale) with unit bone scales, and its base bodies span 1.93 m from feet to head top, inside
        // §15.3's 1.2–2.4 m band, so no correction is needed. Applied to characters, weapons and animation FBX alike so hip
        // heights, sockets and weapons agree (the §14.1 rule).
        public const float GlobalScale = 1f;

        public const string LeftHandBone = "Hand_L";
        public const string RightHandBone = "Hand_R";
        public const string LeftPrefix = "L_";
        public const string RightPrefix = "R_";

        // One explicit Humanoid map for the hero FBX, the Big Rig FBX and every animation FBX (§15.7, §15.18.1), so source and target map
        // the same transforms. Synty's modular rig repeats the finger names under both hands (Thumb_01 under Hand_L and
        // Hand_R), which a map that addresses bones by name cannot reach, so OnPostprocessMeshHierarchy renames them L_/R_
        // first. The animation files cannot keep Unity's automatic map: it maps fingers inconsistently from file to file (the
        // Finger_ chain as Ring, Middle or Little; in one file the thumb as Little and the prop bone as a finger).
        // Transform names as SyntyImportCheck printed them on 2026-10-17. The toes go on Toes_L/R, not Ball_L/R, because
        // that is where Unity's automatic map put them in every Synty animation file.
        public static readonly (HumanBodyBones Bone, string Transform)[] CharacterBoneMap =
        {
            (HumanBodyBones.Hips, "Hips"),
            (HumanBodyBones.Spine, "Spine_01"),
            (HumanBodyBones.Chest, "Spine_02"),
            (HumanBodyBones.UpperChest, "Spine_03"),
            (HumanBodyBones.Neck, "Neck"),
            (HumanBodyBones.Head, "Head"),
            (HumanBodyBones.LeftShoulder, "Clavicle_L"),
            (HumanBodyBones.LeftUpperArm, "Shoulder_L"),
            (HumanBodyBones.LeftLowerArm, "Elbow_L"),
            (HumanBodyBones.LeftHand, "Hand_L"),
            (HumanBodyBones.RightShoulder, "Clavicle_R"),
            (HumanBodyBones.RightUpperArm, "Shoulder_R"),
            (HumanBodyBones.RightLowerArm, "Elbow_R"),
            (HumanBodyBones.RightHand, "Hand_R"),
            (HumanBodyBones.LeftUpperLeg, "UpperLeg_L"),
            (HumanBodyBones.LeftLowerLeg, "LowerLeg_L"),
            (HumanBodyBones.LeftFoot, "Ankle_L"),
            (HumanBodyBones.LeftToes, "Toes_L"),
            (HumanBodyBones.RightUpperLeg, "UpperLeg_R"),
            (HumanBodyBones.RightLowerLeg, "LowerLeg_R"),
            (HumanBodyBones.RightFoot, "Ankle_R"),
            (HumanBodyBones.RightToes, "Toes_R"),

            // Each POLYGON hand has three chains, not five: Thumb_01..03, IndexFinger_01..04 and Finger_01..04 (the other
            // three fingers as one), where _04 is a tip end. So 9 bones per hand map, not Humanoid's 15. Finger_ goes on
            // Middle, as in Synty's own .meta for the hero and every clip pack (read from the scratch extract, 2026-10-17).
            (HumanBodyBones.LeftThumbProximal, LeftPrefix + "Thumb_01"),
            (HumanBodyBones.LeftThumbIntermediate, LeftPrefix + "Thumb_02"),
            (HumanBodyBones.LeftThumbDistal, LeftPrefix + "Thumb_03"),
            (HumanBodyBones.LeftIndexProximal, LeftPrefix + "IndexFinger_01"),
            (HumanBodyBones.LeftIndexIntermediate, LeftPrefix + "IndexFinger_02"),
            (HumanBodyBones.LeftIndexDistal, LeftPrefix + "IndexFinger_03"),
            (HumanBodyBones.LeftMiddleProximal, LeftPrefix + "Finger_01"),
            (HumanBodyBones.LeftMiddleIntermediate, LeftPrefix + "Finger_02"),
            (HumanBodyBones.LeftMiddleDistal, LeftPrefix + "Finger_03"),
            (HumanBodyBones.RightThumbProximal, RightPrefix + "Thumb_01"),
            (HumanBodyBones.RightThumbIntermediate, RightPrefix + "Thumb_02"),
            (HumanBodyBones.RightThumbDistal, RightPrefix + "Thumb_03"),
            (HumanBodyBones.RightIndexProximal, RightPrefix + "IndexFinger_01"),
            (HumanBodyBones.RightIndexIntermediate, RightPrefix + "IndexFinger_02"),
            (HumanBodyBones.RightIndexDistal, RightPrefix + "IndexFinger_03"),
            (HumanBodyBones.RightMiddleProximal, RightPrefix + "Finger_01"),
            (HumanBodyBones.RightMiddleIntermediate, RightPrefix + "Finger_02"),
            (HumanBodyBones.RightMiddleDistal, RightPrefix + "Finger_03"),
        };

        // The animation files were exported with unique names, so their right hand reads Thumb_01_1; stripping that
        // suffix gives every file the same names. Matches only a two-digit bone index followed by a copy number.
        static readonly Regex CopySuffix = new Regex(@"^(.+_\d{2})_\d+$");

        // Part of the import hash: bump it when a rule below changes so already-imported files pick the change up.
        public override uint GetVersion() => 4;

        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(Root))
                return;

            var importer = (ModelImporter)assetImporter;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.isReadable = false;
            importer.importBlendShapes = false;
            importer.importCameras = false;
            importer.importLights = false;
            // The generator parents sockets to bones, and the presenter reads chest and head transforms.
            importer.optimizeGameObjects = false;
            importer.useFileScale = true;
            importer.globalScale = GlobalScale;
            importer.meshCompression = ModelImporterMeshCompression.Off;

            if (assetPath.StartsWith(CharactersFolder))
            {
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.importAnimation = false;
                if (UsesSharedMap(assetPath))
                    ApplyBoneMap(importer);
            }
            else if (assetPath.StartsWith(WeaponsFolder))
            {
                importer.animationType = ModelImporterAnimationType.None;
            }
            else if (assetPath.StartsWith(AnimationsFolder))
            {
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.importAnimation = true;
                importer.animationCompression = ModelImporterAnimationCompression.Optimal;
                ApplyBoneMap(importer);
            }
        }

        /// The character files mapped by CharacterBoneMap; every animation file is too.
        public static bool UsesSharedMap(string path) => path == HeroModel || path == BigRigModel;

        static bool HasRenamedHands(string path) => UsesSharedMap(path) || path.StartsWith(AnimationsFolder);

        /// Runs before the avatar and clips are built from the hierarchy, so both see the renamed finger bones. Skinning
        /// binds to transforms, not names, so the rename changes nothing else.
        void OnPostprocessMeshHierarchy(GameObject root)
        {
            if (!HasRenamedHands(assetPath))
                return;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == LeftHandBone)
                    PrefixDescendants(t, LeftPrefix);
                else if (t.name == RightHandBone)
                    PrefixDescendants(t, RightPrefix);
            }
        }

        static void PrefixDescendants(Transform hand, string prefix)
        {
            foreach (var t in hand.GetComponentsInChildren<Transform>(true))
            {
                if (t != hand)
                    t.name = HandBoneName(t.name, prefix);
            }
        }

        /// Idempotent, because the stored skeleton below may already carry the prefix from an earlier import.
        static string HandBoneName(string name, string prefix)
        {
            if (name.StartsWith(prefix))
                return name;
            var copy = CopySuffix.Match(name);
            return prefix + (copy.Success ? copy.Groups[1].Value : name);
        }

        static void ApplyBoneMap(ModelImporter importer)
        {
            importer.humanDescription = SyntyHumanDescription(importer.humanDescription);
            RenameStoredSkeleton(importer);
        }

        /// Unity builds the avatar from the explicit map only together with the stored skeleton, its record of the
        /// hierarchy's rest pose written at the first import: with the skeleton emptied it auto-maps instead, or with
        /// autoGenerateAvatarMappingIfUnspecified off builds no Humanoid at all (both tried 2026-10-17). So the stored
        /// entries are renamed exactly as the hierarchy is. The skeleton is depth-first, so an entry's parent is the latest
        /// earlier entry of that name, which tells the left Thumb_01 from the right. Its parent names have no script API,
        /// hence SerializedObject. A file with no stored skeleton yet (a new .meta) gets Unity's automatic map on its first
        /// import; SyntyImportCheck then fails, and a second import (SQ_REIMPORT=1) applies this map.
        static void RenameStoredSkeleton(ModelImporter importer)
        {
            var serialized = new SerializedObject(importer);
            var skeleton = serialized.FindProperty("m_HumanDescription.m_Skeleton");
            if (skeleton == null || !skeleton.isArray)
            {
                Debug.LogError("[SyntyPostprocessor] " + importer.assetPath + ": m_HumanDescription.m_Skeleton not found; the finger map cannot apply");
                return;
            }
            int n = skeleton.arraySize;
            var names = new string[n];
            var parents = new string[n];
            for (int i = 0; i < n; i++)
            {
                var bone = skeleton.GetArrayElementAtIndex(i);
                names[i] = bone.FindPropertyRelative("m_Name").stringValue;
                parents[i] = bone.FindPropertyRelative("m_ParentName").stringValue;
            }

            var parentIndex = new int[n];
            var prefix = new string?[n];
            var renamed = (string[])names.Clone();
            for (int i = 0; i < n; i++)
            {
                parentIndex[i] = Array.LastIndexOf(names, parents[i], Math.Max(i - 1, 0));
                if (parentIndex[i] >= i)
                    parentIndex[i] = -1;
                int p = parentIndex[i];
                if (p >= 0)
                    prefix[i] = names[p] == LeftHandBone ? LeftPrefix : names[p] == RightHandBone ? RightPrefix : prefix[p];
                if (prefix[i] != null)
                    renamed[i] = HandBoneName(names[i], prefix[i]!);
            }
            for (int i = 0; i < n; i++)
            {
                var bone = skeleton.GetArrayElementAtIndex(i);
                bone.FindPropertyRelative("m_Name").stringValue = renamed[i];
                if (parentIndex[i] >= 0)
                    bone.FindPropertyRelative("m_ParentName").stringValue = renamed[parentIndex[i]];
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static HumanDescription SyntyHumanDescription(HumanDescription description)
        {
            description.human = CharacterBoneMap
                .Select(m => new HumanBone
                {
                    humanName = HumanTrait.BoneName[(int)m.Bone],
                    boneName = m.Transform,
                    limit = new HumanLimit { useDefaultValues = true },
                })
                .ToArray();
            // Unity's defaults, written out so the result does not depend on what an earlier import left in the .meta.
            description.upperArmTwist = 0.5f;
            description.lowerArmTwist = 0.5f;
            description.upperLegTwist = 0.5f;
            description.lowerLegTwist = 0.5f;
            description.armStretch = 0.05f;
            description.legStretch = 0.05f;
            description.feetSpacing = 0f;
            description.hasTranslationDoF = false;
            return description;
        }

        void OnPreprocessAnimation()
        {
            if (!assetPath.StartsWith(AnimationsFolder))
                return;

            // Registering the table as a dependency reimports the clips when a number in it changes.
            context?.DependsOnSourceAsset(ClipTable.FilePath);
            var table = ClipTable.Load();

            var importer = (ModelImporter)assetImporter;
            var clips = importer.clipAnimations;
            if (clips.Length == 0)
                clips = importer.defaultClipAnimations;

            // One take per Synty file: naming the clip after the file makes names unique and lets data name the file.
            string fileName = Path.GetFileNameWithoutExtension(assetPath);
            if (clips.Length > 1)
                Debug.LogWarning("[SyntyPostprocessor] " + assetPath + " has " + clips.Length + " takes; only the first is named " + fileName);

            var takes = importer.importedTakeInfos;
            var defaults = importer.defaultClipAnimations;
            for (int i = 0; i < clips.Length; i++)
            {
                var clip = clips[i];
                clip.name = i == 0 ? fileName : fileName + "_" + clip.takeName;
                // The whole take unless clips.json trims it, so removing a "start" or "end" restores the clip.
                if (i < defaults.Length)
                {
                    clip.firstFrame = defaults[i].firstFrame;
                    clip.lastFrame = defaults[i].lastFrame;
                }

                // Bake root rotation, height and XZ into the pose, based on Original: the clip plays as authored and the
                // root never moves, which applyRootMotion = false and the root knockback assume (§15.2).
                clip.lockRootRotation = true;
                clip.lockRootHeightY = true;
                clip.lockRootPositionXZ = true;
                clip.keepOriginalOrientation = true;
                clip.keepOriginalPositionY = true;
                clip.keepOriginalPositionXZ = true;

                if (table == null || !table.TryGetValue(clip.name, out var entry))
                    continue;
                // §15.18.3, §15.18.8: "start" and "end" are seconds from the take's start; the importer counts frames.
                // Impacts stay normalized, so they are read on the trimmed clip.
                float rate = i < takes.Length && takes[i].sampleRate > 0f ? takes[i].sampleRate : 30f;
                float takeStart = clip.firstFrame;
                if (entry.End.HasValue)
                    clip.lastFrame = Mathf.Min(takeStart + Mathf.Round(entry.End.Value * rate), clip.lastFrame);
                if (entry.Start.HasValue)
                    clip.firstFrame = Mathf.Min(takeStart + Mathf.Round(entry.Start.Value * rate), clip.lastFrame);
                clip.loopTime = entry.Loop;
                clip.events = entry.Impact.HasValue
                    ? new[] { new AnimationEvent { functionName = ClipTable.ImpactFunction, time = entry.Impact.Value } }
                    : new AnimationEvent[0];
            }
            importer.clipAnimations = clips;
        }

        void OnPreprocessTexture()
        {
            if (assetPath.StartsWith(EditorOnlyFolder))
            {
                PreprocessEditorOnlyTexture();
                return;
            }
            if (!assetPath.StartsWith(TexturesFolder))
                return;
            if (assetPath == SkinMask)
            {
                PreprocessSkinMask();
                return;
            }

            // 1024, not 512: Synty atlases are colour-swatch grids, and at 512 the first mip bleeds swatches together.
            var importer = (TextureImporter)assetImporter;
            importer.sRGBTexture = true;
            importer.maxTextureSize = MaxTextureSize;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.mipmapEnabled = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.isReadable = false;

            var ios = importer.GetPlatformTextureSettings("iPhone");
            ios.overridden = true;
            ios.maxTextureSize = MaxTextureSize;
            ios.format = TextureImporterFormat.ASTC_6x6;
            ios.textureCompression = TextureImporterCompression.Compressed;
            importer.SetPlatformTextureSettings(ios);
        }

        /// §15.18.8: linear, RG16 uncompressed (ASTC would soften swatch edges into their neighbours), and the same size,
        /// mips and filtering as 01_A so mask and albedo minify alike. Red is skin; the swizzle puts the source's blue,
        /// the stubble, into green, which RG16 keeps.
        void PreprocessSkinMask()
        {
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = false;
            importer.swizzleR = TextureImporterSwizzle.R;
            importer.swizzleG = StubbleSourceChannel;
            importer.swizzleB = TextureImporterSwizzle.B;
            importer.swizzleA = TextureImporterSwizzle.A;
            importer.maxTextureSize = MaxTextureSize;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.isReadable = false;

            var ios = importer.GetPlatformTextureSettings("iPhone");
            ios.overridden = true;
            ios.maxTextureSize = MaxTextureSize;
            ios.format = SkinMaskFormat;
            ios.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SetPlatformTextureSettings(ios);
        }

        /// SyntyImportCheck reads these pixels exactly as Synty authored them (§15.18.2). No material may reference this
        /// folder, so nothing in it reaches a build; the shipped copies in Textures/ stay compressed and non-readable.
        void PreprocessEditorOnlyTexture()
        {
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = !Path.GetFileName(assetPath).Contains("_Mask_");
            importer.isReadable = true;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = EditorOnlyMaxTextureSize;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;

            var defaults = importer.GetDefaultPlatformTextureSettings();
            defaults.maxTextureSize = EditorOnlyMaxTextureSize;
            defaults.format = TextureImporterFormat.RGBA32;
            defaults.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SetPlatformTextureSettings(defaults);

            var ios = importer.GetPlatformTextureSettings("iPhone");
            ios.overridden = false;
            importer.SetPlatformTextureSettings(ios);
        }
    }
}
