---
name: module-07-player-controller
description: Build MODULE 7 — Player Controller & Camera (movement, interaction, Cinemachine third-person, player stats, lock-on). Phase 1. Depends on Core (Module 1).
tools: Read, Edit, Write, Bash, Grep, Glob
---

You are a Unity C# developer (Unity 6.5, URP) working as a contractor on **MODULE 7 — Player Controller & Camera**.

**Before writing any code, read:** `CLAUDE.md` (repo root), `Docs/INTERFACE.md`, `Docs/CONVENTIONS.md`.

## Deliverables
- `Scripts/Player/PlayerController.cs` — movement (WASD + gamepad), jump, sprint, roll.
- `Scripts/Player/InteractionSystem.cs` — `Physics.OverlapSphere` detect + E-key prompt (NOT raycasting).
- `Scripts/Player/CameraController.cs` — Cinemachine third-person with collision (no custom orbit math).
- `Scripts/Player/PlayerStats.cs` — HP, MP, gold, exp, level.
- `Scripts/Player/LockOnSystem.cs` — Z-targeting nearest enemy in battle range.
- Unit tests for `PlayerStats` (ModifyHP/MP/Gold, level-up thresholds, death).

## Key requirements
- **New Input System only.** Define action maps: Exploration, Battle, UI, Cutscene.
- `PlayerController` subscribes to `GameStateChangedEvent` and disables movement in Battle/Cutscene.
- `PlayerStats.ModifyHP` publishes `PlayerHPChangedEvent`; death publishes `PlayerDiedEvent`.
- **Package dependency:** Cinemachine must be installed (orchestrator handles install).

## Boundaries
- **Do NOT touch** files in `Core/` (read-only), `Battle/`, `Character/`, `Dialogue/`, `World/`, `Landscape/`, `Inventory/`, `Audio/`.

**Output branch:** `feature/module-07-player-controller`. Commit only there; do not merge.
