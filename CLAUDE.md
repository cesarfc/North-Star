# North-Star — 3D Adventure Game (Unity 6.5 · URP)

Solo-developed with parallel AI agents. This file is loaded into **every** agent and session — it is
the always-on guardrail. Read it, then read the two contracts below before writing any code.

## Read these first (every module, every time)
1. **`Docs/INTERFACE.md`** — the frozen, authoritative API contract (signatures, events, SO schemas).
2. **`Docs/CONVENTIONS.md`** — naming, folder structure, code style, performance budgets.
3. **`ORCHESTRATION.md`** — how the multi-agent workflow runs (roles, phases, branches, review).

If the contract and the shipped `Assets/_Game/Scripts/Core` code ever disagree, **the code wins** —
flag it so `INTERFACE.md` gets corrected. Do not silently re-shape signatures.

## The 6 non-negotiable rules
1. **No direct cross-module references.** Modules talk only through `EventBus.cs` (publish/subscribe).
2. **All data lives in ScriptableObjects.** No hardcoded stats, strings, or IDs in MonoBehaviours.
3. **One class per file.** Filename matches the class name exactly.
4. **Null-check EventBus subscriptions; unsubscribe in `OnDisable`/`OnDestroy`.** No leaked handlers.
5. **Every public method gets an XML `<summary>` doc comment.**
6. **Unity 6.5 + URP. New Input System only** (no legacy `Input.GetAxis`).

## Unity 6 specifics (we target 6.5, not 2023)
- **Cinemachine 3.x** ships with Unity 6. Use `CinemachineCamera` (the 2.x `CinemachineVirtualCamera`
  and `CinemachineFreeLook` are deprecated/renamed); namespace is `Unity.Cinemachine`. `CinemachineImpulseSource` still exists.
- Input System and Test Framework are first-party packages in Unity 6 — no extra registry setup beyond enabling them.
- Don't write code against pre-6 APIs; if unsure whether an API survived the 6.x migration, flag it rather than guessing.

## Code style (see CONVENTIONS.md for the full list)
- Public properties `PascalCase`; private fields `_camelCase`; constants `ALL_CAPS`; events `OnEventName`; coroutines `CoName`.
- `[SerializeField] private` for Inspector fields — not `public`.
- **Never** use `FindObjectOfType`, `GameObject.Find`, or `Update()` for non-movement logic — use references or EventBus.

## IDs & naming
- All `itemId` / `questId` / `zoneId` / `abilityId` etc. are **lowercase-with-hyphens**, unique
  project-wide, and **never changed after creation** (assets and save files reference them by ID).
- Asset prefixes: `SO_` ScriptableObjects, `PFB_` prefabs, `SCN_` scenes, `MAT_` materials,
  `SFX_`/`MUS_` audio, `T_` textures, `ANIM_` clips, `AC_` animator controllers, `UI_` UI sprites.

## Where things live
- Code: `Assets/_Game/Scripts/<Module>/` (Core, Character, Battle, Dialogue, World, Landscape, Player, Inventory, Audio).
- SO class definitions: `Assets/_Game/ScriptableObjects/*.cs`; SO `.asset` instances: the matching subfolder.
- Docs live at repo root (`Docs/`, outside `Assets/`) so Unity doesn't import them.

## Branch & boundary discipline
- Each module is built on its own branch: `feature/module-0X-<name>`. Commit only there.
- **Touch only your module's folder.** `Scripts/Core/` is **read-only** for every module except Module 1.
- Cross-module needs are met by publishing/subscribing to events in `Core/GameEvents.cs` — never by
  referencing another module's MonoBehaviour. Need a new event? Add the struct to `GameEvents.cs`
  (Module 1's file) via the orchestrator, don't reach across modules.

## Testing
- Write one unit test per public method using the **Unity Test Framework** (EditMode for pure logic).
- Work with no tests is rejected at review.

## Verification reality (important)
- Unity is **not installed in this environment** — there is **no local `dotnet` build** of this code
  (it depends on `UnityEngine`). Do **not** claim "it compiles" from here.
- Real compile/play verification happens in the Unity editor (Ctrl/Cmd+B → 0 errors) — the human
  orchestrator runs that at each integration gate. Write code to the contract; keep pure logic
  separable from `MonoBehaviour` glue so it's testable.
