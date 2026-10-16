# StoryQuest starter kit

Everything Claude Code needs to build V1 of StoryQuest, before a single Unity file exists.

## How to use it

1. Create an empty folder on your Mac for the game, make it a git repo, and copy the contents of this kit into it (including the hidden `.claude/` folder and `.gitignore`).
2. Do the one-time Mac setup in `docs/DEVICE-TESTING.md` §1: Xcode, Unity Hub with the iOS module, VS Code with the Unity extension.
3. Open the folder in VS Code, open the terminal, start Claude Code, and paste the prompt from `KICKOFF-PROMPT.md`.
4. Claude Code, acting as the lead, creates the Unity project inside this folder and works through the milestones in `docs/V1-SCOPE.md`, using the personas in `.claude/agents/`.
5. Watch `docs/STATUS.md` for the "Needs Martin" list and sign-offs.

## What is in here

```
CLAUDE.md                 Project rules Claude Code reads automatically
KICKOFF-PROMPT.md         The prompt to paste to start the build
.claude/agents/           The five personas: producer, architect, lead, developer, story-writer
docs/V1-SCOPE.md          What V1 is, milestones, acceptance criteria, test policy
docs/ARCHITECTURE.md      Stack, code-first rule, layout, battle and story design
docs/STORY-BIBLE.md       Tone, chapter spine, Ink conventions
docs/DEVICE-TESTING.md    Tests and builds from the CLI, Xcode, TestFlight
.gitignore                Standard Unity ignores
```

Files Claude Code creates as it works: `docs/STATUS.md`, `docs/PLAN-M*.md`, `docs/EDITOR-STEPS.md`, `docs/BACKLOG.md`, and the Unity project itself.

## Changing the plan

Scope changes go through the producer persona, which edits `docs/V1-SCOPE.md` and records the reason. Design changes go through the architect, which edits `docs/ARCHITECTURE.md`. If you want something changed, tell Claude Code which persona should rule on it; the documents stay the source of truth.
