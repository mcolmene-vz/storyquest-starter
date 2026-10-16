# PLAN-M0 — Device loop

Lead's plan for V1-SCOPE §12 M0. Design basis: ARCHITECTURE.md §11 (all six items resolved 2026-10-15) and §13. Status of each task is kept current here; sign-off evidence goes to STATUS.md.

**M0 done when:** a build with a spinning placeholder and a tap counter runs on Martin's iPhone and iPad from Xcode, and `docs/DEVICE-TESTING.md` matches what actually happened.

## Dependency order

```
T1 skeleton ─► T2 editor scripts ─► T3 runtime scripts ─► T4 first open + ProjectSetup (needs Unity) ─► T5 iOS build + simulator check ─► T6 device run (Martin) ─► producer sign-off
```

T1–T3 are written before Unity is installed and cannot compile until T4. Their verification is a lead review of the diff plus static checks; T4 is where compile errors from T1–T3 are fixed, and only those.

## Lead's readings (assumptions written down so nobody has to ask)

- **Version string.** ARCHITECTURE §13.6 says `0.<milestone>.0`. A literal `0.0.0` for M0 reads as "no version", so M0 builds carry `0.1.0`, M1 `0.2.0`, and so on, `1.0.0` at M5. Still one constant, still bumped by the lead at sign-off.
- **Apple team ID.** The keychain already holds "Apple Distribution: Vendoza LLC (W727YBJNK6)", so the lead runs builds with `SQ_APPLE_TEAM_ID=W727YBJNK6`. Martin confirms it in STATUS.md; if wrong, one rebuild fixes it.
- **Two-aspect evidence for M0.** ARCHITECTURE §13.10 accepts Unity's Device Simulator; that is an editor window and cannot be driven headless. The lead instead builds the simulator target (`SQ_SIMULATOR=1`) and screenshots a 4:3 iPad simulator and a 19.5:9 iPhone simulator with `xcrun simctl`. Same evidence, scriptable, and it doubles as the iPad check until Martin pairs an iPad.
- **Empty assemblies.** `Game.Core` and `Game.Tests` have zero scripts in M0. Unity warns that an assembly with no scripts is skipped; that warning is expected and is not fixed by adding filler files.
- **Namespace clash.** `Game.Editor.BuildPipeline` shadows `UnityEditor.BuildPipeline` inside its own namespace; the editor code fully qualifies `UnityEditor.BuildPipeline.BuildPlayer`. The CLI method name `Game.Editor.BuildPipeline.BuildIOS` in CLAUDE.md and DEVICE-TESTING stays as written.
- **Screen never sleeps** (§13.4) is applied in M0 as the architect specified. If a parent complains in M5 testing, it becomes a producer question then.

## Tasks

### T1 — Project skeleton (blind)

- **Goal:** the files Unity needs to open the folder as a project with the pinned editor, the four assemblies, nullable settings and stripping rules, exactly per ARCHITECTURE §13.1 step 1, §13.2, §13.3.
- **Files:** `Packages/manifest.json`; `ProjectSettings/ProjectVersion.txt`; `Assets/_Game/Scripts/Core/Game.Core.asmdef` + `csc.rsp`; `Assets/_Game/Scripts/Runtime/Game.Runtime.asmdef` + `csc.rsp`; `Assets/_Game/Scripts/Editor/Game.Editor.asmdef` + `csc.rsp`; `Assets/_Game/Tests/EditMode/Game.Tests.asmdef` + `csc.rsp`; `Assets/_Game/link.xml`; `docs/DEVICE-TESTING.md` §2 editor path only.
- **Verification:** every `.json`/`.asmdef` parses (`node -e 'JSON.parse(...)'`); manifest equals §13.2 byte-for-byte in content; asmdef fields match the §13.3 table; `link.xml` preserves `Game.Core` and `Newtonsoft.Json` with `preserve="all"`; `git diff` touches only the files above. Lead reviews.
- **Not part of it:** any `.cs` file, any `.meta` file (Unity writes them in T4), `ProjectSettings/*.asset`, `packages-lock.json`, `.gitkeep` files.
- **Status:** done 2026-10-15, verified by the lead (JSON/XML parse, manifest equals §13.2, asmdefs equal §13.3 table, Runtime references `Ink` and `Ink-Libraries` as found in the package's upm branch).

### T2 — Editor scripts: ProjectSetup and BuildPipeline (blind)

- **Goal:** `Game.Editor.ProjectSetup.Run()` (idempotent, §13.1 step 3, §13.4 scene creation, §13.5 URP, §13.6 settings) and `Game.Editor.BuildPipeline` with `ApplyPlayerSettings()`, `BuildIOS()`, `CompileCheck()` and the `[PostProcessBuild]` plist step, per §13.6.
- **Files:** `Assets/_Game/Scripts/Editor/ProjectSetup.cs`, `Assets/_Game/Scripts/Editor/BuildPipeline.cs`.
- **Verification:** lead reads the diff against §13.1, §13.4–§13.6 line by line; every setting in §13.6 appears once; `BuildIOS` exits non-zero on failure; the plist step fails the build if `PrivacyInfo.xcprivacy` is missing; no `Shader.Find` at runtime paths; `UnityEditor.BuildPipeline` is fully qualified. Compiles in T4.
- **Not part of it:** icons, launch screen, splash (M5); asset postprocessors (M1); anything under `Runtime/`.
- **Status:** done 2026-10-15, verified by the lead (diff read against §13.1, §13.4–§13.6; one accepted deviation: default PostProcessData is loaded by asset path because Unity's helper is internal). Compile pending T4.

### T3 — Runtime: GameBootstrap, SafeAreaFitter, Spinner, TapCounter (blind)

- **Goal:** the M0 placeholder exactly as §13.4: `GameBootstrap.Awake()` builds camera, light, spinning cube (collider removed, material from `Resources/Materials/Placeholder`), canvas with scaler 1920×1080 match 0.5, `EventSystem` + `InputSystemUIInputModule`, `SafeArea` root with `SafeAreaFitter`, one TMP label reading `Taps: {n}  {w}×{h}  safe {safeArea}` incremented on any finger down via Enhanced Touch.
- **Files:** `Assets/_Game/Scripts/Runtime/Boot/GameBootstrap.cs`, `Assets/_Game/Scripts/Runtime/Boot/Spinner.cs`, `Assets/_Game/Scripts/Runtime/Boot/TapCounter.cs`, `Assets/_Game/Scripts/Runtime/UI/SafeAreaFitter.cs`.
- **Verification:** lead reads the diff against §13.4; `GameBootstrap` has no serialized fields (no public fields, no `[SerializeField]`); no physics component survives; no `UnityEngine` usage outside `Runtime/`; `SafeAreaFitter` re-applies when `Screen.safeArea` changes. Compiles in T4; behaviour seen in T5 screenshots.
- **Not part of it:** Cinemachine, services, flow state machine, anything from M1. `Spinner` and `TapCounter` are deleted in M1.
- **Status:** done 2026-10-15, verified by the lead (diff read against §13.4; no serialized fields, no physics, material from Resources). One accepted addition: an `AudioListener` on the camera, because Unity warns every frame without one and ARCHITECTURE §4 needs it for `AudioService` anyway. Compile pending T4.

### T4 — First headless open, ProjectSetup, compile gate (needs Unity installed)

- **Goal:** turn the skeleton into a resolved Unity project with the generated Boot scene, URP assets, placeholder material and TMP essentials, and establish the compile gate.
- **Files:** everything Unity writes: `Packages/packages-lock.json`, `ProjectSettings/*.asset`, all `.meta`, `Assets/_Game/Scenes/Boot.unity`, `Assets/_Game/Settings/URP/*.asset`, `Assets/_Game/Resources/Materials/Placeholder.mat`, `Assets/TextMesh Pro/**`, `Assets/UniversalRenderPipelineGlobalSettings.asset` (wherever URP puts it); fixes to T1–T3 files only for compile errors; `docs/DEVICE-TESTING.md` if the gate command changes; `docs/EDITOR-STEPS.md` only if a click proved unavoidable.
- **Commands:** §13.1 steps 2 and 3, then the EditMode test command from DEVICE-TESTING §2.
- **Verification:** `-runTests` exits 0 with zero tests (else switch the gate to `CompileCheck` and update DEVICE-TESTING); log has no `error CS`; `grep -r UnityEngine Assets/_Game/Scripts/Core` empty; `manifest.json` still equals §13.2 apart from core-package patch rewrites; `packages-lock.json` has no `com.unity.services.*` and no `unityanalytics`; `ProjectSettings.asset` shows landscape only, bundle id, iOS 16.0, IL2CPP ARM64, Linear, analytics/crash reporting off; `Boot.unity` is the only `.unity`; no `.prefab`/`.controller`/`.playable` anywhere. Lead runs the checks.
- **Not part of it:** the iOS build; any new feature; any test file.
- **Status:** done 2026-10-16, verified by the lead (own gate run: exit 0, zero `error CS`, runner executed with zero tests and wrote `Builds/test-results.xml`; §13.10 structural checks pass). Findings and lead rulings:
  - `-runTests … -quit` exits the editor before the runner starts; it was a compile gate by accident. Lead removed `-quit` from the test command in DEVICE-TESTING §2 and CLAUDE.md. Architect asked to align §13.3.
  - API moves fixed in code only: `PostProcessBuild` needs `UnityEditor.Callbacks`; `prepareIOSForRecording` left the public API (flag set through `SerializedObject`); `gpuResidentDrawerMode` enum lives in an unreferenced assembly (set through `SerializedObject`); TMP essentials must be imported synchronously (internal `ImportPackageImmediately` by reflection, one-time step, verified on disk). Architect asked to record these in §13.1/§13.6.
  - Files the editor placed itself and we commit where they land: `Assets/DefaultVolumeProfile.asset`, `Assets/UniversalRenderPipelineGlobalSettings.asset`, `ProjectSettings/InkSettings.asset`, `SceneTemplateSettings.json`, `ShaderGraphSettings.asset`, Rendering Debugger axes in `InputManager.asset`. Generated `.vscode/` is git-ignored.
  - `submitAnalytics: 1` (legacy hardware-statistics flag) remains in ProjectSettings; architect asked whether §13.6 adds `PlayerSettings.submitAnalytics = false` (lead recommends yes; applied in T5 if approved).
  - No editor step needed a click; no `docs/EDITOR-STEPS.md` yet.

### T5 — iOS build from the CLI and two-aspect simulator check

- **Goal:** `BuildIOS` produces `Builds/iOS/Unity-iPhone.xcodeproj` from the CLI with the build number incremented and the plist flag set; a simulator build runs on an iPad (4:3) and an iPhone (19.5:9) simulator and the label sits inside the safe area on both.
- **Files:** `ProjectSettings/ProjectSettings.asset` (build number), `docs/DEVICE-TESTING.md` (anything that differed from the written steps). Screenshots go to the lead's evidence folder, not the repo.
- **Commands:** `SQ_APPLE_TEAM_ID=W727YBJNK6 "$UNITY" -batchmode -projectPath . -executeMethod Game.Editor.BuildPipeline.BuildIOS -logFile - -quit`; then `SQ_SIMULATOR=1` for `Builds/iOS-sim`; `xcodebuild` for the simulator destination; `xcrun simctl boot/install/launch/io screenshot` on one iPad and one notch iPhone simulator.
- **Verification:** exit 0; `Builds/iOS/Info.plist` contains `ITSAppUsesNonExemptEncryption` = false; `PrivacyInfo.xcprivacy` exists in the generated project; build number went up by one; two screenshots, label fully inside the safe area, iPhone readout shows a non-zero horizontal inset, cube visibly rotating between two screenshots a second apart.
- **Not part of it:** TestFlight, archive, signing fixes beyond the team id, performance measurement.
- **Status:** done 2026-10-16, verified by the lead. Simulator screenshots (iPad 10th gen 2360×1640 safe y:40; iPhone 15 Pro 2556×1179 safe x:177) in `Builds/evidence/M0/`; device Xcode project `Builds/iOS` at build 4, automatic signing for team W727YBJNK6, `ITSAppUsesNonExemptEncryption` false, privacy manifest present, compiles for arm64 unsigned. Findings and rulings:
  - Unity throws when asked to append where no build exists; `BuildIOS` now appends only when `BuildCanBeAppended` says yes. A failed build still consumes a build number (harmless).
  - `PlayerSettings.submitAnalytics` has no public API in Unity 6; set through `SerializedObject` (architect-approved pattern). Manifest aligned to the lock for `test-framework` 1.6.0.
  - One-time simulator error "Attachment 0 was created with 1 samples but 4 samples were requested": architect diagnosed a first-frame drawable created from the quality level's `antiAliasing` 0; all six quality levels now carry 4 (ARCHITECTURE §13.5). If it still appears once on device, it is disclosed to the producer as an exception to smoke item 10; per frame would be a blocker.
  - Nothing was signed, archived, installed or launched on a physical device by Claude Code; that is Martin's T6 and uses his Apple account.

### T6 — Device run (Martin) and DEVICE-TESTING reconciliation

- **Goal:** the same build runs on Martin's iPhone and iPad from Xcode (DEVICE-TESTING §3); the doc is corrected to match what actually happened.
- **Files:** `docs/DEVICE-TESTING.md`, `docs/STATUS.md`.
- **Verification:** Martin reports taps counting and the cube spinning on both devices; the lead edits DEVICE-TESTING with any step that differed; the developer confirms the privacy manifest in the Xcode project (ARCHITECTURE §10).
- **Not part of it:** TestFlight (M5).
- **Status:** iPad done 2026-10-16 (iPad A16, iPadOS 26.5.2: taps count, cube spins, no red console lines; first attempt failed on Developer Mode, recorded in DEVICE-TESTING §6; `Builds/evidence/M0/device-ipad.txt`). iPhone run pending.

## Acceptance evidence to assemble for the producer

| V1-SCOPE §12 M0 criterion | Evidence |
|---|---|
| Unity 6 LTS project with URP | `ProjectVersion.txt`, URP assets in `Settings/URP`, `GraphicsSettings.asset` pipeline reference |
| Packages installed | `manifest.json` + `packages-lock.json` matching §13.2 |
| Folder structure | `tree Assets/_Game` vs ARCHITECTURE §3 |
| Code-first bootstrap scene | `Boot.unity` in one commit, generated by `ProjectSetup`; `GameBootstrap` with no serialized fields |
| Landscape lock | `ProjectSettings.asset` orientation flags; simulator rotated screenshot stays landscape |
| Bundle ID | `ProjectSettings.asset` `applicationIdentifier` |
| iOS build script | T5 log, exit 0, `Builds/iOS/Unity-iPhone.xcodeproj` |
| Spinning placeholder + tap counter on iPhone and iPad from Xcode | T5 simulator screenshots (both ratios) + Martin's T6 report on both devices |
| DEVICE-TESTING matches what happened | T6 diff of the doc |

Smoke checklist items that apply in M0 (V1-SCOPE §14): 1 (cold start under 5 s), 8 (orientation stays landscape), 9 (nothing under notch or home indicator), 10 (no console errors over a few minutes). The rest need M1+ features.
