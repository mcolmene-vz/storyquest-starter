using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Game.Editor.Import
{
    /// -executeMethod gate for the KayKit import (PLAN-M1 D1): every clip named in clips.json resolves under Animations/
    /// with the loop flag and OnImpact event the postprocessor adds, and every character model stands 1.2–2.4 m.
    public static class KayKitImportCheck
    {
        const string Tag = "[KayKitImportCheck] ";
        const float MinHeight = 1.2f;
        const float MaxHeight = 2.4f;
        const float ImpactTolerance = 0.02f;

        public static void Run()
        {
            if (Environment.GetEnvironmentVariable("SQ_REIMPORT") == "1")
            {
                Debug.Log(Tag + "SQ_REIMPORT=1: forcing a recursive reimport of " + KayKitPostprocessor.Root);
                AssetDatabase.ImportAsset(KayKitPostprocessor.Root.TrimEnd('/'),
                    ImportAssetOptions.ForceUpdate | ImportAssetOptions.ImportRecursive);
            }

            var failures = new List<string>();
            CheckClips(failures);
            CheckCharacters(failures);

            if (failures.Count == 0)
            {
                Debug.Log(Tag + "ok");
                return;
            }

            string message = Tag + failures.Count + " failure(s):\n  " + string.Join("\n  ", failures);
            Debug.LogError(message);
            if (Application.isBatchMode)
                EditorApplication.Exit(1);
            else
                throw new InvalidOperationException(message);
        }

        static void CheckClips(List<string> failures)
        {
            var found = new Dictionary<string, AnimationClip>();
            var files = ModelPaths(KayKitPostprocessor.AnimationsFolder);
            foreach (string path in files)
            {
                var clips = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                    .Where(c => !c.name.StartsWith("__preview__")).OrderBy(c => c.name).ToList();
                Debug.Log(Tag + Path.GetFileName(path) + ": " + clips.Count + " clips: " + string.Join(", ", clips.Select(c => c.name)));
                foreach (var clip in clips)
                {
                    if (found.ContainsKey(clip.name))
                        Debug.LogWarning(Tag + "clip " + clip.name + " exists in more than one rig file; the generator resolves clips by name");
                    else
                        found[clip.name] = clip;
                }
            }
            Debug.Log(Tag + files.Count + " rig files, " + found.Count + " distinct clips");

            var table = KayKitPostprocessor.LoadClipTable();
            if (table == null)
            {
                failures.Add(KayKitPostprocessor.ClipTablePath + " missing or unreadable");
                return;
            }

            foreach (var pair in table.OrderBy(p => p.Key))
            {
                string name = pair.Key;
                if (!found.TryGetValue(name, out var clip))
                {
                    failures.Add("clip not found in any rig file: " + name);
                    continue;
                }

                var events = AnimationUtility.GetAnimationEvents(clip);
                var impacts = events.Where(e => e.functionName == KayKitPostprocessor.ImpactFunction)
                    .Select(e => e.time / clip.length).ToList();
                string impactText = impacts.Count == 0 ? "" : " OnImpact@" + string.Join(",", impacts.Select(t => t.ToString("F3")));
                Debug.Log(Tag + $"{name}: {clip.length:F2}s loop={clip.isLooping} events={events.Length}{impactText}");

                if (clip.isLooping != pair.Value.Loop)
                    failures.Add($"{name}: loopTime is {clip.isLooping}, clips.json says {pair.Value.Loop} (postprocessor did not run? try SQ_REIMPORT=1)");
                if (pair.Value.Impact is float impact)
                {
                    if (impacts.Count != 1 || Mathf.Abs(impacts[0] - impact) > ImpactTolerance)
                        failures.Add($"{name}: expected one OnImpact event at {impact:F3}, found [{string.Join(",", impacts.Select(t => t.ToString("F3")))}] (postprocessor did not run? try SQ_REIMPORT=1)");
                }
                else if (impacts.Count > 0)
                {
                    failures.Add($"{name}: has an OnImpact event but no impact in clips.json");
                }
            }
        }

        static void CheckCharacters(List<string> failures)
        {
            foreach (string path in ModelPaths(KayKitPostprocessor.CharactersFolder))
            {
                string file = Path.GetFileName(path);
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (importer == null || model == null)
                {
                    failures.Add(file + ": no model imported");
                    continue;
                }

                var instance = UnityEngine.Object.Instantiate(model);
                try
                {
                    var renderers = instance.GetComponentsInChildren<Renderer>(true);
                    var bounds = new Bounds();
                    for (int i = 0; i < renderers.Length; i++)
                    {
                        if (i == 0) bounds = renderers[i].bounds;
                        else bounds.Encapsulate(renderers[i].bounds);
                    }
                    float height = renderers.Length == 0 ? 0f : bounds.size.y;
                    var top = renderers.OrderByDescending(r => r.bounds.max.y).FirstOrDefault();
                    Debug.Log(Tag + $"{file}: rig {importer.animationType}, avatar {importer.avatarSetup}, animation {(importer.importAnimation ? "on" : "off")}, " +
                              $"fileScale {importer.fileScale}, globalScale {importer.globalScale}, {renderers.Length} renderers, height {height:F2} m " +
                              $"(y {bounds.min.y:F2}..{bounds.max.y:F2}" + (top == null ? ")" : $", top is {top.name} at {top.bounds.max.y:F2})"));
                    if (importer.animationType != ModelImporterAnimationType.Generic)
                        failures.Add($"{file}: rig is {importer.animationType}, expected Generic");
                    if (height < MinHeight || height > MaxHeight)
                        failures.Add($"{file}: height {height:F2} m outside {MinHeight}–{MaxHeight} m");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(instance);
                }
            }
        }

        static List<string> ModelPaths(string folder) =>
            AssetDatabase.FindAssets("t:Model", new[] { folder.TrimEnd('/') })
                .Select(AssetDatabase.GUIDToAssetPath).Distinct().OrderBy(p => p).ToList();
    }
}
