using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Game.Editor.Import
{
    /// Prints, for every clips.json entry that has an impact, the normalized time at which the fastest limb peaks.
    /// A tool for choosing the provisional numbers in clips.json (PLAN-M1 lead's reading); the number is the artifact,
    /// and the device run in D6 is what confirms it.
    public static class ImpactFinder
    {
        const string Tag = "[ImpactFinder] ";
        const string ModelPath = KayKitPostprocessor.CharactersFolder + "Knight.fbx";
        const int Samples = 100;
        static readonly string[] Limbs = { "hand.r", "hand.l", "foot.r", "foot.l" };

        public static void Run()
        {
            var table = KayKitPostprocessor.LoadClipTable();
            if (table == null)
                return;

            var clips = AssetDatabase.FindAssets("t:Model", new[] { KayKitPostprocessor.AnimationsFolder.TrimEnd('/') })
                .Select(AssetDatabase.GUIDToAssetPath).Distinct()
                .SelectMany(AssetDatabase.LoadAllAssetsAtPath).OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__"))
                .GroupBy(c => c.name).ToDictionary(g => g.Key, g => g.First());

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null)
            {
                Debug.LogError(Tag + ModelPath + " not imported");
                return;
            }

            var instance = Object.Instantiate(model);
            var limbs = Limbs.Select(n => FindDeep(instance.transform, n)).ToArray();
            for (int i = 0; i < limbs.Length; i++)
                if (limbs[i] == null)
                    Debug.LogWarning(Tag + "bone " + Limbs[i] + " not found under " + ModelPath);

            AnimationMode.StartAnimationMode();
            try
            {
                foreach (var pair in table.Where(p => p.Value.Impact.HasValue).OrderBy(p => p.Key))
                {
                    if (!clips.TryGetValue(pair.Key, out var clip))
                    {
                        Debug.LogWarning(Tag + pair.Key + ": clip not found");
                        continue;
                    }
                    Report(instance, clip, limbs);
                }
            }
            finally
            {
                AnimationMode.StopAnimationMode();
                Object.DestroyImmediate(instance);
            }
        }

        static void Report(GameObject instance, AnimationClip clip, Transform?[] limbs)
        {
            var bindings = AnimationUtility.GetCurveBindings(clip);
            int resolved = bindings.Count(b => instance.transform.Find(b.path) != null);

            var previous = new Vector3[limbs.Length];
            var peakTime = new float[limbs.Length];
            var peakSpeed = new float[limbs.Length];
            float dt = clip.length / Samples;
            for (int i = 0; i <= Samples; i++)
            {
                AnimationMode.BeginSampling();
                AnimationMode.SampleAnimationClip(instance, clip, i * dt);
                AnimationMode.EndSampling();
                for (int l = 0; l < limbs.Length; l++)
                {
                    if (limbs[l] == null)
                        continue;
                    var position = limbs[l]!.position;
                    if (i > 0)
                    {
                        float speed = (position - previous[l]).magnitude / dt;
                        if (speed > peakSpeed[l])
                        {
                            peakSpeed[l] = speed;
                            peakTime[l] = (i - 0.5f) / Samples;
                        }
                    }
                    previous[l] = position;
                }
            }

            int fastest = 0;
            for (int l = 1; l < limbs.Length; l++)
                if (peakSpeed[l] > peakSpeed[fastest])
                    fastest = l;

            string perLimb = string.Join(", ", Limbs.Select((n, l) => $"{n} {peakTime[l]:F2}@{peakSpeed[l]:F1}m/s"));
            Debug.Log(Tag + $"{clip.name}: {clip.length:F2}s, bindings resolved {resolved}/{bindings.Length}, {perLimb} -> impact {peakTime[fastest]:F2} ({Limbs[fastest]})");
        }

        static Transform? FindDeep(Transform root, string name)
        {
            if (root.name == name)
                return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var hit = FindDeep(root.GetChild(i), name);
                if (hit != null)
                    return hit;
            }
            return null;
        }
    }
}
