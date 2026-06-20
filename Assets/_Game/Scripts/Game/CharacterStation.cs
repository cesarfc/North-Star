using NorthStar.Character;
using UnityEngine;

/// <summary>
/// Interactable customization station. Opens an IMGUI panel that drives the real
/// CharacterCustomizer (equip armor, set hair/colour, unequip) and shows the live
/// CharacterLoadout. Mesh swaps no-op without rigged renderers, but the loadout logic
/// + OnLoadoutChanged + LoadoutChangedEvent all fire. Composition-root glue (NorthStar.Game).
/// </summary>
public class CharacterStation : MonoBehaviour, IInteractable
{
    [SerializeField] private CharacterCustomizer _customizer;
    [SerializeField] private ArmorData[] _armors;
    [SerializeField] private HairStyleData[] _hairs;

    private bool _open;

    /// <inheritdoc />
    public string InteractionPrompt => "Customize (E)";

    /// <inheritdoc />
    public void Interact(GameObject interactor) => _open = !_open;

    private void OnGUI()
    {
        if (!_open || _customizer == null) return;

        GUILayout.BeginArea(new Rect(20f, 150f, 320f, 320f), GUI.skin.box);
        GUILayout.Label("Character Customization");

        var lo = _customizer.GetCurrentLoadout();
        GUILayout.Label($"Chest: {Or(lo.chestArmorId)}");
        GUILayout.Label($"Hair:  {Or(lo.hairStyleId)}");
        GUILayout.Space(6f);

        if (_armors != null)
            foreach (var a in _armors)
                if (a != null && GUILayout.Button($"Equip {a.displayName}"))
                    _customizer.Equip(a.slot, a);

        if (GUILayout.Button("Unequip Chest"))
            _customizer.Unequip(EquipmentSlot.Chest);

        GUILayout.Space(4f);
        if (_hairs != null)
            foreach (var h in _hairs)
                if (h != null && GUILayout.Button($"Hair: {h.displayName}"))
                    _customizer.SetHair(h);

        if (GUILayout.Button("Random hair colour"))
            _customizer.SetHairColor(Random.ColorHSV());

        GUILayout.Space(6f);
        if (GUILayout.Button("Close")) _open = false;
        GUILayout.EndArea();
    }

    private static string Or(string id) => string.IsNullOrEmpty(id) ? "(none)" : id;
}
