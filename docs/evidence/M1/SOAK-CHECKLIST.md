# M1 soak checklist (for Martin)

The producer made this a hard requirement for M1 sign-off: a timed 10 minutes of play on **each** device with the Xcode console attached. Do the iPhone first, then the iPad. Budget about 15 minutes per device.

## Before you start

1. Open `Builds/iOS/Unity-iPhone.xcodeproj` in Xcode (the lead tells you which build number is the one to test).
2. Pick the device, press Run. Leave Xcode open: the console at the bottom and the **Debug navigator** (the gauges tab on the left, Cmd+7) are what we read.
3. Note the wall-clock time when the battle screen appears. That is the start.

## During the 10 minutes, in any order

- Win at least two battles. Use **every command at least once**: Attack, Skill (it says "ready in n" while cooling down), Item → Potion on a hurt hero, Defend.
- Use **Revive** once: let the troll knock a hero down first (keep tapping Defend with everyone for a while; the troll hits a random hero), then Item → Revive → tap that hero's card.
- Lose once on **Always Win** (the default): tap Defend until the party is knocked down. You should see "Knocked down! Try again" and the same battle restart at full HP.
- Switch to **Normal**: tap the invisible top-right corner of the screen three times quickly; a toast says "Difficulty: Normal". Lose once more: you should see "The heroes retreat to camp" with a coins line, then a fresh battle.
- Aim for 40 or more hits in total (a battle is about 12 hits).
- **iPhone only:** turn Silent mode on during a battle (iPhone 15 Pro and later have no switch: use the Action button, or the bell in Control Center). The hit sounds must still play.

## What to watch in Xcode

- **Console:** any line in red. Write down the first one word for word and whether it repeats every frame or appears once.
- **FPS gauge:** should sit at 60 the whole time, with no downward drift in the last three minutes.
- **Memory gauge:** should stay flat, give or take a few MB.
- **Device:** warm is fine, hot is not.

## At the end

Note the wall-clock time when you stop. Then take two screenshots on the device (side button + volume up), both at a moment when the command panel is showing: one at the start of a turn (the wide group shot), one during the Fighter's attack if you can catch it (the close shot). AirDrop them to the Mac or just describe them.

## What to tell me, per device

- Start time and end time.
- Red console lines: none, once, or repeating (and the text).
- FPS steady at 60: yes or no. Memory flat: yes or no. Device warm or hot.
- Every command used, Revive used, both defeats seen: yes or no.
- iPhone: sound with Silent mode on: yes or no.
- Anything that looked wrong: a hero clipped by the screen edge, text under the notch, a hit that did not read as a hit, a button that needed two taps.
