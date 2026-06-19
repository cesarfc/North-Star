using System.Collections.Generic;
using NorthStar.Inventory;
using NUnit.Framework;

namespace NorthStar.Inventory.Tests
{
    /// <summary>
    /// EditMode unit tests for <see cref="InventoryCore"/> — the pure, engine-free
    /// storage logic behind the Inventory MonoBehaviour. Covers stacking, spill
    /// across stacks, non-stackable handling, all-or-nothing removal, counts,
    /// sorting, and snapshot round-tripping.
    /// </summary>
    public class InventoryCoreTests
    {
        /// <summary>Engine-free test item; avoids creating ScriptableObjects in EditMode.</summary>
        private readonly struct TestItem : IItemInfo
        {
            public TestItem(string id, bool stackable, int maxStack)
            {
                ItemId = id;
                IsStackable = stackable;
                MaxStackSize = maxStack;
            }

            public string ItemId { get; }
            public bool IsStackable { get; }
            public int MaxStackSize { get; }
        }

        private static TestItem Potion => new TestItem("potion-health", true, 99);
        private static TestItem Sword => new TestItem("sword-iron", false, 1);
        private static TestItem Herb => new TestItem("material-herb", true, 5);

        // ── AddItem / stacking ──────────────────────────────────────────────

        [Test]
        public void AddItem_Stackable_MergesIntoOneStack()
        {
            var inv = new InventoryCore();
            inv.AddItem(Potion, 3);
            inv.AddItem(Potion, 2);

            Assert.AreEqual(5, inv.GetItemCount("potion-health"));
            Assert.AreEqual(1, inv.StackCount);
        }

        [Test]
        public void AddItem_Stackable_SpillsOverMaxStackSize()
        {
            var inv = new InventoryCore();
            inv.AddItem(Herb, 12); // maxStack 5 -> 5 + 5 + 2

            Assert.AreEqual(12, inv.GetItemCount("material-herb"));
            Assert.AreEqual(3, inv.StackCount);
        }

        [Test]
        public void AddItem_NonStackable_OneStackPerUnit()
        {
            var inv = new InventoryCore();
            inv.AddItem(Sword, 3);

            Assert.AreEqual(3, inv.GetItemCount("sword-iron"));
            Assert.AreEqual(3, inv.StackCount);
        }

        [Test]
        public void AddItem_NullOrNonPositive_Fails()
        {
            var inv = new InventoryCore();
            Assert.IsFalse(inv.AddItem(null, 1));
            Assert.IsFalse(inv.AddItem(Potion, 0));
            Assert.IsFalse(inv.AddItem(Potion, -5));
            Assert.AreEqual(0, inv.StackCount);
        }

        [Test]
        public void AddItem_RaisesItemAddedOnceWithTotalQuantity()
        {
            var inv = new InventoryCore();
            int calls = 0, lastQty = 0; string lastId = null;
            inv.ItemAdded += (id, qty) => { calls++; lastId = id; lastQty = qty; };

            inv.AddItem(Herb, 12); // spills into 3 stacks but is one logical add

            Assert.AreEqual(1, calls);
            Assert.AreEqual("material-herb", lastId);
            Assert.AreEqual(12, lastQty);
        }

        // ── RemoveItem ──────────────────────────────────────────────────────

        [Test]
        public void RemoveItem_AcrossStacks_RemovesExactQuantity()
        {
            var inv = new InventoryCore();
            inv.AddItem(Herb, 12); // 5 + 5 + 2
            bool ok = inv.RemoveItem("material-herb", 7);

            Assert.IsTrue(ok);
            Assert.AreEqual(5, inv.GetItemCount("material-herb"));
        }

        [Test]
        public void RemoveItem_EmptiesStacksAndDropsThem()
        {
            var inv = new InventoryCore();
            inv.AddItem(Potion, 5);
            inv.RemoveItem("potion-health", 5);

            Assert.AreEqual(0, inv.GetItemCount("potion-health"));
            Assert.IsFalse(inv.HasItem("potion-health"));
            Assert.AreEqual(0, inv.StackCount);
        }

        [Test]
        public void RemoveItem_MoreThanHeld_FailsAndChangesNothing()
        {
            var inv = new InventoryCore();
            inv.AddItem(Potion, 3);
            bool ok = inv.RemoveItem("potion-health", 4);

            Assert.IsFalse(ok);
            Assert.AreEqual(3, inv.GetItemCount("potion-health"));
        }

        [Test]
        public void RemoveItem_UnknownOrInvalid_Fails()
        {
            var inv = new InventoryCore();
            inv.AddItem(Potion, 1);
            Assert.IsFalse(inv.RemoveItem("nope", 1));
            Assert.IsFalse(inv.RemoveItem("potion-health", 0));
            Assert.IsFalse(inv.RemoveItem(null, 1));
        }

        [Test]
        public void RemoveItem_RaisesItemRemovedOnce()
        {
            var inv = new InventoryCore();
            inv.AddItem(Potion, 5);
            int calls = 0, lastQty = 0;
            inv.ItemRemoved += (_, qty) => { calls++; lastQty = qty; };

            inv.RemoveItem("potion-health", 2);
            Assert.AreEqual(1, calls);
            Assert.AreEqual(2, lastQty);
        }

        // ── HasItem / GetItemCount ──────────────────────────────────────────

        [Test]
        public void HasItem_ReflectsPresence()
        {
            var inv = new InventoryCore();
            Assert.IsFalse(inv.HasItem("potion-health"));
            inv.AddItem(Potion, 1);
            Assert.IsTrue(inv.HasItem("potion-health"));
        }

        [Test]
        public void GetItemCount_UnknownId_IsZero()
        {
            var inv = new InventoryCore();
            Assert.AreEqual(0, inv.GetItemCount("missing"));
            Assert.AreEqual(0, inv.GetItemCount(null));
        }

        // ── Sort ────────────────────────────────────────────────────────────

        [Test]
        public void Sort_Default_OrdersByItemIdAscending()
        {
            var inv = new InventoryCore();
            inv.AddItem(Sword, 1);   // sword-iron
            inv.AddItem(Potion, 1);  // potion-health
            inv.AddItem(Herb, 1);    // material-herb

            inv.Sort();

            var entries = inv.GetAllItems();
            Assert.AreEqual("material-herb", entries[0].itemId);
            Assert.AreEqual("potion-health", entries[1].itemId);
            Assert.AreEqual("sword-iron", entries[2].itemId);
        }

        [Test]
        public void Sort_CustomComparer_IsRespected()
        {
            var inv = new InventoryCore();
            inv.AddItem(Herb, 1);
            inv.AddItem(Potion, 1);

            // Descending by itemId.
            inv.Sort(Comparer<InventoryEntry>.Create(
                (a, b) => string.CompareOrdinal(b.itemId, a.itemId)));

            var entries = inv.GetAllItems();
            Assert.AreEqual("potion-health", entries[0].itemId);
            Assert.AreEqual("material-herb", entries[1].itemId);
        }

        // ── Snapshot serialization ──────────────────────────────────────────

        [Test]
        public void GetAllItems_ReturnsEntriesPerStack()
        {
            var inv = new InventoryCore();
            inv.AddItem(Herb, 7); // 5 + 2
            var entries = inv.GetAllItems();

            Assert.AreEqual(2, entries.Length);
            Assert.AreEqual(7, entries[0].quantity + entries[1].quantity);
        }

        [Test]
        public void LoadFromSnapshot_RestoresContentsExactly()
        {
            var snapshot = new InventorySnapshot
            {
                gold = 0,
                entries = new[]
                {
                    new InventoryEntry { itemId = "potion-health", quantity = 5 },
                    new InventoryEntry { itemId = "material-herb", quantity = 5 },
                    new InventoryEntry { itemId = "material-herb", quantity = 2 },
                }
            };

            var inv = new InventoryCore();
            inv.LoadFromSnapshot(snapshot);

            Assert.AreEqual(5, inv.GetItemCount("potion-health"));
            Assert.AreEqual(7, inv.GetItemCount("material-herb"));
            Assert.AreEqual(3, inv.StackCount);
        }

        [Test]
        public void Snapshot_RoundTrips()
        {
            var original = new InventoryCore();
            original.AddItem(Potion, 4);
            original.AddItem(Herb, 7);
            original.AddItem(Sword, 1);

            var snapshot = new InventorySnapshot { entries = original.GetAllItems() };

            var restored = new InventoryCore();
            restored.LoadFromSnapshot(snapshot);

            Assert.AreEqual(original.GetItemCount("potion-health"),
                            restored.GetItemCount("potion-health"));
            Assert.AreEqual(original.GetItemCount("material-herb"),
                            restored.GetItemCount("material-herb"));
            Assert.AreEqual(original.GetItemCount("sword-iron"),
                            restored.GetItemCount("sword-iron"));
            Assert.AreEqual(original.StackCount, restored.StackCount);
        }

        [Test]
        public void LoadFromSnapshot_SkipsInvalidEntriesAndClearsFirst()
        {
            var inv = new InventoryCore();
            inv.AddItem(Potion, 3); // should be cleared by the load

            var snapshot = new InventorySnapshot
            {
                entries = new[]
                {
                    new InventoryEntry { itemId = "material-herb", quantity = 2 },
                    new InventoryEntry { itemId = "", quantity = 5 },     // skipped
                    new InventoryEntry { itemId = "bad", quantity = 0 },  // skipped
                }
            };
            inv.LoadFromSnapshot(snapshot);

            Assert.AreEqual(0, inv.GetItemCount("potion-health"));
            Assert.AreEqual(2, inv.GetItemCount("material-herb"));
            Assert.AreEqual(1, inv.StackCount);
        }

        [Test]
        public void Clear_RemovesEverything()
        {
            var inv = new InventoryCore();
            inv.AddItem(Potion, 5);
            inv.Clear();
            Assert.AreEqual(0, inv.StackCount);
            Assert.IsFalse(inv.HasItem("potion-health"));
        }
    }
}
