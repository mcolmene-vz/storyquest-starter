using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace Game.Editor.Import
{
    /// Resources/Data/clips.json (ARCHITECTURE §14.2, §15.15.5): per clip name, its loop flag, OnImpact time and trim.
    /// Read by SyntyPostprocessor at import, and by the generators and ImpactFinder.
    public static class ClipTable
    {
        public const string FilePath = "Assets/_Game/Resources/Data/clips.json";
        public const string ImpactFunction = "OnImpact";

        public sealed class ClipEntry
        {
            [JsonProperty("loop")] public bool Loop { get; set; }
            [JsonProperty("impact")] public float? Impact { get; set; }
            // Seconds from the take's start to the clip's first and last frame (§15.18.3, §15.18.8: the trimmed Archer and
            // Mage Skills).
            [JsonProperty("start")] public float? Start { get; set; }
            [JsonProperty("end")] public float? End { get; set; }
        }

        sealed class ClipTableFile
        {
            [JsonProperty("version")] public int Version { get; set; }
            [JsonProperty("clips")] public Dictionary<string, ClipEntry>? Clips { get; set; }
        }

        /// Reads clips.json straight from disk: this runs inside another asset's import, where the AssetDatabase is not a safe source.
        public static Dictionary<string, ClipEntry>? Load()
        {
            if (!File.Exists(FilePath))
            {
                Debug.LogError("[ClipTable] " + FilePath + " not found; clips imported without loop flags or OnImpact events");
                return null;
            }

            var file = JsonConvert.DeserializeObject<ClipTableFile>(File.ReadAllText(FilePath));
            if (file?.Clips == null)
            {
                Debug.LogError("[ClipTable] " + FilePath + " has no \"clips\" object; clips imported without loop flags or OnImpact events");
                return null;
            }
            return file.Clips;
        }
    }
}
