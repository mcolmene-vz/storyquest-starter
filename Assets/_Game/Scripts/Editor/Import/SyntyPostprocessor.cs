using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Game.Editor.Import
{
    /// Import rules for the Synty files that Tools/synty/extract.sh copies (ARCHITECTURE §15.7, §15.15.5), keyed on the
    /// folder so no setting lives in a click. Synty's own .meta files are never copied, so every setting comes from here.
    /// Loop flags and OnImpact events come from Resources/Data/clips.json, read exactly as KayKitPostprocessor reads it.
    public sealed class SyntyPostprocessor : AssetPostprocessor
    {
        public const string Root = "Assets/ThirdParty/Synty/";
        public const string CharactersFolder = Root + "Characters/";
        public const string WeaponsFolder = Root + "Weapons/";
        public const string AnimationsFolder = Root + "Animations/";
        public const string TexturesFolder = Root + "Textures/";
        public const string HeroModel = CharactersFolder + "ModularCharacters.fbx";
        public const int MaxTextureSize = 1024;

        // Measured 2026-10-17 by SyntyImportCheck: FixedScale/ModularCharacters.fbx is authored in centimetres (file scale
        // 0.01, applied by useFileScale) with unit bone scales, and its base bodies span 1.93 m from feet to head top, inside
        // §15.3's 1.2–2.4 m band, so no correction is needed. Applied to characters, weapons and animation FBX alike so hip
        // heights, sockets and weapons agree (the §14.1 rule).
        public const float GlobalScale = 1f;

        // Synty's modular rig gives the finger bones the same names under both hands (Thumb_01 under Hand_L and Hand_R),
        // so Unity's automatic mapping fails, and a Humanoid map, which addresses bones by name, cannot reach them. The
        // body is mapped explicitly (§15.7) and the fingers stay unmapped: they hold the bind pose in every clip. Only the
        // hero FBX gets this map; another character file keeps Unity's automatic mapping until its own rig needs one.
        // Transform names as SyntyImportCheck printed them on 2026-10-17. The toes go on Toes_L/R, not Ball_L/R, because
        // that is where Unity's automatic map puts them in every Synty animation file, and retargeting is most faithful
        // when source and target map the same transforms.
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
        };

        // Part of the import hash: bump it when a rule below changes so already-imported files pick the change up.
        public override uint GetVersion() => 1;

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
                if (assetPath == HeroModel)
                    importer.humanDescription = CharacterHumanDescription(importer.humanDescription);
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
            }
        }

        static HumanDescription CharacterHumanDescription(HumanDescription description)
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
            context?.DependsOnSourceAsset(KayKitPostprocessor.ClipTablePath);
            var table = KayKitPostprocessor.LoadClipTable();

            var importer = (ModelImporter)assetImporter;
            var clips = importer.clipAnimations;
            if (clips.Length == 0)
                clips = importer.defaultClipAnimations;

            // One take per Synty file: naming the clip after the file makes names unique and lets data name the file.
            string fileName = Path.GetFileNameWithoutExtension(assetPath);
            if (clips.Length > 1)
                Debug.LogWarning("[SyntyPostprocessor] " + assetPath + " has " + clips.Length + " takes; only the first is named " + fileName);

            for (int i = 0; i < clips.Length; i++)
            {
                var clip = clips[i];
                clip.name = i == 0 ? fileName : fileName + "_" + clip.takeName;

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
                clip.loopTime = entry.Loop;
                clip.events = entry.Impact.HasValue
                    ? new[] { new AnimationEvent { functionName = KayKitPostprocessor.ImpactFunction, time = entry.Impact.Value } }
                    : new AnimationEvent[0];
            }
            importer.clipAnimations = clips;
        }

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(TexturesFolder))
                return;

            // 1024, not KayKit's 512: Synty atlases are colour-swatch grids, and at 512 the first mip bleeds swatches together.
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
    }
}
