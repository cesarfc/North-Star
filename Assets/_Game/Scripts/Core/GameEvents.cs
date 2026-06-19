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

/// <summary>
/// Request to change the player's gold. The source of truth lives in
/// PlayerStats (Module 7). Modules that must NOT reference Player — e.g. the
/// Inventory/Economy module's shop — publish this to ask for a gold change.
/// PlayerStats listens, applies it via ModifyGold, then broadcasts the
/// resulting PlayerGoldChangedEvent. Negative delta = spend, positive = grant.
/// </summary>
public struct GoldChangeRequestEvent
{
    public int delta;
}

// ─────────────────────────────────────────────
// BATTLE
// ─────────────────────────────────────────────

public struct BattleStartedEvent
{
    public ICombatant[] allies;
    public ICombatant[] enemies;
}

public struct BattleEndedEvent
{
    public BattleResult result;
}

public struct UnitDiedEvent
{
    public ICombatant unit;
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
