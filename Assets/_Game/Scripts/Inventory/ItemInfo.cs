namespace NorthStar.Inventory
{
    /// <summary>
    /// Adapter exposing an <c>ItemData</c> ScriptableObject as an engine-free
    /// <see cref="IItemInfo"/> for <see cref="InventoryCore"/>. Lets the pure
    /// core reason about stacking without referencing UnityEngine types.
    /// </summary>
    public readonly struct ItemInfo : IItemInfo
    {
        private readonly ItemData _data;

        /// <summary>Wrap an <c>ItemData</c> asset.</summary>
        public ItemInfo(ItemData data)
        {
            _data = data;
        }

        /// <inheritdoc/>
        public string ItemId => _data != null ? _data.itemId : null;

        /// <inheritdoc/>
        public bool IsStackable => _data != null && _data.isStackable;

        /// <inheritdoc/>
        public int MaxStackSize => _data != null ? _data.maxStackSize : 1;
    }
}
