using System;
using System.Collections.Generic;
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

    /// <summary>
    /// Roll the loot table and return the dropped items, using Unity's RNG.
    /// Picks a random number of drops in [minDrops, maxDrops]; each drop selects
    /// one entry by weighted cumulative probability and is repeated per the
    /// entry's quantity. Returns an empty array when there is nothing to drop.
    /// </summary>
    public ItemData[] Roll()
    {
        return Roll(
            () => UnityEngine.Random.value,                 // uniform [0,1)
            (min, max) => UnityEngine.Random.Range(min, max) // [min,max)
        );
    }

    /// <summary>
    /// Deterministic, engine-free overload of <see cref="Roll()"/> for testing.
    /// <paramref name="rng01"/> must yield a value in [0,1); <paramref name="rangeInt"/>
    /// must return an int in the half-open range [minInclusive, maxExclusive).
    /// Selection uses cumulative weights: an entry of weight w is chosen with
    /// probability w / sum(weights). Entries with weight &lt;= 0 are never chosen.
    /// </summary>
    public ItemData[] Roll(Func<float> rng01, Func<int, int, int> rangeInt)
    {
        if (entries == null || entries.Length == 0) return Array.Empty<ItemData>();

        // Total weight across all entries that can actually drop.
        float totalWeight = 0f;
        for (int i = 0; i < entries.Length; i++)
            if (entries[i].weight > 0f && entries[i].item != null)
                totalWeight += entries[i].weight;

        if (totalWeight <= 0f) return Array.Empty<ItemData>();

        int lo = Mathf.Max(0, Mathf.Min(minDrops, maxDrops));
        int hi = Mathf.Max(minDrops, maxDrops);
        int dropCount = lo == hi ? lo : rangeInt(lo, hi + 1); // inclusive upper bound

        var results = new List<ItemData>(dropCount);
        for (int d = 0; d < dropCount; d++)
        {
            var picked = PickWeighted(totalWeight, rng01);
            if (picked == null) continue;

            int qMin = Mathf.Max(1, picked.Value.minQuantity);
            int qMax = Mathf.Max(qMin, picked.Value.maxQuantity);
            int qty = qMin == qMax ? qMin : rangeInt(qMin, qMax + 1);

            for (int q = 0; q < qty; q++)
                results.Add(picked.Value.item);
        }

        return results.ToArray();
    }

    /// <summary>
    /// Select one entry by cumulative weight. <paramref name="totalWeight"/> is the
    /// pre-summed weight of all droppable entries; <paramref name="rng01"/> supplies
    /// a uniform [0,1) sample. Returns null only if nothing is droppable.
    /// </summary>
    private LootEntry? PickWeighted(float totalWeight, Func<float> rng01)
    {
        float roll = Mathf.Clamp01(rng01()) * totalWeight;
        float cumulative = 0f;

        for (int i = 0; i < entries.Length; i++)
        {
            if (entries[i].weight <= 0f || entries[i].item == null) continue;

            cumulative += entries[i].weight;
            if (roll < cumulative)
                return entries[i];
        }

        // Floating-point edge case (roll == totalWeight): fall back to the last
        // droppable entry.
        for (int i = entries.Length - 1; i >= 0; i--)
            if (entries[i].weight > 0f && entries[i].item != null)
                return entries[i];

        return null;
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
