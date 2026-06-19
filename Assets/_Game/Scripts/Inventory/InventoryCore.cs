using System;
using System.Collections.Generic;

namespace NorthStar.Inventory
{
    /// <summary>
    /// Pure, MonoBehaviour-free implementation of inventory storage: stacking,
    /// removal, queries, sorting, and snapshot (de)serialization. Holds no Unity
    /// dependencies so it can be exercised directly in EditMode unit tests. The
    /// <see cref="Inventory"/> MonoBehaviour owns one of these and forwards its
    /// callbacks onto the EventBus — keeping the rules separable from engine glue.
    ///
    /// Each item is described by an <see cref="IItemInfo"/> (the data the core
    /// needs to make stacking decisions) so the core never touches
    /// <c>UnityEngine.ScriptableObject</c> directly.
    /// </summary>
    public class InventoryCore
    {
        // Ordered list of stacks. Insertion order is preserved for a stable UI
        // until Sort() is called. Keyed lookups go through _index for O(1) access.
        private readonly List<InventoryStack> _stacks = new List<InventoryStack>();
        private readonly Dictionary<string, InventoryStack> _index =
            new Dictionary<string, InventoryStack>();

        /// <summary>Raised after an add succeeds. Args: (itemId, quantityAdded).</summary>
        public event Action<string, int> ItemAdded;

        /// <summary>Raised after a remove succeeds. Args: (itemId, quantityRemoved).</summary>
        public event Action<string, int> ItemRemoved;

        /// <summary>Number of distinct item stacks currently held.</summary>
        public int StackCount => _stacks.Count;

        /// <summary>
        /// Add <paramref name="quantity"/> of an item. Stackable items merge into
        /// the existing stack up to <see cref="IItemInfo.MaxStackSize"/>, spilling
        /// into additional stacks as needed; non-stackable items occupy one stack
        /// each. Returns false (and adds nothing) on a null item or a
        /// non-positive quantity. Raises <see cref="ItemAdded"/> once on success.
        /// </summary>
        public bool AddItem(IItemInfo item, int quantity)
        {
            if (item == null || string.IsNullOrEmpty(item.ItemId) || quantity <= 0)
                return false;

            int remaining = quantity;

            if (item.IsStackable)
            {
                int cap = Math.Max(1, item.MaxStackSize);

                // Top up existing stacks of this item first.
                foreach (var stack in _stacks)
                {
                    if (remaining <= 0) break;
                    if (stack.ItemId != item.ItemId) continue;

                    int room = cap - stack.Quantity;
                    if (room <= 0) continue;

                    int moved = Math.Min(room, remaining);
                    stack.Quantity += moved;
                    remaining -= moved;
                }

                // Open new stacks for any overflow.
                while (remaining > 0)
                {
                    int moved = Math.Min(cap, remaining);
                    AddNewStack(item, moved);
                    remaining -= moved;
                }
            }
            else
            {
                // Non-stackable: one entry per unit.
                for (int i = 0; i < quantity; i++)
                    AddNewStack(item, 1);
                remaining = 0;
            }

            ItemAdded?.Invoke(item.ItemId, quantity);
            return true;
        }

        /// <summary>
        /// Remove up to <paramref name="quantity"/> of an item across all its
        /// stacks. Fails (removing nothing) unless the full requested quantity is
        /// present, so the operation is all-or-nothing. Returns false on an empty
        /// id, a non-positive quantity, or insufficient count. Raises
        /// <see cref="ItemRemoved"/> once on success.
        /// </summary>
        public bool RemoveItem(string itemId, int quantity)
        {
            if (string.IsNullOrEmpty(itemId) || quantity <= 0) return false;
            if (GetItemCount(itemId) < quantity) return false;

            int remaining = quantity;

            // Walk back-to-front so emptied stacks can be removed safely.
            for (int i = _stacks.Count - 1; i >= 0 && remaining > 0; i--)
            {
                var stack = _stacks[i];
                if (stack.ItemId != itemId) continue;

                int taken = Math.Min(stack.Quantity, remaining);
                stack.Quantity -= taken;
                remaining -= taken;

                if (stack.Quantity == 0)
                {
                    _stacks.RemoveAt(i);
                    _index.Remove(stack.ItemId);
                }
            }

            // A non-stackable item may have multiple single stacks; rebuild the
            // index so it points at any surviving stack of this id.
            ReindexItem(itemId);

            ItemRemoved?.Invoke(itemId, quantity);
            return true;
        }

        /// <summary>Returns true if at least one unit of the item is held.</summary>
        public bool HasItem(string itemId) => GetItemCount(itemId) > 0;

        /// <summary>Total quantity of an item summed across all its stacks.</summary>
        public int GetItemCount(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return 0;

            int total = 0;
            foreach (var stack in _stacks)
                if (stack.ItemId == itemId)
                    total += stack.Quantity;
            return total;
        }

        /// <summary>
        /// Snapshot every stack as serializable <see cref="InventoryEntry"/>
        /// records, in current stack order. Used to build an
        /// <see cref="InventorySnapshot"/> for the SaveSystem.
        /// </summary>
        public InventoryEntry[] GetAllItems()
        {
            var entries = new InventoryEntry[_stacks.Count];
            for (int i = 0; i < _stacks.Count; i++)
                entries[i] = new InventoryEntry
                {
                    itemId = _stacks[i].ItemId,
                    quantity = _stacks[i].Quantity
                };
            return entries;
        }

        /// <summary>
        /// Sort stacks in place. By default orders alphabetically by itemId for a
        /// stable, deterministic layout; pass a comparer to order by type, value,
        /// etc. Sorting never merges or splits stacks.
        /// </summary>
        public void Sort(IComparer<InventoryEntry> comparer = null)
        {
            if (comparer == null)
            {
                _stacks.Sort((a, b) =>
                    string.CompareOrdinal(a.ItemId, b.ItemId));
            }
            else
            {
                _stacks.Sort((a, b) => comparer.Compare(
                    new InventoryEntry { itemId = a.ItemId, quantity = a.Quantity },
                    new InventoryEntry { itemId = b.ItemId, quantity = b.Quantity }));
            }
        }

        /// <summary>
        /// Replace the entire contents from a saved <see cref="InventorySnapshot"/>.
        /// Clears current stacks first. Skips null/empty ids and non-positive
        /// quantities. Does not raise add/remove events — this is a load, not a
        /// gameplay action.
        /// </summary>
        public void LoadFromSnapshot(InventorySnapshot snapshot)
        {
            Clear();
            if (snapshot.entries == null) return;

            foreach (var entry in snapshot.entries)
            {
                if (string.IsNullOrEmpty(entry.itemId) || entry.quantity <= 0)
                    continue;

                // Snapshots store pre-collapsed stacks; restore them verbatim so a
                // saved layout round-trips exactly.
                AddNewStack(entry.itemId, entry.quantity);
            }
        }

        /// <summary>Remove all stacks. Does not raise events.</summary>
        public void Clear()
        {
            _stacks.Clear();
            _index.Clear();
        }

        // ── internals ──────────────────────────────────────────────────────

        private void AddNewStack(IItemInfo item, int quantity) =>
            AddNewStack(item.ItemId, quantity);

        private void AddNewStack(string itemId, int quantity)
        {
            var stack = new InventoryStack { ItemId = itemId, Quantity = quantity };
            _stacks.Add(stack);
            _index[itemId] = stack; // last-write-wins; index is a fast existence probe
        }

        private void ReindexItem(string itemId)
        {
            _index.Remove(itemId);
            foreach (var stack in _stacks)
            {
                if (stack.ItemId == itemId)
                {
                    _index[itemId] = stack;
                    return;
                }
            }
        }

        /// <summary>A single mutable stack of one item type.</summary>
        private class InventoryStack
        {
            public string ItemId;
            public int Quantity;
        }
    }
}
