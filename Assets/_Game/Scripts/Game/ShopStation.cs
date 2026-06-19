using NorthStar.Inventory;
using NorthStar.Player;
using UnityEngine;

/// <summary>
/// Interactable shop. Opens an IMGUI panel that drives the real ShopUI (buy/sell), which
/// publishes GoldChangeRequestEvent (→ PlayerStats applies it) and updates the Inventory.
/// Live gold/items show in the slice HUD. Composition-root glue (NorthStar.Game).
/// </summary>
public class ShopStation : MonoBehaviour, IInteractable
{
    [SerializeField] private ShopUI _shop;
    [SerializeField] private PlayerStats _stats;   // for live gold display
    [SerializeField] private ItemData[] _forSale;

    private bool _open;
    private string _message;

    /// <inheritdoc />
    public string InteractionPrompt => "Shop (E)";

    /// <inheritdoc />
    public void Interact(GameObject interactor) => _open = !_open;

    private void OnGUI()
    {
        if (!_open || _shop == null) return;

        GUILayout.BeginArea(new Rect(Screen.width - 360f, 150f, 340f, 340f), GUI.skin.box);
        GUILayout.Label("Shop");
        GUILayout.Label($"Gold: {(_stats != null ? _stats.Gold : _shop.DisplayedGold)}");
        if (!string.IsNullOrEmpty(_message)) GUILayout.Label(_message);
        GUILayout.Space(6f);

        if (_forSale != null)
            foreach (var item in _forSale)
            {
                if (item == null) continue;
                GUILayout.BeginHorizontal();
                GUILayout.Label(item.displayName, GUILayout.Width(140f));
                if (GUILayout.Button($"Buy {item.buyPrice}g"))
                    _message = _shop.Buy(item, 1) ? $"Bought {item.displayName}" : "Can't buy that";
                if (GUILayout.Button($"Sell {item.sellPrice}g"))
                    _message = _shop.Sell(item, 1) ? $"Sold {item.displayName}" : "None to sell";
                GUILayout.EndHorizontal();
            }

        GUILayout.Space(6f);
        if (GUILayout.Button("Close")) _open = false;
        GUILayout.EndArea();
    }
}
