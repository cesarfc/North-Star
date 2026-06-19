using UnityEngine;

// ─────────────────────────────────────────────
// GAME STATE
// ─────────────────────────────────────────────

public struct GameStateChangedEvent
{
    public GameState prev;
    public GameState next;
}

// ─────────────────────────────────────────────
// PLAYER
// ─────────────────────────────────────────────

public struct PlayerDiedEvent
{
    public Vector3 position;
}

public struct PlayerLeveledUpEvent
{
    public int oldLevel;
    public int newLevel;
}

public struct PlayerHPChangedEvent
{
    public int current;
    public int max;
}

public struct PlayerGoldChangedEvent
{
    public int newTotal;
    public int delta;
}

// ─────────────────────────────────────────────
// BATTLE
// ─────────────────────────────────────────────

public struct BattleStartedEvent
{
    public CombatUnit[] allies;
    public CombatUnit[] enemies;
}

public struct BattleEndedEvent
{
    public BattleResult result;
}

public struct UnitDiedEvent
{
    public CombatUnit unit;
    public bool wasAlly;
}

// ─────────────────────────────────────────────
// QUEST
// ─────────────────────────────────────────────

public struct QuestStartedEvent
{
    public string questId;
}

public struct QuestCompletedEvent
{
    public string questId;
}

public struct ObjectiveCompletedEvent
{
    public string questId;
    public string objectiveId;
}

// ─────────────────────────────────────────────
// WORLD
// ─────────────────────────────────────────────

public struct ZoneEnteredEvent
{
    public string zoneId;
    public string fromZoneId;
}

public struct ZoneDiscoveredEvent
{
    public string zoneId;
}

// ─────────────────────────────────────────────
// INVENTORY
// ─────────────────────────────────────────────

public struct ItemAddedEvent
{
    public string itemId;
    public int quantity;
}

public struct ItemRemovedEvent
{
    public string itemId;
    public int quantity;
}

// ─────────────────────────────────────────────
// DIALOGUE
// ─────────────────────────────────────────────

public struct DialogueStartedEvent
{
    public string graphId;
}

public struct DialogueEndedEvent
{
    public string graphId;
}
