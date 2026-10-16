# StoryQuest — Status

Owner: **Producer** persona keeps this current; the lead maintains "Needs Martin". Short and factual. Dates are absolute.

## Current milestone

**M0 — Device loop.** Started 2026-10-15. Architect confirmed the design and resolved ARCHITECTURE §11 the same day. Plan: `docs/PLAN-M0.md`. T1–T3 (skeleton, editor scripts, runtime scripts) written and reviewed on 2026-10-15 without a compiler. T4 onward (first open, compile, iOS build, device run) wait for the Unity install, Needs Martin item 1.

## Needs Martin (in order)

Do these top to bottom. Items marked *done* were verified or completed by Claude Code on 2026-10-15 and need nothing from you.

1. **Unity Hub + Unity 6000.3.26f1 with iOS Build Support.** *Done 2026-10-16* (editor, iOS module and license present). You also installed 6000.6.6f1; the project does not use it, so you can remove it in the Hub to free disk whenever you like. Original ask kept for the record: Unity Hub 3.22.3 is already installed; no editor is. Open Unity Hub, sign in with your Unity ID (Unity Personal is free and activates on sign-in), then click this link or paste it in a browser: `unityhub://6000.3.26f1/de8167e7440c`. In the module list tick **iOS Build Support**, nothing else. About 8 GB, 20–40 minutes on a normal connection. The editor must end up at `/Applications/Unity/Hub/Editor/6000.3.26f1/`. (A headless install attempt from the terminal hung and was stopped, so this one is really yours.)
2. **Xcode.** *Done.* Xcode 27.0 is installed, the license is accepted and the iOS platform is present. Nothing to do.
3. **VS Code with the Unity extension.** *Done.* Claude Code installed `VisualStudioToolsForUnity.vstuc` (it pulled in C# Dev Kit and the .NET runtime). Two clicks remain for you, only after Unity is installed and the project opens once: in Unity, Preferences → External Tools → External Script Editor → Visual Studio Code. If VS Code asks to install the .NET SDK the first time it opens a `.cs` file, say yes.
4. **Apple account in Xcode.** *Probably done; confirm.* Your keychain already holds "Apple Development: martin@vendariz.com" and "Apple Distribution: Vendoza LLC (W727YBJNK6)". Open Xcode → Settings → Accounts and confirm the Vendoza LLC team is listed. If it is not, sign in with that Apple ID.
   Also confirm the team ID: builds will use `W727YBJNK6` (read from that certificate) for automatic signing. Tell me if the Vendoza LLC team shows a different ID.
5. **Asset purchases for M0.** *None.* M0 uses a built-in primitive. The free CC0 KayKit Adventurers pack is first needed in M1; I will ask for it then.
6. **Devices, before the M0 device run.** On your iPhone and on the iPad: Settings → Privacy & Security → Developer Mode → on, restart when asked. Plug the iPad into the Mac once and tap Trust; only two iPhones are paired with this Mac today, no iPad.
7. **The M0 device run itself** (when I tell you the build is ready): `docs/DEVICE-TESTING.md` §3, steps 1–5. First time only: pick the Vendoza LLC team under Signing & Capabilities and trust the developer on each device.

Not needed yet, flagged early so nothing blocks later:
- **Final app name and bundle ID** (decision, needed before M5). Builds use the working title "StoryQuest" and bundle ID `com.vendoza.storyquest`. Both can change freely until the App Store Connect record is created in M5; after that the bundle ID is permanent.
- **App Store Connect app record** (M5, needs your account).
- **Optional:** the repo lives in `~/Downloads/storyquest-starter`. That works, but macOS may ask once whether Unity may access your Downloads folder. Say yes, or move the folder somewhere like `~/Projects/storyquest` before you first open it in Unity (moving a Unity project is safe).

## Blockers

- None. T4 done 2026-10-16: the project opens, generates itself from code and compiles clean. T5 (iOS build, simulator aspect check) in progress.

## Decisions the lead took without asking (reversible, free)
- 2026-10-16: removed `-quit` from the EditMode test command in CLAUDE.md and DEVICE-TESTING.md; with it the editor exits before the test runner starts (verified in logs). Added generated `.vscode/` to `.gitignore`.

- 2026-10-15: `git init` on `main` in place; the starter kit is the first commit.
- 2026-10-15: installed the VS Code Unity extension from the CLI.
- 2026-10-15: tried a headless Unity Hub install of 6000.3.26f1 + iOS module. The Hub CLI started and then idled with no download for 18 minutes, so it was stopped. Item 1 above is the way to install.

## Budget log

| Date | Item | Cost | Approved by |
|---|---|---|---|
| — | nothing yet | $0 | — |

## Compliance checklist (producer runs before M5 sign-off)

- [ ] No analytics, no Unity Gaming Services, zero outbound requests (network capture on device).
- [ ] No ads, no external links, no purchases.
- [ ] Kids category, age band 6–8, privacy policy says nothing leaves the device.
- [ ] `ITSAppUsesNonExemptEncryption = NO` in the built Info.plist.
- [ ] Privacy manifest in the Xcode project reviewed.

## Sign-offs

| Milestone | Signed by producer on | Evidence |
|---|---|---|
| M0 | — | — |
| M1 | — | — |
| M2 | — | — |
| M3 | — | — |
| M4 | — | — |
| M5 | — | — |
