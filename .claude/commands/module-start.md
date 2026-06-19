---
description: Dispatch a North-Star module agent into its own git worktree on its feature branch
argument-hint: <module-number 1-9>
---
Start **module $1** of the North-Star build as the orchestrator.

Steps:
1. Resolve module `$1` to its agent + branch using the table below.
2. Dispatch the matching subagent via the **Agent tool** with:
   - `subagent_type` = the agent name (e.g. `module-0$1-...`)
   - `isolation: "worktree"` (isolated git worktree so it can run in parallel with other modules)
   - `run_in_background: true` if you're fanning out several modules in the same phase.
3. In the dispatch prompt, tell the agent to: read `CLAUDE.md`, `Docs/INTERFACE.md`, and
   `Docs/CONVENTIONS.md` first; checkout/create its branch `feature/module-0$1-<name>` inside the
   worktree; build **only** its deliverables; write tests; and commit to that branch. It must **not**
   merge — merging happens after `/module-review`.
4. When it finishes, summarize what it produced and remind me to run `/module-review $1`.

| # | subagent_type | branch |
|---|---|---|
| 1 | module-01-core-architecture | feature/module-01-core-architecture |
| 2 | module-02-character-customization | feature/module-02-character-customization |
| 3 | module-03-battle-mechanics | feature/module-03-battle-mechanics |
| 4 | module-04-dialogue-quests | feature/module-04-dialogue-quests |
| 5 | module-05-world-building | feature/module-05-world-building |
| 6 | module-06-landscape-environment | feature/module-06-landscape-environment |
| 7 | module-07-player-controller | feature/module-07-player-controller |
| 8 | module-08-inventory-economy | feature/module-08-inventory-economy |
| 9 | module-09-audio-polish | feature/module-09-audio-polish |

Respect the phase schedule in `ORCHESTRATION.md` — don't start a module whose phase gate isn't open.
