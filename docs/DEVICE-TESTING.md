# StoryQuest — Getting it onto your iPhone and iPad

Owner: **Developer** persona keeps this accurate. It is written for Martin, who runs the Mac side. Claude Code can run the Unity build step; the Xcode signing and device steps need the Mac in front of you the first time.

## 1. One-time setup on the Mac (Martin)

1. **Xcode** from the Mac App Store (large download). Open it once and accept the license. Install the iOS platform support when prompted.
2. **Unity Hub**, then install the Unity 6 LTS version pinned in `docs/ARCHITECTURE.md` §11 with the **iOS Build Support** module ticked.
3. **Visual Studio Code** with the Microsoft **Unity** extension (it pulls in C# Dev Kit and the .NET SDK). In Unity: Preferences → External Tools → External Script Editor → Visual Studio Code. From then on, double-clicking a script in Unity opens VS Code, and Claude Code runs in the VS Code terminal at the repo root.
4. **Apple Developer**: sign in to Xcode → Settings → Accounts with the Apple ID on your developer account so the Vendoza LLC team appears.
5. **App Store Connect**: create the app record once you have the final name: Apps → + → New App, platform iOS, bundle ID matching the one in the build script (default `com.vendoza.storyquest`, change both together), SKU anything.
6. On each test device: Settings → Privacy & Security → **Developer Mode** on (iOS 16+). Restart when asked.

## 2. Build from Unity

Claude Code or you run the editor script. From the repo root:

```bash
# set once per shell; this is the editor version pinned in docs/ARCHITECTURE.md §11
export UNITY="/Applications/Unity/Hub/Editor/6000.3.26f1/Unity.app/Contents/MacOS/Unity"

# EditMode tests (fast, headless)
"$UNITY" -batchmode -projectPath . -runTests -testPlatform EditMode -testResults Builds/test-results.xml -logFile - -quit

# iOS build → Builds/iOS/ (an Xcode project)
"$UNITY" -batchmode -projectPath . -executeMethod Game.Editor.BuildPipeline.BuildIOS -logFile - -quit
```

The build script sets: landscape-only orientation, bundle ID, version and build number, minimum iOS 16, `ITSAppUsesNonExemptEncryption = NO`, and turns off Unity Analytics. If the build fails, the log prints to the terminal and Claude Code can read it.

Building from the Unity Editor menu (File → Build Profiles → iOS → Build) produces the same result if you prefer clicking.

## 3. Run on a connected device (first time, Martin at the Mac)

1. Open `Builds/iOS/Unity-iPhone.xcodeproj` in Xcode.
2. Select the `Unity-iPhone` target → **Signing & Capabilities** → tick *Automatically manage signing* → choose the Vendoza LLC team. Xcode creates the provisioning profile.
3. Plug in the iPhone, trust the computer on the phone, pick the phone as the run destination, press **Run** (⌘R).
4. The first launch asks you to trust the developer: Settings → General → VPN & Device Management → your team → Trust.
5. Repeat with the iPad. Both devices use the same build.

After the first time, Xcode remembers the signing. Rebuilding from Unity with "Append" keeps the Xcode project settings; "Replace" loses them, so always append.

## 4. TestFlight (so the family can install without a cable)

1. In Xcode, set the run destination to **Any iOS Device (arm64)**, then **Product → Archive**.
2. When the Organizer opens: **Distribute App → App Store Connect → Upload**. Accept the defaults. Upload takes a few minutes; processing on Apple's side takes 10–30 minutes.
3. In App Store Connect → your app → **TestFlight**: the build appears. Answer the export compliance question (no, no custom encryption) if it asks.
4. **Internal testing**: add up to 100 testers from your team's Apple IDs. Testers install the TestFlight app from the App Store and accept the email invite. Internal builds do not go through App Review.
5. Every new upload needs a higher build number. The build script increments it automatically.

Internal testers see new builds within minutes of processing. That is the loop for the family: build → archive → upload → they tap Update in TestFlight.

## 5. What Claude Code can and cannot do here

Claude Code on your Mac can edit all code, run the EditMode tests, run the Unity iOS build, read Unity and Xcode logs, and can run `xcodebuild` to archive and upload once signing is configured. It cannot press Trust on your phone, sign in to your Apple account, or watch the screen unless you let it. Expect the first device run to need you; after that, most of the loop is scriptable.

## 6. Common failures

- *"Unity Analytics" appears in the privacy report*: Services were not fully disabled. Project Settings → Services → turn everything off, rebuild.
- *No audio with the silent switch on*: the AVAudioSession plugin is missing from `Assets/Plugins/iOS`. See `docs/ARCHITECTURE.md` §1.
- *UI under the notch on iPhone*: a panel is missing `SafeAreaFitter`.
- *Build number rejected by TestFlight*: the previous upload used the same number; increment and re-archive.
- *Xcode "Untrusted Developer"*: step 3.4 above.
