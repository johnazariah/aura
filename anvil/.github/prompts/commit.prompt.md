---
agent: agent
description: "Create logical commits from staged changes using conventional commit format"
---

@workspace

Analyze the staged changes and create logical, atomic commits.

${{input}}

Follow the instructions in [.sdd/prompts/commit.md](../../.sdd/prompts/commit.md).

Group related changes together and use conventional commit format (feat, fix, refactor, docs, test, chore).
