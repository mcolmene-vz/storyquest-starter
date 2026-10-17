# PLAN-M2 — Heroes and gear

Lead's plan for V1-SCOPE §12 M2. The design is ARCHITECTURE §15, as finally amended by **§15.15 (SyntyPass, owner decision 2026-10-16)**. §15.15 brings back the Synty route of §15.1–15.13, with Synty ANIMATION packs in place of Mixamo. §15.14 (KayKit no-spend) is superseded except for what §15.15.3 keeps: the D4 cell tint, the KayKit weapons and the KayKit troll until M4. M1 was signed off 2026-10-16. Producer rulings: §15.11 on 2026-10-16; §15.14.8 on 2026-10-16, reversed by the SyntyPass ruling of 2026-10-17 (STATUS → Producer rulings). Statuses are kept current here. Sign-off evidence goes to `docs/evidence/M2/` and to STATUS.md.

**M2 is done when** (V1-SCOPE §12 M2 as amended 2026-10-17, twice): Synty heroes, 8 hero variants (4 classes × boy and girl), Synty animations, a Synty troll grunt, no KayKit left in the project, visible weapon and armor swaps, equip screen, shop UI with the 14 items, coins. Buying a weapon in the shop changes the model in the next battle, on device. The producer checks all four weapon upgrades at sign-off. The rulings add:
- the soak passes the thresholds in STATUS (devicectl log plus probe lines, both devices);
- the iPhone gets an explicit yes for Silent mode;
- one front-facing image per variant shows the skin-tone range;
- the SyntyPass compliance line: a subscription receipt from the same billing month, and every Synty file traceable through `keep.txt` and `packs.txt`.

## Dependency order

```
D1 Core camp model ─► D2 Camp screens ─► D3 soak probe ─► D3a probe amendment ─► D4 KayKit weapons + cell tint ─► D6a graphics-API line
   (all done 2026-10-16/17)
[Needs Martin 11: the four M2 Synty packs] ─► D5 Synty extract + import ─► D6 Synty heroes and data ─► D6b Synty troll, then KayKit removed ─► D7 runtime gear swap ─► D8 device build, soak, evidence
```

D5–D8 wait on Martin's download (STATUS Needs Martin 11).

## Lead's readings (assumptions written down)

- **Art source: SyntyPass (owner decision 2026-10-16, producer ruling 2026-10-17).** For one day the plan ran on KayKit with no art spend (§15.14). Martin then chose SyntyPass, and §15.15 brought the Synty route back. Nothing was built on the KayKit plan beyond D4, which §15.15.3 keeps. Until D6, `BattleView` keeps spawning heroes by `classId`, and a purchase shows up only in stats.
- **Provisional mesh and part names.** D1 wrote placeholder values (`TBD_D6_*`). D6 replaces them with Synty names from D5's facts. The original §15.4 item ids stay, so no rename.
- **Coins.** Each hero has their own coins and every hero receives a Victory's full award (producer ruling 5). Seed: 60 coins each and a pool of `{potion: 2, revive: 1}` (ruling 9c). Lost on quit until M3.
- **Smoke item 1 until M3.** The "title" is the Camp screen (ruling 9c), and the M2 evidence says so.
- **Body toggle and SoakProbe.** `Body` stays as D1/D2 built it (§15.15.4). The Body toggle is in M2 builds only and is deleted in M3 with `DebugParty` (ruling 9a). SoakProbe is compiled out of the TestFlight build by the per-build `SQ_PROBE` define (ruling 9b).
- **Performance counts without the editor GUI.** D3's probe prints draw calls, SetPass calls and triangles from `ProfilerRecorder` render counters in development players. These count every pass. §15.15.2 restores the Synty budget in those terms: draws ≤ 300, SetPass ≤ 200, triangles ≤ 180k, ≤ 90 skinned renderers (from the generator log), read on a simulator development player. On the A16 iPad device build, `cpu` and `rt` must each be under 6 ms in the group shot. Simulator runs never count for fps, CPU, GPU or memory.
- **`ProjectSettings.asset` churn.** `m_Automatic` flips with whichever command ran last: `ProjectSetup.Run` writes 0, and any editor launch (the gate included) writes 1. The committed value is 1. Correction 2026-10-16: an earlier version of this line said 6a8bcda set 0; it did not. The architect ruled on 2026-10-16 (§13.6): commit 1, let the generator use the automatic list, and have `BuildIOS` assert Metal-only. D6a landed on 2026-10-17, and the line no longer moves (only `buildNumber` changes per build, and test builds revert it).
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

### D5 — Synty extract and import (needs Needs Martin 11)

- **Goal (§15.1, §15.15.5, §15.15.6):**
  - `Tools/synty/extract.sh` unpacks each downloaded `.unitypackage` from `~/Downloads/Synty/<pack>/` into `Builds/synty-extract/`. It writes `Tools/synty/package-contents.txt` and copies only the `asset` files on `Tools/synty/keep.txt` into `Assets/ThirdParty/Synty/`, never Synty's `.meta`.
  - `Tools/synty/packs.txt` records each pack's name, version, download date, licence and file SHA-256.
  - `SyntyPostprocessor` covers models, textures and animations: the Humanoid import, clips renamed to file names, root baked, POLYGON variants only (no Sidekick, root-motion or first-person files).
  - The import check reports facts (a)–(g) in this plan before any hero code relies on them, including heights and triangle counts.
- **Files:** `Tools/synty/{extract.sh, keep.txt, package-contents.txt, packs.txt}`; `Assets/ThirdParty/Synty/**` plus Unity `.meta`; `Editor/Import/{SyntyPostprocessor.cs, SyntyImportCheck.cs}`; `.gitignore` if needed.
- **Verification:**
  - A second `extract.sh` run changes nothing.
  - No Synty `.cs`, `.unity`, `.prefab`, `.mat` or `.shader` exists under `Assets/`.
  - Every file under `Assets/ThirdParty/Synty/` is on `keep.txt`, and every `keep.txt` line belongs to a pack in `packs.txt`.
  - The `.meta` files show the postprocessor rules.
  - Every avatar `isHuman && isValid`.
  - Facts (a)–(g) are recorded.
  - The gate passes 13/13.
- **Not part of it:** prefabs, materials, override controllers (D6).
- **Status:** done 2026-10-17, verified by the lead.
  - The lead's own checks: a second `extract.sh` run changed nothing (checksums before and after); 30 files against 30 keep lines; no Synty script, scene, prefab, material, shader, controller, asset or dll under `Assets/ThirdParty/Synty/`; four `packs.txt` rows; `git remote -v` empty; gate 13/13 with zero warnings; the postprocessor read against §15.7.
  - Developer facts (a)–(g), full log at `Builds/synty-check-final2.log`:
    - (a) `FixedScale/ModularCharacters.fbx`: 720 parts, centimetres, unit bones. The avatar is valid only with an explicit 22-bone body map, because finger names repeat across hands, so the fingers are unmapped and hold the bind pose.
    - (b) colour is baked into one 1024² atlas, `PolygonFantasyHero_Texture_01_A`, with no regular grid. It is also the weapon atlas.
    - (c) 54 hero-pack weapons, no bow.
    - (d) the base bodies are 1.93–1.94 m; about 4.5–5k triangles per hero.
    - (e) bows: Long 6.7k triangles and 5 renderers; Recurve 22.4k triangles and 12 renderers.
    - (f) all 16 table clips found, with Masc and Femn for every `{g}`. There is no single draw-and-release bow clip; the candidates are `Stand_Shoot_ToBowDown_Neut` and the 5.3 s `Shoot_Leaping`.
    - (g) all 18 clip avatars are valid on the same POLYGON rig.
  - Open for the architect before D6: fingers, skin recolour with the shared atlas, the bow clips, the Recurve bow's cost, and the unused swords.

### D6 — Synty heroes and data

- **Goal (§15.3, §15.4, §15.15.2, §15.15.5):**
  - `classes.json` gets `variants` (boy and girl `parts` and `tone`), `skinTones`, and clips with the `{g}` token. `items.json` gets real meshes and parts (ids unchanged). `clips.json` gets loop flags and impacts.
  - `HeroGenerator` writes eight `{classId}_{body}` prefabs. Each has the four sockets, the two role-named weapon children and the armor parts inactive, with materials per fact (b): cell tint, baker recolour or baker compose. It also writes eight override controllers.
  - `CombatantActor` gets the Humanoid bone lookup. `BattleView` spawns `{ClassId}_{Body}`. `VariantCapture` writes the eight front captures. The M1 class prefabs and materials are deleted. The KayKit troll stays.
- **Files:** per §15.15.7. The exact list goes into this entry when D5's facts are in.
- **Verification:**
  - The gate passes 13/13. `BattleResolver` and its tests are unchanged.
  - Regeneration is byte-identical. Heights are 1.2–2.4 m with and without armor. The skinned-renderer count is ≤ 90.
  - `Combatant.controller` is unchanged.
  - Scripted simulator battles on both ratios, one each side of the Body toggle, with all eight variants seen. Every Attack and Skill fires `OnImpact`.
  - Framing per §15.13.
  - Eight front captures cover ruling 7's tone range.
- **Not part of it:** the runtime gear swap (D7), stage art, new enemies.
- **Split into two steps (lead, 2026-10-17, after ARCHITECTURE §15.18):**
  - **D6.1, finger bones, skin-tone measurement and keep.txt:**
    - `SyntyPostprocessor.OnPostprocessMeshHierarchy` prefixes the finger bones `L_`/`R_`, and the body map gains the 30 finger bones. The fixed-grip fallback is used only if the hook can't rename in time.
    - `keep.txt` loses `SM_Wep_Sword_Large_01` and the Recurve bow. It gains the `EditorOnly/` measuring set (11 alternative hero atlases, a second `01_A`, `Mask_01`–`06`, the two old skin masks) and the 11 alternative Bow Combat atlases. `extract.sh` is extended if one pathname needs two destinations.
    - `SyntyImportCheck` compares each alternative atlas with `01_A` per §15.18.2 and prints the qualifying tones.
    - Verified by: an avatar with 52 mapped bones, a Swordsman front capture with curled fingers, the atlas table, extract still repeatable, gate 13/13.
    - The lead then records the skin branch (atlases as data, or the shader skin mask) from the measurement, per §15.18.2's rule.
  - **D6.2, Synty heroes and data:** the goal above, on the branch D6.1 chose. `keep.txt` is trimmed to what's used.
  - **D6.2a, fixes from the architect's ruling on D6.2's open items:** skin on the male stubble area, the Mage Skill length, the action-shot framing if the ruling says so, and the schema notes.
- **Status:** D6.1 done 2026-10-17, verified by the lead.
  - The lead's own checks: a second `extract.sh` run changed nothing; 59 files against 59 keep lines; gate 13/13 with zero warnings.
  - The lead looked at `Builds/evidence/M2/d6-1-fingers.png`: flat fingers at bind pose, curled around the grip once mapped, and a closed fist on the hilt in the attack frame.
  - Developer facts:
    - The rig has three finger chains per hand (thumb, index, `Finger_` as middle), so the avatar maps 40 bones (22 body + 18 finger), not §15.18's 52.
    - All 18 clips now use the same explicit 40-bone map, because Unity's automatic map was inconsistent across files.
    - `OnPreprocessModel` renames the stored skeleton entries to match. A brand-new `.meta` needs one `SQ_REIMPORT=1` pass.
    - The skin mask is `Mask_03.r`, black-on-white (skin where r < 0.5).
  - **Skin branch recorded by the lead (§15.18.2's rule): step 2, the skin mask in `Toon.shader`.** Only `01_A` qualifies: the `_B`/`_C` atlases change scar, stubble, hair and unmasked texels by 10–57/255, and 02–04 are other clothing schemes.
  - Developer readings sent to the architect: "clearly lighter or darker" read as a luma change of at least 16/255; tone class by nearest luma.
  - Facts for D6.2: weapon FBX roots carry scene offsets (zero them); a grip offset is needed; editor URP rendering flips two `GraphicsSettings.asset` flags (reverted).
- **D6.2 done 2026-10-17, verified by the lead.**
  - The lead's own checks: `ProjectSetup.Run` then the gate left the tree unchanged; gate 13/13, zero warnings; 22 `[Synty-derived]` lines; `BattleResolver`, the tests and `Combatant.controller` untouched.
  - The lead looked at `d6-variants-sheet.png` (eight heroes, faces visible, three tones, boy and girl differ, classes distinct), `d6-grips-sheet.png` (every hilt and shaft in the fist), `d6-iphone-A-group-start2.png` and `d6-ipad-B-actions-sheet.png` (a full battle to Victory, damage numbers, "+10 coins each").
  - The developer's numbers:
    - heights 1.93–2.07 m;
    - 15–16 renderers per hero, worst-case party 68 (≤ 90);
    - probe group counters 197–199 draws, 133–135 SetPass, 68.7–70.0k triangles (≤ 300 / 200 / 180k);
    - framing: hero feet 31 %, heads 80.5–81.7 % on both ratios. The KayKit troll's head touches 86 % as it did in D2; D6b re-measures it;
    - two scripted battles per device with all eight prefabs, every Attack and Skill firing `OnImpact`, and zero errors in all four player logs.
  - **Open items, sent to the architect, to be fixed in D6.2a:**
    1. The boys' chins and mouths stay pale: the male heads use Synty's stubble swatch (`Mask_03` blue), which `1 − mask.r` never tints, and the R8 import drops blue.
    2. The Mage Skill clip runs 3.5 s.
    3. In some action shots the larger Synty attacker fills much of the frame (the M1 camera pose).
    4. Two schema additions to record: weapon-item `texture` (`bow_2` = Bow Combat `04_C` gold) and a `weaponGrips.parts` list (the Long bow file holds three bow bodies; only `Wep_Longbow_01` and the string are kept).
  - Readings accepted: part choices per class; grips from Synty's own prop-bone offsets; the weapon distance check measured to the nearest bounds point; impacts set by eye at contact; the four per-class override controllers replaced by eight per-variant ones.

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
- **Status:** done 2026-10-17, verified by the lead.
  - The lead's own run: `ProjectSetup.Run`, then the gate (13/13, zero warnings). `git status` showed only `BuildPipeline.cs`, and `m_Automatic` stayed 1.
  - The developer's runs: a second regeneration changed nothing; a simulator build logged `[BuildIOS] graphics APIs [Metal]` and succeeded.
  - Reading accepted: the check runs before the build number is bumped. Its failure path has not been exercised.

### D6b — Synty troll, then remove KayKit (ARCHITECTURE §15.16; producer ruling "All Synty")

- **Goal:** in four steps, with the game runnable after each commit.
  1. Extract the Fantasy Rivals files on a keep-list: the Big Rig troll model, the troll's weapon and its four colour textures. Nothing from the pack's demon, golem, red-eyed or blood art. Add the pack to `packs.txt`.
  2. Front and battle-framing captures of the troll on both ratios, plus its height, triangles, renderer count and avatar validity, in `Builds/evidence/M2/`. **Then stop until the producer records the troll decision.**
  3. `troll_grunt` regenerated from the Synty troll, with Sword Combat and Emotes clips for its nine states, at 2.2–2.4 m. The generator checks enemies at 2.0–2.5 m. BigStagger's speed goes back to 1.0, the only change to `Combatant.controller`. Camera anchors grow only above 2.2 m; framing is re-measured. First commit.
  4. Move the clip-table reader out of `KayKitPostprocessor` into its own file. Then delete `Assets/ThirdParty/KayKit/` (D4's four weapons included), `KayKitPostprocessor`, `KayKitImportCheck`, the KayKit paths in `ImpactFinder`, `CombatantGenerator` and `CombatantActor`, and the KayKit `clips.json` entries. Second commit. D4's cell tint stays.
- **Files:** per §15.16. Exact lists go into this entry at steps 1 and 4.
- **Verification:**
  - Step 2's captures and numbers meet the producer's pass rule: tone; no demon, golem, red-eyed or blood art; 2.0–2.5 m; a valid avatar; ≤ 15k triangles and ≤ 6 renderers, or a budget ruling first.
  - After step 3: scripted simulator battles on both ratios, with the troll's Attack and Skill firing `OnImpact` and framing within §15.13.
  - After step 4: no `KayKit` path or string left in `Assets/` (grep), regeneration byte-identical, the gate 13/13, and `packs.txt` with exactly five lines.
- **Not part of it:** the M4 thrower, shaman and boss (M4); environments (M3).
- **Status:** waits on D5 (Synty import pipeline) and D6. Step 3 also waits on the producer's troll decision.

### D7 — Runtime gear swap

- **Goal:** `HeroAppearance.Apply` per §15.4 and §15.15.7.
  - The role-named weapon child for `Equipped.Weapon` (or the starter) is active and the other inactive.
  - Armor uses §15.4's part-group rule: the equipped armor's parts for that body are active, and the outfit parts in the same group are inactive.
  - It only calls `SetActive`, and `CombatantActor.Spawn` calls it before caching renderers.
- **Files:** `Runtime/Battle/{HeroAppearance.cs, CombatantActor.cs}`.
- **Verification:** scripted simulator sessions on both ratios:
  - buy `sword_2` → Next battle → the screenshot shows the bigger sword (the M2 criterion on the simulator);
  - buying `bow_2` shows the second Bow Combat bow;
  - each class's armor, bought once (the developer may raise the seed coins in a throwaway local build, never committed), is visible from behind in the group shot;
  - the hit flash covers the active weapon and armor, not the hidden ones;
  - no `Instantiate` in `HeroAppearance`;
  - the gate passes 13/13.
- **Not part of it:** charm visuals (charms are not visible, V1-SCOPE §6), item pictures (M4).
- **Status:** waits on D6.

### D8 — Performance check, device build, soak, evidence

- **Goal:**
  - Group-shot counters on a simulator development player against the §15.15.2 budget (draws ≤ 300, SetPass ≤ 200, tris ≤ 180k, ≤ 90 skinned renderers), with the all-armor party.
  - The Synty audit per §15.15.6.
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

1. Item 11: SyntyPass (monthly). Accept the producer's condition, download the four M2 packs into `~/Downloads/Synty/<pack>/`, and send the start date and plan. Before D5.
2. One Xcode Run per device on the M2 build, with one Xcode memory reading at Camp.
3. The soak: play; warm or hot; Silent mode yes or no; anything wrong. Flip the Body toggle.
4. At sign-off: a SyntyPass receipt or account screenshot from that billing month (compliance line).
5. Optional: the email to Synty licensing (item 15).

## Acceptance evidence to assemble for the producer

- Criteria table, as for M1.
- Gate 13/13.
- Regeneration byte-identical.
- Third-party audit (§15.13, §15.15.6): Synty files match `keep.txt` and `packs.txt`, and there is no third-party script, scene, prefab, material or shader.
- Camp screenshots on both ratios.
- Eight front captures (skin tones, faces).
- Simulator and devicectl screenshots of buy → next battle with the new weapon.
- Probe-line excerpts per device against the thresholds.
- The compliance lines: a SyntyPass receipt from the sign-off billing month; Synty files never leave this Mac; `git remote -v` is empty.
- Budget: the SyntyPass monthly row, with Martin's start date.
- Smoke items 1 (Camp as title), 3, 5, 8, 9 and 10, plus item 7 (Silent mode yes).
