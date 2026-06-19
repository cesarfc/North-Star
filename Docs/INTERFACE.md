# INTERFACE.md — Shared Contract for All Agents
> **Read this before writing a single line of code.**
> Every public method, event, and ScriptableObject schema listed here is frozen.
> Do not rename, retype, or restructure these signatures without orchestrator approval.
>
> **Authoritative.** This contract has been reconciled to match the shipped `Assets/_Game/Scripts/Core`
> code (2026-06-19). Where this doc and the code ever disagree, the code wins — flag it to the
> orchestrator so the doc is corrected.

---

## Architecture Rules (Non-Negotiable)

1. **No direct cross-module references.** Modules communicate only through `EventBus.cs`.
2. **All data lives in ScriptableObjects.** No hardcoded stats, strings, or IDs in MonoBehaviours.
3. **One class per file.** Filename must match class name exactly.
4. **Null checks on all EventBus subscriptions.** Unsubscribe in `OnDestroy`.
5. **Every public method gets an XML `<summary>` doc comment.**
6. **Use Unity 2023 LTS + URP. New Input System only (no legacy Input.GetAxis).**

---

## Module 1 — Core Architecture

### EventBus.cs
```csharp
// Publish a typed event to all subscribers
EventBus.Publish<T>(T eventData)

// Subscribe a handler — always unsubscribe in OnDestroy
EventBus.Subscribe<T>(Action<T> handler)
EventBus.Unsubscribe<T>(Action<T> handler)
```

### GameManager.cs
```csharp
// Singleton access
GameManager.Instance

// Game state enum
enum GameState { MainMenu, Exploring, Battle, Cutscene, Paused, GameOver }

GameManager.ChangeState(GameState newState)
GameManager.CurrentState → GameState
GameManager.OnStateChanged → event(GameState prev, GameState next)
```

### SaveSystem.cs
```csharp
SaveSystem.Save(string slotName, GameSaveData data) → bool
SaveSystem.Load(string slotName) → GameSaveData
SaveSystem.DeleteSave(string slotName)
SaveSystem.SaveExists(string slotName) → bool
```

### GameSaveData.cs (serializable class)
```csharp
// [Serializable] CLASS, not a struct — JsonUtility serializes reference types and
// cannot serialize Dictionary, so quest flags are stored as an array of entries.
[Serializable]
class GameSaveData {
    string savedAt;                 // ISO-8601 UTC, set by SaveSystem.Save
    float playTimeSeconds;
    string currentZoneId;
    string activeQuestId;
    CharacterLoadout loadout;
    InventorySnapshot inventory;
    QuestFlagEntry[] questFlags;    // key = questId, value = completed
}

[Serializable]
struct QuestFlagEntry { string questId; bool completed; }
```
> ⚠️ `SaveSystem.Load` returns `null` (not a default struct) when a slot is missing — callers must null-check.

---

## Module 2 — Character & Customization

### CharacterCustomizer.cs
```csharp
CharacterCustomizer.Equip(EquipmentSlot slot, ArmorData data)
CharacterCustomizer.Unequip(EquipmentSlot slot)
CharacterCustomizer.SetHair(HairStyleData data)
CharacterCustomizer.SetHairColor(Color color)
CharacterCustomizer.GetCurrentLoadout() → CharacterLoadout
CharacterCustomizer.OnLoadoutChanged → event(CharacterLoadout loadout)
```

### ArmorData.asset (ScriptableObject)
```csharp
class ArmorData : ScriptableObject {
    string itemId;
    string displayName;
    EquipmentSlot slot;      // Head, Chest, Legs, Hands, Feet
    Mesh mesh;
    Material[] materials;
    int defenseBonus;
    int weightClass;         // 1=Light, 2=Medium, 3=Heavy
    Sprite icon;
}
```

### HairStyleData.asset (ScriptableObject)
```csharp
class HairStyleData : ScriptableObject {
    string styleId;
    string displayName;
    Mesh mesh;
    Color[] availableColors;
    Sprite previewIcon;
}
```

### CharacterLoadout.cs (serializable struct)
```csharp
struct CharacterLoadout {
    string headArmorId;
    string chestArmorId;
    string legsArmorId;
    string handsArmorId;
    string feetArmorId;
    string hairStyleId;
    Color hairColor;
}
```

---

## Module 3 — Battle Mechanics

### BattleManager.cs
```csharp
BattleManager.StartBattle(CombatUnit[] allies, CombatUnit[] enemies)
BattleManager.EndBattle(BattleResult result)
BattleManager.OnBattleStart  → event(CombatUnit[] allies, CombatUnit[] enemies)
BattleManager.OnBattleEnd    → event(BattleResult result)
BattleManager.OnTurnStart    → event(CombatUnit activeUnit)
BattleManager.OnTurnEnd      → event(CombatUnit unit)
```

### CombatUnit.cs
```csharp
CombatUnit.TakeDamage(int amount, DamageType type) → int actualDamage
CombatUnit.Heal(int amount) → int actualHeal
CombatUnit.ApplyStatus(StatusEffectData effect)        // StatusEffectData = the ScriptableObject
CombatUnit.RemoveStatus(string statusId)
CombatUnit.UseAbility(AbilityData ability, CombatUnit[] targets) → bool success
CombatUnit.OnDeath → event(CombatUnit unit)
CombatUnit.OnStatusApplied → event(StatusEffectData effect)
CombatUnit.OnStatusRemoved → event(StatusEffectData effect)

// Stats (read-only from outside)
int MaxHP, CurrentHP, MaxMP, CurrentMP, Attack, Defense, Speed;
bool IsAlive → bool
List<AbilityData>      Abilities;
List<StatusEffectData> ActiveStatuses;
```

> **⚠️ Known Issue (Module 3 must fix):** the stub `CombatUnit.TakeDamage` publishes
> `PlayerHPChangedEvent` for *every* unit, including enemies. Module 3 should either guard on
> `isPlayerControlled` or introduce a dedicated `UnitHPChangedEvent { CombatUnit unit; int current; int max; }`
> and reserve `PlayerHPChangedEvent` for the player. Add the new event struct to `GameEvents.cs` if you go that route.

### AbilityData.asset (ScriptableObject)
```csharp
class AbilityData : ScriptableObject {
    string abilityId;
    string displayName;
    string description;
    int mpCost;
    DamageType damageType;    // Physical, Fire, Ice, Lightning, Dark, Light, Heal, Buff, Debuff
    float damageMultiplier;
    bool isMultiTarget;
    string animationTrigger;  // matches Animator parameter name
    StatusEffectData[] appliedEffects;
    Sprite icon;
}
```

### BattleResult.cs (defined in Core/SharedTypes.cs)
```csharp
enum BattleOutcome { Victory, Defeat, Fled }
[Serializable]
struct BattleResult {
    BattleOutcome outcome;
    int           expEarned;
    int           goldEarned;
    string[]      lootItemIds;   // item IDs resolved from LootTable.Roll() at battle end
}
```
> Loot is carried as **item IDs** (strings), not `ItemData` references, so a `BattleResult` is
> serializable/save-safe. Resolve IDs back to `ItemData` via the inventory/item registry.

---

## Module 4 — Storyline & Dialogue

### DialogueSystem.cs
```csharp
DialogueSystem.StartDialogue(string yarnGraphId)
DialogueSystem.AdvanceDialogue()
DialogueSystem.SelectChoice(int choiceIndex)
DialogueSystem.EndDialogue()
DialogueSystem.OnDialogueStart  → event(string graphId)
DialogueSystem.OnLineDelivered  → event(DialogueLine line)
DialogueSystem.OnChoicesPresented → event(DialogueChoice[] choices)
DialogueSystem.OnDialogueEnd    → event
```

### QuestManager.cs
```csharp
QuestManager.StartQuest(string questId)
QuestManager.CompleteObjective(string questId, string objectiveId)
QuestManager.CompleteQuest(string questId)
QuestManager.FailQuest(string questId)
QuestManager.IsQuestActive(string questId) → bool
QuestManager.IsQuestComplete(string questId) → bool
QuestManager.GetActiveQuests() → QuestData[]
QuestManager.OnQuestStarted   → event(QuestData quest)
QuestManager.OnQuestCompleted → event(QuestData quest)
QuestManager.OnObjectiveCompleted → event(string questId, string objectiveId)
```

### QuestData.asset (ScriptableObject)
```csharp
class QuestData : ScriptableObject {
    string questId;
    string displayName;
    string description;
    string[] prerequisiteQuestIds;
    QuestObjective[] objectives;
    ItemData[] rewardItems;
    int rewardGold;
    int rewardExp;
}

struct QuestObjective {
    string objectiveId;
    string description;
    bool isOptional;
}
```

---

## Module 5 — World Building

### ZoneTransition.cs
```csharp
ZoneTransition.TravelTo(string sceneId, string spawnPointId)
ZoneTransition.OnTransitionStart → event(string fromScene, string toScene)
ZoneTransition.OnTransitionComplete → event(string sceneId)
```

### WorldZoneData.asset (ScriptableObject)
```csharp
class WorldZoneData : ScriptableObject {
    string zoneId;
    string displayName;
    string sceneId;
    string[] connectedZoneIds;
    ZoneType zoneType;       // Town, Dungeon, Wilderness, Boss
    bool isDiscoveredByDefault;
    Sprite mapIcon;
    Vector2 mapPosition;
}
```

### WorldMapManager.cs
```csharp
WorldMapManager.DiscoverZone(string zoneId)
WorldMapManager.IsZoneDiscovered(string zoneId) → bool
WorldMapManager.MarkPOI(string zoneId, POIData poi)
WorldMapManager.GetDiscoveredZones() → WorldZoneData[]
```

---

## Module 6 — Landscape & Environment

### EnvironmentManager.cs
```csharp
EnvironmentManager.SetWeather(WeatherType type, float transitionDuration)
EnvironmentManager.GetCurrentWeather() → WeatherType
EnvironmentManager.OnWeatherChanged → event(WeatherType newWeather)

enum WeatherType { Clear, Overcast, Rain, HeavyRain, Fog, Snow }
```

### DayNightCycle.cs
```csharp
DayNightCycle.GetCurrentHour() → float          // 0.0–23.99
DayNightCycle.SetHour(float hour)
DayNightCycle.SetTimeScale(float scale)         // 1=realtime, 60=1min/sec
DayNightCycle.OnHourChanged → event(int hour)
DayNightCycle.OnDawnStart   → event
DayNightCycle.OnDuskStart   → event
```

---

## Module 7 — Player Controller

### PlayerController.cs
```csharp
PlayerController.SetMovementEnabled(bool enabled)
PlayerController.SetPosition(Vector3 position)
PlayerController.Teleport(Vector3 position, Quaternion rotation)
PlayerController.IsMoving → bool
PlayerController.OnLanded → event
```

### InteractionSystem.cs
```csharp
InteractionSystem.OnInteractableEntered → event(IInteractable target)
InteractionSystem.OnInteractableExited  → event(IInteractable target)
InteractionSystem.OnInteract            → event(IInteractable target)

// IInteractable interface — all NPCs, doors, pickups must implement
interface IInteractable {
    string InteractionPrompt { get; }
    void Interact(GameObject interactor);
}
```

### PlayerStats.cs
```csharp
PlayerStats.ModifyHP(int delta)       → int newHP
PlayerStats.ModifyMP(int delta)       → int newMP
PlayerStats.ModifyGold(int delta)     → int newGold
PlayerStats.AddExp(int amount)
PlayerStats.GetLevel()                → int
PlayerStats.OnDeath                   → event
PlayerStats.OnLevelUp                 → event(int newLevel)
PlayerStats.OnHPChanged               → event(int current, int max)

// Read-only properties
int MaxHP, CurrentHP, MaxMP, CurrentMP, Gold, Exp, Level;
```

---

## Module 8 — Inventory & Economy

### Inventory.cs
```csharp
Inventory.AddItem(ItemData item, int quantity)    → bool success
Inventory.RemoveItem(string itemId, int quantity) → bool success
Inventory.HasItem(string itemId)                  → bool
Inventory.GetItemCount(string itemId)             → int
Inventory.GetAllItems()                           → InventoryEntry[]
Inventory.OnItemAdded   → event(ItemData item, int quantity)
Inventory.OnItemRemoved → event(ItemData item, int quantity)
```

### ItemData.asset (ScriptableObject)
```csharp
class ItemData : ScriptableObject {
    string itemId;
    string displayName;
    string description;
    ItemType itemType;      // Weapon, Armor, Consumable, KeyItem, Material
    int buyPrice;
    int sellPrice;
    bool isStackable;
    int maxStackSize;
    Sprite icon;
    // Subclassed by ConsumableData, WeaponData, etc.
}
```

### LootTable.asset (ScriptableObject)
```csharp
class LootTable : ScriptableObject {
    LootEntry[] entries;
    int minDrops;
    int maxDrops;
    LootTable.Roll() → ItemData[]
}

struct LootEntry {
    ItemData item;
    float weight;
    int minQuantity;
    int maxQuantity;
}
```

---

## Module 9 — Audio & Polish

### AudioManager.cs
```csharp
AudioManager.PlaySFX(string clipId, Vector3 worldPosition)
AudioManager.PlaySFX(string clipId)                        // 2D version
AudioManager.PlayMusic(string trackId, float fadeTime)
AudioManager.StopMusic(float fadeTime)
AudioManager.SetMasterVolume(float volume)                 // 0–1
AudioManager.SetMusicVolume(float volume)
AudioManager.SetSFXVolume(float volume)
```

### CameraShake.cs
```csharp
CameraShake.Shake(float intensity, float duration)
CameraShake.Shake(ShakePreset preset)

enum ShakePreset { Light, Medium, Heavy, BossHit }
```

### UIAnimator.cs
```csharp
UIAnimator.PopIn(RectTransform target, float duration = 0.25f)
UIAnimator.PopOut(RectTransform target, float duration = 0.2f)
UIAnimator.FadeIn(CanvasGroup group, float duration = 0.3f)
UIAnimator.FadeOut(CanvasGroup group, float duration = 0.3f)
UIAnimator.SlideIn(RectTransform target, SlideDirection dir, float duration = 0.3f)
```

---

## EventBus Event Types (Canonical List)

All events are plain C# structs. Add fields but never remove or rename existing ones.

```csharp
// State
struct GameStateChangedEvent   { GameState prev; GameState next; }

// Player
struct PlayerDiedEvent         { Vector3 position; }
struct PlayerLeveledUpEvent    { int newLevel; int oldLevel; }
struct PlayerHPChangedEvent    { int current; int max; }
struct PlayerGoldChangedEvent  { int newTotal; int delta; }

// Battle
struct BattleStartedEvent      { CombatUnit[] allies; CombatUnit[] enemies; }
struct BattleEndedEvent        { BattleResult result; }
struct UnitDiedEvent           { CombatUnit unit; bool wasAlly; }

// Quest
struct QuestStartedEvent       { string questId; }
struct QuestCompletedEvent     { string questId; }
struct ObjectiveCompletedEvent { string questId; string objectiveId; }

// World
struct ZoneEnteredEvent        { string zoneId; string fromZoneId; }
struct ZoneDiscoveredEvent     { string zoneId; }

// Inventory
struct ItemAddedEvent          { string itemId; int quantity; }
struct ItemRemovedEvent        { string itemId; int quantity; }

// Dialogue
struct DialogueStartedEvent    { string graphId; }
struct DialogueEndedEvent      { string graphId; }
```
