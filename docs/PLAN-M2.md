# PLAN-M2 — Heroes and gear

Lead's plan for V1-SCOPE §12 M2. The design is in ARCHITECTURE §15 (15.1–15.13) plus the amended §3, §6 and §9. **Amended 2026-10-16 by §15.14 (owner: no art spend):** M2 ships on the KayKit heroes and KayKit animations already in the repo, and Synty and Mixamo are off. M1 was signed off on 2026-10-16. The producer ruled on §15.11 the same day and on §15.14.8 afterwards (STATUS → Producer rulings). Statuses are kept current here. Sign-off evidence goes to `docs/evidence/M2/` and to STATUS.md.

**M2 is done when** (V1-SCOPE §12 M2 as amended by the producer): KayKit heroes, 8 hero looks (4 classes × 2 skin tones), KayKit animations, visible weapon and armor swaps, equip screen, shop UI with the 14 items, coins. Buying a weapon in the shop changes the model in the next battle, on device. The producer's rulings add three things:
- the soak passes the thresholds in STATUS (devicectl log plus probe lines, both devices);
- the iPhone gets an explicit yes for Silent mode;
- one front-facing image per look shows the skin-tone range.

## Dependency order

```
D1 Core camp model + items + tests ─► D2 party, flow and Camp screens ─► D3 soak probe + build settings + capture commands ─► D3a probe amendment
   (done 2026-10-16)
D4 KayKit weapons + shader cell tint ─► D6 Body→Look, data, generator: eight looks ─► D6a graphics-API line ─► D7 runtime gear swap ─► D8 device build, soak, evidence
D5 (Mixamo): dropped by §15.14
```

Nothing in D4–D8 waits on Martin until D8's install and soak.

## Lead's readings (assumptions written down)

- **Owner direction 2026-10-16: no art spend.** §15.14 supersedes the Synty and Mixamo parts of §15. Until D6, `BattleView` keeps spawning heroes by `classId`, and a purchase shows up only in stats. D6 brings the eight looks, and D7 brings the visible swap.
- **Provisional mesh and part names.** D1 wrote placeholder values (`TBD_D6_*`). D6 replaces them with the §15.14.2 KayKit names and renames three items (`hat_mage` → `cape_mage`, `mace_2` → `axe_2`, `pads_fighter` → `hat_fighter`). No test uses those ids.
- **Coins.** Each hero has their own coins and every hero receives a Victory's full award (producer ruling 5). Seed: 60 coins each and a pool of `{potion: 2, revive: 1}` (ruling 9c). Lost on quit until M3.
- **Smoke item 1 until M3.** The "title" is the Camp screen (ruling 9c), and the M2 evidence says so.
- **Look toggle and SoakProbe.** The Body toggle becomes a Look toggle in D6. It is in M2 builds only and is deleted in M3 with `DebugParty` (ruling 9a). SoakProbe is compiled out of the TestFlight build by the per-build `SQ_PROBE` define (ruling 9b).
- **Performance counts without the editor GUI.** D3's probe prints draw calls, SetPass calls and triangles from `ProfilerRecorder` render counters in development players. These count every pass. §15.14.5 sets the M2 budget in those terms: draws ≤ 140, SetPass ≤ 100, triangles ≤ 120k, ≤ 42 combatant renderers (from the generator log), read on a simulator development player. On the A16 iPad device build, `cpu` and `rt` must each be under 6 ms in the group shot. Simulator runs never count for fps, CPU, GPU or memory.
- **`ProjectSettings.asset` churn.** `m_Automatic` flips with whichever command ran last: `ProjectSetup.Run` writes 0, and any editor launch (the gate included) writes 1. The committed value is 1. Correction 2026-10-16: an earlier version of this line said 6a8bcda set 0; it did not. The architect ruled on 2026-10-16 (§13.6): commit 1, let the generator use the automatic list, and have `BuildIOS` assert Metal-only (task D6a). Until D6a lands, task diffs revert the line to HEAD.
- **Version string.** `bundleVersion` stays `0.2.0` through M2 and becomes `0.3.0` in the sign-off commit (§15.9).
- **Tests.** `ProgressionTests.cs` holds exactly the five §15.6 tests and is closed at five (ruling 3). The total after M2 is 13.

## Tasks

### D1 — Core camp model, items table, ProgressionTests

- **Goal:** the §15.4/§15.5 Core model.
  - `Equipment`, `HeroRecord` and `PartyRecord`.
  - `Progression.HeroStats`, `ToBattleSetup`, `Buy`, `Equip` and `AwardCoins`.
  - `HeroSpec(ClassId, Level, Name, Body = "boy", Equipment? Equipment = null)`; `BattleFactory` uses `HeroStats` for heroes.
  - `items.json` with all 14 items, and the `TableLoader` item checks: one weapon, one armor and one charm per class; weapons have a `mesh`; armor has `parts.boy` and `parts.girl`; the item `socket` field is dropped; an optional `tint`.
  - The five §15.6 tests.
- **Files:**
  - `Core/Progression/{Progression.cs, PartyRecord.cs}`;
  - `Core/Battle/{BattleSetup.cs, BattleFactory.cs}`;
  - `Core/Data/{ItemDefinition.cs, TableLoader.cs}` (plus `GameTables.cs` if it needs an item lookup);
  - `Resources/Data/items.json`;
  - `Tests/EditMode/ProgressionTests.cs`.
- **Verification:**
  - gate exit 0 with 13 tests, 13 passed, zero warnings;
  - `BattleResolverTests.cs` and `BattleResolver.cs` unchanged (`git diff` empty for both);
  - `grep -r UnityEngine Core` empty;
  - the lead reads `Buy` and `Equip` against §15.5: validate first, then mutate; `ArgumentException` on illegal calls; buying equipment also equips it.
- **Not part of it:** any Runtime or UI code; `SaveService`; tests for `Equip`, `AwardCoins` or the generator; class `variants` fields.
- **Status:** done 2026-10-16, verified by the lead.
  - The lead's own gate: 13/13 passed, zero warnings.
  - `BattleResolver.cs` and `BattleResolverTests.cs` unchanged; Core has no `UnityEngine` reference.
  - `Buy` and `Equip` read against §15.5: every check comes before any change; buying equipment also equips it; an unknown item id throws `ArgumentException` from `GameTables.Item`.
  - The developer's negative run broke `items.json` three ways and the loader named all four problems.
  
  Developer readings accepted:
  - the camp types sit in namespace `Game.Core`;
  - armor `Parts` is keyed by body;
  - provisional values are `TBD_D6_*`;
  - names for the nine items §15.4 left unnamed are placeholders until M5 (Hunter Bow, Crystal Staff, Spiked Mace, Ranger Cape, Wizard Hat, Shoulder Pads, Tough Charm, Wise Charm, Quick Charm);
  - `HeroStats` throws on a slot or class mismatch (a bug guard);
  - test 2 hard-codes three `levels.json` rows, so M4 tuning updates those lines.

### D2 — Party service, flow and Camp screens

- **Goal:**
  - `Services.Party` is seeded by `DebugParty.CreateParty()`.
  - `BattleService` starts from `Progression.ToBattleSetup`. On Victory it writes XP, coins and the consumable pool back (`xpByClass` goes). A defeat leaves the pool as it was.
  - `FlowState.Camp` gets a `CampView` laid out per §15.5: a 1600 × 900 box in the safe area; a hero column with coins; Shop and Equip tabs; five item buttons per hero in the states Buy n, Owned and greyed; three equip slots that cycle; the M2-only Body toggle; `Next battle`.
  - `GameBootstrap` opens Camp. Victory shows "+10 coins each" and goes to Camp. A Normal defeat's Continue goes to Camp; an Always Win defeat still replays.
- **Files:**
  - `Runtime/Services/{Services.cs, BattleService.cs}`, `Runtime/Battle/{DebugParty.cs, BattleView.cs}`;
  - `Runtime/Flow/{FlowState.cs, FlowController.cs}`, `Runtime/Boot/GameBootstrap.cs`;
  - `Runtime/UI/{CampView.cs, ResultView.cs}`, plus `UiFactory.cs` only if it needs a helper.
- **Verification:** scripted simulator sessions (Tools/sim-driver) on the iPad 10th gen and the iPhone 15 Pro, with screenshots of:
  - the Camp Shop and Equip tabs;
  - buying `sword_2` for the Swordsman (60 → 15 coins, Owned, Equip shows Steel Sword);
  - the Swordsman's first Attack damage number, 3 higher than before (the lead computes the expected value from the tables);
  - Victory → Camp with +10 coins on every hero;
  - a Normal defeat → Camp.
  
  Also: every control is at least 170 reference px and inside the safe area and the 1600 × 900 box on both ratios; `GameBootstrap` has no serialized fields; the player log is clean; the gate passes 13/13.
- **Not part of it:** item pictures (M4, ruling 6), confirmation dialogs, scrolling, a 3D camp, hero previews, save, story, any change to the hero models.
- **Status:** done 2026-10-16, verified by the lead.
  - The lead's own gate: 13/13, zero warnings. `GameBootstrap` has no serialized fields. ProjectSettings is unchanged: each build bumped only `buildNumber`, which was reverted.
  - The flow, service and `CampView` diffs were read against §15.5.
  - The lead looked at `d2-iphone-bought-sword.png` and `d2-ipad-camp-equip.png`: the §15.5 grid, Owned in green, unaffordable items greyed, empty equip slots disabled, and the Body toggle on the Equip tab only.
  - The developer's runs on both simulators:
    - Steel Sword takes the Swordsman from 60 to 15 coins, shows Owned, and Equip reads Steel Sword.
    - The Swordsman's damage goes from 6 to 9, exactly as the tables predict.
    - Victory shows "+10 coins each", then Camp shows 25/70/70/70.
    - A Normal defeat returns to Camp with nothing changed; an Always Win defeat replays the battle at full HP.
    - Player logs have no errors.
    - A temporary dev-only layout probe, deleted before the final build, found 0 controls under 170 px or outside the safe area or the box on either ratio.

  Developer readings accepted:
  - `Services.Party` is the `PartyRecord` itself;
  - the troll encounter is a constant in `BattleService` until M3;
  - XP is written back by slot index;
  - the pool is replaced on Victory;
  - the grid aligns to the hero rows (24 px column gap);
  - gear sits in the left column and consumables in the right;
  - the pool count goes on the name line;
  - Owned is green, distinct from the disabled grey;
  - empty slots read "starter" or "none";
  - tap SFX play on actions only;
  - Camp reopens on the Swordsman and the Shop tab;
  - no backdrop until M3;
  - the Body toggle writes `HeroRecord.Body` directly;
  - there is no difficulty corner in Camp.

  The driver header now notes that `taps:` is too slow for the 1 s difficulty corner (use three `tap:` steps).

### D3 — Soak probe, build settings, capture commands

- **Goal:**
  - `SoakProbe` per §15.8: no per-frame allocation; one `[Probe]` line every 30 s with FPS avg and min, group-shot GPU avg and max, CPU and memory. In development builds the line also carries draw calls, SetPass calls and triangles.
  - `BattleCameras.GroupShotLive`.
  - `BuildPipeline`: `enableFrameTimingStats`; stack traces `None` for Log and Warning, `ScriptOnly` for errors; the per-build `SQ_PROBE` define through `BuildPlayerOptions.extraScriptingDefines`. It is on unless `SQ_TESTFLIGHT=1`, never written to Player Settings, and `BuildIOS` logs `SQ_PROBE on` or `off` as its first line (§15.8).
  - `docs/DEVICE-TESTING.md`: a soak section with the `devicectl` install, launch-and-capture and screenshot commands.
- **Probe line format:** exactly the §15.8 one, `[Probe] t=…s flow=Camp|Battle fps avg … min … | gpu group avg … max … ms | cpu … ms | mem … MB`, with `gpu group -` when the window had no group-shot frame. `SoakProbe.cs` is under `#if SQ_PROBE || UNITY_EDITOR`; `Services.Create` adds it only under `#if SQ_PROBE`.
- **Files:** `Runtime/Services/{SoakProbe.cs, Services.cs}`, `Runtime/Battle/BattleCameras.cs`, `Editor/BuildPipeline.cs`, `docs/DEVICE-TESTING.md`.
- **Verification:**
  - A simulator build logs `[Probe]` lines every 30 s with plausible FPS and memory; GPU may read 0 on the simulator, which is noted, not a failure.
  - The positive control: `grep -rla '\[Probe\]' Builds/iOS*/Data` finds `global-metadata.dat` on a probe build. On an `SQ_TESTFLIGHT=1` build the same grep finds nothing.
  - `ProjectSettings.asset` holds no `SQ_PROBE`.
  - `git status` after both builds shows no settings drift.
  - The gate passes 13/13.
- **Not part of it:** sending anything anywhere, an on-screen FPS counter, any change to gameplay.
- **Status:** done 2026-10-16, verified by the lead.
  - The lead's own gate: 13/13, zero warnings.
  - `SoakProbe` read against §15.8: no per-frame allocation, invariant culture, the exact line format, `-` and `avg 0 max 0` as specified.
  - ProjectSettings diff: exactly `enableFrameTimingStats` and `m_StackTraceTypes` (Log and Warning to None); no `SQ_PROBE` anywhere; `buildNumber` reverted.
  - The developer's runs:
    - a probe simulator build logged `SQ_PROBE on` and wrote twelve probe lines 30 s apart over 6 min, with `flow` going Camp → Battle → Camp, fps 58–60 and memory flat at 219–225 MB;
    - an `SQ_TESTFLIGHT=1` build logged `SQ_PROBE off`; the grep found nothing in its Data or its `.app`, and found `global-metadata.dat` on the probe build.
  - `FlowController.Current` is now static (the smallest change; the brief allowed it).

  Findings sent to the architect the same day:
  1. Unity 6000.3 ships only a development simulator runtime, so every simulator build is a development player. The release `mem` value was confirmed instead on a throwaway macOS standalone player (`GetTotalReservedMemoryLong` is non-zero in release).
  2. The render counters on unchanged M1 content read 131 draws, 89 SetPass and about 111k triangles against §15.7's 280 / 12 / 60k. Either they count every pass or M1 was already over, so the budget's terms need reconciling before D8.
  3. `mem` is Unity-reserved memory, not Xcode's whole-app figure.
  4. The TestFlight build still lists the class name `SoakProbe` in Unity's script table, with no code and no literal.

### D3a — Probe amendment from the architect's D3 rulings

- **Goal:** the §15.8 probe change of 2026-10-16:
  - `mem` becomes `ProfilerRecorder(ProfilerCategory.Memory, "System Used Memory")`. It is started in `Awake` in every probe build, printed as `LastValue` in MB and disposed in `OnDestroy`. It prints `mem -` if the recorder is invalid or reads 0.
  - `GetTotalReservedMemoryLong` stays as a second field, `unity … MB`.
  - A new `rt` field is the average `cpuRenderThreadFrameTime` over group-shot frames (`-` when there are none).
  - Line format: `| cpu 4.1 rt 3.2 ms | mem 212.4 MB unity 140.2 MB`.
  - DEVICE-TESTING §3b's probe-line key matches.
- **Files:** `Runtime/Services/SoakProbe.cs`, `docs/DEVICE-TESTING.md` (§3b only).
- **Verification:**
  - The gate passes 13/13.
  - A simulator probe build prints lines in the new format, with a non-zero `mem` and `unity` and an `rt` value or `-`.
  - `git status` shows only these two files.
- **Not part of it:** any other probe change; the render budget itself (that is the architect's §15.7 text).
- **Status:** done 2026-10-16, verified by the lead (own gate 13/13, zero warnings; diff read; only the two files changed).
  - Simulator probe lines in the new format: `cpu 1.6 rt 1.0 ms | mem 218.0 MB unity 225.2 MB`; the counters are unchanged.
  - Developer readings accepted: `rt -` covers both "no group shot" and "zero timings"; `cpu - rt - ms` when there are no samples; `mem - unity … MB` when the recorder is invalid. The §3b sentence now lists every simulator measurement that does not count.
  - **Lead's reading for D8:** `cpu` is averaged over the whole 30 s window, not only group-shot frames. §15.13's "cpu under 6 ms in the group shot" is judged on the `flow=Battle` lines, where the group shot is most of the frames. That is stricter than the rule as written, not looser. If a device reading lands within 10 % of 6 ms, the architect decides whether to make `cpu` group-only (one line).
  - On the simulator `mem` reads below `unity` (184–218 against 219–225 MB). Simulator memory is not evidence, but it makes Martin's Xcode memory reading at Needs Martin 13 a real test.

### D4 — KayKit weapons and the shader cell tint

- **Goal:**
  - Copy `sword_2handed_color.fbx`, `axe_1handed.fbx`, `axe_2handed.fbx` and `wand.fbx` from `KayKit_Adventurers_2.0_FREE/Assets/fbx(unity)/` into `Assets/ThirdParty/KayKit/Weapons/`. They import through the existing `KayKitPostprocessor` weapon rule, with no rule change and `GetVersion` staying 2.
  - `Toon.shader` gains the §15.14.3 palette-cell tint: `_CellA`/`_CellAMul`/`_CellB`/`_CellBMul` in the one `UnityPerMaterial` block, `CellTint` applied in `ToonForward` only, no texture sample added.
- **Files:** the four FBX plus Unity `.meta`; `Assets/_Game/Art/Shaders/Toon.shader`; generated materials only where the new properties appear.
- **Verification:**
  - The four `.meta` files show the weapon rule (no animation, `globalScale` 0.70, no materials).
  - `ProjectSetup.Run` passes the shader check.
  - The regeneration diff touches only the shader and the new property entries in existing materials, and a second run is byte-identical.
  - A simulator battle looks exactly like M1: no cell is set yet, so nothing changes colour.
  - The gate passes 13/13.
- **Not part of it:** materials that set a cell (D6), data changes, other KayKit files.
- **Status:** done 2026-10-16, verified by the lead.
  - The lead's own `ProjectSetup.Run`: shader ok (four passes); all 18 generated assets unchanged after the developer's run. The only drift was `m_Automatic`, reverted (see readings).
  - The shader diff was read against §15.14.3: four properties in `UnityPerMaterial`; `CellTint` only in `ToonForward`; no texture sample added.
  - The six toon materials gained exactly the four default property lines.
  - The developer's checks:
    - the four new `.meta` files are byte-identical to `sword_1handed.fbx.meta` apart from the guid;
    - `cmp` shows the FBX match the pack;
    - the gate passes 13/13;
    - the simulator build compiled all four passes for Metal;
    - a simulator group shot matches D2's, with counters 131 / 89 / 111,471, exactly M1's.
  - Reading accepted: `CellTint` sits in ToonForward's own program block, so it appears only there.

### D5 — Mixamo import

- **Status:** dropped 2026-10-16 (§15.14.5). The KayKit clips are already imported; the Fighter's two new `clips.json` entries go into D6.

### D6 — Body → Look, data, and the eight looks

- **Goal:**
  - The Core rename per §15.14.4: `HeroRecord.Look`; `HeroSpec(..., string Look = "", ...)`; `Bodies` deleted. `ItemDefinition.Parts` becomes a list, and `CellTint` replaces `Tint`. The `TableLoader` checks per §15.14.4, including ruling 7. `ProgressionTests` changes one fixture line.
  - `classes.json`: `looks`, `skinCells`, `skinTones`, `skinReference`; the Mage's `wand`; the Fighter's `axe_1handed` and its clips.
  - `items.json` per §15.14.2, including `bow_2`'s `cellTint` and the three renamed ids.
  - `clips.json` gains `Melee_2H_Idle` (loop) and `Melee_1H_Attack_Chop` (impact from `ImpactFinder`).
  - The generator writes:
    - eight `{classId}_{look}` materials and `bow_2.mat`;
    - eight `{classId}_{look}` prefabs, each with the armor parts inactive and `starter` plus item-id weapon children under `handslot.r`.
    
    It deletes the four M1 class prefabs and materials, remaps the Fighter's override, checks heights with armor on and off, and logs active renderer counts.
  - `DebugParty` seeds Swordsman dark, Archer medium, Mage light and Fighter dark. `BattleView` spawns `{ClassId}_{Look}`. `CampView`'s toggle becomes "Look: …".
  - `VariantCapture.Run` writes the eight `variant-*.tga` plus `armor-swordsman.tga`.
- **Files:**
  - Core: `PartyRecord.cs`, `BattleSetup.cs`, `Progression.cs`, `ItemDefinition.cs`, `ClassDefinition.cs`, `TableLoader.cs`;
  - Tests: `ProgressionTests.cs`, one line;
  - Data: `classes.json`, `items.json`, `clips.json`;
  - Editor: `CombatantGenerator.cs` (or a new hero generator class, the developer's call), `VariantCapture.cs`;
  - Runtime: `DebugParty.cs`, `BattleView.cs`, `CampView.cs`;
  - generated materials, prefabs and override controllers, and the deletions.
- **Verification:**
  - The gate passes 13/13. `BattleResolver.cs` and `BattleResolverTests.cs` are unchanged. The `ProgressionTests` diff is one line. A grep finds no `Body`/`Bodies` in Core or Runtime.
  - `ProjectSetup.Run` twice leaves `git status` clean.
  - The heights log is 1.2–2.4 m with armor on and off, and there are at most 42 combatant renderers.
  - `Combatant.controller`'s diff is empty.
  - Scripted simulator battles on both ratios, one each side of the Look toggle, so all eight prefabs spawn with no missing-prefab error. The Fighter's axe Attack and Skill fire `OnImpact`.
  - Framing per §15.14.6: feet ≥ 27 %, head ≤ 86 %, no combatant under a card.
  - Nine captures converted with `sips`: three tones, faces visible, and only skin differs between a class's two looks.
- **Not part of it:** the runtime gear swap (D7); hair tints; a stage; new enemies; any Synty, Mixamo or Sidekick code.
- **Status:** not started. Waits on D4.

### D6a — Stop the graphics-API line churning (ARCHITECTURE §13.6 note, 2026-10-16)

- **Goal:**
  - `BuildPipeline.ApplyPlayerSettings` calls `PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.iOS, true)` and drops the explicit `SetGraphicsAPIs` call.
  - `BuildIOS` checks that `PlayerSettings.GetGraphicsAPIs(BuildTarget.iOS)` is exactly `[Metal]` before building, and fails with a message naming what it found.
- **Files:** `Editor/BuildPipeline.cs`.
- **Verification:**
  - `ProjectSetup.Run` followed by the gate leaves `m_Automatic: 1` and `git status` clean.
  - A simulator build passes the Metal check.
  - The gate passes 13/13.
- **Not part of it:** any other player setting.
- **Status:** queued after D6. The developer works one task at a time, and D6 is running.

### D7 — Runtime gear swap

- **Goal:** `HeroAppearance.Apply` per §15.14.2.
  - The weapon child named `Equipped.Weapon ?? "starter"` is active and the other inactive.
  - The class armor parts are active exactly when that armor is equipped.
  - It only calls `SetActive`, and `CombatantActor.Spawn` calls it before caching renderers.
- **Files:** `Runtime/Battle/{HeroAppearance.cs, CombatantActor.cs}`.
- **Verification:** scripted simulator sessions on both ratios:
  - buy `sword_2` → Next battle → the screenshot shows the bigger sword (the M2 criterion on the simulator);
  - buying `bow_2` shows the gold bow;
  - each class's armor, bought once (the developer may raise the seed coins in a throwaway local build, never committed), is visible from behind in the group shot;
  - the hit flash covers the active weapon and armor, not the hidden ones;
  - no `Instantiate` in `HeroAppearance`;
  - the gate passes 13/13.
- **Not part of it:** charm visuals (charms are not visible, V1-SCOPE §6), item pictures (M4).
- **Status:** waits on D6.

### D8 — Performance check, device build, soak, evidence

- **Goal:**
  - Group-shot counters on a simulator development player against the §15.14.5 budget (draws ≤ 140, SetPass ≤ 100, tris ≤ 120k), with the all-armor party.
  - A release device build for the soak (`SQ_PROBE on`).
  - The soak checklist and fill-in block for Martin; the lead captures each device's log with `devicectl` and takes screenshots of the purchase and the next battle.
  - Evidence in `docs/evidence/M2/`.
- **Files:** none beyond tuning numbers a measurement forces (each one written into this plan); `docs/evidence/M2/*`.
- **Verification:**
  - The producer's soak thresholds (STATUS, ruling 4) on both devices.
  - On the A16 iPad, `gpu`, `cpu` and `rt` each under 6 ms in the group shot, with `cpu` judged on `flow=Battle` lines (D3a reading).
  - Martin's Xcode memory reading at Needs Martin 13 is within about 10 % of the probe's `mem`.
  - devicectl screenshots of the purchase and the new weapon on both devices.
  - Martin's warm-or-hot answer and his Silent mode yes.
- **Not part of it:** render-scale changes (M5), new content.
- **Status:** waits on D7.

## Needs Martin for M2 (also STATUS)

1. One Xcode Run per device on the M2 build, with one Xcode memory reading at Camp.
2. The soak: play; warm or hot; Silent mode yes or no; anything wrong.

Items 11 (Synty) and 12 (Mixamo) were withdrawn on 2026-10-16: nothing is bought or downloaded for M2.

## Acceptance evidence to assemble for the producer

- Criteria table, as for M1.
- Gate 13/13.
- Regeneration byte-identical.
- Third-party folder audit (§15.14.6): only KayKit under `Assets/ThirdParty/`, and no third-party script.
- Camp screenshots on both ratios.
- Eight front captures plus `armor-swordsman` (skin tones, faces).
- Simulator and devicectl screenshots of buy → next battle with the new weapon.
- Probe-line excerpts per device against the thresholds.
- The compliance line: there are no Synty or Mixamo files, and `git remote -v` is empty.
- Budget: $0 for M2 (the Synty row closed unpaid).
- Smoke items 1 (Camp as title), 3, 5, 8, 9 and 10, plus item 7 (Silent mode yes).
