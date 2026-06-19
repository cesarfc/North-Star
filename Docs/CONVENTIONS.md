# CONVENTIONS.md — Naming & Structure Rules
> Every agent must follow these. Consistency = fewer merge conflicts.

---

## File Naming

| Asset Type | Pattern | Example |
|---|---|---|
| C# Script | `PascalCase.cs` | `PlayerController.cs` |
| ScriptableObject asset | `SO_Type_Name` | `SO_Armor_IronChest` |
| Prefab | `PFB_DescriptiveName` | `PFB_Enemy_Goblin` |
| Scene | `SCN_ZoneName` | `SCN_Forest_Zone01` |
| Animation Clip | `ANIM_Subject_Action` | `ANIM_Player_Attack01` |
| Animator Controller | `AC_CharacterName` | `AC_Player` |
| Texture (Albedo) | `T_MeshName_Albedo` | `T_IronArmor_Albedo` |
| Texture (Normal) | `T_MeshName_Normal` | `T_IronArmor_Normal` |
| Audio SFX | `SFX_Context_Action` | `SFX_Combat_SwordHit` |
| Audio Music | `MUS_ZoneName_Mood` | `MUS_Forest_Ambient` |
| UI Sprite | `UI_ElementName` | `UI_Button_Primary` |
| Material | `MAT_SurfaceName` | `MAT_Forest_Ground` |

---

## Folder Structure

```
Assets/
├── _Game/                     ← All project files live here
│   ├── Scripts/
│   │   ├── Core/              ← EventBus, GameManager, SaveSystem, SharedTypes
│   │   ├── Character/         ← Module 2
│   │   ├── Battle/            ← Module 3
│   │   ├── Dialogue/          ← Module 4
│   │   ├── World/             ← Module 5
│   │   ├── Landscape/         ← Module 6
│   │   ├── Player/            ← Module 7
│   │   ├── Inventory/         ← Module 8
│   │   └── Audio/             ← Module 9
│   ├── ScriptableObjects/
│   │   ├── Armor/
│   │   ├── Hair/
│   │   ├── Abilities/
│   │   ├── StatusEffects/
│   │   ├── Items/
│   │   ├── LootTables/
│   │   ├── Quests/
│   │   ├── Zones/
│   │   └── Playlists/
│   ├── Prefabs/
│   │   ├── Characters/
│   │   ├── Enemies/
│   │   ├── Environment/
│   │   ├── UI/
│   │   └── VFX/
│   ├── Scenes/
│   │   ├── SCN_MainMenu
│   │   ├── SCN_Hub_Town
│   │   ├── SCN_Forest_Zone01
│   │   ├── SCN_Ruins_Zone01
│   │   └── SCN_Dungeon_Zone01
│   ├── Art/
│   │   ├── Characters/
│   │   ├── Environment/
│   │   ├── UI/
│   │   └── VFX/
│   ├── Audio/
│   │   ├── SFX/
│   │   └── Music/
│   └── Animations/
│       ├── Player/
│       ├── Enemies/
│       └── UI/
├── Plugins/                   ← Third-party (DOTween, Yarn Spinner, etc.)
└── Settings/                  ← URP renderer, Input Actions, etc.)
```

---

## Code Style

```csharp
// ✅ Public properties: PascalCase
public int CurrentHP { get; private set; }

// ✅ Private fields: _camelCase with underscore prefix
private int _turnIndex;

// ✅ Constants: ALL_CAPS
private const int MAX_PARTY_SIZE = 4;

// ✅ Events: OnEventName
public event Action<CombatUnit> OnDeath;

// ✅ Coroutines: named with Co prefix
private IEnumerator CoFadeIn(float duration) { ... }

// ✅ Every public method needs XML doc
/// <summary>Apply damage and fire death event if HP reaches zero.</summary>
public int TakeDamage(int amount, DamageType type) { ... }

// ❌ Never use FindObjectOfType in production code
// ❌ Never use string tags for GameObject.FindWithTag — use direct references
// ❌ Never use Update() for non-movement logic — use EventBus instead
```

---

## ScriptableObject IDs

All `itemId`, `questId`, `zoneId`, `abilityId` etc. must be:
- **Lowercase with hyphens**: `iron-chest-armor`, `quest-find-the-key`, `forest-zone-01`
- **Unique across the entire project** — prefix by type if needed: `armor-iron-chest`
- **Never changed after creation** — other assets and save files reference them by ID

---

## EventBus Usage

```csharp
// ✅ Subscribe in OnEnable, unsubscribe in OnDisable
private void OnEnable()  => EventBus.Subscribe<PlayerDiedEvent>(OnPlayerDied);
private void OnDisable() => EventBus.Unsubscribe<PlayerDiedEvent>(OnPlayerDied);

private void OnPlayerDied(PlayerDiedEvent e)
{
    // handle it
}

// ✅ Publish with a fully populated struct
EventBus.Publish(new PlayerDiedEvent { position = transform.position });

// ❌ Never cache another module's MonoBehaviour component
// ❌ Never call methods on another module directly — publish an event instead
```

---

## Performance Rules

- Terrain: max **4 texture layers** per zone
- Draw calls: **< 150 per scene** (use static batching + GPU instancing)
- Triangle budget: **< 200k per scene**
- Avoid `FindObjectOfType` and `GameObject.Find` — use references or EventBus
- Pool particles and audio sources — never Instantiate/Destroy per-frame
- Use `[SerializeField] private` not `public` for Inspector-visible fields
