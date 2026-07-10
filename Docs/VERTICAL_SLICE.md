# North-Star — Phase 4 Vertical Slice

A hand-playable game loop that wires the independently-built modules together end-to-end **on
real, factory-generated assets**: boot menu → explore a terrain zone → talk to the quest giver
(Yarn dialogue) → pick up items → fight a player-driven battle → shop → customize the rigged
character → cross into a second zone. Modules stay isolated behind their own asmdefs and talk
only through `EventBus`.

Unity **6000.5.0f1** · URP · New Input System · Yarn Spinner 3.2.

---

## How to run

1. Open `Assets/_Game/Scenes/SCN_Boot.unity` and press **Play** (New Game → gameplay), or open
   `Assets/_Game/Scenes/SCN_VerticalSlice.unity` directly.
2. **Click the Game view** so it has keyboard/mouse input focus.

The scene assets are committed. They are *generated* headlessly by the builders in
`Assets/_Game/Editor/Smoke/` — see "How the scenes are built" below — but you do not need to
run those to play.

## Controls (Exploration input map)

| Action   | Binding            |
|----------|--------------------|
| Move     | WASD / arrow keys  |
| Sprint   | Shift              |
| Jump     | Space              |
| Roll     | Ctrl               |
| Interact | E                  |
| Dialogue advance | E / Space / click |

Bindings come from `Assets/Settings/PlayerInputActions.inputactions` (the `Exploration` action
map). UI in this slice is intentionally **IMGUI (`OnGUI`)** so it has no font/TMP/Canvas
dependencies.

---

## Asset provenance (the two factory packs)

All art in the slice comes from the sibling `../Characters` factory repo:

- **Character pack** (`npm run export-northstar`): rigged chibi body + 12 equipment FBX +
  `northstar-manifest.json` (bones, sockets, slot map) → `Assets/_Game/Art/Characters/`.
  `CharacterAssetLibraryBuilder` turns the manifest into `ArmorData`/`HairStyleData` wardrobe SOs.
- **Land pack** (`npm run export-northstar-land`): 8 low-poly props (trees/rocks/bush/stump/
  mushrooms/grass tuft) + 4 tileable ground textures + `northstar-land-manifest.json` (scatter
  densities) → `Assets/_Game/Art/Environment/`. `SliceEnvironmentBuilder` turns it into Unity
  Terrain (plateau/hills/rim, slope-splatted layers), deterministic prop scatter, and
  GPU-instanced grass (`ProceduralFoliage`).
- **Audio** is generated in-repo by `Tools/generate_audio.py` (deterministic stdlib synth):
  4 SFX + 2 ambient music loops.

## What's in the scene, and which module each piece exercises

| Object in scene        | Component (in `NorthStar.Game`) | Module exercised | What it does |
|------------------------|---------------------------------|------------------|--------------|
| `SCN_Boot` (scene)     | `MainMenuUI`                    | **Core**         | New Game / Continue (when the save slot exists) / Quit; `GameManager` starts in `MainMenu` state; the dressed rig poses behind the menu. |
| `Player`               | `PlayerController`, `InteractionSystem`, `PlayerStats`, `Inventory`, `CharacterCustomizer` | **Player + Inventory + Character** | Third-person move/sprint/jump/roll on the terrain; the visual is the rigged chibi (`SliceCharacterRig`): tunic/pants/boots pre-equipped through the real `SkeletonRebinder`, hair on the head socket, sword/shield/cape mounted on manifest sockets. |
| `Terrain_Slice` + props + grass | (Landscape `ProceduralFoliage`) | **Landscape** | 120 m terrain bowl (flat plateau, Perlin hills, raised rim), 176 scattered land-pack props with colliders, ~6 000 GPU-instanced grass tufts sampling terrain height. |
| `Sun` + `DayNightCycle`| (Landscape `DayNightCycle`)     | **Landscape**    | Auto-advancing clock (`_timeScale = 720`) rotating the sun; HUD clock ticks. |
| `SliceHud`             | `SliceHud`                      | **Player / Inventory / Landscape / Dialogue** | Live HUD: time, HP/Gold, item count, and the active **quest log**. |
| `NPC_ElderVane`        | `DialogueNPC` + `DialogueSystem`/`YarnDialogueRunner` + `SliceDialogueUI` | **Dialogue** | Talk → runs the `ElderVane_Intro` Yarn graph (lines + a choice), flips `GameState` to `Cutscene` and back. `DialogueQuestBridge` then starts `quest-find-the-spark` and completes its talk objective — watch the HUD quest log. Repeat visits get the "back again" branch (`$met_vane`). |
| `Pickup_HealthPotion`  | `PickupItem` (`IInteractable`)  | **Inventory**    | Adds `SO_Item_HealthPotion` (publishes `ItemAddedEvent` → pickup SFX), HUD `Items` ticks up. |
| `BattleTrigger`        | `BattleEncounter` (`IInteractable`) | **Battle**   | Real `BattleManager` turn loop, **player-driven**: on the hero's turn an action menu offers Attack / Fireball / Heal (resolved via `CombatUnit.UseAbility` with the Ability SOs, MP costs included); goblins play simple AI turns. Victory/Defeat via the module. |
| `ZoneGate` (+ `ZoneBanner`) | `ZoneTransition` (World) / `ZoneBanner` | **World** | Walking in additively loads `SCN_Zone02`, **teleports the player to `SpawnPoint_spawn-outpost`**, publishes `ZoneEnteredEvent` (→ zone banner + music crossfade to the Outpost playlist). |
| `SCN_Zone02` (loaded)  | `ZoneLabel`                     | **World**        | Second zone ("Outpost") on its own 80 m terrain bowl at +100 X with its own prop scatter and spawn point. |
| `CharacterStation`     | `CharacterStation` (`IInteractable`) | **Character** | IMGUI panel driving the real `CharacterCustomizer`: equip any of the 8 wardrobe pieces (iron chestplate/helmet, cloth tunic, leather pants/boots + 3 placeholder chests) and 3 hairstyles — **mesh swaps are visible** on the shared skeleton via bone-name rebind; hair recolors via property block. |
| `ShopStation` (+ `ShopUI`) | `ShopStation` (`IInteractable`) | **Economy** | IMGUI shop: Buy/Sell through the real `ShopUI` (publishes `GoldChangeRequestEvent` → `PlayerStats`); gold + items move live in the HUD. |
| `AudioManager` + `SliceSfx` + `FootstepSystem` | `SliceSfx`, `SliceFootsteps` | **Audio** | Registered clip sets: pickup + battle-start SFX on events, grass footsteps every stride (surface raycast), and per-zone music playlists (Wildwood / Outpost) crossfaded on `ZoneEnteredEvent` (the bootstrap announces `zone-slice-01` on play). |

---

## Architecture note

All cross-system wiring lives in the **`NorthStar.Game`** composition-root assembly — the only
asmdef permitted to reference multiple gameplay modules (Core, Data, Player, Inventory,
Landscape, Battle, Character, Audio, Dialogue). The glue classes (`MainMenuUI`, `DialogueNPC`,
`SliceDialogueUI`, `DialogueQuestBridge`, `PickupItem`, `SliceHud`, `BattleEncounter`,
`ZoneBanner`, `ZoneLabel`, `CharacterStation`, `ShopStation`, `SliceSfx`, `SliceFootsteps`,
`SmokeBootstrap`) are the *only* place modules are joined up.

The gameplay modules stay isolated: each has its own `NorthStar.<Module>` asmdef, references
only Core + Data + the Unity packages it uses, and **never** references a sibling module.
Cross-module communication goes through `EventBus`. Yarn Spinner types appear **only** in
`YarnDialogueRunner` (Dialogue module), compiled under the `YARN_SPINNER` define which the
asmdef's `versionDefines` turns on automatically when the package is installed.

### How the scenes are built (for maintainers)

`EndToEndBuilder.BuildAll` (editor-only `NorthStar.Editor.Smoke` asmdef) chains everything in
dependency order: wardrobe SOs from the character manifest → `SCN_Zone02` + `SCN_VerticalSlice`
(terrain/props/grass, dressed rig, dialogue/battle/audio wiring) → `SCN_Boot` (registered first
in Build Settings). Run it with:

```
Unity -batchmode -projectPath . -executeMethod NorthStar.EditorTools.EndToEndBuilder.BuildAll
```

Individual builders remain runnable (`SliceSceneBuilder.Build`, `BootSceneBuilder.Build`,
`CharacterAssetLibraryBuilder.BuildAndExit`, `ArmorSmokeTestBuilder.Build`). You normally don't
need any of them: the resulting `.unity`/`.asset` files are committed.

---

## What still needs CONTENT (not code)

- **Ember shrines / quest completion** — `quest-find-the-spark` starts and its talk objective
  completes via dialogue; the three `gather-three-embers` shrine pickups aren't placed yet, so
  the quest can't be finished.
- **Animations** — the rig is posed but not animated (no walk/attack clips retargeted to the
  24-bone skeleton yet).
- **Enemy visuals in battle** — battles are stat-driven (`CombatUnit` GameObjects with no
  meshes); goblin models would come from the factory's enemy pipeline.
- **DOTween** — optional; `UIAnimator` uses coroutines, so no tween package is required.
- **Richer audio** — the generated placeholder WAVs prove the wiring; real clips can replace
  them 1:1 by clipId.

---

## Verification status

- **9 gameplay modules** + Game composition root, all compiled via the central headless gate.
- EditMode tests **green: 253 / 253** (includes `ZoneTransitionTests` for the spawn-point fix).
- Content build green: `END_TO_END_BUILD_OK` (8 wardrobe assets, both terrains, 3 scenes).
- Standalone macOS player build from `SCN_Boot` (see `Builds/`).
