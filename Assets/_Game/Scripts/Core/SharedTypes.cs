using System;
using UnityEngine;

// ─────────────────────────────────────────────
// CHARACTER / CUSTOMIZATION
// ─────────────────────────────────────────────

public enum EquipmentSlot { Head, Chest, Legs, Hands, Feet }

[Serializable]
public struct CharacterLoadout
{
    public string headArmorId;
    public string chestArmorId;
    public string legsArmorId;
    public string handsArmorId;
    public string feetArmorId;
    public string hairStyleId;
    public Color  hairColor;
}

// ─────────────────────────────────────────────
// BATTLE
// ─────────────────────────────────────────────

public enum DamageType { Physical, Fire, Ice, Lightning, Dark, Light, Heal, Buff, Debuff }
public enum BattleOutcome { Victory, Defeat, Fled }

[Serializable]
public struct BattleResult
{
    public BattleOutcome outcome;
    public int           expEarned;
    public int           goldEarned;
    // Populated by LootTable.Roll() — resolved at battle end
    public string[]      lootItemIds;
}

// ─────────────────────────────────────────────
// INVENTORY
// ─────────────────────────────────────────────

public enum ItemType { Weapon, Armor, Consumable, KeyItem, Material, QuestItem }

[Serializable]
public struct InventoryEntry
{
    public string itemId;
    public int    quantity;
}

[Serializable]
public struct InventorySnapshot
{
    public InventoryEntry[] entries;
    public int              gold;
}

// ─────────────────────────────────────────────
// QUEST
// ─────────────────────────────────────────────

[Serializable]
public struct QuestObjective
{
    public string objectiveId;
    public string description;
    public bool   isOptional;
}

// ─────────────────────────────────────────────
// WORLD
// ─────────────────────────────────────────────

public enum ZoneType { Town, Wilderness, Dungeon, Boss, Hub }

[Serializable]
public struct POIData
{
    public string   poiId;
    public string   label;
    public Vector2  mapPosition;
    public Sprite   icon;
}

// ─────────────────────────────────────────────
// DIALOGUE
// ─────────────────────────────────────────────

[Serializable]
public struct DialogueLine
{
    public string speakerName;
    public string text;
    public Sprite speakerPortrait;
}

[Serializable]
public struct DialogueChoice
{
    public int    index;
    public string text;
    public bool   isAvailable;
}

// ─────────────────────────────────────────────
// ENVIRONMENT
// ─────────────────────────────────────────────

public enum WeatherType { Clear, Overcast, Rain, HeavyRain, Fog, Snow }

// ─────────────────────────────────────────────
// AUDIO / POLISH
// ─────────────────────────────────────────────

public enum SlideDirection { Left, Right, Up, Down }
public enum ShakePreset    { Light, Medium, Heavy, BossHit }
