using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace NorthStar.Inventory
{
    /// <summary>
    /// Event-driven inventory grid. Reflects inventory state purely by subscribing
    /// to <see cref="ItemAddedEvent"/> / <see cref="ItemRemovedEvent"/> on the
    /// EventBus — it never polls the Inventory in Update(). Resolves item ids to
    /// ItemData via the <see cref="ItemRegistry"/> for icons and labels.
    /// </summary>
    public class InventoryUI : MonoBehaviour
    {
        [Header("Data")]
        [Tooltip("Resolves itemId strings from events back to ItemData assets.")]
        [SerializeField] private ItemRegistry _itemRegistry;
        [Tooltip("Inventory queried once on enable to seed the grid.")]
        [SerializeField] private Inventory _inventory;

        [Header("Grid")]
        [Tooltip("Parent transform that lays out the item slots (e.g. a GridLayoutGroup).")]
        [SerializeField] private Transform _slotContainer;
        [Tooltip("Prefab with an InventorySlotView component, instantiated per stack.")]
        [SerializeField] private InventorySlotView _slotPrefab;

        // Live count per itemId, mirrored from events so the grid never polls.
        private readonly Dictionary<string, int> _counts = new Dictionary<string, int>();
        private readonly Dictionary<string, InventorySlotView> _slots =
            new Dictionary<string, InventorySlotView>();

        private void OnEnable()
        {
            EventBus.Subscribe<ItemAddedEvent>(OnItemAdded);
            EventBus.Subscribe<ItemRemovedEvent>(OnItemRemoved);
            SeedFromInventory();
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<ItemAddedEvent>(OnItemAdded);
            EventBus.Unsubscribe<ItemRemovedEvent>(OnItemRemoved);
        }

        /// <summary>
        /// Populate the grid once from the current inventory snapshot. Called on
        /// enable so the UI matches state captured before it was listening; all
        /// subsequent updates arrive via events.
        /// </summary>
        private void SeedFromInventory()
        {
            ClearGrid();
            if (_inventory == null) return;

            foreach (var entry in _inventory.GetAllItems())
            {
                _counts.TryGetValue(entry.itemId, out int existing);
                _counts[entry.itemId] = existing + entry.quantity;
            }

            foreach (var kvp in _counts)
                RefreshSlot(kvp.Key, kvp.Value);
        }

        private void OnItemAdded(ItemAddedEvent e)
        {
            _counts.TryGetValue(e.itemId, out int existing);
            int total = existing + e.quantity;
            _counts[e.itemId] = total;
            RefreshSlot(e.itemId, total);
        }

        private void OnItemRemoved(ItemRemovedEvent e)
        {
            if (!_counts.TryGetValue(e.itemId, out int existing)) return;

            int total = existing - e.quantity;
            if (total <= 0)
            {
                _counts.Remove(e.itemId);
                RemoveSlot(e.itemId);
            }
            else
            {
                _counts[e.itemId] = total;
                RefreshSlot(e.itemId, total);
            }
        }

        /// <summary>Create or update the slot view for an item id.</summary>
        private void RefreshSlot(string itemId, int quantity)
        {
            if (_slotPrefab == null || _slotContainer == null) return;

            if (!_slots.TryGetValue(itemId, out var view) || view == null)
            {
                view = Instantiate(_slotPrefab, _slotContainer);
                _slots[itemId] = view;
            }

            var data = _itemRegistry != null ? _itemRegistry.Resolve(itemId) : null;
            view.Bind(data, itemId, quantity);
        }

        private void RemoveSlot(string itemId)
        {
            if (_slots.TryGetValue(itemId, out var view))
            {
                if (view != null) Destroy(view.gameObject);
                _slots.Remove(itemId);
            }
        }

        private void ClearGrid()
        {
            foreach (var view in _slots.Values)
                if (view != null) Destroy(view.gameObject);
            _slots.Clear();
            _counts.Clear();
        }
    }
}
