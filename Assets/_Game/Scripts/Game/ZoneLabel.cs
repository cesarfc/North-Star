using UnityEngine;

/// <summary>
/// Simple on-screen label so an additively-loaded zone scene announces itself in play mode.
/// Used by the vertical-slice second zone (SCN_Zone02). Composition-root glue (NorthStar.Game).
/// </summary>
public class ZoneLabel : MonoBehaviour
{
    [SerializeField] private string _text = "ZONE";

    private void OnGUI()
    {
        var style = new GUIStyle(GUI.skin.box) { fontSize = 22, alignment = TextAnchor.MiddleCenter };
        GUI.Box(new Rect(Screen.width / 2f - 220f, Screen.height - 84f, 440f, 48f), _text, style);
    }
}
