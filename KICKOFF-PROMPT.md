# Kickoff prompt

Paste everything below the line into Claude Code, running in VS Code's terminal at the root of this repo on your Mac.

---

Read CLAUDE.md, then docs/V1-SCOPE.md, docs/ARCHITECTURE.md, docs/STORY-BIBLE.md and docs/DEVICE-TESTING.md in full before doing anything else.

You are the lead for this project. Your job is to deliver V1 exactly as defined in docs/V1-SCOPE.md, nothing more and nothing less, by coordinating the five personas in .claude/agents. Work through milestones M0 to M5 in order and never start a milestone before the producer has signed off the previous one in docs/STATUS.md.

For every milestone:

1. Ask the architect to confirm the design for that milestone in docs/ARCHITECTURE.md, including resolving any open items that block it. No code until the architect has answered.
2. Write the task plan to docs/PLAN-M{n}.md. Each task names its goal, the files it touches, how it is verified, and what is not part of it.
3. Hand code tasks to the developer one at a time in dependency order. From M3 onward, hand story tasks to the story-writer in parallel.
4. Verify every delivered task yourself: compile, run the EditMode tests, read the diff, confirm the verification ran. Reject anything that adds scope, adds tests outside V1-SCOPE §13, hand-edits scene or prefab YAML, adds a package, or leaves an editor step unrecorded.
5. When the acceptance criteria are met, assemble evidence for each one and ask the producer to sign off. Fix any gaps the producer lists before moving on.

Start now with M0. Before writing any code, create docs/STATUS.md and give me a short "Needs Martin" checklist of the steps only I can do on my Mac, in the order I should do them: Unity Hub and the pinned Unity version with iOS Build Support, Xcode, VS Code with the Unity extension, Apple account sign-in in Xcode, and any asset purchases needed for M0 (there should be none; KayKit placeholders are free). Then proceed with everything that does not need me.

Stop and ask me only when a decision is irreversible, costs money, or needs my Apple account. For anything else, take the most reasonable reading of the documents, write the reading into the task notes, and keep going. When you finish each milestone, tell me in a few lines what was delivered, what I need to do on the device, and what the next milestone will produce.
