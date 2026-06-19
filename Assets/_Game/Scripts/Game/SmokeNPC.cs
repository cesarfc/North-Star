using System.Collections;
using UnityEngine;

/// <summary>
/// Phase 1 smoke-test NPC. Implements <see cref="IInteractable"/> so the player's
/// InteractionSystem invokes it on the interact key. On interaction it runs a tiny
/// "conversation" (driving GameManager state) and then writes a save via SaveSystem —
/// exercising the walk → talk → save sync goal end-to-end. Uses IMGUI so it has no
/// font/TMP/Canvas dependencies.
/// </summary>
public class SmokeNPC : MonoBehaviour, IInteractable
{
    [SerializeField]
    private string[] _lines =
    {
        "Hello, traveler!",
        "Follow the North Star — it always leads home.",
    };

    private string _status = "";
    private bool _talking;

    /// <inheritdoc />
    public string InteractionPrompt => "Talk (E)";

    /// <inheritdoc />
    public void Interact(GameObject interactor)
    {
        if (!_talking) StartCoroutine(CoTalk());
    }

    private IEnumerator CoTalk()
    {
        _talking = true;
        GameManager.Instance?.ChangeState(GameState.Cutscene);

        foreach (var line in _lines)
        {
            _status = line;
            yield return new WaitForSeconds(1.5f);
        }

        GameManager.Instance?.ChangeState(GameState.Exploring);

        var data = new GameSaveData
        {
            currentZoneId = "smoke-zone",
            playTimeSeconds = Time.time,
        };
        bool ok = SaveSystem.Save("smoke", data);
        _status = ok ? "Conversation saved (slot 'smoke')." : "Save FAILED — check the console.";

        yield return new WaitForSeconds(2.5f);
        _status = "";
        _talking = false;
    }

    private void OnGUI()
    {
        GUI.Label(new Rect(12, 10, 700, 24), "WASD/arrows to move · walk to the cube · press E to talk");
        if (string.IsNullOrEmpty(_status)) return;

        var style = new GUIStyle(GUI.skin.box) { fontSize = 20, alignment = TextAnchor.MiddleCenter, wordWrap = true };
        GUI.Box(new Rect(Screen.width / 2f - 280f, 44f, 560f, 64f), _status, style);
    }
}
