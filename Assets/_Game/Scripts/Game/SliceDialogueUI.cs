using UnityEngine;

/// <summary>
/// Minimal IMGUI presenter for Dialogue-module conversations (matching the slice's other
/// IMGUI glue — no Canvas/TMP deps). Subscribes to the local <see cref="DialogueSystem"/>
/// events: shows the current line with a continue hint (button, E or Space), and renders
/// choice buttons that route back through <see cref="DialogueSystem.SelectChoice"/>.
/// Composition-root glue (NorthStar.Game); the uGUI DialogueUI remains the shipping skin.
/// </summary>
public class SliceDialogueUI : MonoBehaviour
{
    [SerializeField] private DialogueSystem _dialogue;

    private DialogueLine _line;
    private DialogueChoice[] _choices;
    private bool _hasLine;

    private void OnEnable()
    {
        if (_dialogue == null) return;
        _dialogue.OnLineDelivered += HandleLine;
        _dialogue.OnChoicesPresented += HandleChoices;
        _dialogue.OnDialogueEnd += HandleEnd;
    }

    private void OnDisable()
    {
        if (_dialogue == null) return;
        _dialogue.OnLineDelivered -= HandleLine;
        _dialogue.OnChoicesPresented -= HandleChoices;
        _dialogue.OnDialogueEnd -= HandleEnd;
    }

    private void HandleLine(DialogueLine line)
    {
        _line = line;
        _hasLine = true;
        _choices = null;
    }

    private void HandleChoices(DialogueChoice[] choices) => _choices = choices;

    private void HandleEnd()
    {
        _hasLine = false;
        _choices = null;
    }

    private void OnGUI()
    {
        if (_dialogue == null || !_dialogue.IsDialogueActive) return;
        if (!_hasLine && _choices == null) return;

        float w = Mathf.Min(680f, Screen.width - 60f);
        float h = 150f + (_choices != null ? _choices.Length * 30f : 0f);
        var area = new Rect((Screen.width - w) / 2f, Screen.height - h - 30f, w, h);
        GUILayout.BeginArea(area, GUI.skin.box);

        if (_hasLine)
        {
            if (!string.IsNullOrEmpty(_line.speakerName))
            {
                var speaker = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 16 };
                GUILayout.Label(_line.speakerName, speaker);
            }
            var body = new GUIStyle(GUI.skin.label) { fontSize = 16, wordWrap = true };
            GUILayout.Label(_line.text, body);
        }

        GUILayout.FlexibleSpace();
        if (_choices != null)
        {
            foreach (DialogueChoice choice in _choices)
            {
                GUI.enabled = choice.isAvailable;
                if (GUILayout.Button(choice.text, GUILayout.Height(26f)))
                    _dialogue.SelectChoice(choice.index);
                GUI.enabled = true;
            }
        }
        else if (GUILayout.Button("Continue  (E / Space)", GUILayout.Height(26f)))
        {
            _dialogue.AdvanceDialogue();
        }
        GUILayout.EndArea();

        // Keyboard advance for lines (choices need an explicit click).
        Event e = Event.current;
        if (_choices == null && e.type == EventType.KeyDown &&
            (e.keyCode == KeyCode.Space || e.keyCode == KeyCode.E))
        {
            _dialogue.AdvanceDialogue();
            e.Use();
        }
    }
}
