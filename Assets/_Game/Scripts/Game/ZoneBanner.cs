using UnityEngine;

/// <summary>
/// Listens for <see cref="ZoneEnteredEvent"/> on the EventBus and flashes a banner naming the
/// entered zone — visible confirmation that a ZoneTransition (World module) fired. Composition-root
/// glue (NorthStar.Game); subscribes in OnEnable, unsubscribes in OnDisable.
/// </summary>
public class ZoneBanner : MonoBehaviour
{
    private string _text;
    private float _until;

    private void OnEnable() => EventBus.Subscribe<ZoneEnteredEvent>(OnZoneEntered);
    private void OnDisable() => EventBus.Unsubscribe<ZoneEnteredEvent>(OnZoneEntered);

    private void OnZoneEntered(ZoneEnteredEvent e)
    {
        _text = $"Entered zone: {e.zoneId}";
        _until = Time.time + 3f;
    }

    private void OnGUI()
    {
        if (Time.time > _until || string.IsNullOrEmpty(_text)) return;
        var style = new GUIStyle(GUI.skin.box) { fontSize = 20, alignment = TextAnchor.MiddleCenter };
        GUI.Box(new Rect(Screen.width / 2f - 220f, 80f, 440f, 44f), _text, style);
    }
}
