---
description: Review a North-Star module branch against the contract & conventions before merge
argument-hint: <module-number 1-9>
---
Review **module $1**'s branch (`feature/module-0$1-<name>`) as the orchestrator, before any merge.

First diff the branch against `main` (e.g. `git diff main...feature/module-0$1-<name> --stat` then the
full diff). Then check every item below and report PASS/FAIL with file:line evidence:

- [ ] **Contract** — public signatures, events, and SO schemas match `Docs/INTERFACE.md` exactly (no silent renames/retyping).
- [ ] **Decoupling** — no direct references to other modules; cross-talk is `EventBus` publish/subscribe only.
- [ ] **Boundaries** — only this module's folder changed; `Scripts/Core/` untouched (except Module 1).
- [ ] **Conventions** — naming rules, lowercase-hyphen IDs, one class per file, `[SerializeField] private` for Inspector fields.
- [ ] **Subscriptions** — every `EventBus.Subscribe` has a matching `Unsubscribe` in `OnDisable`/`OnDestroy`.
- [ ] **Docs** — every public method has an XML `<summary>`.
- [ ] **Tests** — one EditMode test per public method; pure logic separated from MonoBehaviour glue.
- [ ] **No banned calls** — no `FindObjectOfType`, `GameObject.Find`, or `Update()` for non-movement logic.

If anything fails, **do not merge** — list the fixes and re-dispatch the module agent to address them.
If everything passes, summarize the change, note that Unity compile verification (Ctrl/Cmd+B) is still
required by the human, and present the merge command for approval (merge `feature/module-0$1-<name>`
into `main`). Do not push unless asked.
