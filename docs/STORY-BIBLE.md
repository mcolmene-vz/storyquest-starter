# StoryQuest — Story Bible (Land 1)

Owner: **Story Writer** persona. The producer approves the land outline before chapters are written. The architect owns the tag and variable conventions in §5; the story writer uses them exactly.

## 1. Audience and tone

- Players are 5 to 11 years old, usually siblings playing together with a parent nearby. Every line is narrated, so the text can run at a grade 2–3 reading level without blocking a 5-year-old.
- Tone: funny, warm, a little spooky. Think bedtime story with a monster in it, not a dark fantasy. Trolls are big, grumpy, greedy and a bit dim. They are never scary for more than a line.
- No blood, no death, no weapons described cutting anyone. Heroes get "bonked," "knocked down," "sent tumbling." After a lost battle the party "runs back to camp to catch their breath."
- Kids are the heroes. Adults in the story ask for help; they do not rescue.
- Short lines. One idea per line. Narration lines under 20 words; dialogue lines under 15. Kids tap to advance, so a long paragraph is a wall.
- Humor comes from character, repetition and surprises, never from mocking anyone.

## 2. Land 1 brief

The producer and story writer settle the land in a one-page outline before chapter writing starts. The brief must answer:

- The place (a village with a river, a forest, a hill fort — anything that gives five distinct chapter locations).
- The problem the trolls are causing, stated so a 5-year-old can repeat it ("the trolls took all the bread").
- The **troll weakness**: something kids can remember and that the battle system can represent as a damage type or a skill. Examples: trolls can't stand bells, trolls fall asleep when they hear a lullaby, trolls are ticklish. Revealed in chapter 2 by an NPC. Used from chapter 3. Required against the boss.
- The **two memory callbacks**: concrete things from early chapters that pay off later. Example: the miller tells the party her name in chapter 1; in chapter 4 a locked gate opens for "friends of" her, and the game asks the kids to pick her name from four options.
- The **two endings**: both good. One is the "best" ending for parties that made generous or brave choices; the other is still a win with a different final scene. Nobody loses the land.

## 3. Chapter structure

Each of the 5 chapters follows the same spine, which makes writing and integration predictable:

1. **Arrive** — a new location, one line of scene-setting, one NPC with a problem.
2. **Choice A** — a real choice with two or three options. The branches differ in flavor and small rewards and rejoin within the chapter.
3. **Encounter** — one battle (two in chapters 4 and 5). Battles are placed with the `# battle:` tag.
4. **Choice B** — a second choice, often about kindness, sharing coins, or taking a risk. This is where ending points accumulate.
5. **Rejoin and reward** — all branches meet, coins and an item are awarded, the chapter ends on a hook.
6. **Camp** — the `# camp` tag opens shop and rest. The next chapter starts from camp.

Chapter length target: 40–70 narrated lines plus the battle(s), about 15–20 minutes.

## 4. Writing the choices

- Every option is something a kid can picture doing. "Sneak past" and "Knock on the door" beat "Consider your options."
- Both or all options lead somewhere good enough that a kid is not punished for picking wrong. Branches change the route, a reward, or a joke, not whether they can finish.
- Ending points: generous or brave options add `ending_points += 1`. Final threshold for the best ending is decided with the producer once the chapters exist.
- Memory callbacks present four options, one correct. A wrong answer costs a small detour, never the chapter.

## 5. Ink conventions (owned by the architect, used exactly)

Files: `Assets/_Game/Story/land1/main.ink` includes `globals.ink`, `ch1.ink` … `ch5.ink`. One knot per chapter, stitches per scene, gathers for rejoin points.

Line tags the game understands:

```
# scene: village_day          backdrop swap
# speaker: miller             portrait + voice; omit for narrator
# audio: ch1_n_012            narration clip id (see §6)
# battle: troll_grunt,troll_grunt
# reward: coins=20
# reward: item=potion
# shop
# camp
# memory: ask=millers_name    presents the four options that follow as a memory check
```

Variables (declared in `globals.ink`):

```
VAR troll_weakness_known = false
VAR ending_points = 0
VAR lost_battle_ch3 = false
VAR lost_battle_ch4 = false
VAR lost_battle_ch5 = false
VAR remembered_millers_name = false
VAR remembered_secret_path = false
VAR battle_result = ""        // set by the game after each battle: "win" or "retreat"
```

External functions the game provides: `heroClass(index)`, `partySize()`, `hasItem(id)`, `difficulty()`. Use them sparingly; a line that mentions the mage only when a mage is in the party is a nice touch, not a requirement.

After a `# battle:` line, always branch on `battle_result`. In *Always Win* difficulty the game never returns `retreat`, so the retreat branch is only reached in *Normal*.

## 6. Narration manifest

Every line that should be spoken gets an `# audio:` tag with an id in the form `ch{chapter}_{speaker-initial}_{3-digit}`, for example `ch2_m_014` for the miller's 14th line in chapter 2 and `ch2_n_031` for a narrator line. The story writer runs `Tools/narration/export-manifest` (the developer builds it) to produce `narration-manifest.json` with `{ id, speaker, text }` entries, and the developer runs the Polly step. If a line changes, its id stays the same and the clip is regenerated.

## 7. Deliverables by milestone

- **M3**: a 2-chapter test story that exercises every tag and variable at least once. It does not need to be good; it needs to be complete.
- **M4**: the approved land outline, then `ch1.ink` … `ch5.ink` in order, each reviewed by the producer against this bible before the next is written, then the narration manifest.
- **M5**: shop item names and one-line descriptions (14 items), enemy names, the two ending titles, and the app's one-paragraph store description.

## 8. Checklist before handing a chapter to the lead

- Compiles in Inky with no warnings.
- Follows the chapter spine in §3 and the length target.
- Every spoken line has an `# audio:` tag with a unique id.
- Every `# battle:` is followed by a `battle_result` branch.
- No line over the word limits in §1. No reading required to make a choice (options are short and will be narrated).
- Someone reading it aloud would laugh at least twice.
