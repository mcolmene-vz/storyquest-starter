using System;
using System.IO;
using TMPro.EditorUtilities;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Game.Editor
{
    /// Turns the committed skeleton into a working project (ARCHITECTURE §13.1 step 3).
    /// Every step skips when its artifact already exists, so Run() is safe to repeat.
    public static class ProjectSetup
    {
        public const string BootScenePath = "Assets/_Game/Scenes/Boot.unity";

        const string TmpSettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";
        const string PlaceholderMaterialPath = "Assets/_Game/Resources/Materials/Placeholder.mat";
        const string UrpFolder = "Assets/_Game/Settings/URP";
        const string RendererPath = UrpFolder + "/StoryQuest-Renderer.asset";
        const string PipelinePath = UrpFolder + "/StoryQuest-URP.asset";
        const string ProjectSettingsPath = "ProjectSettings/ProjectSettings.asset";
        const string DefaultPostProcessDataPath = "Runtime/Data/PostProcessData.asset";

        public static void Run()
        {
            ImportTmpEssentials();
            CreatePlaceholderMaterial();
            CreateRenderPipeline();
            CreateBootScene();
            SetInputSystemOnly();
            BuildPipeline.ApplyPlayerSettings();
            SwitchToIOS();
            AssetDatabase.SaveAssets();

            if (Application.isBatchMode)
                Debug.Log("[ProjectSetup] done: TMP essentials, Placeholder.mat, URP assets, Boot.unity, Input System only, player settings, iOS target");
        }

        static void ImportTmpEssentials()
        {
            if (File.Exists(TmpSettingsPath))
            {
                Log("TMP essentials", false);
                return;
            }

            TMP_PackageResourceImporter.ImportResources(true, false, false);
            AssetDatabase.Refresh();
            Log("TMP essentials", true);
        }

        static void CreatePlaceholderMaterial()
        {
            if (File.Exists(PlaceholderMaterialPath))
            {
                Log("Placeholder.mat", false);
                return;
            }

            // A committed material is what carries the URP Lit shader into the build (§13.4).
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                throw new InvalidOperationException("[ProjectSetup] shader 'Universal Render Pipeline/Lit' not found; is URP installed?");

            EnsureFolder(Path.GetDirectoryName(PlaceholderMaterialPath)!);
            var material = new Material(shader);
            material.SetColor("_BaseColor", new Color(0.95f, 0.55f, 0.15f, 1f));
            AssetDatabase.CreateAsset(material, PlaceholderMaterialPath);
            Log("Placeholder.mat", true);
        }

        static void CreateRenderPipeline()
        {
            if (File.Exists(PipelinePath))
            {
                Log("URP assets", false);
                return;
            }

            EnsureFolder(UrpFolder);

            var renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
            // Same default post-process data Unity's own create menu assigns; post stays off (§13.5).
            renderer.postProcessData = AssetDatabase.LoadAssetAtPath<PostProcessData>(
                UniversalRenderPipelineAsset.packagePath + "/" + DefaultPostProcessDataPath);
            if (renderer.postProcessData == null)
                Debug.LogWarning("[ProjectSetup] default PostProcessData not found under the URP package; renderer saved without it");
            using (var so = new SerializedObject(renderer))
            {
                so.FindProperty("m_RenderingMode").intValue = (int)RenderingMode.Forward;
                so.FindProperty("m_DepthPrimingMode").intValue = (int)DepthPrimingMode.Disabled;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            AssetDatabase.CreateAsset(renderer, RendererPath);

            var pipeline = UniversalRenderPipelineAsset.Create(renderer);
            pipeline.renderScale = 1f;
            pipeline.msaaSampleCount = 4;
            pipeline.supportsHDR = false;
            pipeline.shadowDistance = 25f;
            pipeline.shadowCascadeCount = 1;
            pipeline.mainLightShadowmapResolution = 1024;
            pipeline.supportsCameraDepthTexture = false;
            pipeline.supportsCameraOpaqueTexture = false;
            pipeline.useSRPBatcher = true;
            pipeline.supportsDynamicBatching = false;
            pipeline.gpuResidentDrawerMode = GPUResidentDrawerMode.Disabled;
            using (var so = new SerializedObject(pipeline))
            {
                so.FindProperty("m_MainLightRenderingMode").intValue = (int)LightRenderingMode.PerPixel;
                so.FindProperty("m_MainLightShadowsSupported").boolValue = true;
                so.FindProperty("m_SoftShadowsSupported").boolValue = false;
                so.FindProperty("m_AdditionalLightsRenderingMode").intValue = (int)LightRenderingMode.Disabled;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            AssetDatabase.CreateAsset(pipeline, PipelinePath);

            // One asset for every quality level (§13.5).
            GraphicsSettings.defaultRenderPipeline = pipeline;
            int originalLevel = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = pipeline;
            }
            QualitySettings.SetQualityLevel(originalLevel, false);

            Log("URP assets", true);
        }

        static void CreateBootScene()
        {
            if (File.Exists(BootScenePath))
            {
                Log("Boot.unity", false);
            }
            else
            {
                EnsureFolder(Path.GetDirectoryName(BootScenePath)!);
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var bootstrap = new GameObject("GameBootstrap");
                bootstrap.AddComponent<Game.Runtime.GameBootstrap>();
                if (!EditorSceneManager.SaveScene(scene, BootScenePath))
                    throw new InvalidOperationException("[ProjectSetup] could not save " + BootScenePath);
                Log("Boot.unity", true);
            }

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(BootScenePath, true) };
        }

        static void SetInputSystemOnly()
        {
            // 0 = Input Manager, 1 = Input System package, 2 = both. No public API for this setting.
            var settings = AssetDatabase.LoadAllAssetsAtPath(ProjectSettingsPath)[0];
            using (var so = new SerializedObject(settings))
            {
                var handler = so.FindProperty("activeInputHandler");
                if (handler == null)
                    throw new InvalidOperationException("[ProjectSetup] activeInputHandler not found in " + ProjectSettingsPath);
                if (handler.intValue == 1)
                {
                    Log("Input System only", false);
                    return;
                }
                handler.intValue = 1;
                so.ApplyModifiedProperties();
            }
            Log("Input System only", true);
        }

        static void SwitchToIOS()
        {
            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.iOS)
            {
                Log("iOS build target", false);
                return;
            }

            EditorUserBuildSettings.SwitchActiveBuildTarget(NamedBuildTarget.iOS, BuildTarget.iOS);
            Log("iOS build target", true);
        }

        static void EnsureFolder(string assetFolder)
        {
            if (AssetDatabase.IsValidFolder(assetFolder))
                return;
            Directory.CreateDirectory(assetFolder);
            AssetDatabase.Refresh();
        }

        static void Log(string step, bool created) =>
            Debug.Log("[ProjectSetup] " + step + ": " + (created ? "created" : "exists"));
    }
}
