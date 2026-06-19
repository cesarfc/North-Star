# ORCHESTRATION.md — How We Build North-Star with Agents

This is the operating manual for the multi-agent workflow. `CLAUDE.md` is the rulebook every agent
obeys; this is the playbook **you** and the orchestrator follow to run the project.

## Roles
- **You — Product Owner.** Set priorities, review merged work, decide when a module is "done enough,"
  write game content (dialogue, quests, loot), and run Unity (the only place code actually compiles).
- **Orchestrator — the main Claude Code session.** Dispatches module agents, reviews their branches
  against the contract, coordinates shared-contract changes, and merges to `main`. (That's the
  session you're in now when you're not inside a module.)
- **Module agents — 9 specialized contractors** (`.claude/agents/module-0X-*.md`). Each owns one
  folder, reads the frozen contracts, builds to spec, writes tests, and commits to its own branch.

## The golden rule
You are the orchestrator; agents are contractors. Your leverage is the **contract** (`INTERFACE.md` +
`CONVENTIONS.md`), the **boundaries** (one folder per module, EventBus-only cross-talk), and the
**review gate**. Hold those three and parallel work stays mergeable.

## Two ways to run a module
Both use the same agent persona files, so output is consistent either way:

1. **Orchestrated (default).** From the main session, dispatch the agent into its own git worktree so
   several modules run in parallel without colliding:
   - Use the `Agent` tool with `subagent_type: "module-0X-..."`, `isolation: "worktree"`, and
     (for parallelism) `run_in_background: true`.
   - Or run `/module-start <n>`, which sets up the worktree/branch and dispatches the agent.
2. **Solo session.** Open a separate Claude Code session, adopt the module persona, and drive it
   yourself on that module's branch. Good when you want to pair closely on a tricky module.

## The loop (per module)
```
dispatch → agent builds on feature/module-0X → review → fix/iterate → merge to main → Unity compile gate
```
- **Dispatch:** `/module-start <n>` (or Agent tool, worktree isolation).
- **Review:** `/module-review <n>` — checks contract compliance, EventBus-only cross-module calls,
  boundary respect, tests present, naming/IDs. Reject and send back if it fails.
- **Merge:** orchestrator merges the reviewed branch to `main`. Agents never merge themselves.
- **Compile gate:** after each merge (and always at a sync point), **you** open Unity and Ctrl/Cmd+B.
  There is no local compile here — `UnityEngine` code can't be built without the editor.

## Phase schedule & sync gates
| Phase | Weeks | Modules (run in parallel) | Sync gate (you verify in Unity) |
|---|---|---|---|
| 0 | 1–2 | Foundation (done — this pack) | Project compiles, `INTERFACE.md` authoritative |
| 1 | 3–6 | **1 Core**, **7 Player**, **4 Dialogue/Quests** | Player walks an empty scene, talks to a placeholder NPC, conversation saves |
| 2 | 7–12 | **2 Character**, **3 Battle**, **5 World**, **8 Inventory** | Equip armor → enter battle → use an item → travel between two zones |
| 3 | 13–18 | **6 Landscape**, **9 Audio** + you writing content | One full zone: weather, day/night, ambient audio, music, one complete quest |
| 4 | 19–24 | Integration | Shippable vertical slice: one story chapter end-to-end, profiled |

Don't start a phase until the previous phase's sync gate passes. Within a phase, modules are
independent by design (they only meet through EventBus + the contracts).

## Review checklist (what `/module-review` enforces)
- [ ] **Contract:** public signatures, events, and SO schemas match `INTERFACE.md` exactly.
- [ ] **Decoupling:** no direct references to other modules — cross-talk is EventBus publish/subscribe.
- [ ] **Boundaries:** only this module's folder changed; `Core/` untouched (except Module 1).
- [ ] **Conventions:** naming, lowercase-hyphen IDs, one class per file, `[SerializeField] private`.
- [ ] **Subscriptions:** every `EventBus.Subscribe` has a matching `Unsubscribe` in `OnDisable`/`OnDestroy`.
- [ ] **Docs:** every public method has an XML `<summary>`.
- [ ] **Tests:** one EditMode test per public method; pure logic separated from MonoBehaviour glue.
- [ ] **No banned calls:** no `FindObjectOfType` / `GameObject.Find` / `Update()` for non-movement logic.

## Changing the contract
`INTERFACE.md` is frozen. If a module genuinely needs a new event or signature, the **agent stops and
asks the orchestrator** — the orchestrator updates `INTERFACE.md` and (for events) `GameEvents.cs`,
then unblocks the agent. Never let a module fork the contract locally.

## One-time human setup (outside this repo)
1. Install **Unity 2023 LTS** + Unity Hub. Create a **3D (URP)** project pointed at this folder
   (Unity adopts the existing `Assets/` and generates `ProjectSettings/`, `Packages/`, `Library/`).
2. Install Day-1 packages: Cinemachine, Input System (new), Terrain Tools, ProBuilder, Test Framework,
   plus DOTween and Yarn Spinner. (See `README` in the handoff pack / `Docs/`.)
3. Ctrl/Cmd+B → confirm 0 errors. Then start Phase 1.
