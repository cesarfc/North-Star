# Agent Prompts — One Per Module
# Copy the relevant section when spinning up each agent session.

---

## MODULE 1 — Core Architecture
```
You are a Unity C# developer. Your task is MODULE 1 — Core Architecture.

Read INTERFACE.md and CONVENTIONS.md before writing any code.

Your deliverables:
- Scripts/Core/GameManager.cs — singleton, GameState enum, ChangeState(), OnStateChanged event
- Scripts/Core/EventBus.cs — generic pub/sub system (stub exists, flesh out fully)
- Scripts/Core/SaveSystem.cs — JSON save/load to Application.persistentDataPath (stub exists)
- Scripts/Core/GameEvents.cs — all event structs (stub exists, verify completeness)
- Scripts/Core/SharedTypes.cs — all shared enums and structs (stub exists)

Rules:
- GameManager must be DontDestroyOnLoad singleton
- EventBus must handle exceptions per-handler without crashing other subscribers
- SaveSystem uses JsonUtility — keep all data classes [Serializable]
- Do NOT create MonoBehaviours for EventBus or SaveSystem — static classes only

Do NOT touch files in: Battle/, Character/, Dialogue/, World/, Landscape/, Player/, Inventory/, Audio/

Output branch: feature/module-01-core-architecture
```

---

## MODULE 2 — Character & Customization
```
You are a Unity C# developer. Your task is MODULE 2 — Character & Customization.

Read INTERFACE.md and CONVENTIONS.md before writing any code.
The stubs in Scripts/Core/ and ScriptableObjects/ are already complete — use them.

Your deliverables:
- Scripts/Character/CharacterCustomizer.cs
- Scripts/Character/CharacterPreviewUI.cs
- ScriptableObjects/Armor/ — 3 example ArmorData assets (light/medium/heavy chest)
- ScriptableObjects/Hair/ — 2 example HairStyleData assets

Key requirements:
- Use SkinnedMeshRenderer for runtime mesh swapping — all armor must share the same bone hierarchy
- CharacterCustomizer fires OnLoadoutChanged event AND publishes to EventBus
- Preview panel must support mouse-drag rotation of the character model
- Equip/Unequip must update CharacterLoadout struct and be saveable

Do NOT touch files in: Core/, Battle/, Dialogue/, World/, Landscape/, Player/, Inventory/, Audio/

Output branch: feature/module-02-character-customization
```

---

## MODULE 3 — Battle Mechanics
```
You are a Unity C# developer. Your task is MODULE 3 — Battle Mechanics.

Read INTERFACE.md and CONVENTIONS.md before writing any code.
CombatUnit.cs stub exists in Scripts/Battle/ — flesh it out fully.

Your deliverables:
- Scripts/Battle/BattleManager.cs — turn queue (SPD + d6 roll), round management, win/lose
- Scripts/Battle/BattleUI.cs — action menu, turn order display, damage numbers, HP bars
- Scripts/Battle/BattleCamera.cs — dynamic camera framing during combat
- ScriptableObjects/Abilities/ — 4 example AbilityData assets (basic attack, fireball, heal, stun)
- ScriptableObjects/StatusEffects/ — 3 examples (poison, stun, burn)

Key requirements:
- Turn order = Speed stat + Random.Range(1, 7). Re-roll ties.
- BattleManager publishes BattleStartedEvent and BattleEndedEvent via EventBus
- BattleManager.StartBattle() must call GameManager.Instance.ChangeState(GameState.Battle)
- On battle end, restore GameState.Exploring

Do NOT touch files in: Core/(read only), Character/, Dialogue/, World/, Landscape/, Player/, Inventory/, Audio/

Output branch: feature/module-03-battle-mechanics
```

---

## MODULE 4 — Dialogue & Quests
```
You are a Unity C# developer. Your task is MODULE 4 — Dialogue & Quests.

Read INTERFACE.md and CONVENTIONS.md before writing any code.

Your deliverables:
- Scripts/Dialogue/DialogueSystem.cs — integrates with Yarn Spinner package
- Scripts/Dialogue/DialogueUI.cs — speaker portrait, text box, animated text, choice buttons
- Scripts/Dialogue/QuestManager.cs — tracks active/completed quests, fires EventBus events
- Scripts/Dialogue/CutsceneDirector.cs — disables player input, triggers Cinemachine shots
- ScriptableObjects/Quests/ — 2 example QuestData assets

Key requirements:
- Use Yarn Spinner (yarn.build) for dialogue graphs — do not roll a custom parser
- DialogueSystem must call GameManager.Instance.ChangeState(GameState.Cutscene) on start
- QuestManager persists state through SaveSystem (quest flags as Dictionary<string,bool>)
- All quest IDs must follow CONVENTIONS.md ID format (lowercase-hyphen)

Package dependency: Yarn Spinner must be installed in the project.

Do NOT touch: Core/(read only), Battle/, Character/, World/, Landscape/, Player/, Inventory/, Audio/

Output branch: feature/module-04-dialogue-quests
```

---

## MODULE 5 — World Building
```
You are a Unity C# developer. Your task is MODULE 5 — World Building.

Read INTERFACE.md and CONVENTIONS.md before writing any code.

Your deliverables:
- Scripts/World/ZoneTransition.cs — trigger volume that calls SceneManager.LoadSceneAsync (additive)
- Scripts/World/WorldMapManager.cs — tracks discovered zones, POIs
- Scripts/World/WorldMapUI.cs — top-down map screen with zone icons and fog-of-war
- ScriptableObjects/Zones/ — 4 example WorldZoneData assets (Hub, Forest, Ruins, Dungeon)
- Scene template prefab — standard lighting rig, URP post-process volume, AudioListener

Key requirements:
- Scenes load additively — never use LoadSceneMode.Single for zone transitions
- ZoneTransition fires ZoneEnteredEvent and ZoneDiscoveredEvent via EventBus
- WorldMapUI only shows discovered zones — undiscovered zones shown as greyed icons
- Player spawn point is a named GameObject tag "SpawnPoint_[id]" in each scene

Do NOT touch: Core/(read only), Battle/, Character/, Dialogue/, Landscape/, Player/, Inventory/, Audio/

Output branch: feature/module-05-world-building
```

---

## MODULE 6 — Landscape & Environment
```
You are a Unity C# developer and technical artist. Your task is MODULE 6 — Landscape & Environment.

Read INTERFACE.md and CONVENTIONS.md before writing any code.

Your deliverables:
- Scripts/Landscape/EnvironmentManager.cs — weather system, transitions
- Scripts/Landscape/DayNightCycle.cs — directional light rotation, skybox blend, events
- Scripts/Landscape/ProceduralFoliage.cs — GPU-instanced grass using Graphics.DrawMeshInstanced
- 1 example terrain scene (SCN_Forest_Zone01) with basic heightmap, 2 texture layers, trees

Key requirements:
- DayNightCycle uses configurable time scale (default: 1 game hour = 2 real minutes)
- Weather transitions use coroutines — never snap
- Terrain: max 4 texture layers, LODs on all vegetation, static batching on rocks/props
- Performance budget per scene: < 200k tris, < 150 draw calls

Do NOT touch: Core/(read only), Battle/, Character/, Dialogue/, World/, Player/, Inventory/, Audio/

Output branch: feature/module-06-landscape-environment
```

---

## MODULE 7 — Player Controller & Camera
```
You are a Unity C# developer. Your task is MODULE 7 — Player Controller & Camera.

Read INTERFACE.md and CONVENTIONS.md before writing any code.

Your deliverables:
- Scripts/Player/PlayerController.cs — movement (WASD + gamepad), jump, sprint, roll
- Scripts/Player/InteractionSystem.cs — sphere overlap detect + E-key prompt
- Scripts/Player/CameraController.cs — Cinemachine third-person with collision
- Scripts/Player/PlayerStats.cs — HP, MP, gold, exp, level
- Scripts/Player/LockOnSystem.cs — Z-targeting nearest enemy in battle range

Key requirements:
- Use Unity new Input System only — define action maps: Exploration, Battle, UI, Cutscene
- PlayerController subscribes to GameStateChangedEvent and disables movement in Battle/Cutscene
- InteractionSystem uses Physics.OverlapSphere, NOT raycasting
- PlayerStats.ModifyHP publishes PlayerHPChangedEvent; death publishes PlayerDiedEvent
- CameraController uses Cinemachine FreeLook or CinemachineVirtualCamera — no custom orbit math

Package dependency: Cinemachine must be installed.

Do NOT touch: Core/(read only), Battle/, Character/, Dialogue/, World/, Landscape/, Inventory/, Audio/

Output branch: feature/module-07-player-controller
```

---

## MODULE 8 — Inventory & Economy
```
You are a Unity C# developer. Your task is MODULE 8 — Inventory & Economy.

Read INTERFACE.md and CONVENTIONS.md before writing any code.

Your deliverables:
- Scripts/Inventory/Inventory.cs — item storage, stacking, sorting
- Scripts/Inventory/InventoryUI.cs — grid layout, drag-and-drop, item tooltip
- Scripts/Inventory/ShopUI.cs — buy/sell interface with gold display
- Scripts/Inventory/CraftingSystem.cs — recipe matching and item combining
- ScriptableObjects/Items/ — 5 example ItemData/ConsumableData assets
- ScriptableObjects/LootTables/ — 2 example LootTable assets (goblin, chest)

Key requirements:
- LootTable.Roll() must implement weighted random using cumulative weight
- Inventory publishes ItemAddedEvent and ItemRemovedEvent via EventBus
- InventoryUI must reflect Inventory state — subscribe to events, never poll
- Inventory state serializes to InventorySnapshot for SaveSystem
- Gold is stored in PlayerStats, not Inventory — use PlayerStats.ModifyGold()

Do NOT touch: Core/(read only), Battle/, Character/, Dialogue/, World/, Landscape/, Player/, Audio/

Output branch: feature/module-08-inventory-economy
```

---

## MODULE 9 — Audio & Polish
```
You are a Unity C# developer. Your task is MODULE 9 — Audio & Polish.

Read INTERFACE.md and CONVENTIONS.md before writing any code.

Your deliverables:
- Scripts/Audio/AudioManager.cs — pooled SFX (20 sources), music crossfade, volume control
- Scripts/Audio/FootstepSystem.cs — Physics.Raycast surface detection, plays correct clip set
- Scripts/Audio/UIAnimator.cs — DOTween-based PopIn/PopOut/FadeIn/FadeOut/SlideIn
- Scripts/Audio/CameraShake.cs — Cinemachine Impulse-based shake with ShakePreset enum
- Scripts/Audio/VFXManager.cs — particle pool (30 pooled objects), Play(prefab, pos) API
- ScriptableObjects/Playlists/ — 2 example MusicPlaylist assets (Town, Forest)

Key requirements:
- AudioManager uses object pooling — never AudioSource.PlayClipAtPoint (not poolable)
- AudioManager subscribes to ZoneEnteredEvent and auto-crossfades music playlist
- CameraShake uses CinemachineImpulseSource — not manual camera transform jitter
- UIAnimator uses DOTween (free version) — always call .SetUpdate(true) so UI tweens work while paused
- FootstepSystem must support at least 4 surface types: Grass, Stone, Wood, Water

Package dependencies: DOTween, Cinemachine must be installed.

Do NOT touch: Core/(read only), Battle/, Character/, Dialogue/, World/, Landscape/, Player/, Inventory/

Output branch: feature/module-09-audio-polish
```
