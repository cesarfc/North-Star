using System.Collections.Generic;
using UnityEngine;

namespace NorthStar.Inventory
{
    /// <summary>
    /// Lookup table mapping itemId -> ItemData. EventBus inventory events carry
    /// only the string itemId (so they stay serializable and module-agnostic);
    /// UI and gameplay resolve those ids back to the full ItemData asset through
    /// this registry. Populate the list in the Inspector with every ItemData in
    /// the project.
    /// </summary>
    [CreateAssetMenu(fileName = "SO_ItemRegistry", menuName = "Game/Items/Item Registry")]
    public class ItemRegistry : ScriptableObject
    {
        [SerializeField] private List<ItemData> _items = new List<ItemData>();

        private Dictionary<string, ItemData> _lookup;

        /// <summary>
        /// Resolve an item id to its <c>ItemData</c>, or null if unknown. Builds
        /// the lookup lazily on first call and caches it.
        /// </summary>
        public ItemData Resolve(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return null;
            EnsureBuilt();
            return _lookup.TryGetValue(itemId, out var data) ? data : null;
        }

        /// <summary>True if the registry knows the given item id.</summary>
        public bool Contains(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return false;
            EnsureBuilt();
            return _lookup.ContainsKey(itemId);
        }

        /// <summary>Force the id->asset lookup to rebuild (e.g. after editing the list).</summary>
        public void Rebuild()
        {
            _lookup = new Dictionary<string, ItemData>();
            foreach (var item in _items)
            {
                if (item == null || string.IsNullOrEmpty(item.itemId)) continue;
                _lookup[item.itemId] = item;
            }
        }

        private void EnsureBuilt()
        {
            if (_lookup == null) Rebuild();
        }
    }
}
