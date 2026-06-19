using UnityEngine;

/// <summary>
/// Weighted random drop table. Roll() returns between minDrops and maxDrops items.
/// Instances live in ScriptableObjects/LootTables/.
/// </summary>
[CreateAssetMenu(fileName = "SO_LootTable_New", menuName = "Game/Items/LootTable")]
public class LootTable : ScriptableObject
{
    public LootEntry[] entries;
    [Range(0, 10)] public int minDrops = 1;
    [Range(0, 10)] public int maxDrops = 3;

    /// <summary>Roll the loot table and return a list of dropped items.</summary>
    public ItemData[] Roll()
    {
        // TODO: Module 8 implements weighted random roll (cumulative weight)
        // Placeholder — returns empty
        return System.Array.Empty<ItemData>();
    }
}

/// <summary>A single weighted entry in a LootTable.</summary>
[System.Serializable]
public struct LootEntry
{
    public ItemData item;
    [Range(0f, 100f)] public float weight;
    public int minQuantity;
    public int maxQuantity;
}
