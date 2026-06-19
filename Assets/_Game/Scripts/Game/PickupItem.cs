using System.Collections;
using NorthStar.Inventory;
using UnityEngine;

/// <summary>
/// A world pickup the player can interact with. On interaction it adds its item to the
/// target <see cref="Inventory"/> (which publishes ItemAddedEvent), shows brief feedback,
/// then removes itself. Composition-root glue — lives in NorthStar.Game so it may reference
/// both Core (IInteractable) and the Inventory module.
/// </summary>
public class PickupItem : MonoBehaviour, IInteractable
{
    [SerializeField] private ItemData _item;
    [SerializeField] private int _quantity = 1;
    [SerializeField] private Inventory _inventory;

    private bool _collected;
    private string _feedback;
    private float _feedbackUntil;

    /// <inheritdoc />
    public string InteractionPrompt => _item != null ? $"Pick up {_item.displayName}" : "Pick up";

    /// <inheritdoc />
    public void Interact(GameObject interactor)
    {
        if (_collected || _item == null || _inventory == null) return;
        if (!_inventory.AddItem(_item, _quantity)) return;

        _collected = true;
        _feedback = $"Picked up {_item.displayName} x{_quantity}";
        _feedbackUntil = Time.time + 2.5f;

        // Hide the visual + stop it being interactable, but keep the object alive
        // long enough to render the pickup feedback, then remove it.
        foreach (var r in GetComponentsInChildren<Renderer>()) r.enabled = false;
        foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
        StartCoroutine(CoRemoveAfterFeedback());
    }

    private IEnumerator CoRemoveAfterFeedback()
    {
        yield return new WaitForSeconds(2.5f);
        Destroy(gameObject);
    }

    private void OnGUI()
    {
        if (Time.time > _feedbackUntil || string.IsNullOrEmpty(_feedback)) return;
        var style = new GUIStyle(GUI.skin.box) { fontSize = 18, alignment = TextAnchor.MiddleCenter };
        GUI.Box(new Rect(Screen.width / 2f - 200f, 120f, 400f, 40f), _feedback, style);
    }
}
