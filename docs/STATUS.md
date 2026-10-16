# StoryQuest — Status

Owner: **Producer** persona keeps this current; the lead maintains "Needs Martin". Short and factual. Dates are absolute.

## Current milestone

**M0 — Device loop.** Started 2026-10-15. Architect confirmed the design and resolved ARCHITECTURE §11 the same day. Plan: `docs/PLAN-M0.md`. T1–T3 (code written blind) in progress; T4 onward wait for the Unity install.

## Needs Martin (in order)

Do these top to bottom. Items marked *done* were verified or completed by Claude Code on 2026-10-15 and need nothing from you.

1. **Unity Hub + Unity 6000.3.26f1 with iOS Build Support.** Unity Hub 3.22.3 is already installed; no editor is. Open Unity Hub, sign in with your Unity ID (Unity Personal is free and activates on sign-in), then click this link or paste it in a browser: `unityhub://6000.3.26f1/de8167e7440c`. In the module list tick **iOS Build Support**, nothing else. About 8 GB, 20–40 minutes on a normal connection. The editor must end up at `/Applications/Unity/Hub/Editor/6000.3.26f1/`. (Claude Code tried a headless Hub install at the same time; if the Hub already shows 6000.3.26f1 installed, just sign in and confirm the iOS module is ticked under the gear → Add modules.)
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

- No Unity editor on the Mac yet (item 1). Code can be written, but nothing compiles or builds until it is installed. This is the only blocker for M0.

## Decisions the lead took without asking (reversible, free)

- 2026-10-15: `git init` on `main` in place; the starter kit is the first commit.
- 2026-10-15: installed the VS Code Unity extension from the CLI.
- 2026-10-15: started a headless Unity Hub download of 6000.3.26f1 + iOS module. If it did not complete, item 1 above covers it.

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
