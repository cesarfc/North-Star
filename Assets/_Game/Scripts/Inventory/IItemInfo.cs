namespace NorthStar.Inventory
{
    /// <summary>
    /// Minimal, engine-free view of an item that <see cref="InventoryCore"/>
    /// needs to make stacking decisions. Implemented by the lightweight
    /// <see cref="ItemInfo"/> adapter that wraps an <c>ItemData</c>
    /// ScriptableObject, so the pure core never touches UnityEngine types and
    /// stays EditMode-testable.
    /// </summary>
    public interface IItemInfo
    {
        /// <summary>Stable, lowercase-with-hyphens item identifier.</summary>
        string ItemId { get; }

        /// <summary>True if multiple units share one stack.</summary>
        bool IsStackable { get; }

        /// <summary>Maximum units per stack (ignored when not stackable).</summary>
        int MaxStackSize { get; }
    }
}
