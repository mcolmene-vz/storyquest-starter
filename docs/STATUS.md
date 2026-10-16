# StoryQuest — Status

Owner: **Producer** persona keeps this current; the lead maintains "Needs Martin". Short and factual. Dates are absolute.

## Current milestone

**M0 — Device loop: signed off by the producer 2026-10-16** (see Sign-offs). Started 2026-10-15, plan `docs/PLAN-M0.md`, build 4 (0.1.0) running on the iPad (A16) and the iPhone 17 Pro.

**M1 — Battle slice is next.** Not started. Architect's M1 design is written (ARCHITECTURE §14, 2026-10-16; §5 and §6 amended). Its two scope questions (no misses; no Archer/Mage projectile) were ruled by the producer 2026-10-16, see Producer rulings; V1-SCOPE §5 and §7 amended to match. Remaining order of operations per CLAUDE.md: lead writes `docs/PLAN-M1.md` → developer delivers → lead verifies → producer signs off. Both KayKit packs are on disk (Needs Martin item 9).

## Needs Martin (in order)

Do these top to bottom. Items marked *done* were verified or completed by Claude Code on 2026-10-15 and need nothing from you.

1. **Unity Hub + Unity 6000.3.26f1 with iOS Build Support.** *Done 2026-10-16* (editor, iOS module and license present). You also installed 6000.6.6f1; the project does not use it, so you can remove it in the Hub to free disk whenever you like. Original ask kept for the record: Unity Hub 3.22.3 is already installed; no editor is. Open Unity Hub, sign in with your Unity ID (Unity Personal is free and activates on sign-in), then click this link or paste it in a browser: `unityhub://6000.3.26f1/de8167e7440c`. In the module list tick **iOS Build Support**, nothing else. About 8 GB, 20–40 minutes on a normal connection. The editor must end up at `/Applications/Unity/Hub/Editor/6000.3.26f1/`. (A headless install attempt from the terminal hung and was stopped, so this one is really yours.)
2. **Xcode.** *Done.* Xcode 27.0 is installed, the license is accepted and the iOS platform is present. Nothing to do.
3. **VS Code with the Unity extension.** *Done.* Claude Code installed `VisualStudioToolsForUnity.vstuc` (it pulled in C# Dev Kit and the .NET runtime). Two clicks remain for you, only after Unity is installed and the project opens once: in Unity, Preferences → External Tools → External Script Editor → Visual Studio Code. If VS Code asks to install the .NET SDK the first time it opens a `.cs` file, say yes.
4. **Apple account in Xcode.** *Probably done; confirm.* Your keychain already holds "Apple Development: martin@vendariz.com" and "Apple Distribution: Vendoza LLC (W727YBJNK6)". Open Xcode → Settings → Accounts and confirm the Vendoza LLC team is listed. If it is not, sign in with that Apple ID.
   Also confirm the team ID: builds will use `W727YBJNK6` (read from that certificate) for automatic signing. Tell me if the Vendoza LLC team shows a different ID.
5. **Asset purchases for M0.** *None.* M0 uses a built-in primitive. The free CC0 KayKit Adventurers pack is first needed in M1; I will ask for it then.
6. **Devices, before the M0 device run.** On your iPhone and on the iPad: Settings → Privacy & Security → Developer Mode → on, restart when asked. Plug the iPad into the Mac once and tap Trust; only two iPhones are paired with this Mac today, no iPad.
7. **The M0 device run itself.** *Ready since 2026-10-16, build 4 at `Builds/iOS/Unity-iPhone.xcodeproj`.* `docs/DEVICE-TESTING.md` §3, steps 1–5. First time only: pick the Vendoza LLC team under Signing & Capabilities and trust the developer on each device. Report back: tap count goes up, cube spins, and whether the Xcode console shows any red line containing "RenderPass" or "Attachment".

8. **Smoke-checklist items for M0 sign-off.** *Done 2026-10-16:* cold start under 5 s, landscape held on rotation. The timed 10-minute soak was not reported; producer ruling 2026-10-16: deferred to M1, not waived (see Producer rulings). At M1 you will be asked for a timed 10 minutes on each device with the Xcode console open.
9. **For M1 (next):** *Done 2026-10-16.* KayKit Adventurers 2.0 FREE at `/Users/mcolmene/Downloads/KayKit_Adventurers_2.0_FREE` and KayKit Character Animations 1.1 (free, CC0, same Rig_Medium) at `/Users/mcolmene/Downloads/KayKit_Character_Animations_1.1`; the M1 import task copies what it needs into `Assets/ThirdParty/KayKit/`.

Not needed yet, flagged early so nothing blocks later:
- **Final app name and bundle ID** (decision, needed before M5). Builds use the working title "StoryQuest" and bundle ID `com.vendoza.storyquest`. Both can change freely until the App Store Connect record is created in M5; after that the bundle ID is permanent.
- **App Store Connect app record** (M5, needs your account).
- **Performance-floor device** (decision, needed before M5). V1-SCOPE §2 names the iPad 8th/9th gen as the floor; the iPad in the house is an A16. Before M5 either borrow a 2020–2021 iPad for one session or tell the producer to amend §2 to the A16. No action now.
- **Optional:** the repo lives in `~/Downloads/storyquest-starter`. That works, but macOS may ask once whether Unity may access your Downloads folder. Say yes, or move the folder somewhere like `~/Projects/storyquest` before you first open it in Unity (moving a Unity project is safe).

## Blockers

- None. M0 signed off 2026-10-16. Architect's M1 design is in ARCHITECTURE §14 and the producer has ruled on its scope questions; M1 waits only on `docs/PLAN-M1.md`.

## Producer rulings

- 2026-10-16, **smoke item 10 at M0: deferred to M1, not waived.** V1-SCOPE §14 item 10 is "play 10 minutes"; M0 has nothing to play, and the soak exists to catch leaks, thermal throttling and per-frame errors under game load, which a cube and a label cannot produce. Martin's two device sessions already cover "no console errors" for the minutes they ran. Conditions: from M1 on, item 10 is a timed 10-minute soak on both devices with the Xcode console attached, reported with the time, and it is a hard requirement for M1 sign-off. The simulator-only render-pass message (ARCHITECTURE §13.5) keeps PLAN-M0 T5's rule on device: once is a disclosed exception, per frame is a blocker.
- 2026-10-16, **AudioListener on the camera** (PLAN-M0 T3): accepted. One component, needed by ARCHITECTURE §4 `AudioService` in M3, no cost.
- 2026-10-16, **M0 evidence location.** `Builds/evidence/M0/` is git-ignored, so the sign-off pointer below also names the commits. Lead: copy the text files (`M0-EVIDENCE.md`, `device-ipad.txt`, `device-iphone.txt`) to `docs/evidence/M0/` in the next commit so the record survives a `Builds/` clean; PNGs and logs stay out of git.
- 2026-10-16, **No misses in V1: accepted.** V1-SCOPE §5 now says every Attack and Skill lands (no accuracy, no crits, no variance); "miss" is struck from the §7 SFX list; both recorded in the V1-SCOPE change log. Reason: §4's no-pressure tone and §5's "on every hit" were already pointing this way, and a miss gives a 5-year-old a failure with nothing to learn from it. One fewer sound, no miss test; the `BattleResolver` budget stays at the eight tests in ARCHITECTURE §14.7. The always-false `missed` field in `Attacked` is the architect's call and costs nothing; it does not come back into scope without a change-log entry.
- 2026-10-16, **Archer arrow and Mage bolt: not required for V1.** Not an M1, M2 or M5 acceptance item; V1-SCOPE §5 now says so. M2's criterion stays exactly "buying a weapon changes the model in the next battle" and nothing is added to it. Backlog entry (lead: add this row verbatim to `docs/BACKLOG.md` in the PLAN-M1 commit; this ruling was limited to editing V1-SCOPE and STATUS): `| 2026-10-16 | architect (ARCHITECTURE §14.1) | Visible projectiles for Archer (arrow) and Mage (bolt) | A flying arrow reads as "I shot it" and the Mixamo draw/shoot clip invites one | V1-SCOPE §5 never asked for one; the hit recipe carries the read; zero acceptance criteria depend on it. Revisit once at M4 sign-off: if a kid tester cannot tell the troll was hit after an Archer or Mage Attack on device, it becomes a bounded M5 task (code + existing meshes, no new package, no purchase); otherwise V2. |`. The one revisit is at **M4 sign-off, not M5**: M5 is the ship milestone and does not take on new work the kids have not already shown they need.
- 2026-10-16, **`docs/AWS-INFRA-PLAYBOOK.md`** (untracked) is a Terraform/Lambda playbook for other projects. Nothing in V1 has a backend (V1-SCOPE §10); the only AWS use is Polly from a local narration tool in M3. Do not commit it to this repo and do not build from it. Martin: move it out or add it to `.gitignore`.

## Decisions the lead took without asking (reversible, free)
- 2026-10-16: removed `-quit` from the EditMode test command in CLAUDE.md and DEVICE-TESTING.md; with it the editor exits before the test runner starts (verified in logs). Added generated `.vscode/` to `.gitignore`.

- 2026-10-15: `git init` on `main` in place; the starter kit is the first commit.
- 2026-10-15: installed the VS Code Unity extension from the CLI.
- 2026-10-15: tried a headless Unity Hub install of 6000.3.26f1 + iOS module. The Hub CLI started and then idled with no download for 18 minutes, so it was stopped. Item 1 above is the way to install.

## Budget log

| Date | Item | Cost | Approved by |
|---|---|---|---|
| 2026-10-16 | M0 complete; nothing bought (built-in primitive, free Unity Personal, existing Apple Developer Program) | $0 | producer |

## Compliance checklist (producer runs before M5 sign-off)

- [ ] No analytics, no Unity Gaming Services, zero outbound requests (network capture on device).
- [ ] No ads, no external links, no purchases.
- [ ] Kids category, age band 6–8, privacy policy says nothing leaves the device.
- [ ] `ITSAppUsesNonExemptEncryption = NO` in the built Info.plist. (Seen `<false/>` in build 4 `Builds/iOS/Info.plist` on 2026-10-16; recheck on the TestFlight build.)
- [ ] Privacy manifest in the Xcode project reviewed. (`Builds/iOS/UnityFramework/PrivacyInfo.xcprivacy` present in build 4; content review is M5.)

## Sign-offs

| Milestone | Signed by producer on | Evidence |
|---|---|---|
| M0 | 2026-10-16 | `Builds/evidence/M0/M0-EVIDENCE.md` (criteria table, smoke items 1/8/9 pass, 10 deferred to M1 by ruling); device reports `device-ipad.txt`, `device-iphone.txt`; commits 42e00ce, 648f498, b1109fa; build 4 / 0.1.0 |
| M1 | — | — |
| M2 | — | — |
| M3 | — | — |
| M4 | — | — |
| M5 | — | — |
