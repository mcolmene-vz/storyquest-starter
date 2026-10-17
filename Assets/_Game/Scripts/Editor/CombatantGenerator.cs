using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Game.Core.Data;
using Game.Editor.Import;
using Game.Runtime;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Rendering.Universal.ShaderGUI;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Editor
{
    /// Builds the toon materials, the shared Combatant controller, one override controller per enemy, and the Synty enemy
    /// prefabs from the data tables (ARCHITECTURE §14.4, §15.16.8); HeroGenerator builds the eight Synty hero variants
    /// (§15.3). Run by ProjectSetup.Run and by -executeMethod. Existing assets are loaded and modified in place, so a run
    /// on unchanged inputs writes every file byte-identically and `git status` stays clean.
    public static class CombatantGenerator
    {
        public const string ShaderName = "StoryQuest/Toon";
        public const string DataFolder = "Assets/_Game/Resources/Data";
        public const string MaterialsFolder = "Assets/_Game/Resources/Materials/Toon";
        public const string AnimatorsFolder = "Assets/_Game/Resources/Animators";
        public const string PrefabsFolder = "Assets/_Game/Resources/Prefabs/Combatants";
        public const string ControllerPath = AnimatorsFolder + "/Combatant.controller";

        const string Tag = "[CombatantGenerator] ";
        const string ParticleShaderName = "Universal Render Pipeline/Particles/Unlit";
        const string IdleState = "Idle";
        const string BigStaggerState = "BigStagger";
        // §15.16.2: every combatant has a dedicated stagger clip, so it plays at its authored speed. Set by eye on a
        // weakness hit.
        const float BigStaggerSpeed = 1.0f;
        const float OneShotExitDuration = 0.1f;
        internal const float MinHeight = 1.2f;
        internal const float MaxHeight = 2.4f;
        // §15.16.2: an enemy's height after its scale, for the three troll types; M4's boss gets its own band.
        const float MinEnemyHeight = 2.0f;
        const float MaxEnemyHeight = 2.5f;
        const string EnemyWeaponRole = "weapon";
        static readonly string[] OneShotStates = { "Attack", "Skill", "Hit", "BigStagger", "Victory" };
        static readonly string[] ImpactStates = { "Attack", "Skill" };
        // §15.18.8: no action shot holds longer than this plus §14.4's 0.7 s rule. The tolerance absorbs float rounding of
        // a clip trimmed to exactly 2.5 s.
        const float MaxActionClipSeconds = 2.5f;
        const float ClipLengthTolerance = 0.001f;
        // Placeholder grass for the §14.4 ground plane; a flat colour under the toon ramp until the stage gets art.
        static readonly Color GroundColor = new Color(0.36f, 0.62f, 0.27f, 1f);

        public static void Generate()
        {
            var tables = TableLoader.Load(new FileTableSource(DataFolder));
            var clipTable = ClipTable.Load();
            if (clipTable == null)
                throw new InvalidOperationException(Tag + ClipTable.FilePath + " missing or unreadable");
            var clips = LoadClips();
            CheckClips(tables, clipTable, clips);

            var toon = FindShader(ShaderName);
            var particle = FindShader(ParticleShaderName);
            EnsureFolder(MaterialsFolder);
            EnsureFolder(AnimatorsFolder);
            EnsureFolder(PrefabsFolder);

            var controller = GenerateController(out var placeholders);

            var partyRenderers = HeroGenerator.Generate(tables, controller, placeholders, clips, toon);

            var enemyMaterials = new Dictionary<string, Material>();
            int enemyRenderers = 0;
            foreach (var e in tables.Enemies)
            {
                var material = EnemyMaterial(e.Texture, toon, enemyMaterials);
                foreach (var clip in e.Clips.Values.Distinct())
                    HeroGenerator.CheckAvatar(AssetDatabase.GetAssetPath(clips[clip]));
                var overrides = GenerateOverrideController(e.Id, controller, placeholders, e.Clips, clips);
                HeroGenerator.LogDerived(AnimatorsFolder + "/" + e.Id + ".overrideController",
                    e.Clips.Values.Select(c => AssetDatabase.GetAssetPath(clips[c])));
                enemyRenderers = Math.Max(enemyRenderers, GenerateEnemyPrefab(e, material, overrides, tables.Heroes.WeaponMeshes));
            }
            HeroGenerator.CheckPartyBudget(partyRenderers, enemyRenderers);
            GenerateToonMaterial(MaterialsFolder + "/ground.mat", toon, null, GroundColor);
            GenerateParticleMaterial(MaterialsFolder + "/particle.mat", particle);

            AssetDatabase.SaveAssets();
            Debug.Log(Tag + "done: " + tables.Classes.Count + " classes, " + tables.Enemies.Count + " enemies");
        }

        // ----- inputs -----

        sealed class FileTableSource : ITableSource
        {
            readonly string folder;
            public FileTableSource(string folder) => this.folder = folder;
            public string ReadText(string tableName) => File.ReadAllText(Path.Combine(folder, tableName + ".json"));
        }

        /// Every AnimationClip under the Synty Animations folder by name, first file wins (the import check warns on
        /// duplicates).
        static Dictionary<string, AnimationClip> LoadClips()
        {
            var clips = new Dictionary<string, AnimationClip>();
            var folders = new[] { SyntyPostprocessor.AnimationsFolder.TrimEnd('/') };
            var paths = AssetDatabase.FindAssets("t:Model", folders)
                .Select(AssetDatabase.GUIDToAssetPath).Distinct().OrderBy(p => p, StringComparer.Ordinal);
            foreach (string path in paths)
                foreach (var clip in AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>())
                    if (!clip.name.StartsWith("__preview__") && !clips.ContainsKey(clip.name))
                        clips[clip.name] = clip;
            return clips;
        }

        /// §14.2: every clip in a clips map exists under Animations/, has a clips.json entry, and Attack/Skill clips have
        /// an impact and, trimmed, run at most 2.5 s (§15.18.8). Hero maps are checked once per body, with {g} resolved (§15.15.5).
        static void CheckClips(GameTables tables, Dictionary<string, ClipTable.ClipEntry> clipTable,
            Dictionary<string, AnimationClip> clips)
        {
            var problems = new List<string>();
            var maps = tables.Classes.SelectMany(c => Bodies.All.Select(b => (HeroGenerator.VariantId(c.Id, b), HeroGenerator.ResolveClips(c.Clips, b))))
                .Concat(tables.Enemies.Select(e => (e.Id, e.Clips)));
            foreach (var (id, map) in maps)
                foreach (var state in TableLoader.ClipStates)
                {
                    string name = map[state];
                    if (!clips.ContainsKey(name))
                        problems.Add($"{id}.{state}: clip '{name}' not found in any FBX under {SyntyPostprocessor.AnimationsFolder}");
                    if (!clipTable.TryGetValue(name, out var entry))
                        problems.Add($"{id}.{state}: clip '{name}' has no entry in {ClipTable.FilePath}");
                    else if (Array.IndexOf(ImpactStates, state) >= 0 && !entry.Impact.HasValue)
                        problems.Add($"{id}.{state}: clip '{name}' has no impact in {ClipTable.FilePath}");
                    if (Array.IndexOf(ImpactStates, state) >= 0 && clips.TryGetValue(name, out var clip) &&
                        clip.length > MaxActionClipSeconds + ClipLengthTolerance)
                        problems.Add($"{id}.{state}: clip '{name}' runs {clip.length:F3} s, over §15.18.8's {MaxActionClipSeconds} s; " +
                                     $"trim it with \"start\"/\"end\" in {ClipTable.FilePath}");
                }
            if (problems.Count > 0)
                throw new InvalidOperationException(Tag + problems.Count + " clip problem(s):\n - " + string.Join("\n - ", problems));
        }

        static Shader FindShader(string name)
        {
            var shader = Shader.Find(name);
            if (shader == null)
                throw new InvalidOperationException(Tag + "shader '" + name + "' not found");
            return shader;
        }

        internal static GameObject LoadModel(string path)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (model == null)
                throw new InvalidOperationException(Tag + "model not imported: " + path);
            return model;
        }

        // ----- materials -----

        internal static Material GenerateToonMaterial(string path, Shader shader, Texture2D? texture, Color baseColor)
        {
            var before = ReadBytes(path);
            var material = LoadOrCreate(path, () => new Material(shader));
            if (material.shader != shader)
                material.shader = shader;
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", baseColor);
            Save(material, path, before);
            return material;
        }

        /// §15.16.8: one material per enemy texture, for the body and the weapon alike, with no skin tint.
        static Material EnemyMaterial(string texture, Shader toon, Dictionary<string, Material> cache)
        {
            if (cache.TryGetValue(texture, out var cached))
                return cached;
            string texturePath = SyntyPostprocessor.TexturesFolder + texture + ".png";
            string path = MaterialsFolder + "/enemy_" + texture + ".mat";
            var material = GenerateToonMaterial(path, toon, HeroGenerator.LoadTexture(texturePath), Color.white);
            HeroGenerator.LogDerived(path, new[] { texturePath });
            cache[texture] = material;
            return material;
        }

        static void GenerateParticleMaterial(string path, Shader shader)
        {
            var before = ReadBytes(path);
            var material = LoadOrCreate(path, () => new Material(shader));
            if (material.shader != shader)
                material.shader = shader;
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Surface", (float)BaseShaderGUI.SurfaceType.Transparent);
            material.SetFloat("_Blend", (float)BaseShaderGUI.BlendMode.Additive);
            material.SetFloat("_ColorMode", (float)ParticleGUI.ColorMode.Multiply);
            // URP's own inspector code derives blend state, ZWrite, keywords, queue and the RenderType tag from those
            // three floats, so the file is exactly what the inspector would have written.
            BaseShaderGUI.SetMaterialKeywords(material, null, ParticleGUI.SetMaterialKeywords);
            Save(material, path, before);
        }

        // ----- animators -----

        static AnimatorController GenerateController(out Dictionary<string, AnimationClip> placeholders)
        {
            var before = ReadBytes(ControllerPath);
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
                controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

            while (controller.layers.Length > 1)
                controller.RemoveLayer(controller.layers.Length - 1);
            if (controller.layers.Length == 0)
                controller.AddLayer("Base Layer");
            foreach (var parameter in controller.parameters)
                controller.RemoveParameter(parameter);

            var machine = controller.layers[0].stateMachine;
            var existingClips = AssetDatabase.LoadAllAssetsAtPath(ControllerPath).OfType<AnimationClip>().ToList();
            placeholders = new Dictionary<string, AnimationClip>();
            var states = new Dictionary<string, AnimatorState>();
            for (int i = 0; i < TableLoader.ClipStates.Length; i++)
            {
                string name = TableLoader.ClipStates[i];

                // One empty placeholder clip per state, named like the state, so an override controller maps each state on its own.
                var clip = existingClips.FirstOrDefault(c => c.name == name);
                if (clip == null)
                {
                    clip = new AnimationClip { name = name };
                    AssetDatabase.AddObjectToAsset(clip, controller);
                }
                placeholders[name] = clip;

                var state = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == name);
                if (state == null)
                    state = machine.AddState(name, new Vector3(320f, 80f * i, 0f));
                state.motion = clip;
                state.speed = name == BigStaggerState ? BigStaggerSpeed : 1f;
                states[name] = state;
            }

            foreach (var child in machine.states)
                if (!states.ContainsKey(child.state.name))
                    machine.RemoveState(child.state);
            foreach (var clip in existingClips)
                if (!placeholders.ContainsKey(clip.name))
                {
                    AssetDatabase.RemoveObjectFromAsset(clip);
                    Object.DestroyImmediate(clip, true);
                }
            foreach (var transition in machine.anyStateTransitions)
                machine.RemoveAnyStateTransition(transition);
            foreach (var transition in machine.entryTransitions)
                machine.RemoveEntryTransition(transition);
            machine.defaultState = states[IdleState];

            foreach (var pair in states)
            {
                var state = pair.Value;
                var existing = state.transitions;
                bool oneShot = Array.IndexOf(OneShotStates, pair.Key) >= 0;
                for (int i = oneShot ? 1 : 0; i < existing.Length; i++)
                    state.RemoveTransition(existing[i]);
                if (!oneShot)
                    continue;

                // One-shot states return to Idle when the clip ends; Defend and Defeat hold until the presenter cross-fades.
                var transition = existing.Length > 0 ? existing[0] : state.AddTransition(states[IdleState]);
                transition.destinationState = states[IdleState];
                transition.destinationStateMachine = null;
                transition.isExit = false;
                transition.hasExitTime = true;
                transition.exitTime = 1f;
                transition.hasFixedDuration = true;
                transition.duration = OneShotExitDuration;
                transition.offset = 0f;
                transition.interruptionSource = TransitionInterruptionSource.None;
                transition.orderedInterruption = true;
                transition.conditions = Array.Empty<AnimatorCondition>();
                transition.mute = false;
                transition.solo = false;
            }

            Save(controller, ControllerPath, before);
            return controller;
        }

        internal static AnimatorOverrideController GenerateOverrideController(string id, AnimatorController controller,
            Dictionary<string, AnimationClip> placeholders, Dictionary<string, string> clipMap, Dictionary<string, AnimationClip> clips)
        {
            string path = AnimatorsFolder + "/" + id + ".overrideController";
            var before = ReadBytes(path);
            var overrides = LoadOrCreate(path, () => new AnimatorOverrideController(controller));
            overrides.runtimeAnimatorController = controller;

            var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>(TableLoader.ClipStates.Length);
            foreach (var state in TableLoader.ClipStates)
                pairs.Add(new KeyValuePair<AnimationClip, AnimationClip>(placeholders[state], clips[clipMap[state]]));
            overrides.ApplyOverrides(pairs);

            Save(overrides, path, before);
            return overrides;
        }

        // ----- prefabs -----

        /// §15.16.2, §15.16.8: the enemy's parts kept from its Synty character file (the troll's body as the derived mesh
        /// without the skull), its weapon in a hero-made socket, one material on every renderer, and the scale from data.
        /// Returns the prefab's renderer count, for the §15.7 party budget.
        static int GenerateEnemyPrefab(EnemyDefinition enemy, Material material, AnimatorOverrideController overrides,
            Dictionary<string, WeaponMeshDefinition> meshes)
        {
            string id = enemy.Id;
            string modelPath = SyntyPostprocessor.CharactersFolder + enemy.Model + ".fbx";
            HeroGenerator.CheckAvatar(modelPath);
            var source = LoadModel(modelPath);
            string path = PrefabsFolder + "/" + id + ".prefab";
            var before = ReadBytes(path);

            var root = new GameObject(id);
            try
            {
                var model = Object.Instantiate(source);
                model.name = source.name;
                model.transform.SetParent(root.transform, false);
                var animator = model.GetComponent<Animator>();
                if (animator == null || !animator.isHuman)
                    throw new InvalidOperationException($"{Tag}{id}: {modelPath} has no Humanoid Animator");

                var body = HeroGenerator.KeepParts(id, model, modelPath, enemy.Parts, new List<string>(), out _);
                foreach (var renderer in body)
                {
                    if (renderer is SkinnedMeshRenderer skinned && skinned.name == SkullRemoval.SourcePart)
                        skinned.sharedMesh = SkullRemoval.Derive(skinned.sharedMesh);
                    ApplyMaterial(renderer, material);
                }

                // Body renderers at the bind pose, before the scale and the weapon (the measure VariantCapture reported in D6b step 2).
                var bounds = body[0].bounds;
                foreach (var renderer in body)
                    bounds.Encapsulate(renderer.bounds);
                float native = bounds.size.y;
                float scale = (float)enemy.Scale;
                float height = native * scale;
                if (height < MinEnemyHeight || height > MaxEnemyHeight)
                    throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture,
                        "{0}{1}: {2} stands {3:F3} m x {4} = {5:F3} m, outside {6}–{7} m; fix the scale in enemies.json",
                        Tag, id, enemy.Model, native, scale, height, MinEnemyHeight, MaxEnemyHeight));

                var sockets = HeroGenerator.AddSockets(id, animator);
                HeroGenerator.AddWeapon(id, sockets[enemy.WeaponSocket], enemy.WeaponMesh, EnemyWeaponRole, material, meshes);

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

                root.transform.localScale = Vector3.one * scale;

                PrefabUtility.SaveAsPrefabAsset(root, path, out bool success);
                if (!success)
                    throw new InvalidOperationException(Tag + "could not save " + path);
                Report(path, before);
                int renderers = root.GetComponentsInChildren<Renderer>().Length;
                long triangles = root.GetComponentsInChildren<Renderer>()
                    .Select(r => r is SkinnedMeshRenderer s ? s.sharedMesh : r.GetComponent<MeshFilter>().sharedMesh)
                    .Distinct().Sum(m => Enumerable.Range(0, m.subMeshCount).Sum(i => (long)m.GetIndexCount(i) / 3));
                Debug.Log(Tag + string.Format(CultureInfo.InvariantCulture,
                    "{0}: {1} {2:F3} m x {3} = {4:F3} m (bind pose, body bounds); {5} under {6}; {7} renderers, {8} unique triangles; {9}",
                    id, string.Join("+", enemy.Parts), native, scale, height, enemy.WeaponMesh, enemy.WeaponSocket, renderers, triangles,
                    string.Join(", ", body.Select(r => r.name + " mesh " + ((SkinnedMeshRenderer)r).sharedMesh.name))));
                HeroGenerator.LogDerived(path, new[] { modelPath, HeroGenerator.WeaponPath(enemy.WeaponMesh) });
                return renderers;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        internal static void ApplyMaterial(Renderer renderer, Material material)
        {
            var materials = renderer.sharedMaterials;
            if (materials.Length == 0)
                materials = new Material[1];
            for (int i = 0; i < materials.Length; i++)
                materials[i] = material;
            renderer.sharedMaterials = materials;
        }

        // ----- asset plumbing -----

        internal static T LoadOrCreate<T>(string path, Func<T> create) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
                return asset;
            asset = create();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        internal static void Save(Object asset, string path, byte[]? before)
        {
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssetIfDirty(asset);
            Report(path, before);
        }

        internal static byte[]? ReadBytes(string path) => File.Exists(path) ? File.ReadAllBytes(path) : null;

        internal static void Report(string path, byte[]? before)
        {
            string state = before == null ? "created"
                : before.SequenceEqual(File.ReadAllBytes(path)) ? "unchanged"
                : "updated";
            Debug.Log(Tag + path + ": " + state);
        }

        internal static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
                return;
            string parent = Path.GetDirectoryName(folder)!.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
