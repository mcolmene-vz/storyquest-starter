using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Game.Core.Data;
using Game.Editor.Import;
using Game.Runtime;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Game.Editor
{
    /// Front captures of the eight hero variants for the producer's skin-tone check (ruling 7, ARCHITECTURE §15.3), and
    /// one hand close-up per weapon mesh for tuning classes.json weaponMeshes (§15.18.3). RunTroll captures the Fantasy
    /// Rivals troll for the producer's troll decision (PLAN-M2 D6b step 2). Renders, so it runs in batch mode without
    /// -nographics:
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

        // ----- D6b step 2: the Fantasy Rivals troll (ARCHITECTURE §15.16.2, §15.16.4) -----

        const string TrollBody = "Character_Troll_01";
        const string TrollClub = "SM_Wep_Troll_01";
        const string TrollIdle = "A_Idle_Base_Sword";
        const string CurrentTroll = "troll_grunt";
        static readonly string[] TrollColours = { "FantasyRivals_01_A", "FantasyRivals_01_B", "FantasyRivals_01_C", "FantasyRivals_01_D" };
        // §15.16.2 asks for 2.2–2.4 m after scale; the middle of the band until step 3 picks the height by eye.
        const float TrollHeight = 2.3f;
        // The club in the right fist. The Big Rig hand bone's axes differ from the hero's, so the hero grip does not carry
        // over: these are the Swordsman's sword pose in the first frame of A_Idle_Base_Sword, in body space, moved onto the
        // troll's hand, with the palm offset scaled by hand length (0.158 m against 0.105 m), measured once on 2026-10-17.
        // The club's head lies along its -Y where the sword's blade lies along +Y, so z gains 180° to point it forward.
        // Checked on d6b-troll-grip. Step 3 moves the grip into classes.json weaponMeshes.
        static readonly Vector3 ClubGripPos = new Vector3(0.0935f, 0.0753f, 0.0841f);
        static readonly Vector3 ClubGripEuler = new Vector3(335.9f, 53.2f, 248.8f);
        // The two ratios CLAUDE.md names, iPhone 19.5:9 (the iPhone 15 Pro simulator's 2556×1179) and iPad 4:3, plus the
        // 2360×1640 of the iPad simulator and the A16 test iPad: the group shot's lateral offset depends on the aspect.
        static readonly (string Name, int Width, int Height)[] Ratios = { ("iphone", 2556, 1179), ("ipad", 2048, 1536), ("ipad2360", 2360, 1640) };
        // BattleStage's ground plane; the stage itself is not built here because its constructor calls Object.Destroy.
        const float GroundScale = 10f;
        const float FrontMargin = 1.08f;
        const float HeadDistance = 1.5f;

        /// Front, grip and battle-framing captures of the troll in each colour, plus the current KayKit troll in the same
        /// group shot for comparison. The troll is assembled in memory from Characters_BR.fbx the way §15.16.2 describes;
        /// no prefab, material or data file is written, and the group shot is BattleCameras itself.
        public static void RunTroll()
        {
            Directory.CreateDirectory(OutFolder);
            var tables = TableLoader.Load(new FileSource(CombatantGenerator.DataFolder));
            var toon = Shader.Find(CombatantGenerator.ShaderName);
            if (toon == null)
                throw new InvalidOperationException(Tag + CombatantGenerator.ShaderName + " not found");
            var camera = BuildScene();
            var material = new Material(toon) { name = "troll_capture" };
            material.SetColor("_BaseColor", Color.white);
            try
            {
                var troll = BuildTroll(material, out var animator);
                var bodies = new List<Color32[]>();
                var heads = new List<Color32[]>();
                foreach (var colour in TrollColours)
                {
                    material.SetTexture("_BaseMap", LoadTexture(SyntyPostprocessor.TexturesFolder + colour + ".png"));
                    var (body, head) = CaptureTrollFront(camera, troll, animator, colour);
                    bodies.Add(body);
                    heads.Add(head);
                }
                WriteSheet(OutFolder + "d6b-troll-colours-sheet.tga", new[] { bodies, heads });

                material.SetTexture("_BaseMap", LoadTexture(SyntyPostprocessor.TexturesFolder + TrollColours[0] + ".png"));
                CaptureTrollGrip(camera, troll);
                CaptureTrollBattle(camera, tables, troll, material);
            }
            finally
            {
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(camera.targetTexture);
            }
            Debug.Log(Tag + "done");
        }

        /// Characters_BR.fbx with every body but the troll's removed, the club on a right-hand socket at world scale 1
        /// (HeroGenerator's socket rule), scaled to TrollHeight and posed in the first frame of the Sword Combat idle.
        static GameObject BuildTroll(Material material, out Animator animator)
        {
            var source = CombatantGenerator.LoadModel(SyntyPostprocessor.BigRigModel);
            var root = new GameObject("troll_capture");
            var model = Object.Instantiate(source);
            model.name = source.name;
            model.transform.SetParent(root.transform, false);
            root.transform.SetPositionAndRotation(Vector3.zero, FacingCamera);

            animator = model.GetComponent<Animator>();
            if (animator == null || animator.avatar == null)
                throw new InvalidOperationException(Tag + SyntyPostprocessor.BigRigModel + " has no Animator with an avatar");
            var avatar = animator.avatar;

            SkinnedMeshRenderer? body = null;
            foreach (var smr in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (smr.name == TrollBody)
                    body = smr;
                else
                    Object.DestroyImmediate(smr.gameObject);
            }
            if (body == null)
                throw new InvalidOperationException(Tag + TrollBody + " is not a renderer in " + SyntyPostprocessor.BigRigModel);
            CombatantGenerator.ApplyMaterial(body, material);

            var hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            if (hand == null)
                throw new InvalidOperationException(Tag + SyntyPostprocessor.BigRigModel + "'s avatar maps no RightHand");
            var socket = new GameObject(SocketNames.HandR).transform;
            socket.SetParent(hand, false);
            var hs = hand.lossyScale;
            socket.localScale = new Vector3(1f / hs.x, 1f / hs.y, 1f / hs.z);
            AddClub(socket, material);

            // The generator's measure (renderer bounds at the bind pose, body only), so step 3's height check agrees.
            float native = body.bounds.size.y;
            float scale = TrollHeight / native;
            root.transform.localScale = Vector3.one * scale;
            float bindHeight = body.bounds.size.y;

            var idle = AssetDatabase.LoadAllAssetsAtPath(SyntyPostprocessor.AnimationsFolder + TrollIdle + ".fbx")
                .OfType<AnimationClip>().FirstOrDefault(c => c.name == TrollIdle)
                ?? throw new InvalidOperationException(Tag + TrollIdle + " not imported");
            idle.SampleAnimation(model, 0f);
            var posed = WorldBounds(root);

            var renderers = root.GetComponentsInChildren<Renderer>();
            var meshes = renderers.Select(r => r is SkinnedMeshRenderer s ? s.sharedMesh : r.GetComponent<MeshFilter>().sharedMesh).Distinct().ToList();
            long triangles = meshes.Sum(m => Enumerable.Range(0, m.subMeshCount).Sum(i => (long)m.GetIndexCount(i) / 3));
            Debug.Log(Tag + string.Format(CultureInfo.InvariantCulture,
                "troll: {0} from {1}; avatar isHuman {2} isValid {3}, {4} bones mapped; native height {5:F3} m, scale {6:F4}, " +
                "height after scale {7:F3} m (bind pose, body bounds), {8:F3} m in {9} frame 0 (mesh, club excluded: {10:F3} m); " +
                "{11} renderers ({12}), {13} unique triangles ({14})",
                TrollBody, SyntyPostprocessor.BigRigModel, avatar.isHuman, avatar.isValid, avatar.humanDescription.human.Length,
                native, scale, bindHeight, posed.size.y, TrollIdle, WorldBounds(body.gameObject, false).size.y,
                renderers.Length, string.Join(", ", renderers.Select(r => r.name)),
                triangles, string.Join(", ", meshes.Select(m => m.name + " " + Enumerable.Range(0, m.subMeshCount).Sum(i => (long)m.GetIndexCount(i) / 3)))));
            return root;
        }

        /// HeroGenerator.AddWeapon's rebuild: the FBX's mesh renderers posed relative to its root, so a root offset stays behind.
        static void AddClub(Transform socket, Material material)
        {
            var source = CombatantGenerator.LoadModel(SyntyPostprocessor.WeaponsFolder + TrollClub + ".fbx");
            var club = new GameObject(TrollClub);
            club.transform.SetParent(socket, false);
            club.transform.localPosition = ClubGripPos;
            club.transform.localEulerAngles = ClubGripEuler;
            foreach (var piece in source.GetComponentsInChildren<MeshRenderer>(true))
            {
                var pose = source.transform.worldToLocalMatrix * piece.transform.localToWorldMatrix;
                var go = new GameObject(piece.name);
                go.transform.SetParent(club.transform, false);
                go.transform.localPosition = pose.GetPosition();
                go.transform.localRotation = pose.rotation;
                go.transform.localScale = pose.lossyScale;
                go.AddComponent<MeshFilter>().sharedMesh = piece.GetComponent<MeshFilter>().sharedMesh;
                CombatantGenerator.ApplyMaterial(go.AddComponent<MeshRenderer>(), material);
            }
        }

        /// The whole troll framed to fit, and a head-and-shoulders close-up, side by side.
        static (Color32[] Body, Color32[] Head) CaptureTrollFront(Camera camera, GameObject troll, Animator animator, string colour)
        {
            var bounds = WorldBounds(troll);
            float tan = Mathf.Tan(Fov * 0.5f * Mathf.Deg2Rad);
            float fit = Mathf.Max(bounds.extents.y, bounds.extents.x) * FrontMargin / tan + bounds.extents.z;
            camera.transform.SetPositionAndRotation(new Vector3(bounds.center.x, bounds.center.y, bounds.center.z - fit), Quaternion.identity);
            var body = Render(camera, Size, Size);

            float s = troll.transform.lossyScale.y;
            var head = animator.GetBoneTransform(HumanBodyBones.Head).position;
            camera.transform.SetPositionAndRotation(head + new Vector3(0f, 0f, -HeadDistance * s), Quaternion.identity);
            var face = Render(camera, Size, Size);

            string path = OutFolder + "d6b-troll-front-" + colour + ".tga";
            WriteTga(path, SideBySide(body, face, Size, Size), Size * 2, Size);
            Debug.Log(Tag + "wrote " + path);
            return (body, face);
        }

        /// The club hand from the front and from the thumb side, as CaptureGrips does for the heroes, far enough back to
        /// show the club's angle too.
        static void CaptureTrollGrip(Camera camera, GameObject troll)
        {
            var socket = troll.GetComponentsInChildren<Transform>(true).First(t => t.name == SocketNames.HandR);
            float distance = GripDistance * 2.5f * troll.transform.lossyScale.y;
            var views = new[] { new Vector3(0f, 0.1f, -1f), new Vector3(-1f, 0.1f, 0f) };
            var halves = new Color32[2][];
            for (int v = 0; v < views.Length; v++)
            {
                camera.transform.position = socket.position + views[v].normalized * distance;
                camera.transform.rotation = Quaternion.LookRotation(socket.position - camera.transform.position);
                halves[v] = Render(camera, Size, Size);
            }
            var pixels = new Color32[Size * Size];
            int half = Size / 2;
            for (int v = 0; v < 2; v++)
                for (int y = 0; y < Size; y++)
                    Array.Copy(halves[v], y * Size + Size / 4, pixels, y * Size + v * half, half);
            string path = OutFolder + "d6b-troll-grip.tga";
            WriteTga(path, pixels, Size, Size);
            Debug.Log(Tag + "wrote " + path);
        }

        /// BattleView's layout and BattleCameras' group shot: the default party in the hero slots and troll_grunt in the
        /// enemy slot, spawned through CombatantActor so the camera rig is the game's own. The group anchors depend only on
        /// the roots, so the Synty troll then takes troll_grunt's place without changing the shot; troll_grunt is hidden by
        /// its renderers, because deactivating its root would drop its anchor from the group. Cinemachine snaps without
        /// damping when deltaTime is negative, which is the settled shot the game reaches after its damping.
        static void CaptureTrollBattle(Camera camera, GameTables tables, GameObject troll, Material material)
        {
            var stage = new GameObject("BattleView").transform;
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            Object.DestroyImmediate(ground.GetComponent<Collider>());
            ground.transform.SetParent(stage, false);
            ground.transform.localScale = Vector3.one * GroundScale;
            ground.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(CombatantGenerator.MaterialsFolder + "/ground.mat");

            var party = DebugParty.CreateParty();
            var actors = new List<CombatantActor>();
            for (int i = 0; i < party.Heroes.Count; i++)
            {
                var hero = party.Heroes[i];
                var cls = tables.Classes.Single(c => c.Id == hero.ClassId);
                var actor = CombatantActor.Spawn(i, HeroGenerator.VariantId(cls.Id, hero.Body),
                    BattleStage.HeroSlot(i, party.Heroes.Count), BattleStage.HeroFacing, stage);
                SampleIdle(actor.Animator, HeroGenerator.ResolveClip(cls.Clips["Idle"], hero.Body));
                actors.Add(actor);
            }
            var enemy = tables.Enemies.Single(e => e.Id == CurrentTroll);
            var current = CombatantActor.Spawn(actors.Count, CurrentTroll, BattleStage.EnemySlot(0, 1), BattleStage.EnemyFacing, stage);
            SampleIdle(current.Animator, enemy.Clips["Idle"]);
            actors.Add(current);
            troll.transform.SetPositionAndRotation(BattleStage.EnemySlot(0, 1), BattleStage.EnemyFacing);

            var cameras = new BattleCameras(camera, actors, stage);
            var brain = camera.GetComponent<CinemachineBrain>();
            brain.UpdateMethod = CinemachineBrain.UpdateMethods.ManualUpdate;
            int frame = 1;

            var previous = camera.targetTexture;
            try
            {
                foreach (var (name, width, height) in Ratios)
                {
                    var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
                    camera.targetTexture = target;
                    camera.aspect = (float)width / height;
                    for (int i = 0; i < 3; i++)
                    {
                        cameras.TargetGroup.DoUpdate();
                        brain.ManualUpdate(frame++, -1f);
                    }
                    Debug.Log(Tag + string.Format(CultureInfo.InvariantCulture, "{0} {1}×{2}: group camera at {3}, euler {4}, fov {5:F1}",
                        name, width, height, camera.transform.position.ToString("F2"), camera.transform.eulerAngles.ToString("F1"), camera.fieldOfView));

                    troll.SetActive(false);
                    ShowRenderers(current, true);
                    string reference = OutFolder + "d6b-troll-" + name + "-kaykit.tga";
                    WriteTga(reference, Render(camera, width, height), width, height);
                    LogFraming(camera, name + " kaykit", actors.Select(a => (a.Root.name, a.Root.gameObject)));

                    ShowRenderers(current, false);
                    troll.SetActive(true);
                    var lineup = actors.Take(party.Heroes.Count).Select(a => (a.Root.name, a.Root.gameObject)).Append(("synty troll", troll)).ToList();
                    LogFraming(camera, name + " synty", lineup);
                    foreach (var colour in TrollColours)
                    {
                        material.SetTexture("_BaseMap", LoadTexture(SyntyPostprocessor.TexturesFolder + colour + ".png"));
                        string path = OutFolder + "d6b-troll-" + name + "-" + colour + ".tga";
                        WriteTga(path, Render(camera, width, height), width, height);
                        Debug.Log(Tag + "wrote " + path);
                    }
                    camera.targetTexture = previous;
                    Object.DestroyImmediate(target);
                }
            }
            finally
            {
                camera.targetTexture = previous;
            }
        }

        static void ShowRenderers(CombatantActor actor, bool show)
        {
            foreach (var renderer in actor.Renderers)
                renderer.enabled = show;
        }

        static void SampleIdle(Animator animator, string clipName)
        {
            var clip = ((AnimatorOverrideController)animator.runtimeAnimatorController).animationClips.First(c => c.name == clipName);
            clip.SampleAnimation(animator.gameObject, 0f);
        }

        /// Each combatant's posed silhouette in viewport percent (y up from the bottom, x from the left), from the skinned
        /// meshes as posed and the weapons, so the numbers compare with §14.4's checks and D6.2's 31 % feet / 81 % heads.
        static void LogFraming(Camera camera, string shot, IEnumerable<(string Name, GameObject Root)> combatants)
        {
            foreach (var (name, root) in combatants)
            {
                float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
                foreach (var p in WorldPoints(root, true))
                {
                    var v = camera.WorldToViewportPoint(p);
                    minX = Mathf.Min(minX, v.x); maxX = Mathf.Max(maxX, v.x);
                    minY = Mathf.Min(minY, v.y); maxY = Mathf.Max(maxY, v.y);
                }
                Debug.Log(Tag + string.Format(CultureInfo.InvariantCulture, "framing {0}: {1} feet {2:F1} %, top {3:F1} %, x {4:F1}–{5:F1} %",
                    shot, name, minY * 100f, maxY * 100f, minX * 100f, maxX * 100f));
            }
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

        static Color32[] SideBySide(Color32[] left, Color32[] right, int width, int height)
        {
            var pixels = new Color32[width * 2 * height];
            for (int y = 0; y < height; y++)
            {
                Array.Copy(left, y * width, pixels, y * width * 2, width);
                Array.Copy(right, y * width, pixels, y * width * 2 + width, width);
            }
            return pixels;
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
