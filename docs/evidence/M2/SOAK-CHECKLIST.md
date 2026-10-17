# M2 soak checklist (for Martin)

M2 sign-off needs a 10-minute play session on each device. This time you only play: the lead starts the app from the terminal and saves the console log, frame rate, memory and screenshots. Budget about 15 minutes per device, iPhone first, then iPad.

## Before you start (once per device)

1. Plug the device in and unlock it.
2. Open `Builds/iOS/Unity-iPhone.xcodeproj` in Xcode, pick the device, press **Run**. This installs the M2 build.
3. When the Camp screen appears, open the Debug navigator (Cmd+7), read the **Memory** number, and write it down. That one number is all you read from Xcode.
4. Press **Stop** in Xcode. Leave the device plugged in and unlocked, and tell the lead "ready".

The lead then starts the app on the device from the terminal. When it opens on the Camp screen, start playing.

## During the 10 minutes, in this order

1. **The M2 check.** At Camp, buy the weapon for every hero you can (60 coins each buys one): Steel Sword, Hunter Bow, Crystal Staff, Spiked Mace. Tap **Next battle** and look: each hero holds the new weapon.
2. On the **Equip** tab, tap **Body** on one hero (girl ↔ boy). The change shows from the next battle.
3. In battles: use **every command at least once** (Attack, Skill, Item → Potion on a hurt hero, Defend). Use **Revive** once on a knocked-down hero.
4. **Win at least twice.**
5. **Lose once on Always Win** (tap Defend until the party falls): "Knocked down! Try again" appears, and the battle restarts.
6. Switch to **Normal**: tap the empty top-right corner of the battle screen three times quickly. Lose once more: "The heroes retreat to camp".
7. **iPhone only:** turn on **Silent mode** during a battle (Action button, or the bell in Control Center). The hit sounds must still play.
8. Keep playing until the lead says 10 minutes are up.

## What to tell the lead, per device (copy and fill in)

```
Device: iPhone / iPad
Xcode memory at Camp: ___ MB
Warm or hot at the end: ___
iPhone only. Hit sounds with Silent mode on: yes / no
Anything that looked wrong (a hero clipped by the screen edge, text under the notch, a hit that didn't read, a button that needed two taps, a weapon that didn't change): ___
```

Start and end times, error lines, frame rate, GPU time and memory come from the saved log; you don't need to note them.

## Also, once, at the end

One screenshot of your Synty account page showing SyntyPass active (any day this week). The producer needs it at each sign-off.
