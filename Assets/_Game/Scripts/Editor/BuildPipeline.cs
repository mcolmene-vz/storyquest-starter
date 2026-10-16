using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Callbacks;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_IOS
using UnityEditor.iOS.Xcode;
#endif

namespace Game.Editor
{
    /// Player settings and the iOS build (ARCHITECTURE §13.6). This class shadows
    /// UnityEditor.BuildPipeline inside Game.Editor, so Unity's is always fully qualified.
    public static class BuildPipeline
    {
        public const string ProductName = "StoryQuest";
        public const string BundleId = "com.vendoza.storyquest";
        public const string BundleVersion = "0.1.0";

        const string CompanyName = "Vendoza LLC";
        const int ArchitectureArm64 = 1;

        public static void ApplyPlayerSettings()
        {
            var ios = NamedBuildTarget.iOS;

            PlayerSettings.productName = ProductName;
            PlayerSettings.companyName = CompanyName;
            PlayerSettings.SetApplicationIdentifier(ios, BundleId);
            PlayerSettings.bundleVersion = BundleVersion;

            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;

            PlayerSettings.iOS.targetOSVersionString = "16.0";
            PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneAndiPad;
            PlayerSettings.iOS.requiresFullScreen = true;
            PlayerSettings.statusBarHidden = true;
            PlayerSettings.iOS.deferSystemGesturesMode = UnityEngine.iOS.SystemGestureDeferMode.BottomEdge;
            PlayerSettings.iOS.hideHomeButton = false;
            PlayerSettings.iOS.appInBackgroundBehavior = iOSAppInBackgroundBehavior.Suspend;
            PlayerSettings.iOS.requiresPersistentWiFi = false;
            SetProjectSettingOff("Prepare IOS For Recording");

            PlayerSettings.SetScriptingBackend(ios, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetArchitecture(ios, ArchitectureArm64);
            PlayerSettings.SetApiCompatibilityLevel(ios, ApiCompatibilityLevel.NET_Standard);
            PlayerSettings.SetApiCompatibilityLevel(NamedBuildTarget.Standalone, ApiCompatibilityLevel.NET_Standard);
            PlayerSettings.SetManagedStrippingLevel(ios, ManagedStrippingLevel.Low);
            PlayerSettings.colorSpace = ColorSpace.Linear;
            // Explicit list first: SetUseDefaultGraphicsAPIs(false) after it keeps m_Automatic at 0 in ProjectSettings.
            PlayerSettings.SetGraphicsAPIs(BuildTarget.iOS, new[] { GraphicsDeviceType.Metal });
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.iOS, false);

            PlayerSettings.insecureHttpOption = InsecureHttpOption.NotAllowed;
            // Legacy hardware statistics, off so nothing phones home (ARCHITECTURE §13.6, 2026-10-16).
            SetProjectSettingOff("submitAnalytics");
            // These may be marked obsolete in Unity 6; they still drive what ends up in ProjectSettings.
#pragma warning disable 618
            UnityEditor.Analytics.AnalyticsSettings.enabled = false;
            UnityEditor.Analytics.PerformanceReportingSettings.enabled = false;
            UnityEditor.CrashReporting.CrashReportingSettings.enabled = false;
#pragma warning restore 618

            var teamId = Environment.GetEnvironmentVariable("SQ_APPLE_TEAM_ID");
            if (!string.IsNullOrEmpty(teamId))
            {
                PlayerSettings.iOS.appleEnableAutomaticSigning = true;
                PlayerSettings.iOS.appleDeveloperTeamID = teamId;
            }
        }

        // For flags whose C# accessors are gone from the Unity 6 API (prepareIOSForRecording, submitAnalytics) but are
        // still serialized in ProjectSettings.asset.
        static void SetProjectSettingOff(string propertyName)
        {
            var settings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0];
            using (var so = new SerializedObject(settings))
            {
                var flag = so.FindProperty(propertyName);
                if (flag == null)
                {
                    Debug.LogWarning("[ApplyPlayerSettings] " + propertyName + " not found in ProjectSettings.asset");
                    return;
                }
                if (flag.propertyType == SerializedPropertyType.Boolean)
                    flag.boolValue = false;
                else
                    flag.intValue = 0;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        public static void BuildIOS()
        {
            ApplyPlayerSettings();

            int previous = int.TryParse(PlayerSettings.iOS.buildNumber, out var parsed) ? parsed : 0;
            PlayerSettings.iOS.buildNumber = (previous + 1).ToString();
            AssetDatabase.SaveAssets();

            bool simulator = Environment.GetEnvironmentVariable("SQ_SIMULATOR") == "1";
            string location = simulator ? "Builds/iOS-sim" : "Builds/iOS";
            PlayerSettings.iOS.sdkVersion = simulator ? iOSSdkVersion.SimulatorSDK : iOSSdkVersion.DeviceSDK;
            // Unity's default X86_64 simulator slice ran under Rosetta on iOS 17 and cannot install on iOS 26/27 simulators
            // (§13.6 amendment, 2026-10-16). Simulator-only setting; device builds never read it.
            if (simulator)
                PlayerSettings.iOS.simulatorSdkArchitecture = AppleMobileArchitectureSimulator.ARM64;

            var options = BuildOptions.None;
            // Append keeps the Xcode signing settings (§13.6), but Unity throws when asked to append where no build exists yet.
            if (UnityEditor.BuildPipeline.BuildCanBeAppended(BuildTarget.iOS, location) == CanAppendBuild.Yes)
                options |= BuildOptions.AcceptExternalModificationsToPlayer;
            if (Environment.GetEnvironmentVariable("SQ_DEV") == "1")
                options |= BuildOptions.Development;

            Debug.Log($"[BuildIOS] {ProductName} {BundleVersion} build {PlayerSettings.iOS.buildNumber} -> {location} sdk={PlayerSettings.iOS.sdkVersion}"
                      + (simulator ? $" simulatorArch={PlayerSettings.iOS.simulatorSdkArchitecture}" : "") + $" options={options}");

            BuildReport report;
            try
            {
                report = UnityEditor.BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { ProjectSetup.BootScenePath },
                    locationPathName = location,
                    target = BuildTarget.iOS,
                    options = options,
                });
            }
            finally
            {
                // The simulator SDK must never be left behind in ProjectSettings.
                PlayerSettings.iOS.sdkVersion = iOSSdkVersion.DeviceSDK;
                AssetDatabase.SaveAssets();
            }

            var summary = report.summary;
            Debug.Log($"[BuildIOS] {summary.result}: {summary.totalErrors} errors, {summary.totalWarnings} warnings, {summary.totalTime.TotalSeconds:F0}s, {summary.outputPath}");
            if (summary.result == BuildResult.Succeeded)
                return;

            Debug.LogError($"[BuildIOS] build failed: {summary.result}");
            if (Application.isBatchMode)
                EditorApplication.Exit(1);
            else
                throw new BuildFailedException("[BuildIOS] build failed: " + summary.result);
        }

        /// Fallback compile gate (§13.3): reaching this method means every assembly compiled.
        public static void CompileCheck() => Debug.Log("[CompileCheck] ok");

        [PostProcessBuild(1)]
        static void OnPostProcessBuild(BuildTarget target, string path)
        {
#if UNITY_IOS
            if (target != BuildTarget.iOS)
                return;

            string plistPath = Path.Combine(path, "Info.plist");
            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);
            plist.root.SetBoolean("ITSAppUsesNonExemptEncryption", false);
            plist.WriteToFile(plistPath);

            // Apple requires the privacy manifest; Unity generates it, we only confirm it is there (§10).
            if (Directory.GetFiles(path, "PrivacyInfo.xcprivacy", SearchOption.AllDirectories).Length == 0)
                throw new BuildFailedException("[BuildIOS] PrivacyInfo.xcprivacy missing from the generated Xcode project at " + path);
#endif
        }
    }
}
