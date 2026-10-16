# PLAN-M1 — Battle slice

Lead's plan for V1-SCOPE §12 M1. Design basis: ARCHITECTURE §5 and §6 (amended 2026-10-16), §13.7, §13.9 and the new §14 (14.1–14.10). M0 was signed off 2026-10-16. Statuses are kept current here; sign-off evidence goes to `docs/evidence/M1/` and STATUS.md.

**M1 done when:** 4 placeholder heroes vs 1 troll grunt; all four commands, turn order, the full hit recipe, camera cuts, damage numbers, victory and defeat screens, both difficulty behaviours; a kid can win and lose a battle on device and the hit looks crisp on both iPad and iPhone framing. Producer ruling 2026-10-16 adds a hard requirement: a timed 10-minute soak on both devices with the Xcode console attached (§14.8 protocol).

## Dependency order

```
D1 KayKit import ─► D2 Core + tables + tests ─► D3 toon shader + generator ─► D4 runtime skeleton (stage, cameras, flow)
   ─► D5 battle UI + difficulty + results ─► D6 presenter + hit recipe ─► D7 audio ─► D8 device build + soak + evidence
L1 (lead, any time after D4): scripted simulator input for verification, outside the repo.
```

D2 does not depend on D1 technically, but the developer works one task at a time and the import is where surprises live (clip names, scale), so it goes first.

## Lead's readings (assumptions written down)

- **Impact fractions without a mouse.** §14.2 says the developer "scrubs each clip once in the editor". No one in this loop opens the editor GUI, so the developer may write a small editor utility (`Game.Editor.Import.ImpactFinder`, run by `-executeMethod`) that samples each Attack/Skill clip on the imported rig and prints the normalized time where the striking hand's speed peaks, then records those numbers in `clips.json`. The number stays the artifact; the utility is a tool, not a feature, and is allowed to stay in `Editor/`. Values are confirmed by eye on the device run and adjusted in data, never code.
- **Verifying taps.** Solved on day one by L1 (see its status): an XCUITest driver scripts taps and screenshots on the simulator from the terminal, so D5 and D6 are verified by scripted battles, not only by screenshots and Martin's device run. This Xcode (27) ships no standalone Simulator GUI app, so AppleScript clicking was never an option.
- **Version string.** `bundleVersion` stays `0.1.0` through M1 and becomes `0.2.0` in the sign-off commit (PLAN-M0 reading, §14.9).
- **Producer rulings 2026-10-16 (both accepted):** no misses in V1 and the miss SFX struck from V1-SCOPE §7; no projectile mesh required for Archer or Mage in V1 (BACKLOG row; one revisit at M4 sign-off).
- **Weakness placeholder.** The grunt's weakness is `blunt` in M1 so a plain Fighter attack shows BigStagger on device (§14.2). The story writer's real weakness replaces it in M4 as data.
- **Default difficulty** is Always Win (§14.5). Both behaviours are exercised in the soak.

## Tasks

### D1 — KayKit import

- **Goal:** the KayKit files §14.1 names are copied into `Assets/ThirdParty/KayKit/`, imported headless with `KayKitPostprocessor` rules (Generic rig, no materials, 512 ASTC textures, `OnImpact` events and loop flags from `clips.json`), and every clip named in the §14.1 table resolves.
- **Files:** `Assets/ThirdParty/KayKit/{Characters,Weapons,Animations,Textures}/…` + `License-Adventurers.txt`, `License-Animations.txt` (copied, never edited) and their `.meta`; `Assets/_Game/Scripts/Editor/Import/KayKitPostprocessor.cs`; `Assets/_Game/Scripts/Editor/Import/KayKitImportCheck.cs` (`-executeMethod`, lists every clip under `Animations/`, fails naming any §14.1 clip that is missing, prints each character's renderer-bounds height); `Assets/_Game/Resources/Data/clips.json` (loop flags; provisional `impact` values from §14.2, refined in D6); optionally `Editor/Import/ImpactFinder.cs`.
- **Verification:** headless open then `KayKitImportCheck` exit 0 with all clips found and heights 1.2–2.4 m; `.meta` of a character shows `animationType: 2` (Generic), `materialImportMode: 0`; a texture `.meta` shows max size 512 and ASTC for iOS; compile gate exit 0; `git status` shows only the files above. Lead reads the postprocessor against §14.1.
- **Not part of it:** materials, prefabs, controllers (D3); any `.cs` under `Runtime/` or `Core/`; glTF/OBJ/Samples/Rig_Large files.
- **Status:** done 2026-10-16, verified by the lead (own run of `KayKitImportCheck`: 5 rig files, 78 clips, all 22 table clips found with loop flags and one `OnImpact` each; heights Barbarian 1.80, Knight 1.90, Mage 1.98, Ranger 1.72, Mannequin 1.63 m at globalScale 0.70; zero compile errors). `ImpactFinder` supplied the provisional impact fractions (melee from peak hand or foot speed; ranged at release plus a 0.15 beat), to be confirmed by eye in D6.

### D2 — Core battle model, data tables, Progression, tests

- **Goal:** `Game.Core` per §14.3 (`Data/`, `Battle/`, `Progression/`) with `ITableSource`, `TableLoader` and validation, `BattleFactory`, `BattleResolver.Start/Resolve/EnemyCommand`, `EnemyBrain`, `SeededRandom`, the full event list, `Progression`; the four hand-written tables per §14.2; `BattleResolverTests` with the eight §14.7 cases.
- **Files:** `Assets/_Game/Scripts/Core/Data/*.cs`, `Core/Battle/*.cs`, `Core/Progression/Progression.cs`; `Assets/_Game/Resources/Data/{classes,enemies,levels,items}.json`; `Assets/_Game/Tests/EditMode/BattleResolverTests.cs` (+ a tiny file-backed `ITableSource` inside the test file if a test needs real tables).
- **Verification:** `-runTests` exit 0 with exactly 8 tests passed (`Builds/test-results.xml` testcasecount=8); `grep -r UnityEngine Assets/_Game/Scripts/Core` empty; nullable warnings-as-errors clean in Core; the tables parse and `TableLoader` validates them (a test or the D3 generator run proves it); damage/turn/Defend/cooldown rules match §14.3 by reading the code. Lead reads the diff.
- **Not part of it:** thrower/shaman/boss behaviours (ids reserved only), shop logic, saving, any MonoBehaviour, more than eight tests.
- **Status:** not started.

### D3 — Toon shader and combatant generator

- **Goal:** `StoryQuest/Toon` per §13.7 (four passes, `_FlashAmount`, SRP-Batcher-compatible CBUFFER); `CombatantGenerator.Generate()` per §14.4 producing materials, the shared `Combatant.controller`, one override controller per class and enemy, and the five prefabs; `ProjectSetup.Run` gains `ToonShaderCheck` and the generator step; `Placeholder.mat` step removed.
- **Files:** `Assets/_Game/Art/Shaders/Toon.shader`; `Assets/_Game/Scripts/Editor/CombatantGenerator.cs`; `ProjectSetup.cs`; generated: `Resources/Materials/Toon/*.mat`, `Resources/Animators/Combatant.controller`, `Resources/Animators/*.overrideController`, `Resources/Prefabs/Combatants/*.prefab` (+ `.meta`); `Resources/Materials/Placeholder.mat` deleted.
- **Verification:** `ProjectSetup.Run` exit 0 with shader found; a second generator run leaves `git status` clean (byte-identical outputs); prefab YAML shows no Collider/Rigidbody, the override controller maps all nine states to the data's clip names, `BigStagger` speed 0.6, one-shot transitions per §14.4; heights assert; compile gate exit 0. Lead reads the shader against §13.7 and the generator against §14.4.
- **Not part of it:** placing anything in the scene; outline tuning by eye (device run); any Runtime code.
- **Status:** blocked on D2.

### D4 — Runtime skeleton: flow, services, stage, cameras, bootstrap cleanup

- **Goal:** `FlowController`/`FlowState`/`IView`, `Services` (tables via `ResourcesTableSource`, `BattleService` without presentation yet, `DebugSettings`), `DebugParty`, `BattleStage` (ground, slots, ambient), `CombatantActor` spawning the five prefabs into their slots, `BattleCameras` (Brain on the M0 camera, `CM_Group` with group framing, `CM_Action`, impulse listener), and `GameBootstrap` cleanup (cube, `Spinner`, `TapCounter`, `TapLabel` deleted) so that launching the app shows the five characters idling in the group shot.
- **Files:** `Runtime/Flow/*.cs`, `Runtime/Services/*.cs`, `Runtime/Battle/{BattleStage,CombatantActor,BattleCameras,DebugParty}.cs`, `Runtime/Boot/GameBootstrap.cs`; deletions of `Runtime/Boot/Spinner.cs`, `TapCounter.cs`.
- **Verification:** simulator build; screenshots on the iPad (4:3) and iPhone 15 Pro (19.5:9) simulators show all five characters inside the frame, toon-shaded (not pink), ground and sky; player log has no errors or exceptions; `GameBootstrap` still has no serialized fields; compile gate exit 0. Lead looks at both screenshots.
- **Not part of it:** HUD, command panel, hit recipe, audio.
- **Status:** blocked on D3.

### D5 — Battle UI, difficulty and result screens

- **Goal:** `UiFactory` (170 px minimum enforced), `BattleHud` (party cards, enemy card, turn banner, active ring), `CommandPanel` (Attack/Skill/Item/Defend with cooldown and item sub-row, hero targeting via cards, auto-target the single enemy), `DebugCorner` toggling `DebugSettings.Difficulty` with a toast, `ResultView` for Victory / Always-Win defeat / Normal defeat per §14.5, and `BattleService` applying the difficulty rule and restarting battles. HP bars update from events (no animation yet: the presenter in D6 will drive timing; in D5 a minimal event applier updates HUD values immediately).
- **Files:** `Runtime/UI/{UiFactory,BattleHud,CommandPanel,ResultView,DebugCorner}.cs`, `Runtime/Services/{BattleService,DebugSettings}.cs`, `Runtime/Battle/BattleView.cs`.
- **Verification:** simulator screenshots at both ratios at the first hero turn: panel bottom-left, cards bottom-right, enemy card top-centre, banner top-left, all inside the safe area, controls ≥ 170 reference px; if L1 works, a scripted tap sequence wins a battle (Victory screen with XP/coins) and loses one on each difficulty (the two defeat screens); compile gate exit 0. Lead reads the sizing and the difficulty rule.
- **Not part of it:** animations, camera cuts, numbers, particles, sounds (D6/D7); multi-enemy target picker (M4); settings screen (M3).
- **Status:** blocked on D4.

### D6 — Battle presenter and the hit recipe

- **Goal:** `BattlePresenter` playing event batches sequentially per §14.4 steps 1–11 (cut, clip, `OnImpact` wait, 80/100 ms unscaled freeze, Hit/BigStagger/Defeat, root knockback 0.35/0.5 m, 2-frame `MaterialPropertyBlock` flash, `HitFx` burst by damage type, impulse 1.0/1.6, pooled damage numbers, blend back), heals/defends/victory/defeat presentations, HUD timing driven by the presenter, `AnimationEventRelay`; final `impact` values in `clips.json`.
- **Files:** `Runtime/Battle/{BattlePresenter,AnimationEventRelay,HitFx,DamageNumberPool}.cs`, small edits to `BattleHud`/`BattleService` for timing, `Resources/Data/clips.json`.
- **Verification:** compile gate exit 0; with L1, a scripted Attack on the simulator yields screenshots showing the action camera, the flash frame, a damage number and the knocked-back troll, and a Fighter attack shows the bigger number and stagger; player log clean; no `Rigidbody`/`Collider` on combatants; `_FlashAmount` set only through a property block; no per-frame allocation in `Update` paths by reading the code (Profiler confirmation on device in D8). Lead reads the recipe order against §14.4 and the numbers.
- **Not part of it:** sounds (D7), projectiles, Timeline, tuning stats.
- **Status:** blocked on D5.

### D7 — Audio: AVAudioSession plugin, AudioService, placeholder SFX

- **Goal:** `Assets/Plugins/iOS/SQAudioSession.mm` per §13.9; `AudioService` with three sources and the `DllImport` call in `Awake` and `OnApplicationFocus(true)`; `PlaceholderSfx` generating hit, skill, victory, defeat and menu-tap clips with `AudioClip.Create`; presenter and UI call `Play`.
- **Files:** `Assets/Plugins/iOS/SQAudioSession.mm` (+ `.meta`), `Runtime/Services/AudioService.cs`, `Runtime/Services/PlaceholderSfx.cs`, one-line call sites in `BattlePresenter`, `CommandPanel`, `ResultView`.
- **Verification:** compile gate exit 0; the device Xcode project contains `SQAudioSession.mm` in the Unity-iPhone target; simulator run plays audio (log line on first `Play`); Martin's silent-switch check in D8. Lead reads the plugin against §13.9.
- **Not part of it:** music, narration, mixer, the real CC0 SFX set (M3).
- **Status:** blocked on D6.

### D8 — Device build, soak, framing evidence

- **Goal:** build 5+ on both devices; Martin runs the §14.8 soak protocol on each (timed 10 minutes, console attached, win and lose on both difficulties, every command, ≥ 40 hits, silent switch on the iPhone); two framing screenshots per device (group shot with four heroes, one action shot); evidence assembled; `bundleVersion` → `0.2.0` in the sign-off commit.
- **Files:** `ProjectSettings/ProjectSettings.asset` (build number, version), `docs/DEVICE-TESTING.md` for anything that differed, `docs/evidence/M1/`.
- **Verification:** soak report with start/end times, zero red lines (one-off render-pass message disclosed if it appears once, per frame blocks), FPS steady at 60 with no drift, memory flat; Xcode GPU gauge for the group shot under 6 ms on the iPad (§14.8 headroom argument); screenshots show all five framed and the panel inside the safe area on both devices.
- **Not part of it:** TestFlight, icon, performance work beyond the §14.8 levers.
- **Status:** blocked on D7 and Needs Martin.

### L1 — Lead: scripted simulator input (not in the repo)

- **Goal:** tap the running app on the iOS Simulator from the terminal so D5/D6 can be verified without a human. Try `idb` first; fall back to `osascript` clicks with Martin's one-time Accessibility grant.
- **Verification:** a tap on the Attack button registers in the player log / a screenshot.
- **Status:** done 2026-10-16. Meta's idb installed cleanly but its injected touches never reached Unity (tested on the iOS 17.2 simulator in both coordinate conventions, with and without a GUI) and was removed. Working route: an XCUITest driver bundle (xcodegen spec + one Swift file) that activates the app by bundle id and runs a `tap/taps/wait/shot/relaunch` script from an environment variable; proven by driving the M0 tap counter to 3 from the terminal (`Builds/evidence/M0/l1-driver-taps3.png`). Lives in `Builds/tools/sqdriver/` until the architect records a home under `Tools/` (asked 2026-10-16). Side finding: Unity's simulator build was x86_64-only (`iOSSimulatorArchitecture: 0`) and cannot install on iOS 26/27 simulators; the arm64 fix goes into `BuildIOS` in D3.

## Needs Martin for M1 (also in STATUS.md)

1. Nothing to download or buy: both KayKit packs are on disk (2026-10-16).
2. The timed 10-minute soak on each device with the Xcode console attached, when D8 hands over the build (§14.8 protocol, the lead sends the checklist).
3. During the iPhone soak: silent switch on, a hit is still audible.
4. Eyes on two framing screenshots per device, which the lead names.
5. Only if L1's first route fails: grant Accessibility to the terminal app once so scripted clicks can drive the simulator.

## Acceptance evidence to assemble for the producer

| V1-SCOPE §12 M1 criterion | Evidence |
|---|---|
| 4 placeholder heroes vs 1 troll grunt | D4/D5 screenshots both ratios; `classes.json`/`enemies.json` |
| All four commands | D5 panel screenshots; L1 scripted session or Martin's soak using each command; tests 1, 4, 7 |
| Turn order by speed | test 3; turn banner sequence in a session |
| Full hit recipe, camera cuts, damage numbers | D6 screenshots (action cam, flash, number, knockback, stagger); `BattlePresenter` read against §14.4 |
| Victory and defeat screens | D5 result screenshots; smoke item 3 (coins and XP increase) |
| Both difficulty behaviours | Always Win restart with full HP; Normal retreat screen with coins lost; smoke item 4 |
| Win and lose on device; hit looks crisp on both framings | Martin's soak report and framing screenshots per device |
| 10-minute soak (producer rule) | Soak report per device: times, zero red lines, FPS, memory |
| Tests (V1-SCOPE §13) | `test-results.xml` testcasecount=8, all passed |
| Architect's §14.10 review list | Lead runs each check and records the output |

Smoke checklist (V1-SCOPE §14) items in M1: 3, 4, 7 (silent-switch part only), 8, 9, 10. Items 2, 5, 6 and the narration toggle wait for M2/M3.
