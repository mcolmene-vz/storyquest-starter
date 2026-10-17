using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Game.Editor.Import
{
    /// Prints, for every clips.json entry that has an impact, the normalized time at which the fastest limb peaks.
    /// A tool for choosing the provisional numbers in clips.json (PLAN-M1 lead's reading); the number is the artifact,
    /// and the device run is what confirms it. KayKit clips sample the Knight by bone name; Synty clips are Humanoid and
    /// sample the modular hero through GetBoneTransform (ARCHITECTURE §15.15.5). A Synty attack clip longer than §15.18.3's
    /// 2.5 s also gets a foot-height profile, which is how its "end" (the landing frame) is found.
    public static class ImpactFinder
    {
        const string Tag = "[ImpactFinder] ";
        const string ModelPath = KayKitPostprocessor.CharactersFolder + "Knight.fbx";
        const int Samples = 100;
        const float LongClipSeconds = 2.5f;
        const float ProfileStep = 0.1f;
        static readonly string[] Limbs = { "hand.r", "hand.l", "foot.r", "foot.l" };
        static readonly HumanBodyBones[] HumanLimbs =
            { HumanBodyBones.RightHand, HumanBodyBones.LeftHand, HumanBodyBones.RightFoot, HumanBodyBones.LeftFoot };

        public static void Run()
        {
            var table = KayKitPostprocessor.LoadClipTable();
            if (table == null)
                return;

            var kayKit = LoadClips(KayKitPostprocessor.AnimationsFolder);
            var synty = LoadClips(SyntyPostprocessor.AnimationsFolder);

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var hero = AssetDatabase.LoadAssetAtPath<GameObject>(SyntyPostprocessor.HeroModel);
            if (model == null || hero == null)
            {
                Debug.LogError(Tag + (model == null ? ModelPath : SyntyPostprocessor.HeroModel) + " not imported");
                return;
            }

            var instance = Object.Instantiate(model);
            var limbs = Limbs.Select(n => FindDeep(instance.transform, n)).ToArray();
            for (int i = 0; i < limbs.Length; i++)
                if (limbs[i] == null)
                    Debug.LogWarning(Tag + "bone " + Limbs[i] + " not found under " + ModelPath);

            var heroInstance = Object.Instantiate(hero);
            var animator = heroInstance.GetComponent<Animator>();
            var humanLimbs = HumanLimbs.Select(b => animator != null && animator.isHuman ? animator.GetBoneTransform(b) : null).ToArray();
            if (humanLimbs.Any(l => l == null))
                Debug.LogError(Tag + SyntyPostprocessor.HeroModel + " has no Humanoid avatar with hands and feet; Synty clips not sampled");

            AnimationMode.StartAnimationMode();
            try
            {
                foreach (var pair in table.Where(p => p.Value.Impact.HasValue).OrderBy(p => p.Key))
                {
                    if (kayKit.TryGetValue(pair.Key, out var clip))
                    {
                        Report(clip, limbs, Limbs, t =>
                        {
                            AnimationMode.BeginSampling();
                            AnimationMode.SampleAnimationClip(instance, clip, t);
                            AnimationMode.EndSampling();
                        });
                    }
                    else if (synty.TryGetValue(pair.Key, out clip))
                    {
                        if (humanLimbs.Any(l => l == null))
                            continue;
                        // AnimationClip.SampleAnimation retargets a Humanoid clip through the instance's avatar; the
                        // AnimationMode path above does not keep the pose for Humanoid clips.
                        var sampled = clip;
                        Report(sampled, humanLimbs, HumanLimbs.Select(b => b.ToString()).ToArray(), t => sampled.SampleAnimation(heroInstance, t));
                        if (clip.length > LongClipSeconds)
                            ReportFeet(sampled, heroInstance, humanLimbs[2]!, humanLimbs[3]!, animator!.GetBoneTransform(HumanBodyBones.Hips));
                    }
                    else
                    {
                        Debug.LogWarning(Tag + pair.Key + ": clip not found");
                    }
                }
            }
            finally
            {
                AnimationMode.StopAnimationMode();
                Object.DestroyImmediate(instance);
                Object.DestroyImmediate(heroInstance);
            }
        }

        static Dictionary<string, AnimationClip> LoadClips(string folder) =>
            AssetDatabase.FindAssets("t:Model", new[] { folder.TrimEnd('/') })
                .Select(AssetDatabase.GUIDToAssetPath).Distinct()
                .SelectMany(AssetDatabase.LoadAllAssetsAtPath).OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__"))
                .GroupBy(c => c.name).ToDictionary(g => g.Key, g => g.First());

        static void Report(AnimationClip clip, Transform?[] limbs, string[] names, System.Action<float> sample)
        {
            var previous = new Vector3[limbs.Length];
            var peakTime = new float[limbs.Length];
            var peakSpeed = new float[limbs.Length];
            float dt = clip.length / Samples;
            for (int i = 0; i <= Samples; i++)
            {
                sample(i * dt);
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

            string perLimb = string.Join(", ", names.Select((n, l) => $"{n} {peakTime[l]:F2}@{peakSpeed[l]:F1}m/s"));
            Debug.Log(Tag + $"{clip.name}: {clip.length:F2}s, {perLimb} -> impact {peakTime[fastest]:F2} ({names[fastest]})");
        }

        /// Lowest foot and hip height every 0.1 s: a landing is where the lowest foot returns to the ground after the leap.
        static void ReportFeet(AnimationClip clip, GameObject instance, Transform footR, Transform footL, Transform hips)
        {
            var lines = new List<string>();
            for (float t = 0f; t <= clip.length + 1e-4f; t += ProfileStep)
            {
                clip.SampleAnimation(instance, t);
                lines.Add($"{t:F1}s foot {Mathf.Min(footR.position.y, footL.position.y):F2} hips {hips.position.y:F2}");
            }
            Debug.Log(Tag + $"{clip.name} is longer than {LongClipSeconds}s; heights in metres: " + string.Join("; ", lines));
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
