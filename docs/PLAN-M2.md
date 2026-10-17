# PLAN-M2 — Heroes and gear

Lead's plan for V1-SCOPE §12 M2. The design is in ARCHITECTURE §15 (15.1–15.13) plus the amended §3, §6 and §9. M1 was signed off on 2026-10-16. The producer ruled on §15.11 the same day (STATUS → Producer rulings). Statuses are kept current here. Sign-off evidence goes to `docs/evidence/M2/` and to STATUS.md.

**M2 is done when:** Synty bases, 8 hero variants, Mixamo retargeting, weapon and armor sockets, equip screen, shop UI with the 14 items, coins. Buying a weapon in the shop changes the model in the next battle, on device. The producer's rulings add three things:
- the soak passes the thresholds in STATUS (devicectl log plus probe lines, both devices);
- the iPhone gets an explicit yes for Silent mode;
- one front-facing image per variant shows the skin-tone range.

## Dependency order

```
D1 Core camp model + items + tests ─► D2 party, flow and Camp screens ─► D3 soak probe + build settings + capture commands ─► D3a probe amendment
   (D1–D3 need no Synty or Mixamo files and start now)
D4 Synty extract + postprocessor ──┐   (needs Needs Martin 11)
D5 Mixamo import ──────────────────┴─► D6 hero generator, 8 variants ─► D7 runtime gear swap ─► D8 device build, soak, evidence
                                        (D5 needs Needs Martin 12)
```

D4 and D5 do not depend on each other. Whichever download arrives first goes first.

## Lead's readings (assumptions written down)

- **Work before the art arrives.** D1–D3 use the M1 KayKit prefabs. Until D6, `BattleView` keeps spawning heroes by `classId`. The Body toggle is built in D2 but changes nothing on screen until D6. In D1–D5 a purchase shows up only in stats (a bigger damage number), not on the model. That is enough to verify the shop and the flow before Synty lands.
- **Provisional mesh and part names.** D1 writes `items.json` with all 14 items. Weapon `mesh` and armor `parts` values are placeholders, replaced in D6 from the facts D4 reports. Nothing reads them before D7. `TableLoader`'s class checks (`variants`, the four socket names) arrive with the data in D6; adding them in D1 would fail M1's `classes.json`.
- **Coins.** Each hero has their own coins and every hero receives a Victory's full award (producer ruling 5). Seed: 60 coins each and a pool of `{potion: 2, revive: 1}` (ruling 9c). Lost on quit until M3.
- **Smoke item 1 until M3.** The "title" is the Camp screen (ruling 9c), and the M2 evidence says so.
- **Body toggle and SoakProbe.** The Body toggle is in M2 builds only and is deleted in M3 with `DebugParty`. SoakProbe is compiled out of the TestFlight build, using the mechanism the architect records in §15.8 (ruling 9b).
- **Performance counts without the editor GUI.** D3's probe prints draw calls, SetPass calls and triangles from `ProfilerRecorder` render counters in development players. These count every pass. The architect restated the §15.7 budget in those terms on 2026-10-16: draws ≤ 300, SetPass ≤ 200, triangles ≤ 180k, ≤ 90 skinned renderers, read on a simulator development player. On the A16 iPad device build, `cpu` and `rt` must each be under 6 ms in the group shot. Simulator runs never count for fps, CPU, GPU or memory.
- **`ProjectSettings.asset` churn.** M1 showed a simulator build flipping `m_Automatic` and `iOSSimulatorArchitecture`. The sign-off commit 6a8bcda set `m_Automatic: 0` from the generator. A task diff that changes either line is reverted or explained, never committed silently.
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
- **Status:** not started.

### D4 — Synty extraction and import (needs Needs Martin 11)

- **Goal:**
  - `Tools/synty/extract.sh` unpacks the downloaded `.unitypackage` into `Builds/synty-extract/` and writes `Tools/synty/package-contents.txt`. It copies only the files listed in `Tools/synty/keep.txt` into `Assets/ThirdParty/Synty/{Characters,Weapons,Textures}/`: the `asset` files, never Synty's `.meta`.
  - `SyntyPostprocessor` per §15.7.
  - An import check reports the four §15.1 facts (a)–(d) in the task notes before any hero code.
- **Files:** `Tools/synty/{extract.sh, keep.txt, package-contents.txt}`; `Assets/ThirdParty/Synty/**` plus Unity `.meta`; `Editor/Import/{SyntyPostprocessor.cs, SyntyImportCheck.cs}`; `.gitignore` if `Builds/synty-extract/` needs a line.
- **Verification:**
  - A second `extract.sh` run changes nothing.
  - No Synty `.cs`, `.unity`, `.prefab`, `.mat` or `.shader` file exists anywhere under `Assets/`.
  - The character `.meta` shows Human, no materials and read/write off; textures are 1024 and ASTC 6×6.
  - `avatar.isHuman && isValid`; the four facts are in the notes.
  - The gate passes 13/13.
- **Not part of it:** prefabs, materials, the baker (D6).
- **Status:** waiting on Martin's purchase.

### D5 — Mixamo import (needs Needs Martin 12)

- **Goal:** the 15 clips and Y Bot are copied into `Assets/ThirdParty/Mixamo/`.
  - `MixamoPostprocessor` per §15.2 handles the Human import: each clip is renamed to its file name, loop flags and `OnImpact` come from `clips.json`, and the root is baked into pose.
  - `ImpactFinder` gets Humanoid sampling, `CombatantGenerator.LoadClips` also scans Mixamo, and the avatar check asserts `isHuman && isValid`.
- **Files:** `Assets/ThirdParty/Mixamo/*.fbx` plus `.meta`, `Editor/Import/{MixamoPostprocessor.cs, ImpactFinder.cs}`, the import check, `Editor/CombatantGenerator.cs` (`LoadClips` only), `Resources/Data/clips.json`.
- **Verification:**
  - All 15 clip names resolve, or any substitutes are listed in the status.
  - Every avatar is valid.
  - Each Attack and Skill clip has exactly one `OnImpact` at the `ImpactFinder` time.
  - Loop flags are on the four looping clips.
  - The gate passes 13/13.
- **Not part of it:** hero prefabs and override-controller remapping (D6).
- **Status:** waiting on Martin's download.

### D6 — Hero generator and the eight variants

- **Goal:**
  - `classes.json` gains `model`, the Synty `weaponSocket`, a starter `weaponMesh`, `variants.boy/girl.parts`, and `colours` (bake branch only). The Fighter becomes a mace fighter. Each variant gets a `tone`, and the top level of `classes.json` gets `skinTones`. The starting assignment is the §15.3 one, and `TableLoader` enforces ruling 7's rule.
  - `items.json` gets real meshes and parts, and `TableLoader` gets the class checks.
  - `HeroGenerator` writes the eight `{classId}_{body}` prefabs with sockets, inactive armor parts and both weapons. It adds `HeroTextureBaker` if fact (b) says colour lives in the masks.
  - The hero override controllers map the §15.2 clips.
  - The M1 hero prefabs are deleted and KayKit is trimmed per §15.8.
  - `BattleView` spawns `{ClassId}_{Body}`; `CombatantActor` looks up bones through the Humanoid avatar.
  - `Game.Editor.VariantCapture.Run` (batch mode, without `-nographics`) writes one front-facing TGA per variant to `Builds/evidence/M2/`, which the lead converts with `sips`.
- **Files:**
  - `Editor/{HeroGenerator.cs, HeroTextureBaker.cs?, VariantCapture.cs, CombatantGenerator.cs}`;
  - `Resources/Data/{classes.json, items.json}`, `Core/Data/{ClassDefinition.cs, TableLoader.cs}`;
  - generated prefabs, materials, textures and override controllers;
  - `Runtime/Battle/{BattleView.cs, CombatantActor.cs}`;
  - KayKit deletions.
- **Verification:**
  - `ProjectSetup.Run` twice leaves `git status` clean.
  - The heights log is 1.2–2.4 m for every variant, with and without armor.
  - Eight prefabs, each with four sockets, no collider and no rigidbody.
  - `Combatant.controller` diff is empty.
  - Scripted simulator battles on both ratios, one each side of the Body toggle, so all eight variants are seen.
  - Framing per §15.13: feet ≥ 27 %, head ≤ 86 %, no combatant under a card.
  - Eight front captures cover at least three tones, each on at least two variants.
  - The gate passes 13/13.
- **Not part of it:** runtime gear swapping (D7), a stage, new enemies.
- **Status:** waits on D4 and D5.

### D7 — Runtime gear swap

- **Goal:** `HeroAppearance.Apply` per §15.4. It only calls `SetActive`, switching the weapon under `weaponSocket` and armor by part group. `CombatantActor.Spawn` calls it before caching renderers.
- **Files:** `Runtime/Battle/{HeroAppearance.cs, CombatantActor.cs}`.
- **Verification:** scripted simulator session on both ratios:
  - buy `sword_2` → Next battle → the screenshot shows the new sword (the M2 criterion on the simulator);
  - a separate session buys an armor item, which then shows in battle;
  - the hit flash covers the new weapon and armor, not the hidden ones;
  - no `Instantiate` in `HeroAppearance`;
  - the gate passes 13/13.
- **Not part of it:** charm visuals (charms are not visible, V1-SCOPE §6), item pictures.
- **Status:** waits on D6.

### D8 — Performance check, device build, soak, evidence

- **Goal:**
  - Group-shot counts against the §15.7 budget (≤ 90 SkinnedMeshRenderers, 280 draw calls, 12 SetPass, 60k triangles), from a `SQ_DEV=1` simulator build's probe; levers only if a number misses.
  - The build log's asset report confirms only the referenced Synty parts ship.
  - Device build for the soak; the soak checklist and fill-in block for Martin; the lead captures the log with `devicectl`.
  - Evidence in `docs/evidence/M2/`.
- **Files:** none beyond tuning numbers a measurement forces (each one written into this plan); `docs/evidence/M2/*`.
- **Verification:** the producer's soak thresholds (STATUS, ruling 4) on both devices; devicectl screenshots of the purchase and of the next battle on both devices; Martin's warm-or-hot answer and his Silent mode yes.
- **Not part of it:** render-scale changes (M5), new content.
- **Status:** waits on D7.

## Needs Martin for M2 (also STATUS items 11–14)

1. Buy the Synty pack (money, his account), then send the receipt figures for the budget log.
2. The Mixamo download (free, his Adobe ID).
3. One Xcode Run per device on the M2 build.
4. The soak: play; warm or hot; Silent mode yes or no; anything wrong.

## Acceptance evidence to assemble for the producer

- Criteria table, as for M1.
- Gate 13/13.
- Regeneration byte-identical.
- Third-party folder audit (§15.13).
- Camp screenshots on both ratios.
- Eight front captures (skin tones).
- Simulator and devicectl screenshots of buy → next battle with the new weapon.
- Probe-line excerpts per device against the thresholds.
- The compliance line: no Synty or Mixamo file has left this Mac, `git remote -v` is empty.
- Budget row filled from Martin's receipt.
- Smoke items 1 (Camp as title), 3, 5, 8, 9 and 10, plus item 7 (Silent mode yes).
