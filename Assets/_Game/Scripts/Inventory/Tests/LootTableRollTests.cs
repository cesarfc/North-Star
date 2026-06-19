using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace NorthStar.Inventory.Tests
{
    /// <summary>
    /// EditMode unit tests for <see cref="LootTable.Roll(System.Func{float},System.Func{int,int,int})"/> —
    /// the weighted cumulative drop logic. Uses the deterministic RNG overload so
    /// outcomes are exact, and creates throwaway ItemData instances in memory.
    /// </summary>
    public class LootTableRollTests
    {
        private ItemData _common;
        private ItemData _rare;

        [SetUp]
        public void SetUp()
        {
            _common = ScriptableObject.CreateInstance<ItemData>();
            _common.itemId = "item-common";
            _rare = ScriptableObject.CreateInstance<ItemData>();
            _rare.itemId = "item-rare";
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_common);
            Object.DestroyImmediate(_rare);
        }

        private LootTable MakeTable(LootEntry[] entries, int min, int max)
        {
            var table = ScriptableObject.CreateInstance<LootTable>();
            table.entries = entries;
            table.minDrops = min;
            table.maxDrops = max;
            return table;
        }

        private static LootEntry Entry(ItemData item, float weight, int qMin = 1, int qMax = 1) =>
            new LootEntry { item = item, weight = weight, minQuantity = qMin, maxQuantity = qMax };

        // ── Empty / degenerate cases ────────────────────────────────────────

        [Test]
        public void Roll_NoEntries_ReturnsEmpty()
        {
            var table = MakeTable(new LootEntry[0], 1, 1);
            var result = table.Roll(() => 0f, (lo, hi) => lo);
            Assert.AreEqual(0, result.Length);
            Object.DestroyImmediate(table);
        }

        [Test]
        public void Roll_AllZeroWeight_ReturnsEmpty()
        {
            var table = MakeTable(new[] { Entry(_common, 0f), Entry(_rare, 0f) }, 1, 3);
            var result = table.Roll(() => 0.5f, (lo, hi) => lo);
            Assert.AreEqual(0, result.Length);
            Object.DestroyImmediate(table);
        }

        // ── Weighted selection ──────────────────────────────────────────────

        [Test]
        public void Roll_LowRoll_PicksFirstEntry()
        {
            // common weight 90, rare weight 10. roll ~0 -> within first bucket.
            var table = MakeTable(new[] { Entry(_common, 90f), Entry(_rare, 10f) }, 1, 1);
            var result = table.Roll(() => 0.0f, (lo, hi) => lo);

            Assert.AreEqual(1, result.Length);
            Assert.AreEqual("item-common", result[0].itemId);
            Object.DestroyImmediate(table);
        }

        [Test]
        public void Roll_HighRoll_PicksSecondEntry()
        {
            // total 100; rng 0.95 -> 95 which falls past the 90 cumulative -> rare.
            var table = MakeTable(new[] { Entry(_common, 90f), Entry(_rare, 10f) }, 1, 1);
            var result = table.Roll(() => 0.95f, (lo, hi) => lo);

            Assert.AreEqual(1, result.Length);
            Assert.AreEqual("item-rare", result[0].itemId);
            Object.DestroyImmediate(table);
        }

        [Test]
        public void Roll_RollAtBoundary_FallsIntoCorrectBucket()
        {
            // total 100; rng 0.90 -> 90.0; first bucket is [0,90), so 90 -> rare.
            var table = MakeTable(new[] { Entry(_common, 90f), Entry(_rare, 10f) }, 1, 1);
            var result = table.Roll(() => 0.90f, (lo, hi) => lo);

            Assert.AreEqual("item-rare", result[0].itemId);
            Object.DestroyImmediate(table);
        }

        [Test]
        public void Roll_DistributionMatchesWeights()
        {
            // Deterministic sweep of rng values; count how many land on each item.
            var table = MakeTable(new[] { Entry(_common, 80f), Entry(_rare, 20f) }, 1, 1);

            int commonCount = 0, rareCount = 0;
            for (int i = 0; i < 100; i++)
            {
                float r = i / 100f; // 0.00 .. 0.99
                var result = table.Roll(() => r, (lo, hi) => lo);
                if (result[0].itemId == "item-common") commonCount++;
                else rareCount++;
            }

            Assert.AreEqual(80, commonCount);
            Assert.AreEqual(20, rareCount);
            Object.DestroyImmediate(table);
        }

        // ── Drop count ──────────────────────────────────────────────────────

        [Test]
        public void Roll_DropCount_RespectsRange()
        {
            var table = MakeTable(new[] { Entry(_common, 100f) }, 2, 2);
            // min==max==2, so the rangeInt for drop count is never consulted.
            var result = table.Roll(() => 0f, (lo, hi) => lo);
            Assert.AreEqual(2, result.Length);
            Object.DestroyImmediate(table);
        }

        [Test]
        public void Roll_DropCount_UsesRangeIntInclusiveUpper()
        {
            var table = MakeTable(new[] { Entry(_common, 100f) }, 1, 3);
            // rangeInt asked for [1,4); force it to return the max (3).
            int capturedLo = -1, capturedHi = -1;
            var result = table.Roll(() => 0f, (lo, hi) =>
            {
                capturedLo = lo; capturedHi = hi; return hi - 1; // = 3
            });

            Assert.AreEqual(1, capturedLo);
            Assert.AreEqual(4, capturedHi); // inclusive upper => hi+1
            Assert.AreEqual(3, result.Length);
            Object.DestroyImmediate(table);
        }

        // ── Quantity per drop ───────────────────────────────────────────────

        [Test]
        public void Roll_EntryQuantity_ExpandsResult()
        {
            // single drop, but the entry yields 3 of the item.
            var table = MakeTable(new[] { Entry(_common, 100f, qMin: 3, qMax: 3) }, 1, 1);
            var result = table.Roll(() => 0f, (lo, hi) => lo);

            Assert.AreEqual(3, result.Length);
            foreach (var item in result)
                Assert.AreEqual("item-common", item.itemId);
            Object.DestroyImmediate(table);
        }

        [Test]
        public void Roll_OnlyDroppableEntriesSelected()
        {
            // rare has zero weight; every roll must pick common.
            var table = MakeTable(new[] { Entry(_common, 50f), Entry(_rare, 0f) }, 1, 1);
            var seen = new HashSet<string>();
            for (int i = 0; i < 20; i++)
            {
                float r = i / 20f;
                var result = table.Roll(() => r, (lo, hi) => lo);
                seen.Add(result[0].itemId);
            }
            Assert.IsTrue(seen.Contains("item-common"));
            Assert.IsFalse(seen.Contains("item-rare"));
            Object.DestroyImmediate(table);
        }
    }
}
