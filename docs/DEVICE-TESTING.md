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

# EditMode tests (fast, headless). No -quit here: with -quit the editor shuts down before the
# test runner starts; without it the runner exits the editor by itself when the run ends.
# This command is also the compile gate: a C# error makes it exit non-zero before any test.
"$UNITY" -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testResults Builds/test-results.xml -logFile -

# iOS build → Builds/iOS/ (an Xcode project). SQ_APPLE_TEAM_ID enables automatic signing;
# SQ_SIMULATOR=1 builds a simulator project into Builds/iOS-sim instead; SQ_DEV=1 makes a development build;
# SQ_TESTFLIGHT=1 leaves the soak probe out (every TestFlight upload, §3b).
SQ_APPLE_TEAM_ID=W727YBJNK6 "$UNITY" -batchmode -nographics -projectPath . -executeMethod Game.Editor.BuildPipeline.BuildIOS -logFile - -quit
```

The build script sets: landscape-only orientation, bundle ID, version and build number, minimum iOS 16, `ITSAppUsesNonExemptEncryption = NO`, and turns off Unity Analytics. If the build fails, the log prints to the terminal and Claude Code can read it.

The very first build into an empty `Builds/iOS` (or `Builds/iOS-sim`) cannot use Xcode's Append mode because there is nothing to append to; the script notices and does a plain build, and appends from the next build on. A failed build still uses up a build number, which is harmless.

Building from the Unity Editor menu (File → Build Profiles → iOS → Build) produces the same result if you prefer clicking.

## 2b. Simulator check (both aspect ratios, no device needed)

Claude Code runs this to see the game at iPad 4:3 and iPhone 19.5:9 before anything touches a real device. From the repo root:

```bash
# simulator Xcode project → Builds/iOS-sim/
SQ_SIMULATOR=1 SQ_APPLE_TEAM_ID=W727YBJNK6 "$UNITY" -batchmode -nographics -projectPath . -executeMethod Game.Editor.BuildPipeline.BuildIOS -logFile - -quit
# Build the simulator project before a device build, or delete Builds/iOS-sim first: a simulator Append made right
# after a device build inherits Burst file references the simulator build does not produce, and Xcode then fails
# with missing lib_burst_generated files.

# compile it for the simulator, unsigned (a few minutes the first time)
xcodebuild -project Builds/iOS-sim/Unity-iPhone.xcodeproj -scheme Unity-iPhone -configuration Debug -sdk iphonesimulator -destination 'generic/platform=iOS Simulator' -derivedDataPath /tmp/sq-dd-sim CODE_SIGNING_ALLOWED=NO CODE_SIGNING_REQUIRED=NO build
APP=/tmp/sq-dd-sim/Build/Products/Debug-iphonesimulator/StoryQuest.app

# iPad (10th generation) = 9897FBFE-0795-45F7-9207-CE75AC2C028E, iPhone 15 Pro = ADE26594-6F5C-4977-AF3F-B19364216167
# (on another Mac, `xcrun simctl list devices` shows the UDIDs)
for UDID in 9897FBFE-0795-45F7-9207-CE75AC2C028E ADE26594-6F5C-4977-AF3F-B19364216167; do
  xcrun simctl boot $UDID                      # "already booted" is fine
  xcrun simctl install $UDID "$APP"
  xcrun simctl launch --console $UDID com.vendoza.storyquest > /tmp/sq-$UDID.log 2>&1 &   # the player log lands in that file
  sleep 90                                     # a cold launch on the simulator can take over a minute before the first frame
  xcrun simctl io $UDID screenshot /tmp/sq-$UDID-1.png
  sleep 1
  xcrun simctl io $UDID screenshot /tmp/sq-$UDID-2.png   # the cube should have turned between the two
  xcrun simctl shutdown $UDID
done
```

What to look for: the label `Taps: 0  <width>×<height>  safe (x, y, width, height)` sits top-left inside the safe area on both (on the iPhone it starts to the right of the Dynamic Island, x is not 0), and the cube is at a different angle in the second screenshot. Every simulator build is a Unity development player: Unity 6000.3 ships the iOS simulator player only as a development build, so the log says Build type 'Development' whatever `SQ_DEV` and the Xcode configuration say. Unity's Development Console can appear along the bottom, and the `[Probe]` line carries the render counters. Use simulator runs for layout, framing and those counters, never for fps, CPU, GPU or memory (ARCHITECTURE §15.7, 2026-10-16). Device builds without `SQ_DEV` are release players and do not show the console.

## 3. Run on a connected device (first time, Martin at the Mac)

1. Open `Builds/iOS/Unity-iPhone.xcodeproj` in Xcode.
2. Select the `Unity-iPhone` target → **Signing & Capabilities** → tick *Automatically manage signing* → choose the Vendoza LLC team. Xcode creates the provisioning profile.
3. Plug in the iPhone, trust the computer on the phone, pick the phone as the run destination, press **Run** (⌘R).
4. The first launch asks you to trust the developer: Settings → General → VPN & Device Management → your team → Trust.
5. Repeat with the iPad. Both devices use the same build.

After the first time, Xcode remembers the signing. Rebuilding from Unity with "Append" keeps the Xcode project settings; "Replace" loses them, so always append.

While the app runs from Xcode, the Debug navigator (⌘7) shows the FPS and memory gauges the milestone soaks read, and red lines in the console under it are what we report.

## 3b. Soak capture from the terminal (lead)

The milestone soak (ARCHITECTURE §14.8 and §15.8) is judged from a console log the lead saves from the terminal while Martin plays. The pass rules are the producer's (STATUS, Producer rulings 2026-10-16, item 4). Nothing here sends anything off the Mac.

**Which build.** Every `BuildIOS` build is a probe build unless `SQ_TESTFLIGHT=1` is set. The first `[BuildIOS]` line in the build log says `SQ_PROBE on` or `SQ_PROBE off`; that line goes in the evidence. A probe build writes one `[Probe]` line to the console every 30 s. The TestFlight upload (§4) is always built with `SQ_TESTFLIGHT=1`.

```bash
# the soak build (a probe build) → Builds/iOS/
SQ_APPLE_TEAM_ID=W727YBJNK6 "$UNITY" -batchmode -nographics -projectPath . -executeMethod Game.Editor.BuildPipeline.BuildIOS -logFile Builds/build-soak.log -quit
grep -m1 "SQ_PROBE" Builds/build-soak.log      # must say: [BuildIOS] SQ_PROBE on

# iPhone 17 Pro = 00008150-00025D6811F0401C, iPad (A16) = 00008120-001C198908A14032
# (on another Mac, `xcrun devicectl list devices` shows the identifiers)

# install: the normal route is Martin's Xcode Run (§3) on each device, then Stop; the app stays installed.
# The terminal route needs a .app signed for the device (a signed xcodebuild; not yet tried on this Mac):
xcrun devicectl device install app --device <UDID> <path>/StoryQuest.app

# launch and capture; <device> is iphone or ipad. Every line gets the wall-clock time in front.
mkdir -p Builds/evidence/M2
xcrun devicectl device process launch --console --terminate-existing --device <UDID> com.vendoza.storyquest 2>&1 \
  | while IFS= read -r l; do printf '%s %s\n' "$(date +%H:%M:%S)" "$l"; done > Builds/evidence/M2/soak-<device>.log
# It returns when the app exits. Ctrl-C ends the capture early; the lines already written stay in the file.

# a device screenshot, with nobody touching the device (works while the capture runs, from a second terminal)
xcrun devicectl device capture screenshot --device <UDID> --destination Builds/evidence/M2/<name>.png
```

`--console` captures the app's stdout and stderr: Unity's startup lines, every `Debug.Log`, and the engine's own warnings and errors. It drops what only Xcode shows: os_log lines from iOS frameworks and Metal API-validation messages. Unity's startup lines and the first `[Probe]` line must be in the file; if they are not, that device's soak falls back to Xcode as in M1.

**Judging the log.** For each device:

```bash
LOG=Builds/evidence/M2/soak-iphone.log
head -1 "$LOG"; tail -1 "$LOG"                            # start and end time: at least 10 minutes apart
grep '\[Probe\]' "$LOG"                                   # one line every 30 s; a gap over 60 s means re-run that device
grep -nE 'Error|Exception|RenderPass|Attachment' "$LOG"   # red lines: write down each text
grep -E 'Error|Exception|RenderPass|Attachment' "$LOG" | cut -d' ' -f2- | sort | uniq -c   # and whether it repeats
```

A probe line reads:

```
[Probe] t=212s flow=Battle fps avg 59.8 min 52.1 | gpu group avg 3.9 max 5.2 ms | cpu 6.1 ms | mem 187.4 MB
```

- `t`: seconds since the app started. `flow`: the screen when the line was written; the first `flow=Battle` is where the first battle starts.
- `fps avg`: frames divided by seconds over the 30 s. `min`: the slowest whole second in them. Pass: avg 58 or more in every line after the first battle starts, with no downward drift over the last 3 minutes.
- `gpu group`: GPU time per frame, averaged only over frames in the group shot (the wide battle camera). `-` means those 30 s had no group-shot frame (Camp, or only action shots). `avg 0 max 0` means the device returned no GPU time: read it from Xcode instead (Debug navigator ⌘7 → GPU, during a group shot); it is not a pass. Pass: the iPad's avg under 6 ms.
- `cpu`: main-thread time per frame, averaged. `mem`: the memory Unity itself has reserved (`Profiler.GetTotalReservedMemoryLong`), in MB of 1,048,576 bytes. It is not the whole-app figure in Xcode's memory gauge. Pass: within ±5 MB after the first battle starts, and under 300 MB on the iPhone.
- Development players add `| group draws … setpass … tris …`: the highest draw-call, SetPass and triangle counts of any group-shot frame in those 30 s, against the ARCHITECTURE §15.7 budget. They stand in for the Frame Debugger; a release player has no such counters. Development players are every `SQ_DEV=1` build and every simulator build: Unity 6000.3 ships the simulator player only in its development variant.

The simulator log from §2b carries the same lines. The simulator's GPU numbers say nothing about a device.

**The probe is out of the TestFlight build.** Before trusting an empty result, run the grep on a probe build, where it must find the string:

```bash
grep -rla '\[Probe\]' Builds/iOS/Data      # probe build: prints Builds/iOS/Data/Managed/Metadata/global-metadata.dat
SQ_TESTFLIGHT=1 SQ_APPLE_TEAM_ID=W727YBJNK6 "$UNITY" -batchmode -nographics -projectPath . -executeMethod Game.Editor.BuildPipeline.BuildIOS -logFile Builds/build-testflight.log -quit
grep -m1 "SQ_PROBE" Builds/build-testflight.log   # must say: [BuildIOS] SQ_PROBE off
grep -rla '\[Probe\]' Builds/iOS/Data      # TestFlight build: prints nothing
# after Product → Archive, the same grep over the archived app must print nothing too:
grep -rla '\[Probe\]' ~/Library/Developer/Xcode/Archives/<date>/<name>.xcarchive/Products/Applications/StoryQuest.app
```

If the probe build's grep prints nothing, the pattern is wrong (IL2CPP keeps string literals in `global-metadata.dat`), not the build; fix the pattern before believing a TestFlight result.

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
- *Run fails before install with "dyld_shared_cache_extract_dylibs failed"* (seen on the iPhone on 2026-10-16): Xcode's one-time copy of that iOS version's symbols from the phone died halfway and left a locked folder behind, so every Run trips over it. Quit Xcode, delete `~/Library/Developer/Xcode/iOS DeviceSupport/<model> <iOS version>` for that phone, unplug and replug it, reopen Xcode, wait in Window → Devices and Simulators until "Preparing" finishes (5–15 minutes), then Run again. Disk space was not the cause here, but the copy needs about 10 GB free.
- *Run fails right after the build with "Developer Mode disabled"*: step 1.6 was skipped on that device. Turn Developer Mode on (Settings → Privacy & Security), let the device restart, then press Run again; the build does not need to be redone. (Happened on the iPad on 2026-10-16.)
- *Unity says "The build cannot be appended"*: there was no previous build in that folder. Newer builds of the script handle this by themselves; if you still see it, delete `Builds/iOS` (or `Builds/iOS-sim`) and build again.
- *Xcode: "Build input file cannot be found: lib_burst_generated…" on a simulator build*: the simulator project was appended on top of a device build (§2b). Delete `Builds/iOS-sim` and build again.
