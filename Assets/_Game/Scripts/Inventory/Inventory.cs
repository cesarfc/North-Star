using System;
using UnityEngine;

namespace NorthStar.Inventory
{
    /// <summary>
    /// Player inventory MonoBehaviour. Owns an engine-free <see cref="InventoryCore"/>
    /// for the actual storage/stacking rules and acts as the glue layer:
    /// it raises local C# events carrying the resolved <c>ItemData</c> and
    /// publishes module-agnostic <see cref="ItemAddedEvent"/>/<see cref="ItemRemovedEvent"/>
    /// onto the EventBus. Persists to / restores from <see cref="InventorySnapshot"/>
    /// for the SaveSystem. Gold is NOT stored here — it lives in PlayerStats
    /// (Module 7); see <see cref="ShopUI"/> for economy transactions.
    /// </summary>
    public class Inventory : MonoBehaviour
    {
        [Tooltip("Resolves itemId strings (from snapshots/events) back to ItemData assets.")]
        [SerializeField] private ItemRegistry _itemRegistry;

        private readonly InventoryCore _core = new InventoryCore();

        /// <summary>Raised when items are added. Args: (item, quantityAdded).</summary>
        public event Action<ItemData, int> OnItemAdded;

        /// <summary>Raised when items are removed. Args: (item, quantityRemoved).</summary>
        public event Action<ItemData, int> OnItemRemoved;

        private void OnEnable()
        {
            _core.ItemAdded += HandleCoreItemAdded;
            _core.ItemRemoved += HandleCoreItemRemoved;
        }

        private void OnDisable()
        {
            _core.ItemAdded -= HandleCoreItemAdded;
            _core.ItemRemoved -= HandleCoreItemRemoved;
        }

        /// <summary>
        /// Add <paramref name="quantity"/> of an item, stacking per the item's
        /// rules. Returns false on a null item or non-positive quantity.
        /// </summary>
        public bool AddItem(ItemData item, int quantity)
        {
            if (item == null) return false;
            return _core.AddItem(new ItemInfo(item), quantity);
        }

        /// <summary>
        /// Remove up to <paramref name="quantity"/> of an item (all-or-nothing).
        /// Returns false if fewer than <paramref name="quantity"/> are held.
        /// </summary>
        public bool RemoveItem(string itemId, int quantity) =>
            _core.RemoveItem(itemId, quantity);

        /// <summary>Returns true if at least one unit of the item is held.</summary>
        public bool HasItem(string itemId) => _core.HasItem(itemId);

        /// <summary>Total quantity of an item across all its stacks.</summary>
        public int GetItemCount(string itemId) => _core.GetItemCount(itemId);

        /// <summary>Every stack as serializable <see cref="InventoryEntry"/> records.</summary>
        public InventoryEntry[] GetAllItems() => _core.GetAllItems();

        /// <summary>
        /// Build a save-ready snapshot of the current contents. Gold is sourced
        /// from PlayerStats elsewhere and written into the snapshot by the save
        /// pipeline; this fills only the item entries.
        /// </summary>
        public InventorySnapshot ToSnapshot() =>
            new InventorySnapshot { entries = _core.GetAllItems(), gold = 0 };

        /// <summary>Restore inventory contents from a saved snapshot (no events raised).</summary>
        public void LoadFromSnapshot(InventorySnapshot snapshot) =>
            _core.LoadFromSnapshot(snapshot);

        // ── core -> events glue ─────────────────────────────────────────────

        private void HandleCoreItemAdded(string itemId, int quantity)
        {
            EventBus.Publish(new ItemAddedEvent { itemId = itemId, quantity = quantity });
            OnItemAdded?.Invoke(Resolve(itemId), quantity);
        }

        private void HandleCoreItemRemoved(string itemId, int quantity)
        {
            EventBus.Publish(new ItemRemovedEvent { itemId = itemId, quantity = quantity });
            OnItemRemoved?.Invoke(Resolve(itemId), quantity);
        }

        private ItemData Resolve(string itemId) =>
            _itemRegistry != null ? _itemRegistry.Resolve(itemId) : null;
    }
}
