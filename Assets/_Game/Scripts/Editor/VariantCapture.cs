using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Game.Core.Data;
using Game.Editor.Import;
using Game.Runtime;
using TMPro;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Game.Editor
{
    /// Front captures of the eight hero variants for the producer's skin-tone check (ruling 7, ARCHITECTURE §15.3), and
    /// one hand close-up per weapon mesh for tuning classes.json weaponMeshes (§15.18.3). RunTroll captures the generated
    /// troll_grunt without its belt skull and re-measures the group and action shots (PLAN-M2 D6b step 3, §15.16.8).
    /// Renders, so it runs in batch mode without -nographics:
    ///   "$UNITY" -batchmode -projectPath . -executeMethod Game.Editor.VariantCapture.Run -logFile - -quit
    ///   "$UNITY" -batchmode -projectPath . -executeMethod Game.Editor.VariantCapture.RunTroll -logFile - -quit
    /// Writes TGA (an 18-byte header and raw pixels; EncodeToPNG lives in the imageconversion module §13.2 dropped) to
    /// Builds/evidence/M2/; convert with `sips -s format png`. Nothing is saved into Assets/ and nothing ships.
    public static class VariantCapture
    {
        const string Tag = "[VariantCapture] ";
        const string OutFolder = "Builds/evidence/M2/";
        const int Size = 1024;
        const float Fov = 30f;
        const float FrontDistance = 3.2f;
        const float GripDistance = 1.1f;
        // The stage's light and fill (GameBootstrap.CreateLight, BattleStage), so tones read as they do in battle.
        static readonly Quaternion LightRotation = Quaternion.Euler(50f, -30f, 0f);
        static readonly Color AmbientFill = new Color(0.55f, 0.60f, 0.70f, 1f);
        static readonly Color Sky = new Color(0.53f, 0.78f, 0.92f, 1f);

        public static void Run()
        {
            Directory.CreateDirectory(OutFolder);
            var tables = TableLoader.Load(new FileSource(CombatantGenerator.DataFolder));
            var camera = BuildScene();
            try
            {
                foreach (var cls in tables.Classes)
                    foreach (var body in Bodies.All)
                        CaptureFront(camera, cls, body);
                CaptureGrips(camera, tables);
            }
            finally
            {
                Object.DestroyImmediate(camera.targetTexture);
            }
            Debug.Log(Tag + "done");
        }

        /// An unsaved empty scene with the stage's light, flat ambient and sky colour, facing the camera from the light's
        /// side so the front of each hero is lit.
        static Camera BuildScene()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // The editor otherwise draws a placeholder while a variant compiles, and the first capture comes out cyan.
            ShaderUtil.allowAsyncCompilation = false;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = AmbientFill;

            var light = new GameObject("Directional Light").AddComponent<Light>();
            light.transform.rotation = LightRotation;
            light.type = LightType.Directional;
            light.intensity = 1f;
            light.shadows = LightShadows.Hard;

            var camera = new GameObject("Capture Camera").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Sky;
            camera.fieldOfView = Fov;
            camera.nearClipPlane = 0.05f;
            camera.targetTexture = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
            return camera;
        }

        /// The light travels towards +z, so the camera stands on -z and the hero turns to face it.
        static readonly Quaternion FacingCamera = Quaternion.Euler(0f, 180f, 0f);

        static GameObject Spawn(string id, ClassDefinition cls, string body, out Animator animator)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CombatantGenerator.PrefabsFolder + "/" + id + ".prefab");
            if (prefab == null)
                throw new InvalidOperationException(Tag + id + ".prefab is missing; run Game.Editor.CombatantGenerator.Generate");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.SetPositionAndRotation(Vector3.zero, FacingCamera);
            animator = instance.GetComponentInChildren<Animator>();
            // The idle's first frame: the pose the hero holds in battle, with the fingers on the grip.
            var idleName = HeroGenerator.ResolveClip(cls.Clips["Idle"], body);
            var idle = ((AnimatorOverrideController)animator.runtimeAnimatorController).animationClips.First(c => c.name == idleName);
            idle.SampleAnimation(animator.gameObject, 0f);
            return instance;
        }

        /// §15.3: FOV 30, at chest height, 3.2 m in front, base outfit and starter weapon.
        static void CaptureFront(Camera camera, ClassDefinition cls, string body)
        {
            string id = HeroGenerator.VariantId(cls.Id, body);
            var instance = Spawn(id, cls, body, out var animator);
            try
            {
                var chest = animator.GetBoneTransform(HumanBodyBones.Chest).position;
                camera.transform.position = new Vector3(0f, chest.y, -FrontDistance);
                camera.transform.rotation = Quaternion.identity;
                string path = OutFolder + "variant-" + id + ".tga";
                WriteTga(path, Render(camera, Size, Size), Size, Size);
                Debug.Log(Tag + "wrote " + path);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        /// One close-up per weapon mesh, on the first variant that carries it, from the front and from the thumb side.
        /// The bow frames both hands (§15.18.3).
        static void CaptureGrips(Camera camera, GameTables tables)
        {
            var done = new HashSet<string>();
            foreach (var cls in tables.Classes)
            {
                var item = tables.Items.Single(i => i.Kind == ItemKinds.Weapon && i.ClassId == cls.Id);
                foreach (var (mesh, role) in new[] { (cls.WeaponMesh, HeroGenerator.StarterWeapon), (item.Mesh!, item.Id) })
                {
                    if (!done.Add(mesh))
                        continue;
                    string id = HeroGenerator.VariantId(cls.Id, Bodies.Boy);
                    var instance = Spawn(id, cls, Bodies.Boy, out var animator);
                    try
                    {
                        var socket = instance.GetComponentsInChildren<Transform>(true).First(t => t.name == cls.WeaponSocket);
                        for (int i = 0; i < socket.childCount; i++)
                            socket.GetChild(i).gameObject.SetActive(socket.GetChild(i).name == role);

                        var left = animator.GetBoneTransform(HumanBodyBones.LeftHand).position;
                        var right = animator.GetBoneTransform(HumanBodyBones.RightHand).position;
                        bool bothHands = cls.WeaponSocket == SocketNames.HandL;
                        var target = bothHands ? (left + right) * 0.5f : socket.position;
                        float distance = bothHands ? GripDistance + Vector3.Distance(left, right) * 1.8f : GripDistance;
                        // Front: from the camera side; side: from the hero's right (world -x after the turn).
                        var views = new[] { new Vector3(0f, 0.1f, -1f), new Vector3(-1f, 0.1f, 0f) };
                        var pixels = new Color32[Size * Size];
                        int half = Size / 2;
                        for (int v = 0; v < views.Length; v++)
                        {
                            camera.transform.position = target + views[v].normalized * distance;
                            camera.transform.rotation = Quaternion.LookRotation(target - camera.transform.position);
                            var view = Render(camera, Size, Size);
                            // Keep the middle half of each square render, side by side.
                            for (int y = 0; y < Size; y++)
                                Array.Copy(view, y * Size + Size / 4, pixels, y * Size + v * half, half);
                        }
                        string path = OutFolder + "d6-grip-" + mesh + ".tga";
                        WriteTga(path, pixels, Size, Size);
                        Debug.Log(Tag + "wrote " + path + " (" + id + ", " + role + " under " + cls.WeaponSocket + ")");
                    }
                    finally
                    {
                        Object.DestroyImmediate(instance);
                    }
                }
            }
        }

        // ----- D6b step 3: the Synty troll_grunt and the framing re-measure (ARCHITECTURE §15.16.8, §15.18.8 item 3) -----

        const string Troll = "troll_grunt";
        const string Prefix = "d6b3-";
        static readonly string[] TrollColours = { "FantasyRivals_01_A", "FantasyRivals_01_B", "FantasyRivals_01_C", "FantasyRivals_01_D" };
        // §14.4's group checks (ruling "Troll" c) and §15.18.8's action-shot check.
        const float MinFeet = 0.27f;
        const float MaxHead = 0.86f;
        // §15.18.8 item 3 as ruled after D6b step 3: a hero (1.93 m) is 0.84 of the troll's size.
        const float MinEnemyTargetHeight = 0.20f;
        const float MinHeroTargetHeight = 0.17f;
        // The ratios CLAUDE.md names, each with its landscape safe area in pixels (x, y from the bottom, width, height):
        // the iPhone 15 Pro simulator (19.5:9), a 4:3 iPad without insets, and the iPad 10th generation simulator and test
        // iPad (2360×1640). The group shot's lateral offset and the card column depend on both.
        static readonly (string Name, int Width, int Height, Rect Safe)[] Ratios =
        {
            ("iphone", 2556, 1179, new Rect(177f, 63f, 2202f, 1116f)),
            ("ipad", 2048, 1536, new Rect(0f, 0f, 2048f, 1536f)),
            ("ipad2360", 2360, 1640, new Rect(0f, 40f, 2360f, 1600f)),
        };
        // BattleStage's ground plane; the stage itself is not built here because its constructor calls Object.Destroy.
        const float GroundScale = 10f;
        const float FrontMargin = 1.08f;
        const float HeadDistance = 1.5f;
        const float HipDistance = 1.2f;
        const int IdlePhases = 12;
        const int SheetDivisor = 4;
        // BattleHud's layout in 1920×1080 reference px (§14.5), placed by the CanvasScaler's match-0.5 scale.
        const float HudMargin = 24f;
        const float CardWidth = 300f;
        const float CardHeight = 170f;
        const float CardGap = 12f;
        const float EnemyCardWidth = 480f;
        const float EnemyCardHeight = 130f;
        const float BannerWidth = 560f;
        const float BannerHeight = 110f;
        // DamageNumberPool's numbers (§14.4 step 10), mirrored here: 3D TMP at font size 4, popping to 1.3 at 0.1 s and back
        // to 1 at 0.15 s, rising 0.6 m over 0.8 s, pulled 0.6 m towards the camera, 1.4x on a weakness hit. "88" is the
        // widest two-digit number; the samples cover the pop peak, the settled pop and the rise.
        const float NumberFontSize = 4f;
        const float NumberRise = 0.6f;
        const float NumberLife = 0.8f;
        const float NumberTowardsCamera = 0.6f;
        const float NumberPopPeak = 1.3f;
        const float NumberPopPeakSeconds = 0.1f;
        const float NumberPopSeconds = 0.15f;
        const float NumberWeaknessScale = 1.4f;
        const string NumberText = "88";
        static readonly Vector2 NumberArea = new Vector2(4f, 1f);
        static readonly float[] NumberTimes = { 0.1f, 0.15f, 0.2f, 0.3f, 0.4f, 0.5f, 0.6f, 0.7f, 0.8f };
        static readonly Color NumberColour = Color.white;
        static readonly Color NumberWeaknessColour = new Color(1f, 0.88f, 0.2f, 1f);
        static readonly string[] ActionStates = { "Attack", "Skill" };

        /// Front captures of troll_grunt as generated, in each colour (ruling "Troll" condition b: no skull), then the
        /// group shot and every action shot of the default party and of the party with every body swapped, on each ratio,
        /// through BattleCameras itself, with §14.4's and §15.18.8's checks logged per shot and summarised at the end.
        public static void RunTroll()
        {
            Directory.CreateDirectory(OutFolder);
            var tables = TableLoader.Load(new FileSource(CombatantGenerator.DataFolder));
            var toon = Shader.Find(CombatantGenerator.ShaderName) ?? throw new InvalidOperationException(Tag + CombatantGenerator.ShaderName + " not found");
            var unlit = Shader.Find("Universal Render Pipeline/Unlit") ?? throw new InvalidOperationException(Tag + "URP Unlit not found");
            var camera = BuildScene();
            var colour = new Material(toon) { name = "troll_capture" };
            colour.SetColor("_BaseColor", Color.white);
            var mask = new Material(unlit) { name = "capture_mask" };
            mask.SetColor("_BaseColor", Color.white);
            var summary = new List<string>();
            try
            {
                LogImpacts(tables, summary);
                CaptureTrollFronts(camera, tables, colour);
                foreach (bool swapped in new[] { false, true })
                    CaptureBattle(camera, tables, swapped, mask, summary);
            }
            finally
            {
                Object.DestroyImmediate(colour);
                Object.DestroyImmediate(mask);
                Object.DestroyImmediate(camera.targetTexture);
                // The number's text upgrades and fills TMP's stock font asset in memory, and batch mode saves dirty assets on
                // quit; a capture must leave Assets/ as it found it.
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(TMP_Settings.defaultFontAsset)))
                    EditorUtility.ClearDirty(asset);
            }
            Debug.Log(Tag + "summary:\n  " + string.Join("\n  ", summary));
            Debug.Log(Tag + "done");
        }

        /// Every Attack and Skill clip in the tables carries its OnImpact event after import (§14.4 step 3), the troll's
        /// Skill included although the grunt never uses it.
        static void LogImpacts(GameTables tables, List<string> summary)
        {
            var names = tables.Classes.SelectMany(c => Bodies.All.SelectMany(b => ActionStates.Select(s => HeroGenerator.ResolveClip(c.Clips[s], b))))
                .Concat(tables.Enemies.SelectMany(e => ActionStates.Select(s => e.Clips[s]))).Distinct().OrderBy(n => n, StringComparer.Ordinal);
            int missing = 0;
            foreach (var name in names)
            {
                var clip = FindClip(name);
                var impacts = AnimationUtility.GetAnimationEvents(clip).Where(e => e.functionName == ClipTable.ImpactFunction).ToList();
                if (impacts.Count != 1)
                    missing++;
                Debug.Log(Tag + string.Format(CultureInfo.InvariantCulture, "impact {0}: {1:F3} s, OnImpact {2}", name, clip.length,
                    impacts.Count == 1 ? $"at {impacts[0].time:F3} s ({impacts[0].time / clip.length:F3})" : $"x{impacts.Count}"));
            }
            summary.Add($"OnImpact: {names.Count() - missing} of {names.Count()} Attack/Skill clips carry exactly one");
        }

        static AnimationClip FindClip(string name) =>
            AssetDatabase.FindAssets(name + " t:Model", new[] { SyntyPostprocessor.AnimationsFolder.TrimEnd('/') })
                .Select(AssetDatabase.GUIDToAssetPath).SelectMany(AssetDatabase.LoadAllAssetsAtPath).OfType<AnimationClip>()
                .FirstOrDefault(c => c.name == name) ?? throw new InvalidOperationException(Tag + name + " not imported");

        // ----- fronts -----

        /// troll_grunt's generated prefab, posed in the first frame of its idle, with each colour on every renderer: the
        /// whole troll, its head and its left hip, where the belt skull hung.
        static void CaptureTrollFronts(Camera camera, GameTables tables, Material material)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CombatantGenerator.PrefabsFolder + "/" + Troll + ".prefab")
                         ?? throw new InvalidOperationException(Tag + Troll + ".prefab is missing; run Game.Editor.CombatantGenerator.Generate");
            var troll = Object.Instantiate(prefab);
            try
            {
                troll.transform.SetPositionAndRotation(Vector3.zero, FacingCamera);
                var animator = troll.GetComponentInChildren<Animator>();
                StateClip(animator, "Idle").SampleAnimation(animator.gameObject, 0f);
                var renderers = troll.GetComponentsInChildren<Renderer>();
                foreach (var renderer in renderers)
                    CombatantGenerator.ApplyMaterial(renderer, material);
                var meshes = renderers.Select(r => r is SkinnedMeshRenderer s ? s.sharedMesh : r.GetComponent<MeshFilter>().sharedMesh).ToList();
                Debug.Log(Tag + string.Format(CultureInfo.InvariantCulture, "troll front: {0} renderers ({1}), meshes {2}, {3:F3} m posed",
                    renderers.Length, string.Join(", ", renderers.Select(r => r.name)),
                    string.Join(", ", meshes.Select(m => m.name + " " + Enumerable.Range(0, m.subMeshCount).Sum(i => (long)m.GetIndexCount(i) / 3) + " triangles")),
                    WorldBounds(troll, false).size.y));

                var rows = new[] { new List<Color32[]>(), new List<Color32[]>(), new List<Color32[]>() };
                foreach (var colour in TrollColours)
                {
                    material.SetTexture("_BaseMap", LoadTexture(SyntyPostprocessor.TexturesFolder + colour + ".png"));
                    var panels = CaptureTrollFront(camera, troll, animator);
                    for (int i = 0; i < panels.Length; i++)
                        rows[i].Add(panels[i]);
                    string path = OutFolder + Prefix + "troll-front-" + colour + ".tga";
                    WriteTga(path, Row(panels, Size, Size), Size * panels.Length, Size);
                    Debug.Log(Tag + "wrote " + path);
                }
                WriteSheet(OutFolder + Prefix + "troll-colours-sheet.tga", rows);
            }
            finally
            {
                Object.DestroyImmediate(troll);
            }
        }

        static Color32[][] CaptureTrollFront(Camera camera, GameObject troll, Animator animator)
        {
            camera.fieldOfView = Fov;
            var bounds = WorldBounds(troll);
            float tan = Mathf.Tan(Fov * 0.5f * Mathf.Deg2Rad);
            float fit = Mathf.Max(bounds.extents.y, bounds.extents.x) * FrontMargin / tan + bounds.extents.z;
            camera.transform.SetPositionAndRotation(new Vector3(bounds.center.x, bounds.center.y, bounds.center.z - fit), Quaternion.identity);
            var body = Render(camera, Size, Size);

            float s = troll.transform.lossyScale.y;
            var head = animator.GetBoneTransform(HumanBodyBones.Head).position;
            camera.transform.SetPositionAndRotation(head + new Vector3(0f, 0f, -HeadDistance * s), Quaternion.identity);
            var face = Render(camera, Size, Size);

            var hip = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg).position;
            camera.transform.SetPositionAndRotation(hip + new Vector3(0f, 0f, -HipDistance * s), Quaternion.identity);
            var belt = Render(camera, Size, Size);
            return new[] { body, face, belt };
        }

        // ----- battle -----

        sealed class Combatant
        {
            public CombatantActor Actor = null!;
            public string Name = "";
            public string AttackType = "";
            public string SkillType = "";
            public string? Weakness;
            public bool Hero;
        }

        sealed class Masks
        {
            public Material White = null!;
            public Renderer[] Scene = null!;
        }

        /// BattleView's layout with the generated prefabs, spawned through CombatantActor so the camera rig is the game's
        /// own. Cinemachine snaps without damping when deltaTime is negative, which is the settled shot the game reaches.
        static void CaptureBattle(Camera camera, GameTables tables, bool swapped, Material white, List<string> summary)
        {
            string party = swapped ? "B" : "A";
            var stage = new GameObject("BattleView").transform;
            var number = BuildNumber(stage);
            try
            {
                var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
                Object.DestroyImmediate(ground.GetComponent<Collider>());
                ground.transform.SetParent(stage, false);
                ground.transform.localScale = Vector3.one * GroundScale;
                ground.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(CombatantGenerator.MaterialsFolder + "/ground.mat");

                var record = DebugParty.CreateParty();
                var combatants = new List<Combatant>();
                for (int i = 0; i < record.Heroes.Count; i++)
                {
                    var hero = record.Heroes[i];
                    string body = swapped ? (hero.Body == Bodies.Boy ? Bodies.Girl : Bodies.Boy) : hero.Body;
                    var cls = tables.Classes.Single(c => c.Id == hero.ClassId);
                    var actor = CombatantActor.Spawn(i, HeroGenerator.VariantId(cls.Id, body),
                        BattleStage.HeroSlot(i, record.Heroes.Count), BattleStage.HeroFacing, stage);
                    combatants.Add(new Combatant { Actor = actor, Name = actor.Root.name, AttackType = cls.DamageType, SkillType = cls.Skill!.DamageType, Hero = true });
                }
                var enemy = tables.Enemies.Single(e => e.Id == Troll);
                var trollActor = CombatantActor.Spawn(combatants.Count, Troll, BattleStage.EnemySlot(0, 1), BattleStage.EnemyFacing, stage);
                var troll = new Combatant { Actor = trollActor, Name = Troll, AttackType = enemy.DamageType, SkillType = enemy.DamageType, Weakness = enemy.Weakness };
                combatants.Add(troll);
                var actors = combatants.Select(c => c.Actor).ToList();
                PoseIdle(combatants, 0f);

                var cameras = new BattleCameras(camera, actors, stage);
                var brain = camera.GetComponent<CinemachineBrain>();
                brain.UpdateMethod = CinemachineBrain.UpdateMethods.ManualUpdate;
                var members = cameras.TargetGroup.Targets;
                for (int i = 0; i < members.Count; i++)
                    Debug.Log(Tag + string.Format(CultureInfo.InvariantCulture, "anchor {0} {1}: body {2:F3} m at spawn, anchor y {3:F3} m, radius {4:F3} m",
                        party, combatants[i].Name, actors[i].BodyHeight, members[i].Object.position.y, members[i].Radius));

                var masks = new Masks { White = white, Scene = stage.GetComponentsInChildren<Renderer>(true) };
                int frame = 1;
                var previous = camera.targetTexture;
                try
                {
                    foreach (var ratio in Ratios)
                    {
                        var target = new RenderTexture(ratio.Width, ratio.Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
                        camera.targetTexture = target;
                        camera.aspect = (float)ratio.Width / ratio.Height;
                        var hud = HudRects(ratio.Width, ratio.Height, ratio.Safe, record.Heroes.Count);

                        cameras.SetActionActive(false);
                        brain.enabled = true;
                        number.SetActive(false);
                        PoseIdle(combatants, 0f);
                        for (int i = 0; i < 3; i++)
                        {
                            cameras.TargetGroup.DoUpdate();
                            brain.ManualUpdate(frame++, -1f);
                        }
                        Debug.Log(Tag + string.Format(CultureInfo.InvariantCulture, "group {0} {1} {2}×{3}: camera at {4}, euler {5}, fov {6:F1}; cards x {7:F1}–{8:F1} % y {9:F1}–{10:F1} %",
                            party, ratio.Name, ratio.Width, ratio.Height, camera.transform.position.ToString("F2"), camera.transform.eulerAngles.ToString("F1"),
                            camera.fieldOfView, hud[0].xMin * 100f, hud[0].xMax * 100f, hud[0].yMin * 100f, hud[0].yMax * 100f));
                        string groupPath = OutFolder + Prefix + "group-" + ratio.Name + "-" + party + ".tga";
                        WriteTga(groupPath, Render(camera, ratio.Width, ratio.Height), ratio.Width, ratio.Height);
                        Debug.Log(Tag + "wrote " + groupPath);
                        GroupChecks(camera, combatants, hud, party + " " + ratio.Name, summary);

                        // Cinemachine re-sorts its cameras by priority once per frame, and Time.frameCount does not
                        // advance in batch edit mode, so the brain would keep the group shot. CM_Action has no
                        // procedural components and Group -> Action is a cut, so the camera takes its pose and lens directly.
                        cameras.SetActionActive(true);
                        brain.enabled = false;
                        number.SetActive(true);
                        var shots = new List<Color32[]>();
                        foreach (var state in ActionStates)
                            foreach (var hero in combatants.Where(c => c.Hero))
                                shots.Add(ActionShot(camera, cameras, combatants, hero, troll, state, number, masks, hud, ratio, party, summary));
                        foreach (var hero in combatants.Where(c => c.Hero))
                            shots.Add(ActionShot(camera, cameras, combatants, troll, hero, "Attack", number, masks, hud, ratio, party, summary));
                        int w = ratio.Width / SheetDivisor, h = ratio.Height / SheetDivisor;
                        string sheet = OutFolder + Prefix + "actions-" + ratio.Name + "-" + party + "-sheet.tga";
                        WriteTga(sheet, Grid(shots.Select(p => Downscale(p, ratio.Width, ratio.Height, SheetDivisor)).ToList(), w, h, record.Heroes.Count), w * record.Heroes.Count, h * 3);
                        Debug.Log(Tag + "wrote " + sheet + " (rows: hero Attacks, hero Skills, the troll's Attack on each hero; columns in party order)");

                        camera.targetTexture = previous;
                        Object.DestroyImmediate(target);
                    }
                }
                finally
                {
                    camera.targetTexture = previous;
                }
            }
            finally
            {
                // BattleCameras.Destroy uses Object.Destroy, which edit mode refuses; its rig lives under the stage.
                Object.DestroyImmediate(stage.gameObject);
            }
        }

        static AnimationClip StateClip(Animator animator, string state) =>
            ((AnimatorOverrideController)animator.runtimeAnimatorController)[state];

        static void PoseIdle(List<Combatant> combatants, float phase)
        {
            foreach (var c in combatants)
            {
                var idle = StateClip(c.Actor.Animator, "Idle");
                idle.SampleAnimation(c.Actor.Animator.gameObject, phase * idle.length);
            }
        }

        /// §14.4's group checks over the idle cycle: the lowest foot, the highest head (skinned meshes only; the Mage's hat
        /// is a body part), and no silhouette point, weapons included, inside a card or the banner.
        static void GroupChecks(Camera camera, List<Combatant> combatants, Rect[] hud, string shot, List<string> summary)
        {
            float worstFeet = 1f, worstHead = 0f;
            int underCard = 0;
            foreach (var c in combatants)
            {
                float feet0 = 0f, head0 = 0f, feet = 1f, head = 0f, minX = 1f, maxX = 0f;
                int inCards = 0, inEnemy = 0, inBanner = 0;
                for (int p = 0; p < IdlePhases; p++)
                {
                    PoseIdle(combatants, (float)p / IdlePhases);
                    float pFeet = 1f, pHead = 0f;
                    foreach (var v in WorldPoints(c.Actor.Root.gameObject, false).Select(w => camera.WorldToViewportPoint(w)))
                    {
                        pFeet = Mathf.Min(pFeet, v.y);
                        pHead = Mathf.Max(pHead, v.y);
                    }
                    foreach (var v in WorldPoints(c.Actor.Root.gameObject, true).Select(w => camera.WorldToViewportPoint(w)))
                    {
                        minX = Mathf.Min(minX, v.x);
                        maxX = Mathf.Max(maxX, v.x);
                        if (hud[0].Contains(v)) inCards++;
                        if (hud[1].Contains(v)) inEnemy++;
                        if (hud[2].Contains(v)) inBanner++;
                    }
                    if (p == 0)
                    {
                        feet0 = pFeet;
                        head0 = pHead;
                    }
                    feet = Mathf.Min(feet, pFeet);
                    head = Mathf.Max(head, pHead);
                }
                worstFeet = Mathf.Min(worstFeet, feet);
                worstHead = Mathf.Max(worstHead, head);
                underCard += inCards + inEnemy + inBanner;
                Debug.Log(Tag + string.Format(CultureInfo.InvariantCulture,
                    "framing {0}: {1} feet {2:F1} %, head {3:F1} % at idle frame 0; over the idle cycle feet {4:F1} %, head {5:F1} %, x {6:F1}–{7:F1} %; " +
                    "points under the cards {8}, the enemy card {9}, the banner {10}",
                    shot, c.Name, feet0 * 100f, head0 * 100f, feet * 100f, head * 100f, minX * 100f, maxX * 100f, inCards, inEnemy, inBanner));
            }
            PoseIdle(combatants, 0f);
            bool pass = worstFeet >= MinFeet && worstHead <= MaxHead && underCard == 0;
            summary.Add(string.Format(CultureInfo.InvariantCulture, "group {0}: lowest foot {1:F1} % (>= 27), highest head {2:F1} % (<= 86), points under a card {3}: {4}",
                shot, worstFeet * 100f, worstHead * 100f, underCard, pass ? "PASS" : "FAIL"));
        }

        /// One action shot at the attacker's impact frame, posed by BattleCameras.PoseOverShoulder, with §15.18.8's three
        /// checks: the target's hips-to-head height, the damage number inside the frame, clear of the HUD and of the
        /// attacker and its weapon, and no neighbour's head over the target. Returns the rendered pixels.
        static Color32[] ActionShot(Camera camera, BattleCameras cameras, List<Combatant> combatants,
            Combatant attacker, Combatant target, string state, GameObject number, Masks masks, Rect[] hud,
            (string Name, int Width, int Height, Rect Safe) ratio, string party, List<string> summary)
        {
            int width = ratio.Width, height = ratio.Height;
            PoseIdle(combatants, 0f);
            cameras.PoseOverShoulder(attacker.Actor, target.Actor);
            camera.transform.SetPositionAndRotation(cameras.Action.transform.position, cameras.Action.transform.rotation);
            camera.fieldOfView = cameras.Action.Lens.FieldOfView;

            var clip = StateClip(attacker.Actor.Animator, state);
            var impact = AnimationUtility.GetAnimationEvents(clip).FirstOrDefault(e => e.functionName == ClipTable.ImpactFunction);
            float impactTime = impact?.time ?? 0f;
            clip.SampleAnimation(attacker.Actor.Animator.gameObject, impactTime);

            // The target's hips-to-head: its hips bone to the top of its body.
            float hips = camera.WorldToViewportPoint(target.Actor.Animator.GetBoneTransform(HumanBodyBones.Hips).position).y;
            float top = WorldPoints(target.Actor.Root.gameObject, false).Select(p => camera.WorldToViewportPoint(p).y).Max();
            float targetHeight = top - hips;

            // The number over its life; the render shows it at the settled pop.
            string damageType = state == "Skill" ? attacker.SkillType : attacker.AttackType;
            bool weakness = target.Weakness != null && string.Equals(damageType, target.Weakness, StringComparison.OrdinalIgnoreCase);
            var text = number.GetComponent<TMP_Text>();
            text.color = weakness ? NumberWeaknessColour : NumberColour;
            var anchor = target.Actor.Chest.position;
            // DamageNumberPool's ceiling: the top band's bottom (the lower of the enemy card and the banner) less 12 reference px.
            float bandBottom = Mathf.Min(hud[1].yMin, hud[2].yMin);
            float ceiling = bandBottom * height - DamageNumberPool.BandGap * Mathf.Sqrt(width * height / (1920f * 1080f));
            var swept = new Rect();
            bool inside = true, clearOfHud = true;
            float rawTop = 0f, highestTop = 0f;
            foreach (float t in NumberTimes)
            {
                var (raw, clamped) = PlaceNumber(number, text, camera, anchor, t, weakness, ceiling);
                rawTop = Mathf.Max(rawTop, raw);
                highestTop = Mathf.Max(highestTop, clamped);
                var r = NumberRect(camera, text);
                swept = swept.width == 0f ? r : Rect.MinMaxRect(Mathf.Min(swept.xMin, r.xMin), Mathf.Min(swept.yMin, r.yMin), Mathf.Max(swept.xMax, r.xMax), Mathf.Max(swept.yMax, r.yMax));
                inside &= r.xMin >= 0f && r.yMin >= 0f && r.xMax <= 1f && r.yMax <= 1f;
                clearOfHud &= !hud.Any(h => h.Overlaps(r));
            }

            // The target, its weapon included, under no card, banner or enemy card; other combatants may be (§15.18.8 item 3).
            int targetUnderHud = WorldPoints(target.Actor.Root.gameObject, true).Select(w => camera.WorldToViewportPoint(w)).Count(v => hud.Any(h => h.Contains(v)));
            // Logged for the record, not a check.
            var attackerPoints = WorldPoints(attacker.Actor.Root.gameObject, true).Select(w => camera.WorldToViewportPoint(w)).ToList();
            float attackerInFrame = attackerPoints.Count(v => v.x >= 0f && v.x <= 1f && v.y >= 0f && v.y <= 1f) / (float)attackerPoints.Count;
            PlaceNumber(number, text, camera, anchor, NumberPopSeconds, weakness, ceiling);
            var pixels = Render(camera, width, height);

            var attackerMask = RenderMask(camera, masks, attacker.Actor.Renderers, width, height);
            var targetMask = RenderMask(camera, masks, target.Actor.Renderers, width, height);
            int crossing = Count(attackerMask, width, height, swept, 0f);
            int headsOver = 0, headsOverHipsUp = 0;
            var overBy = new List<string>();
            foreach (var neighbour in combatants.Where(c => c != attacker && c != target))
            {
                var neck = neighbour.Actor.Animator.GetBoneTransform(HumanBodyBones.Neck).position.y;
                var headPoints = WorldPoints(neighbour.Actor.Root.gameObject, false).Where(p => p.y >= neck).Select(w => camera.WorldToViewportPoint(w)).ToList();
                if (headPoints.Count == 0)
                    continue;
                var headRect = Rect.MinMaxRect(headPoints.Min(p => p.x), headPoints.Min(p => p.y), headPoints.Max(p => p.x), headPoints.Max(p => p.y));
                var neighbourMask = RenderMask(camera, masks, neighbour.Actor.Renderers.OfType<SkinnedMeshRenderer>(), width, height);
                int all = 0, hipsUp = 0;
                Overlap(neighbourMask, targetMask, width, height, headRect, hips, ref all, ref hipsUp);
                headsOver += all;
                headsOverHipsUp += hipsUp;
                if (all > 0)
                    overBy.Add($"{neighbour.Name} {all} px ({hipsUp} hips-up)");
            }

            float minHeight = target.Hero ? MinHeroTargetHeight : MinEnemyTargetHeight;
            bool sizeOk = targetHeight >= minHeight;
            bool numberOk = inside && clearOfHud && crossing == 0 && highestTop < bandBottom;
            bool targetClear = targetUnderHud == 0;
            bool headsOk = headsOver == 0;
            string line = string.Format(CultureInfo.InvariantCulture,
                "action {0} {1} {2} {3} -> {4}: {5} impact {6:F3}/{7:F3} s; target hips-to-head {8:F1} % (hips {9:F1} %, top {10:F1} %, minimum {11:F0} %) {12}; " +
                "number{13} x {14:F1}–{15:F1} % y {16:F1}–{17:F1} %, highest top {18:F1} % (unclamped {19:F1} %) under the band's bottom {20:F1} %, " +
                "inside {21}, clear of the HUD {22}, attacker px in it {23} {24}; target points under the HUD {25} {26}; " +
                "neighbour heads over the target {27} px{28} {29}; attacker {30:F0} % in frame",
                party, ratio.Name, attacker.Name, state, target.Name, clip.name, impactTime, clip.length,
                targetHeight * 100f, hips * 100f, top * 100f, minHeight * 100f, sizeOk ? "PASS" : "FAIL",
                weakness ? " (weakness)" : "", swept.xMin * 100f, swept.xMax * 100f, swept.yMin * 100f, swept.yMax * 100f,
                highestTop * 100f, rawTop * 100f, bandBottom * 100f, inside, clearOfHud, crossing, numberOk ? "PASS" : "FAIL",
                targetUnderHud, targetClear ? "PASS" : "FAIL",
                headsOver, overBy.Count > 0 ? " (" + string.Join(", ", overBy) + ")" : "", headsOk ? "PASS" : "FAIL", attackerInFrame * 100f);
            Debug.Log(Tag + line);
            if (impact == null || !sizeOk || !numberOk || !targetClear || !headsOk)
                summary.Add("FAIL " + line + (impact == null ? " NO OnImpact" : ""));
            else
                summary.Add(string.Format(CultureInfo.InvariantCulture,
                    "action {0} {1} {2} {3} -> {4}: PASS (target {5:F1} % >= {6:F0}, number top {7:F1} % < band {8:F1} %, clear of the HUD, target under no HUD element, heads clear)",
                    party, ratio.Name, attacker.Name, state, target.Name, targetHeight * 100f, minHeight * 100f, highestTop * 100f, bandBottom * 100f));

            if (party == "A" && ((attacker.Name.StartsWith("fighter", StringComparison.Ordinal) && state == "Attack") ||
                                 (!attacker.Hero && target.Name.StartsWith("swordsman", StringComparison.Ordinal))))
            {
                string path = OutFolder + Prefix + "action-" + ratio.Name + "-" + (attacker.Hero ? "fighter-attack" : "troll-attack") + ".tga";
                WriteTga(path, pixels, width, height);
                Debug.Log(Tag + "wrote " + path);
            }
            return pixels;
        }

        static GameObject BuildNumber(Transform parent)
        {
            var go = new GameObject("DamageNumber", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<TextMeshPro>();
            text.font = TMP_Settings.defaultFontAsset;
            text.fontSize = NumberFontSize;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.rectTransform.sizeDelta = NumberArea;
            text.text = NumberText;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            go.SetActive(false);
            return go;
        }

        /// DamageNumberPool.Animate at time t, its clamp included (the pool's own LowerBy). Returns the top's viewport height
        /// before and after the clamp; `ceiling` is in screen px from the bottom.
        static (float Raw, float Clamped) PlaceNumber(GameObject number, TMP_Text text, Camera camera, Vector3 anchor, float t, bool weakness,
            float ceiling)
        {
            float pop = t >= NumberPopSeconds ? 1f
                : t < NumberPopPeakSeconds ? NumberPopPeak * (t / NumberPopPeakSeconds)
                : Mathf.Lerp(NumberPopPeak, 1f, (t - NumberPopPeakSeconds) / (NumberPopSeconds - NumberPopPeakSeconds));
            float scale = pop * (weakness ? NumberWeaknessScale : 1f);
            number.transform.localScale = Vector3.one * scale;
            var position = anchor + Vector3.up * (NumberRise * (t / NumberLife));
            position += (camera.transform.position - position).normalized * NumberTowardsCamera;
            text.ForceMeshUpdate(true);
            var up = camera.transform.up;
            var top = position + up * (text.textBounds.max.y * scale);
            float lower = DamageNumberPool.LowerBy(camera, top, ceiling);
            number.transform.SetPositionAndRotation(position - up * lower, camera.transform.rotation);
            return (camera.WorldToViewportPoint(top).y, camera.WorldToViewportPoint(top - up * lower).y);
        }

        static Rect NumberRect(Camera camera, TMP_Text text)
        {
            var b = text.textBounds;
            var t = text.transform;
            var corners = new[] { new Vector3(b.min.x, b.min.y, 0f), new Vector3(b.max.x, b.min.y, 0f), new Vector3(b.min.x, b.max.y, 0f), new Vector3(b.max.x, b.max.y, 0f) }
                .Select(c => camera.WorldToViewportPoint(t.TransformPoint(c))).ToList();
            return Rect.MinMaxRect(corners.Min(c => c.x), corners.Min(c => c.y), corners.Max(c => c.x), corners.Max(c => c.y));
        }

        /// The party cards, the enemy card and the turn banner in viewport units (x from the left, y from the bottom).
        static Rect[] HudRects(int width, int height, Rect safe, int heroes)
        {
            float s = Mathf.Sqrt(width * height / (1920f * 1080f));
            float column = heroes * CardHeight + (heroes - 1) * CardGap;
            var cards = Rect.MinMaxRect(safe.xMax - (HudMargin + CardWidth) * s, safe.yMin + HudMargin * s, safe.xMax - HudMargin * s, safe.yMin + (HudMargin + column) * s);
            var enemy = Rect.MinMaxRect(safe.center.x - EnemyCardWidth * 0.5f * s, safe.yMax - (HudMargin + EnemyCardHeight) * s,
                safe.center.x + EnemyCardWidth * 0.5f * s, safe.yMax - HudMargin * s);
            var banner = Rect.MinMaxRect(safe.xMin + HudMargin * s, safe.yMax - (HudMargin + BannerHeight) * s, safe.xMin + (HudMargin + BannerWidth) * s, safe.yMax - HudMargin * s);
            Rect Viewport(Rect r) => Rect.MinMaxRect(r.xMin / width, r.yMin / height, r.xMax / width, r.yMax / height);
            return new[] { Viewport(cards), Viewport(enemy), Viewport(banner) };
        }

        /// Which pixels the given renderers cover, drawn alone in flat white on black from the current camera.
        static bool[] RenderMask(Camera camera, Masks masks, IEnumerable<Renderer> show, int width, int height)
        {
            var shown = new HashSet<Renderer>(show);
            var enabled = masks.Scene.Select(r => r != null && r.enabled).ToArray();
            var materials = shown.ToDictionary(r => r, r => r.sharedMaterials);
            var clear = camera.backgroundColor;
            try
            {
                foreach (var r in masks.Scene)
                    if (r != null)
                        r.enabled = shown.Contains(r);
                foreach (var r in shown)
                    CombatantGenerator.ApplyMaterial(r, masks.White);
                camera.backgroundColor = Color.black;
                camera.Render();
                var previous = RenderTexture.active;
                RenderTexture.active = camera.targetTexture;
                var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                RenderTexture.active = previous;
                var pixels = texture.GetPixels32();
                Object.DestroyImmediate(texture);
                return pixels.Select(p => p.r > 127).ToArray();
            }
            finally
            {
                camera.backgroundColor = clear;
                for (int i = 0; i < masks.Scene.Length; i++)
                    if (masks.Scene[i] != null)
                        masks.Scene[i].enabled = enabled[i];
                foreach (var pair in materials)
                    pair.Key.sharedMaterials = pair.Value;
            }
        }

        static int Count(bool[] mask, int width, int height, Rect area, float minY)
        {
            int x0 = Mathf.Clamp(Mathf.FloorToInt(area.xMin * width), 0, width), x1 = Mathf.Clamp(Mathf.CeilToInt(area.xMax * width), 0, width);
            int y0 = Mathf.Clamp(Mathf.FloorToInt(Mathf.Max(area.yMin, minY) * height), 0, height), y1 = Mathf.Clamp(Mathf.CeilToInt(area.yMax * height), 0, height);
            int n = 0;
            for (int y = y0; y < y1; y++)
                for (int x = x0; x < x1; x++)
                    if (mask[y * width + x])
                        n++;
            return n;
        }

        /// Pixels of the neighbour's head (its mask inside its head rectangle) that also cover the target, in all and
        /// above the target's hips.
        static void Overlap(bool[] neighbour, bool[] target, int width, int height, Rect head, float hips, ref int all, ref int hipsUp)
        {
            int x0 = Mathf.Clamp(Mathf.FloorToInt(head.xMin * width), 0, width), x1 = Mathf.Clamp(Mathf.CeilToInt(head.xMax * width), 0, width);
            int y0 = Mathf.Clamp(Mathf.FloorToInt(head.yMin * height), 0, height), y1 = Mathf.Clamp(Mathf.CeilToInt(head.yMax * height), 0, height);
            int hipsRow = Mathf.FloorToInt(hips * height);
            for (int y = y0; y < y1; y++)
                for (int x = x0; x < x1; x++)
                {
                    int i = y * width + x;
                    if (!neighbour[i] || !target[i])
                        continue;
                    all++;
                    if (y >= hipsRow)
                        hipsUp++;
                }
        }

        static Color32[] Downscale(Color32[] pixels, int width, int height, int divisor)
        {
            int w = width / divisor, h = height / divisor;
            var result = new Color32[w * h];
            int n = divisor * divisor;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int r = 0, g = 0, b = 0;
                    for (int dy = 0; dy < divisor; dy++)
                        for (int dx = 0; dx < divisor; dx++)
                        {
                            var p = pixels[(y * divisor + dy) * width + x * divisor + dx];
                            r += p.r;
                            g += p.g;
                            b += p.b;
                        }
                    result[y * w + x] = new Color32((byte)(r / n), (byte)(g / n), (byte)(b / n), 255);
                }
            return result;
        }

        /// Panels of w×h in rows of `columns`, the first row on top.
        static Color32[] Grid(List<Color32[]> panels, int w, int h, int columns)
        {
            int rows = (panels.Count + columns - 1) / columns;
            int width = w * columns, height = h * rows;
            var pixels = new Color32[width * height];
            for (int i = 0; i < panels.Count; i++)
            {
                int top = (rows - 1 - i / columns) * h, left = (i % columns) * w;
                for (int y = 0; y < h; y++)
                    Array.Copy(panels[i], y * w, pixels, (top + y) * width + left, w);
            }
            return pixels;
        }

        static Color32[] Row(Color32[][] panels, int width, int height)
        {
            var pixels = new Color32[width * panels.Length * height];
            for (int y = 0; y < height; y++)
                for (int i = 0; i < panels.Length; i++)
                    Array.Copy(panels[i], y * width, pixels, y * width * panels.Length + i * width, width);
            return pixels;
        }

        static Bounds WorldBounds(GameObject root, bool weapons = true)
        {
            var points = WorldPoints(root, weapons).ToList();
            var bounds = new Bounds(points[0], Vector3.zero);
            foreach (var p in points)
                bounds.Encapsulate(p);
            return bounds;
        }

        /// Every vertex of the active renderers in world space, skinned meshes baked in their current pose. BakeMesh with
        /// useScale false keeps the hierarchy's scale in the vertices (measured: 2.300 m for the scaled troll at its bind
        /// pose, against 1.961 m with true), so only position and rotation are applied here.
        static IEnumerable<Vector3> WorldPoints(GameObject root, bool weapons)
        {
            var baked = new Mesh();
            try
            {
                foreach (var renderer in root.GetComponentsInChildren<Renderer>())
                {
                    if (renderer is SkinnedMeshRenderer skinned)
                    {
                        skinned.BakeMesh(baked, false);
                        var t = skinned.transform;
                        var m = Matrix4x4.TRS(t.position, t.rotation, Vector3.one);
                        foreach (var v in baked.vertices)
                            yield return m.MultiplyPoint3x4(v);
                    }
                    else if (weapons && renderer.TryGetComponent<MeshFilter>(out var filter) && filter.sharedMesh != null)
                    {
                        var m = renderer.transform.localToWorldMatrix;
                        foreach (var v in filter.sharedMesh.vertices)
                            yield return m.MultiplyPoint3x4(v);
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(baked);
            }
        }

        /// Rows of Size×Size panels, the first row on top.
        static void WriteSheet(string path, IReadOnlyList<List<Color32[]>> rows)
        {
            int columns = rows.Max(r => r.Count);
            int width = columns * Size, height = rows.Count * Size;
            var pixels = new Color32[width * height];
            for (int r = 0; r < rows.Count; r++)
            {
                int top = (rows.Count - 1 - r) * Size;
                for (int c = 0; c < rows[r].Count; c++)
                    for (int y = 0; y < Size; y++)
                        Array.Copy(rows[r][c], y * Size, pixels, (top + y) * width + c * Size, Size);
            }
            WriteTga(path, pixels, width, height);
            Debug.Log(Tag + "wrote " + path);
        }

        static Texture2D LoadTexture(string path) =>
            AssetDatabase.LoadAssetAtPath<Texture2D>(path) ?? throw new InvalidOperationException(Tag + "texture not imported: " + path);

        static Color32[] Render(Camera camera, int width, int height)
        {
            camera.aspect = (float)width / height;
            // The first frame after a hero spawns comes out black (skinning and the shadow map are not ready), so each
            // view renders twice and keeps the second.
            camera.Render();
            camera.Render();
            var previous = RenderTexture.active;
            RenderTexture.active = camera.targetTexture;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            texture.Apply(false);
            RenderTexture.active = previous;
            var pixels = texture.GetPixels32();
            Object.DestroyImmediate(texture);
            return pixels;
        }

        /// Uncompressed true-colour TGA, bottom-left origin, BGRA.
        static void WriteTga(string path, Color32[] pixels, int width, int height)
        {
            using var writer = new BinaryWriter(File.Create(path));
            writer.Write(new byte[] { 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0 });
            writer.Write((short)width);
            writer.Write((short)height);
            writer.Write((byte)32);
            writer.Write((byte)8);
            foreach (var p in pixels)
            {
                writer.Write(p.b);
                writer.Write(p.g);
                writer.Write(p.r);
                writer.Write(p.a);
            }
        }

        sealed class FileSource : ITableSource
        {
            readonly string folder;
            public FileSource(string folder) => this.folder = folder;
            public string ReadText(string tableName) => File.ReadAllText(Path.Combine(folder, tableName + ".json"));
        }
    }
}
