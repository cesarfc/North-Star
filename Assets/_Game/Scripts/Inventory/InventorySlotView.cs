using UnityEngine;
using UnityEngine.UI;

namespace NorthStar.Inventory
{
    /// <summary>
    /// View for one inventory grid slot: an icon and a quantity label. Bound by
    /// <see cref="InventoryUI"/> whenever the underlying stack changes. Holds no
    /// state of its own beyond the wired UI references.
    /// </summary>
    public class InventorySlotView : MonoBehaviour
    {
        [SerializeField] private Image _icon;
        [SerializeField] private Text _quantityLabel;
        [SerializeField] private Text _nameLabel;

        /// <summary>
        /// Populate this slot. <paramref name="data"/> may be null if the id could
        /// not be resolved (the slot then shows the raw id and hides the icon).
        /// The quantity label is hidden for single (non-stacked) entries.
        /// </summary>
        public void Bind(ItemData data, string itemId, int quantity)
        {
            if (_icon != null)
            {
                _icon.sprite = data != null ? data.icon : null;
                _icon.enabled = data != null && data.icon != null;
            }

            if (_nameLabel != null)
                _nameLabel.text = data != null ? data.displayName : itemId;

            if (_quantityLabel != null)
            {
                bool show = quantity > 1;
                _quantityLabel.enabled = show;
                _quantityLabel.text = show ? quantity.ToString() : string.Empty;
            }
        }
    }
}
