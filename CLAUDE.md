# StoryQuest — project rules for Claude Code

StoryQuest is a one-screen, turn-based story-adventure for kids aged 5–11, built in Unity 6 LTS for iPad and iPhone, played by one to four kids on a single device. V1 is one land of five chapters delivered through TestFlight. Owner: Martin (Vendoza LLC).

Read these before doing anything:
- `docs/V1-SCOPE.md` — what V1 is and is not, milestones, acceptance criteria, test policy. The producer owns it.
- `docs/ARCHITECTURE.md` — stack, code-first rule, folder and assembly layout, battle and story design. The architect owns it.
- `docs/STORY-BIBLE.md` — tone, chapter spine, Ink tag and variable conventions. The story writer uses it.
- `docs/DEVICE-TESTING.md` — CLI commands for tests and builds, Xcode and TestFlight steps.
- `docs/STATUS.md` — current milestone, blockers, "Needs Martin" list, sign-offs.

## Team

Five personas live in `.claude/agents/`. The main session acts as the **lead** unless told otherwise.

| Persona | Use for |
|---|---|
| `producer` | Scope rulings, milestone sign-off, compliance, budget. Call before adding or removing scope and at the end of every milestone. |
| `architect` | Design confirmation before each milestone, package or pattern decisions, drift between code and ARCHITECTURE.md. |
| `lead` | Planning a milestone into tasks, verifying delivered work, assembling sign-off evidence. |
| `developer` | All implementation: C#, editor scripts, data, shaders, build pipeline, narration tool, permitted tests. |
| `story-writer` | Outline, Ink chapters, narration manifest, names and descriptions. |

Order of operations for every milestone: architect confirms design → lead plans tasks → developer and story-writer deliver → lead verifies → producer signs off. Do not skip steps to save time; the steps are what keep V1 small.

## Non-negotiables

1. **Nothing outside V1-SCOPE ships.** Ideas go to `docs/BACKLOG.md`.
2. **Code-first.** One hand-authored scene (`Boot`). Everything else from C#, data, or editor scripts in the repo. Never hand-edit `.unity`, `.prefab`, `.controller` or `.playable` files. Unavoidable editor clicks are recorded in `docs/EDITOR-STEPS.md`.
3. **`Game.Core` has no `UnityEngine` reference.** Battle and progression rules live there.
4. **Tests are EditMode only, for `BattleResolver`, `Progression` and `SaveService`.** Roughly 12–20 tests in three files. No more.
5. **No analytics, ads, links, network calls or third-party SDKs.** Unity Services off.
6. **No daily rewards, streaks, wheels or timers.** Coins come from play.
7. **Both aspect ratios, always.** iPad 4:3 and iPhone 19.5:9 are checked for every UI and camera change.
8. **No new packages** without the architect recording the reason in ARCHITECTURE.md.
9. **Animation-driven combat.** No physics on characters.

## Working with Martin

Martin runs the Mac, the Apple account and the money. Batch requests for him under "Needs Martin" in `docs/STATUS.md` and raise them early in a milestone. Interrupt him only for decisions that are irreversible, cost money, or need his account. Otherwise take the most reasonable reading of the documents, state the reading in the task notes, and proceed.

## Commands

Set `UNITY` to the editor binary for the pinned version (see `docs/DEVICE-TESTING.md`). Then:

```bash
"$UNITY" -batchmode -projectPath . -runTests -testPlatform EditMode -testResults Builds/test-results.xml -logFile - -quit
"$UNITY" -batchmode -projectPath . -executeMethod Game.Editor.BuildPipeline.BuildIOS -logFile - -quit
```

Compile errors appear in the CLI log. Fix them before anything else.

## Repo conventions

- Namespaces: `Game.Core`, `Game.Runtime`, `Game.Editor`, `Game.Tests`.
- Data tables in `Assets/_Game/Data/*.json`; story in `Assets/_Game/Story/land1/`; third-party imports under `Assets/ThirdParty/` and never edited.
- Commit `.meta` files with their assets. Never commit `Library/`, `Temp/`, `Logs/`, `Builds/`, `UserSettings/`.
- Commit messages: what changed and why, one short paragraph.
- Milestone plans in `docs/PLAN-M{n}.md`; keep them current.

## Definition of done for any task

The verification named in the task actually ran and passed, the diff touches only the files the task named, nothing was added "while in there," and the lead has read the result. For UI or camera work, both aspect ratios were checked. For anything player-facing, it was seen on a device or the Game view at device resolution.
