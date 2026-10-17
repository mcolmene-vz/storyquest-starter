using System;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
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
        const string UrpFolder = "Assets/_Game/Settings/URP";
        const string RendererPath = UrpFolder + "/StoryQuest-Renderer.asset";
        const string PipelinePath = UrpFolder + "/StoryQuest-URP.asset";
        const string ProjectSettingsPath = "ProjectSettings/ProjectSettings.asset";
        const string DefaultPostProcessDataPath = "Runtime/Data/PostProcessData.asset";

        public static void Run()
        {
            ImportTmpEssentials();
            CreateRenderPipeline();
            SetQualityAntiAliasing();
            SetLightUnits();
            ToonShaderCheck();
            CombatantGenerator.Generate();
            CreateBootScene();
            SetInputSystemOnly();
            BuildPipeline.ApplyPlayerSettings();
            SwitchToIOS();
            AssetDatabase.SaveAssets();

            if (Application.isBatchMode)
                Debug.Log("[ProjectSetup] done: TMP essentials, URP assets, quality AA, light units, toon shader, combatants, Boot.unity, Input System only, player settings, iOS target");
        }

        static void ImportTmpEssentials()
        {
            if (File.Exists(TmpSettingsPath))
            {
                Log("TMP essentials", false);
                return;
            }

            // TMP_PackageResourceImporter.ImportResources queues an asynchronous AssetDatabase.ImportPackage that never
            // runs before a -quit batch run exits. The editor's synchronous import is internal, so it is reached by reflection.
            var import = typeof(AssetDatabase).GetMethod("ImportPackageImmediately",
                BindingFlags.Static | BindingFlags.NonPublic, null, new[] { typeof(string) }, null);
            if (import == null)
                throw new InvalidOperationException("[ProjectSetup] AssetDatabase.ImportPackageImmediately(string) not found in this editor; import TMP essentials by hand: Window > TextMeshPro > Import TMP Essential Resources");

            var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(TMP_Settings).Assembly);
            if (package == null)
                throw new InvalidOperationException("[ProjectSetup] could not resolve the package that contains TextMesh Pro");
            import.Invoke(null, new object[] { package.resolvedPath + "/Package Resources/TMP Essential Resources.unitypackage" });
            AssetDatabase.Refresh();

            if (!File.Exists(TmpSettingsPath))
                throw new InvalidOperationException("[ProjectSetup] TMP essentials import did not produce " + TmpSettingsPath);
            Log("TMP essentials", true);
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
            using (var so = new SerializedObject(pipeline))
            {
                so.FindProperty("m_MainLightRenderingMode").intValue = (int)LightRenderingMode.PerPixel;
                so.FindProperty("m_MainLightShadowsSupported").boolValue = true;
                so.FindProperty("m_SoftShadowsSupported").boolValue = false;
                so.FindProperty("m_AdditionalLightsRenderingMode").intValue = (int)LightRenderingMode.Disabled;
                // 0 = Disabled. The enum lives in Unity.RenderPipelines.GPUDriven.Runtime, which Game.Editor does not reference.
                so.FindProperty("m_GPUResidentDrawerMode").intValue = 0;
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
                QualitySettings.antiAliasing = 4;
            }
            QualitySettings.SetQualityLevel(originalLevel, false);

            Log("URP assets", true);
        }

        static void SetQualityAntiAliasing()
        {
            // On iOS the backbuffer is allocated at launch from the active quality level's antiAliasing, before URP
            // instantiates and pushes its 4x; a 0 there gives a 1-sample drawable for the first frame (§13.5).
            // Runs every time because CreateRenderPipeline returns early once the URP asset exists.
            bool changed = false;
            int originalLevel = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                if (QualitySettings.antiAliasing == 4)
                    continue;
                QualitySettings.antiAliasing = 4;
                changed = true;
            }
            QualitySettings.SetQualityLevel(originalLevel, false);

            Debug.Log("[ProjectSetup] quality AA 4x: " + (changed ? "set" : "exists"));
        }

        static void SetLightUnits()
        {
            // §15.18.3: URP turns both on when its pipeline starts, and editor rendering writes them to GraphicsSettings.asset;
            // setting them here keeps regeneration, captures and the gate from flipping the committed file.
            bool changed = !GraphicsSettings.lightsUseLinearIntensity || !GraphicsSettings.lightsUseColorTemperature;
            GraphicsSettings.lightsUseLinearIntensity = true;
            GraphicsSettings.lightsUseColorTemperature = true;
            Debug.Log("[ProjectSetup] light units (linear intensity, colour temperature): " + (changed ? "set" : "exists"));
        }

        static void ToonShaderCheck()
        {
            // Runs every time: a shader that fails to compile stops the CLI here instead of showing up pink on device (§14.9).
            var shader = Shader.Find(CombatantGenerator.ShaderName);
            if (shader == null)
                throw new InvalidOperationException("[ProjectSetup] shader '" + CombatantGenerator.ShaderName + "' not found; is Assets/_Game/Art/Shaders/Toon.shader present and imported?");
            if (ShaderUtil.ShaderHasError(shader))
            {
                var errors = ShaderUtil.GetShaderMessages(shader)
                    .Where(m => m.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error)
                    .Select(m => m.message + " (" + m.file + ":" + m.line + ")");
                throw new InvalidOperationException("[ProjectSetup] shader '" + CombatantGenerator.ShaderName + "' has compile errors:\n - " + string.Join("\n - ", errors));
            }
            // Shader.passCount reads the active subshader on the current device (the Null device under -nographics picks
            // the fallback); ShaderData is the importer's view and lists the four passes regardless.
            var passes = ShaderUtil.GetShaderData(shader).GetSubshader(0);
            Debug.Log("[ProjectSetup] toon shader: ok, passes: " + string.Join(", ",
                Enumerable.Range(0, passes.PassCount).Select(i => passes.GetPass(i).Name)));
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
