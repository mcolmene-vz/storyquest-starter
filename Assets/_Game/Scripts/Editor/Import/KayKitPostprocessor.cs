using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace Game.Editor.Import
{
    /// Import rules for the KayKit placeholders (ARCHITECTURE §14.1), keyed on the folder so no setting lives in a click.
    /// Loop flags and OnImpact events on the rig clips come from Resources/Data/clips.json (§14.2).
    public sealed class KayKitPostprocessor : AssetPostprocessor
    {
        public const string Root = "Assets/ThirdParty/KayKit/";
        public const string CharactersFolder = Root + "Characters/";
        public const string WeaponsFolder = Root + "Weapons/";
        public const string AnimationsFolder = Root + "Animations/";
        public const string TexturesFolder = Root + "Textures/";
        public const string ClipTablePath = "Assets/_Game/Resources/Data/clips.json";
        public const string ImpactFunction = "OnImpact";

        // KayKit bodies are ~2.2 m with headgear to 2.8 m at file scale. 0.70 puts the heroes at 1.72–1.98 m and the
        // ×1.35 troll at 2.20 m, inside §14.1's 1.2–2.4 m band that §14.4's camera, knockback and number heights assume.
        // Applied to every model here, rig files included: the clips carry hip positions in the rig file's units, so
        // the rig files must scale with the characters or the hips animate at the wrong height (lead ruling 2026-10-16).
        const float GlobalScale = 0.70f;

        // Part of the import hash: bump it when a rule below changes so already-imported files pick the change up.
        public override uint GetVersion() => 2;

        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(Root))
                return;

            var importer = (ModelImporter)assetImporter;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.isReadable = false;
            importer.importBlendShapes = false;
            importer.importCameras = false;
            importer.importLights = false;
            // The presenter addresses handslot.r, chest and head as transforms.
            importer.optimizeGameObjects = false;
            importer.useFileScale = true;
            importer.globalScale = GlobalScale;
            importer.meshCompression = ModelImporterMeshCompression.Off;

            if (assetPath.StartsWith(CharactersFolder))
                importer.importAnimation = false;
            else if (assetPath.StartsWith(WeaponsFolder))
                importer.animationType = ModelImporterAnimationType.None;
        }

        void OnPreprocessAnimation()
        {
            if (!assetPath.StartsWith(AnimationsFolder))
                return;

            // Registering the table as a dependency reimports the rig files when a number in it changes,
            // which keeps an impact fraction a data edit rather than a forced reimport.
            context?.DependsOnSourceAsset(ClipTablePath);

            var table = LoadClipTable();
            if (table == null)
                return;

            var importer = (ModelImporter)assetImporter;
            var clips = importer.clipAnimations;
            if (clips.Length == 0)
                clips = importer.defaultClipAnimations;

            foreach (var clip in clips)
            {
                if (!table.TryGetValue(clip.name, out var entry))
                    continue;
                clip.loopTime = entry.Loop;
                clip.events = entry.Impact.HasValue
                    ? new[] { new AnimationEvent { functionName = ImpactFunction, time = entry.Impact.Value } }
                    : new AnimationEvent[0];
            }
            importer.clipAnimations = clips;
        }

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(TexturesFolder))
                return;

            var importer = (TextureImporter)assetImporter;
            importer.sRGBTexture = true;
            importer.maxTextureSize = 512;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.mipmapEnabled = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.isReadable = false;

            var ios = importer.GetPlatformTextureSettings("iPhone");
            ios.overridden = true;
            ios.maxTextureSize = 512;
            ios.format = TextureImporterFormat.ASTC_6x6;
            ios.textureCompression = TextureImporterCompression.Compressed;
            importer.SetPlatformTextureSettings(ios);
        }

        public sealed class ClipEntry
        {
            [JsonProperty("loop")] public bool Loop { get; set; }
            [JsonProperty("impact")] public float? Impact { get; set; }
        }

        public sealed class ClipTableFile
        {
            [JsonProperty("version")] public int Version { get; set; }
            [JsonProperty("clips")] public Dictionary<string, ClipEntry>? Clips { get; set; }
        }

        /// Reads clips.json straight from disk: this runs inside another asset's import, where the AssetDatabase is not a safe source.
        public static Dictionary<string, ClipEntry>? LoadClipTable()
        {
            if (!File.Exists(ClipTablePath))
            {
                Debug.LogError("[KayKitPostprocessor] " + ClipTablePath + " not found; rig clips imported without loop flags or OnImpact events");
                return null;
            }

            var file = JsonConvert.DeserializeObject<ClipTableFile>(File.ReadAllText(ClipTablePath));
            if (file?.Clips == null)
            {
                Debug.LogError("[KayKitPostprocessor] " + ClipTablePath + " has no \"clips\" object; rig clips imported without loop flags or OnImpact events");
                return null;
            }
            return file.Clips;
        }
    }
}
