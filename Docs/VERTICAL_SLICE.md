# North-Star — Phase 4 Vertical Slice

A single, hand-playable scene (`SCN_VerticalSlice`) that wires several independently-built
modules together end-to-end. It exists to prove the architecture: modules stay isolated behind
their own asmdefs and talk only through `EventBus`, yet they compose into one coherent loop —
walk, interact, pick up, fight, and cross into another zone.

Unity **6000.5.0f1** · URP · New Input System.

---

## How to run

1. Open `Assets/_Game/Scenes/SCN_VerticalSlice.unity`.
2. Press **Play**.
3. **Click the Game view** so it has keyboard/mouse input focus.

The scene asset is already committed, so this is all a human needs to do. (It is *generated*
headlessly by `Assets/_Game/Editor/Smoke/SliceSceneBuilder.cs` — see "How the scene is built"
below — but you do not need to run that to play it.)

## Controls (Exploration input map)

| Action   | Binding            |
|----------|--------------------|
| Move     | WASD / arrow keys  |
| Sprint   | Shift              |
| Jump     | Space              |
| Roll     | Ctrl               |
| Interact | E                  |

Bindings come from `Assets/Settings/PlayerInputActions.inputactions` (the `Exploration` action
map). `PlayerController` and `InteractionSystem` both bind to this map.

UI feedback in this slice is intentionally **IMGUI (`OnGUI`)** so it has no font/TMP/Canvas
dependencies — every overlay you see (HUD, NPC lines, pickup toast, battle panel, zone banner)
is drawn directly.

---

## What's in the scene, and which module each piece exercises

Walk the player up to each object (it sits roughly straight ahead, +Z) and press **E**.

| Object in scene        | Component (in `NorthStar.Game`) | Module exercised | What it does |
|------------------------|---------------------------------|------------------|--------------|
| `Player`               | `PlayerController`, `InteractionSystem`, `PlayerStats`, `Inventory` | **Player** + **Inventory** | Third-person move/sprint/jump/roll; raycast-style interaction; HP/Gold stats; item storage. |
| `Sun` + `DayNightCycle`| (Landscape `DayNightCycle`)     | **Landscape**    | Auto-advancing day/night clock at `_timeScale = 720` (≈ 1 game-hour every 5 real seconds) so the HUD clock visibly ticks. Rotates the directional `Sun`. |
| `SliceHud`             | `SliceHud`                      | **Player / Inventory / Landscape** | Top-left HUD reading live state every frame: `DayNightCycle.GetCurrentHour()`, `PlayerStats.CurrentHP/MaxHP/Gold`, and `Inventory.GetAllItems()` count. Proof the three systems are wired and running together. |
| `NPC`                  | `SmokeNPC` (`IInteractable`)    | **Core (GameManager + SaveSystem)** | Talk → runs a tiny placeholder conversation, flips `GameState` to `Cutscene` then back to `Exploring`, and writes a save via `SaveSystem.Save("smoke", …)`. Exercises the walk → talk → save sync loop. |
| `Pickup_HealthPotion`  | `PickupItem` (`IInteractable`)  | **Inventory**    | Adds `SO_Item_HealthPotion` to the player `Inventory` (which publishes `ItemAddedEvent`), shows a toast, then removes itself. Watch the HUD `Items` count tick up. |
| `BattleTrigger`        | `BattleEncounter` (`IInteractable`) | **Battle**   | Spins up a real `BattleManager` with a Hero vs. two Goblins, then **auto-drives** the turn loop (each unit basic-attacks until `BattleManager` resolves Victory/Defeat). An IMGUI panel shows round number, active unit, and live HP. Drives `GameState` Battle ↔ Exploring. |
| `ZoneGate` (+ `ZoneBanner`) | `ZoneTransition` (World) / `ZoneBanner` | **World** | A pass-through trigger (`isTrigger`) — walking into it additively loads `SCN_Zone02` and publishes `ZoneEnteredEvent`. `ZoneBanner` subscribes on the `EventBus` and flashes the entered zone name. |
| `SCN_Zone02` (loaded)  | `ZoneLabel`                     | **World**        | The additively-loaded second zone ("Outpost"), offset at +60 X so it doesn't overlap. `ZoneLabel` announces it on-screen. |
| `CharacterStation`     | `CharacterStation` (`IInteractable`) | **Character** | Press E → IMGUI panel driving the real `CharacterCustomizer` on the player: equip the 3 example armors, set the 2 hairstyles, random hair colour, unequip. Shows the live `CharacterLoadout`; fires `OnLoadoutChanged` + publishes `LoadoutChangedEvent`. (Mesh swap is silent without rigged renderers — the loadout logic + events are what's exercised.) |
| `ShopStation` (+ `ShopUI`) | `ShopStation` (`IInteractable`) | **Economy** | Press E → IMGUI shop: Buy/Sell Health Potion · Mana Potion · Iron Sword via the real `ShopUI.Buy/Sell`, which publishes `GoldChangeRequestEvent` (→ `PlayerStats`) and updates `Inventory`. Gold + item count move live in the HUD. |
| `AudioManager` + `SliceSfx` | `SliceSfx`                 | **Audio**        | `AudioManager` self-subscribes to `ZoneEnteredEvent` (zone-music crossfade); `SliceSfx` plays SFX on `ItemAddedEvent` / `BattleStartedEvent`. **Wired but silent** until clip assets are registered. |

> The `BattleEncounter` auto-driver stands in for player BattleUI input + enemy AI (neither is
> wired in this slice). It demonstrates the Battle turn order / damage / win-lose loop without
> authoring a combat UI.

---

## Architecture note

All cross-system wiring lives in the **`NorthStar.Game`** composition-root assembly — the only
asmdef permitted to reference multiple gameplay modules (it references Core, Data, Player,
Inventory, Landscape, Battle, Character, and Audio). The glue classes (`SmokeNPC`, `PickupItem`,
`SliceHud`, `BattleEncounter`, `ZoneBanner`, `ZoneLabel`, `CharacterStation`, `ShopStation`,
`SliceSfx`, `SmokeBootstrap`) are the *only* place modules are joined up.

The gameplay modules themselves stay isolated: each has its own `NorthStar.<Module>` asmdef,
references only Core + Data + the Unity packages it uses, and **never** references a sibling
module. Cross-module communication goes through `EventBus` (e.g. `ItemAddedEvent`,
`ZoneEnteredEvent`). The *absence* of the asmdef reference is what makes the "no cross-module
calls" rule compiler-enforced.

`SmokeBootstrap` is a minimal stand-in for a real main-menu → gameplay flow: on `Start` it just
calls `GameManager.Instance.ChangeState(GameState.Exploring)` so the `PlayerController` (which
freezes movement outside `Exploring`) is live immediately.

### How the scene is built (for maintainers)

`SliceSceneBuilder.Build` (in the editor-only `NorthStar.Editor.Smoke` asmdef) assembles
`SCN_VerticalSlice` headlessly — it creates the GameObjects, adds components, and wires the
private `[SerializeField]` references via `SerializedObject`. It also builds `SCN_Zone02` and
registers all three scenes (`SCN_Smoke`, `SCN_VerticalSlice`, `SCN_Zone02`) in Build Settings.
Run it with:

```
Unity -batchmode -quit -projectPath . -executeMethod SliceSceneBuilder.Build
```

You normally don't need to: the resulting `.unity` assets are committed.

---

## What still needs CONTENT (not code)

The systems below are coded and tested; what's missing is *authored assets*, not engineering:

- **Rigged character/armor meshes** — the `CharacterStation` already drives the real
  `CharacterCustomizer` (loadout logic + events fire), but the player capsule has no skinned
  renderers, so the mesh swap is invisible until rigged armor/hair meshes on a shared skeleton exist.
- **Audio clips** — `AudioManager` + `SliceSfx` are wired in the scene but silent until real
  `SFX_`/`MUS_` clips are registered (SFX clip set + per-zone `MusicPlaylist` tracks).
- **Yarn `.yarn` dialogue graphs + the `YARN_SPINNER` scripting define** — currently the NPC
  uses a placeholder hard-coded conversation (`SmokeNPC._lines`) instead of the Dialogue
  module's Yarn graphs.
- **DOTween** — optional; `UIAnimator` currently uses coroutines, so no tween package is
  required to run the slice.
- **Real terrain / art** — the scene uses primitive ground/cubes/capsules as placeholders.
- **Player spawn-repositioning on zone load** — `ZoneTransition` passes a `_spawnPointId`
  (`"spawn-outpost"`), but the player is not yet teleported to a spawn point in the newly
  loaded zone.

---

## Verification status

- **9 gameplay modules** built (Core, Player, Inventory, Landscape, World, Battle, Character,
  Dialogue, Audio).
- EditMode tests **green: 241 / 241**.
- Built and verified in Unity **6000.5.0f1** via the central headless gate
  (`-batchmode -runTests -testPlatform EditMode`).
