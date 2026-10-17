using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Core.Data;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Game.Editor
{
    /// Front captures of the eight hero variants for the producer's skin-tone check (ruling 7, ARCHITECTURE §15.3), and
    /// one hand close-up per weapon mesh for tuning classes.json weaponMeshes (§15.18.3). Renders, so it runs in batch
    /// mode without -nographics:
    ///   "$UNITY" -batchmode -projectPath . -executeMethod Game.Editor.VariantCapture.Run -logFile - -quit
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
