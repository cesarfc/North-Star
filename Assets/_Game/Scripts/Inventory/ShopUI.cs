using System;
using UnityEngine;
using UnityEngine.UI;

namespace NorthStar.Inventory
{
    /// <summary>
    /// Buy/sell shop interface. Gold is owned by PlayerStats (Module 7), which this
    /// module must not reference, so all gold changes go out as
    /// <see cref="GoldChangeRequestEvent"/> on the EventBus; the displayed total is
    /// kept in sync by listening for <see cref="PlayerGoldChangedEvent"/>. Items
    /// move through the local <see cref="Inventory"/>. Buying spends gold and adds
    /// the item; selling removes the item and grants gold.
    /// </summary>
    public class ShopUI : MonoBehaviour
    {
        [Tooltip("The player inventory items are bought into / sold from.")]
        [SerializeField] private Inventory _inventory;
        [Tooltip("Label showing the player's current gold.")]
        [SerializeField] private Text _goldLabel;

        // Mirror of the player's gold, fed by PlayerGoldChangedEvent. Lets the
        // shop reject unaffordable purchases up front without referencing Player.
        private int _displayedGold;

        /// <summary>Raised when a purchase succeeds. Args: (item, quantity, totalCost).</summary>
        public event Action<ItemData, int, int> OnItemBought;

        /// <summary>Raised when a sale succeeds. Args: (item, quantity, totalValue).</summary>
        public event Action<ItemData, int, int> OnItemSold;

        private void OnEnable()
        {
            EventBus.Subscribe<PlayerGoldChangedEvent>(OnGoldChanged);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<PlayerGoldChangedEvent>(OnGoldChanged);
        }

        /// <summary>The most recent gold total observed from PlayerStats events.</summary>
        public int DisplayedGold => _displayedGold;

        /// <summary>
        /// Attempt to buy <paramref name="quantity"/> of <paramref name="item"/>. Fails
        /// (changing nothing) on a null item, a non-positive quantity, or when the
        /// last-known gold total cannot cover the cost. On success, publishes a
        /// negative <see cref="GoldChangeRequestEvent"/> and adds the item to the
        /// inventory. Returns true on success.
        /// </summary>
        public bool Buy(ItemData item, int quantity)
        {
            if (item == null || quantity <= 0 || _inventory == null) return false;

            int cost = item.buyPrice * quantity;
            if (cost > _displayedGold) return false; // can't afford

            EventBus.Publish(new GoldChangeRequestEvent { delta = -cost });
            _inventory.AddItem(item, quantity);

            OnItemBought?.Invoke(item, quantity, cost);
            return true;
        }

        /// <summary>
        /// Attempt to sell <paramref name="quantity"/> of <paramref name="item"/>.
        /// Fails (changing nothing) on a null item, a non-positive quantity, or
        /// when the inventory holds fewer than <paramref name="quantity"/>. On
        /// success, removes the item and publishes a positive
        /// <see cref="GoldChangeRequestEvent"/> for the sale value. Returns true
        /// on success.
        /// </summary>
        public bool Sell(ItemData item, int quantity)
        {
            if (item == null || quantity <= 0 || _inventory == null) return false;
            if (!_inventory.RemoveItem(item.itemId, quantity)) return false;

            int value = item.sellPrice * quantity;
            EventBus.Publish(new GoldChangeRequestEvent { delta = value });

            OnItemSold?.Invoke(item, quantity, value);
            return true;
        }

        private void OnGoldChanged(PlayerGoldChangedEvent e)
        {
            _displayedGold = e.newTotal;
            if (_goldLabel != null) _goldLabel.text = _displayedGold.ToString();
        }
    }
}
