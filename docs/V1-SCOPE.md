# StoryQuest — V1 Scope

Owner: **Producer** persona. Nobody else changes this file. Any proposed change goes through the producer, who either rejects it or records it under "Change log" with a reason.

"StoryQuest" is a working title. Replace it before the TestFlight build.

## 1. The one-sentence definition

V1 is **one land, five chapters, playable start to finish by one to four kids on a single iPad or iPhone, installed through TestFlight.** Nothing else ships in V1.

If a task does not move the project toward that sentence, it is out of scope.

## 2. Platform and devices

- iOS and iPadOS, minimum iOS 16.
- **Landscape only.** Orientation locked.
- Must look and play correctly on both an iPad (4:3) and an iPhone (19.5:9). UI anchors to safe areas; the battle camera frames the party correctly at both aspect ratios.
- Performance: 60 fps on iPhone 13 / iPad 9th gen and newer; never below 30 fps on iPhone 11 / iPad 8th gen.
- One device, one screen. No multiplayer, no networking, no accounts.

## 3. Players and heroes

- 1 to 4 kid profiles on the device. A profile is: a chosen name from a preset list or typed by the parent, an avatar, and one hero.
- A hero is a **class** × **body**:
  - Classes (4): Swordsman, Archer, Mage, Fighter.
  - Bodies (2): boy, girl.
  - 8 hero variants total, each with a fixed skin tone. Across the eight: at least three tones (light, medium, dark), each on at least two variants and in at least two classes; the boy and girl of each class differ (producer ruling 7, `docs/STATUS.md`).
  - Each class has one skill, one weapon type, one visible armor slot and one charm slot.
- Party = all active profiles (1–4). Each kid controls their own hero on their turn, on the shared screen.

## 4. Story (Land 1)

- One land, **5 chapters**, each 15–20 minutes of play including battles.
- Written in **Ink**. Structure is branch-and-rejoin: at least 2 meaningful choices per chapter, all paths rejoin by the end of each chapter, and the land ends in **2 possible endings** decided by choices across the land.
- Villains: trolls. Their **weakness** (the story writer picks it) is revealed in chapter 2 and matters in battles from chapter 3 onward. The chapter 5 boss cannot be beaten quickly without it.
- **2 memory callbacks**: moments where the story asks the kids to recall something from an earlier chapter (an NPC's request, a name, a hidden path), with a better outcome if they remember.
- Every story line is **narrated** (pre-generated audio) and shown as text. No reading is required to play.
- Tone: funny, warm, mildly spooky. No blood, no death. Defeat is "knocked down" and "retreating to camp."

## 5. Battles

FF8-style turn-based, one screen, scripted presentation.

- Commands per hero turn: **Attack**, **Skill** (one per class), **Item**, **Defend**.
- Turn order by a speed stat. Enemies act on their turns with simple fixed behaviors.
- Enemies: **3 troll types** (grunt, thrower, shaman) plus **1 boss**. Each has HP, attack, speed, and a weakness.
- Weakness: the matching damage type does double damage and triggers the big stagger reaction.
- **Every Attack and Skill lands.** No accuracy stat, no misses, no critical hits, no damage variance. A kid who taps a button sees it do something every time. (Producer ruling 2026-10-16.)
- Ranged attacks (Archer, Mage) are read through the draw/shoot or cast animation plus the full hit recipe on the target. **No projectile mesh is required in V1.** (Producer ruling 2026-10-16; see `docs/BACKLOG.md`.)
- **Hit recipe** on every hit, in this order: wind-up, impact freeze (60–100 ms), victim hit-reaction animation, short knockback slide that eases out, white flash on the model, particle burst, camera impulse, damage number. Camera cuts to the attacker on each action.
- Combat is **animation-driven**. No physics in combat. Physics is allowed only for loot and decoration.
- **Difficulty** (parent setting, per device):
  - *Always Win*: a party defeat sends the heroes back to camp with full HP and the battle restarts. Nothing is lost.
  - *Normal*: a party defeat sets an Ink variable; the story branches to a "retreat" scene and continues. Coins earned that battle are lost.
- Battle math lives in pure C# with no Unity dependency so it can be tested directly.

## 6. Progression, coins and shop

- XP from battles; levels **1 to 10**. Stats per class per level come from a JSON table.
- Coins come from story events and battle victories only. **No daily rewards, no login streaks, no spin wheel, no timers.**
- Shop opens at camp between chapters. **12 equipment items** (per class: one weapon upgrade, one armor piece, one charm) plus **2 consumables** (potion, revive). Prices force a choice; a kid cannot buy everything in one land.
- Weapons and armor are **visible on the hero model**. Charms are not.
- Equip screen: weapon, armor, charm slots per hero.

## 7. Narration and audio

- All story lines pre-generated with Amazon Polly from a line manifest exported from the Ink files, bundled in the app. One voice for the narrator, distinct voices for up to 4 recurring NPCs.
- Music: one camp track, one battle track, one boss track. Licensed or CC0.
- SFX: hit, skill ×4, coins, menu tap, victory, defeat.
- Audio plays with the iPhone silent switch on (native AVAudioSession playback category).

## 8. Saving and settings

- Local JSON save per profile in `Application.persistentDataPath`, plus the Ink story state. Autosave at every chapter start, battle end and shop exit.
- Parent settings screen: difficulty, narration on/off, manage profiles. No purchases in V1, so no parental gate is required.

## 9. Art and assets (decided)

- Characters: **Synty POLYGON - Modular Fantasy Hero Characters** through **SyntyPass**, Synty's subscription (owner decision 2026-10-16). Boy and girl bodies with modular class outfits; weapon upgrades and armor pieces are Synty meshes (the Archer's two bows come from ANIMATION - Bow Combat). The subscription stays active from the first Synty download until V1 closes (§15); conditions in `docs/STATUS.md`, ruling "SyntyPass".
- Enemies: **one Synty pack** under the same subscription, chosen in M2 under producer ruling 2 (`docs/STATUS.md`) from front and battle-framing captures. The candidate is the Troll from **POLYGON - Fantasy Rivals Pack** (ARCHITECTURE §15.16.2). The M2 grunt and the M4 thrower, shaman and boss all come from that one pack.
- Animations: **Synty ANIMATION - Sword Combat, Bow Combat, and Emotes and Taunts** (same subscription), imported as Humanoid for heroes and trolls: idle, class attacks and skills, draw/shoot, hit reaction, big stagger, defend, defeat, victory. No Mixamo. A clip that reads wrong is replaced by another clip from these packs.
- Environments and props: from M3, **POLYGON - Meadow Forest - Nature Biome** and **POLYGON - Fantasy Village** (same subscription), confirmed by the M3 design against the approved land outline. The outline is written to the places these two give. Replacing either needs the producer's yes, and there is never a third environment pack.
- **All art is Synty** (owner direction 2026-10-17). No KayKit in the project from M2 sign-off. Kenney CC0 is allowed for sound effects only (§7); music stays licensed or CC0 (§7).
- Packs: no Synty pack beyond the seven in ARCHITECTURE §15.16.4 without the producer's yes; a pack that costs nothing extra under the subscription is still import work. No paid art outside the subscription without the producer's yes in the `docs/STATUS.md` budget log and Martin's money.
- Look: stylized low-poly with a toon shader and outline, URP.

## 10. Privacy and store constraints

- No analytics of any kind (Unity Analytics off). No ads. No external links. No network calls. No third-party SDKs.
- App Store **Kids** category, age band 6–8 (confirm at submission).
- Export compliance: no encryption beyond Apple's defaults.

## 11. Explicitly out of scope for V1

Multiplayer or multi-device play. Additional lands. In-app purchases. Daily rewards, streaks, wheels, timers. Android. Accounts or cloud save. Analytics. Mini-games. Achievements. Localization. Voice input. Character creator beyond class × body. Cosmetics beyond the 12 shop items. Any physics-driven combat.

Anything in this list that someone wants becomes a V2 candidate in `docs/BACKLOG.md`, not a V1 task.

## 12. Milestones and acceptance criteria

Work proceeds in order. A milestone is done only when every criterion is met and the producer signs it off in `docs/STATUS.md`.

**M0 — Device loop.** Unity 6 LTS project with URP, packages installed, folder structure, code-first bootstrap scene, landscape lock, bundle ID, iOS build script. *Done when:* a build with a spinning placeholder and a tap counter runs on Martin's iPhone and iPad from Xcode, and `docs/DEVICE-TESTING.md` matches what actually happened.

**M1 — Battle slice.** 4 placeholder heroes vs 1 troll grunt. All four commands, turn order, the full hit recipe, camera cuts, damage numbers, victory and defeat screens, both difficulty behaviors. *Done when:* a kid can win and lose a battle on device and the hit looks crisp on both iPad and iPhone framing.

**M2 — Heroes and gear.** Synty heroes, 8 hero variants (4 classes × boy and girl), a Synty troll grunt, Synty animations, visible weapon and armor swaps, equip screen, shop UI with the 14 items, coins; no KayKit left in the project. *Done when:* buying a weapon in the shop changes the model in the next battle, on device.

**M3 — Story engine.** Ink integration, narration playback, choice UI, chapter flow, camp scene, save/load, parent settings. Uses a 2-chapter test story from the story writer. *Done when:* a profile can play two chapters, quit mid-chapter, relaunch, and continue where it left off.

**M4 — Land 1 content.** All 5 chapters written, narrated and integrated. 3 enemies and boss tuned. Items priced and pictured, so the shop and equip screens need no reading (§4). Both endings reachable. *Done when:* a full playthrough on device reaches each ending and the boss cannot be beaten in under 3 rounds without the weakness.

**M5 — Ship.** iPhone and iPad layout pass, safe areas, performance targets met, icon and launch screen, Kids checklist passed, TestFlight build uploaded, family installed. *Done when:* a kid tester finishes Land 1 on TestFlight without an adult touching the device except for parent settings.

## 13. Test policy

Tests exist only where a bug would be silent and expensive. That means **EditMode tests for pure logic only**:

- `BattleResolver`: damage calculation, weakness multiplier, turn order, victory and defeat detection, Defend behavior.
- `Progression`: level thresholds, stat table lookup including equipment bonuses, shop purchase rules (price, class, ownership).
- `SaveService`: save → load round-trip including Ink state.

Target: roughly 12–20 tests in three files. No PlayMode tests, no UI tests, no mocks of Unity systems, no coverage targets. Anything else is verified by the manual smoke checklist below. The lead rejects pull requests that add tests outside this list.

## 14. Manual smoke checklist (run on device before each milestone sign-off)

1. Cold start to title in under 5 seconds.
2. Create a profile, pick each class and body once; the model matches the choice.
3. Start a battle; use each command once; win; coins and XP increase.
4. Lose a battle on Always Win; return to camp with full HP. Lose on Normal; story branches.
5. Buy an item; it appears on the hero.
6. Quit mid-chapter; relaunch; continue from the autosave.
7. Toggle narration off and on; silent switch on; audio still plays.
8. Rotate the device; orientation stays landscape.
9. Check the layout on iPhone: no UI under the notch or home indicator.
10. Play 10 minutes; no console errors; frame rate steady.

## 15. Definition of V1 done

M0–M5 all signed off, the TestFlight build installed on at least two family devices, and one child completing Land 1 without help. At that point the producer closes V1 and opens `docs/BACKLOG.md` for V2 planning. Not before.

## Change log

- 2026-10-16 — **§5: no misses in V1; §7: "miss" struck from the SFX list.** Raised by the architect's M1 design (ARCHITECTURE §5 amendment and §14.3). Reason: §5 already says the hit recipe plays "on every hit" and §4 sets a no-pressure tone; a 5-year-old who taps Attack and sees MISS learns the button sometimes does nothing, which is pressure with no lesson. Removing it also removes one sound (§7) and keeps the §13 test budget at eight for `BattleResolver`. The `missed` field stays in the `Attacked` event record, always false, so the schema does not churn if V2 ever adds accuracy; that is an architecture detail, not scope.
- 2026-10-16 — **§5: no projectile mesh required for Archer or Mage in V1.** Raised by ARCHITECTURE §14.1 ("arrows are not spawned for the Archer in M1"). Reason: §5 never asked for one, §12 M1/M2/M5 criteria do not depend on one, and the hit recipe (freeze, stagger, knockback, flash, burst, number) is what makes a hit read. Not required for any V1 sign-off. Logged in `docs/BACKLOG.md`; the producer revisits it once, at M4 sign-off, on the evidence named in STATUS.
- 2026-10-16 — **§13: `Progression` tests cover equipment bonuses and the shop purchase rule.** Raised by ARCHITECTURE §15.6 and §15.11 q3. Reason: `Buy` is pure Core logic in `Progression`; a bug in it is silent (a young kid does not audit coins) and expensive once M3 saves it. `ProgressionTests` is five tests and closed at five; totals are 13 after M2 and 16–17 after M3, inside 12–20. Still three files; no test for `Equip`, `AwardCoins`, the generator, the baker or the UI.
- 2026-10-16 — **§12 M4: items are pictured as well as priced.** Raised by ARCHITECTURE §15.11 q6. Reason: §4 already says no reading is required to play, and the M2 shop is words and numbers; the M5 criterion (a kid finishes alone) fails if a 5-year-old needs an adult to read the shop. M4 because items, prices and the weakness are final there and the M4 kid session sees the shop as it will ship; M5 takes no new work. Bound: 14 pictures rendered by an editor script from meshes already in the project (charms and consumables a coloured shape), price as a coin mark and digits, no package, no purchase, no hand-drawn art. This is §4 applied to the shop, not a new feature.
- 2026-10-16 — **§9: KayKit Adventurers 2.0 FREE and KayKit Character Animations 1.1 are the V1 hero art and animations; Synty and Mixamo stop being V1 dependencies; environments are CC0 only.** Raised by ARCHITECTURE §15.14.8 q1–q2 after the owner's direction the same day ("I'm not trying to burn a lot of money on just building something out"). Reason: the money is Martin's call and he made it; both KayKit packs are CC0, already in the repo, and shipped M1; nothing in M2–M5 needs Synty. The pending Synty budget row closes unpaid (`docs/STATUS.md`). Synty stays an upgrade path (ARCHITECTURE §15.14.7) with one revisit at M4 sign-off (`docs/BACKLOG.md`). The environments line drops "Synty Fantasy Kingdom" so §9 names no paid pack.
- 2026-10-16 — **§3, §11, §14 item 2: a hero is class × look, not class × body; boy and girl bodies move to `docs/BACKLOG.md`.** Raised by ARCHITECTURE §15.14.8 q4. Reason: no free pack we own has boy and girl bodies; two skin-tone looks per class keep eight choices and keep ruling 7's rule that no tone belongs to one class; M3 profiles store a look either way. Smoke item 2 follows §3 so the checklist does not test something V1 no longer has. Known cost, logged in the backlog: a kid who wants a hero of their own gender may not find one (the Fighter is a bearded man); the M4 kid session is asked about it.
- 2026-10-16 — **§12 M2: "Synty bases, 8 hero variants, Mixamo retargeting, weapon and armor sockets" becomes "KayKit heroes, 8 hero looks (4 classes × 2 skin tones), KayKit animations, visible weapon and armor swaps".** Raised by ARCHITECTURE §15.14.8 q3. Reason: follows §9 and §3. *Done when* is unchanged word for word: buying a weapon in the shop changes the model in the next battle, on device.
- 2026-10-17 — **§9: Synty POLYGON - Modular Fantasy Hero Characters and three Synty ANIMATION packs (Sword Combat, Bow Combat, Emotes and Taunts), through SyntyPass, are the V1 hero art and animations; no Mixamo; environments may be Synty packs from M3 (at most two); no pack beyond the eight in ARCHITECTURE §15.15.1 without the producer's yes.** Raised by ARCHITECTURE §15.15.8 q1 after the owner's decision on 2026-10-16 ("I'm going to get the synty pass so I will download all the synty assets we need"). Reverses the 2026-10-16 §9 entry. Reason: the money is Martin's call, and he made it after the lead gave him the price, the licence terms and a "not now" recommendation. The producer's three SyntyPass conditions are settled in `docs/STATUS.md`, ruling "SyntyPass": (a) waived; (b) met by keeping the subscription active through V1; (c) met, since monthly costs less than one-time licences for 9 months on M2's four packs alone and about 25 months on all eight. The ANIMATION packs need no Adobe session, so Mixamo stays out. Cost: nothing in V1-SCOPE drops. M2 waits for Martin's download plus one import task (PLAN-M2 D5). The pack ceiling and the outline check are there because a pack that is free under the subscription still costs developer time.
- 2026-10-17 — **§3, §11, §14 item 2: a hero is class × body again (4 classes × boy and girl, each with a fixed tone under ruling 7); boy and girl bodies leave `docs/BACKLOG.md`.** Raised by ARCHITECTURE §15.15.8 q1. Reverses the 2026-10-16 entry. Reason: the Synty hero pack has male and female bodies. The Core code from PLAN-M2 D1–D2 is already class × body (`HeroRecord.Body`), so this path costs less code than the look path. It also settles the backlog's known cost (a kid who wants a hero of their own gender) before the M4 kid session rather than after it. There are eight choices either way. Ruling 7's tone rule applies with "the boy and girl of each class differ".
- 2026-10-17 — **§12 M2: "KayKit heroes, 8 hero looks (4 classes × 2 skin tones), KayKit animations, visible weapon and armor swaps" becomes "Synty heroes, 8 hero variants (4 classes × boy and girl), Synty animations, visible weapon and armor swaps".** Raised by ARCHITECTURE §15.15.8 q1. Reason: follows §9 and §3. "Visible weapon and armor swaps" is kept in place of the original "weapon and armor sockets" because the criterion is what the kid sees, not how it is built. *Done when* is unchanged word for word: buying a weapon in the shop changes the model in the next battle, on device.
- 2026-10-17 — **§9: all art is Synty; KayKit struck; Kenney CC0 for sound effects only; enemies are one Synty pack chosen in M2 (candidate: the Fantasy Rivals Troll); the M3 environment packs are named (Meadow Forest, Fantasy Village); the pack list is the seven in ARCHITECTURE §15.16.4, not the eight in §15.15.1.** Raised by ARCHITECTURE §15.16.7 q1–q3 after the owner's direction the same day ("what about the goblins? I dont want to use any of the kaykits anymore I want to use all synty so which ones do we need"). Reason: art source is the owner's call; every pack is under the subscription he already pays, so there is no new money. The pack cap was always a named list. Goblin War Camp leaves it and its slot is not reassigned, so the list names seven. Naming the two environment packs now fits the outline to the art instead of the art to the outline, so no pack can come in through the outline. Cost: one developer task in M2 (PLAN-M2 D6b: Synty troll, then KayKit removal), against troll art work M4 would have done anyway. M4 loses its troll download and decision. Raises the exit cost if SyntyPass ever lapses, because no free art is left to fall back to (`docs/STATUS.md`, ruling "SyntyPass" item 2 (ii)). Nothing in V1-SCOPE drops. §4 and §5 are unchanged: the villains stay trolls, and goblins go to `docs/BACKLOG.md`.
- 2026-10-17 — **§12 M2 adds "a Synty troll grunt" and "no KayKit left in the project".** Raised by ARCHITECTURE §15.16.5. Reason: follows §9. Without it, D6b is not a sign-off item, and an M2 build could carry a KayKit troll against the owner's direction. *Done when* is unchanged word for word.
