using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Presentation layer for conversations: speaker portrait, name plate, a typewriter
/// text box, and a dynamic list of choice buttons. Subscribes to a <see cref="DialogueSystem"/>
/// and never touches the dialogue engine directly. Choice clicks and the advance input are
/// routed back through the <see cref="DialogueSystem"/> API.
/// </summary>
public class DialogueUI : MonoBehaviour
{
    [Header("Source")]
    [SerializeField] private DialogueSystem _dialogueSystem;

    [Header("Root")]
    [SerializeField] private CanvasGroup _root;

    [Header("Line")]
    [SerializeField] private TMP_Text _speakerNameLabel;
    [SerializeField] private Image    _portraitImage;
    [SerializeField] private TMP_Text _bodyText;

    [Header("Choices")]
    [SerializeField] private RectTransform _choiceContainer;
    [SerializeField] private Button        _choiceButtonPrefab;

    [Header("Typewriter")]
    [Tooltip("Characters revealed per second while animating a line.")]
    [SerializeField] private float _charactersPerSecond = 45f;

    private readonly List<Button> _activeChoiceButtons = new List<Button>();
    private Coroutine _typeRoutine;
    private string _fullLineText = string.Empty;
    private bool _lineFullyShown;

    private void OnEnable()
    {
        if (_dialogueSystem == null) return;
        _dialogueSystem.OnDialogueStart    += HandleDialogueStart;
        _dialogueSystem.OnLineDelivered    += HandleLine;
        _dialogueSystem.OnChoicesPresented += HandleChoices;
        _dialogueSystem.OnDialogueEnd      += HandleDialogueEnd;
    }

    private void OnDisable()
    {
        if (_dialogueSystem == null) return;
        _dialogueSystem.OnDialogueStart    -= HandleDialogueStart;
        _dialogueSystem.OnLineDelivered    -= HandleLine;
        _dialogueSystem.OnChoicesPresented -= HandleChoices;
        _dialogueSystem.OnDialogueEnd      -= HandleDialogueEnd;
    }

    /// <summary>
    /// Advance handler to be wired to the player's "interact/submit" input action.
    /// First press finishes the typewriter reveal; second press asks the
    /// <see cref="DialogueSystem"/> to advance the line.
    /// </summary>
    public void OnAdvancePressed()
    {
        if (_dialogueSystem == null || !_dialogueSystem.IsDialogueActive) return;

        if (!_lineFullyShown)
        {
            CompleteLineInstantly();
            return;
        }

        // Don't auto-advance while choices are on screen — the player must pick one.
        if (_activeChoiceButtons.Count > 0) return;

        _dialogueSystem.AdvanceDialogue();
    }

    private void HandleDialogueStart(string graphId)
    {
        SetVisible(true);
        ClearChoices();
        _bodyText.text = string.Empty;
    }

    private void HandleLine(DialogueLine line)
    {
        ClearChoices();

        _speakerNameLabel.text = line.speakerName;

        if (_portraitImage != null)
        {
            _portraitImage.sprite = line.speakerPortrait;
            _portraitImage.enabled = line.speakerPortrait != null;
        }

        StartTypewriter(line.text);
    }

    private void HandleChoices(DialogueChoice[] choices)
    {
        ClearChoices();
        if (_choiceButtonPrefab == null || _choiceContainer == null) return;

        foreach (var choice in choices)
        {
            var button = Instantiate(_choiceButtonPrefab, _choiceContainer);
            button.interactable = choice.isAvailable;

            var label = button.GetComponentInChildren<TMP_Text>();
            if (label != null) label.text = choice.text;

            int captured = choice.index;
            button.onClick.AddListener(() => OnChoiceClicked(captured));

            _activeChoiceButtons.Add(button);
        }
    }

    private void HandleDialogueEnd()
    {
        StopTypewriter();
        ClearChoices();
        SetVisible(false);
    }

    private void OnChoiceClicked(int choiceIndex)
    {
        ClearChoices();
        if (_dialogueSystem != null) _dialogueSystem.SelectChoice(choiceIndex);
    }

    // ── Typewriter ───────────────────────────────────────────────────────────

    private void StartTypewriter(string text)
    {
        StopTypewriter();
        _fullLineText = text ?? string.Empty;
        _lineFullyShown = false;
        _bodyText.text = string.Empty;
        _typeRoutine = StartCoroutine(CoTypeLine(_fullLineText));
    }

    private void StopTypewriter()
    {
        if (_typeRoutine != null)
        {
            StopCoroutine(_typeRoutine);
            _typeRoutine = null;
        }
    }

    private void CompleteLineInstantly()
    {
        StopTypewriter();
        _bodyText.text = _fullLineText;
        _lineFullyShown = true;
    }

    private IEnumerator CoTypeLine(string text)
    {
        if (_charactersPerSecond <= 0f)
        {
            _bodyText.text = text;
            _lineFullyShown = true;
            _typeRoutine = null;
            yield break;
        }

        float delay = 1f / _charactersPerSecond;
        var sb = new System.Text.StringBuilder(text.Length);

        for (int i = 0; i < text.Length; i++)
        {
            sb.Append(text[i]);
            _bodyText.text = sb.ToString();
            yield return new WaitForSeconds(delay);
        }

        _lineFullyShown = true;
        _typeRoutine = null;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private void ClearChoices()
    {
        foreach (var button in _activeChoiceButtons)
        {
            if (button == null) continue;
            button.onClick.RemoveAllListeners();
            Destroy(button.gameObject);
        }
        _activeChoiceButtons.Clear();
    }

    private void SetVisible(bool visible)
    {
        if (_root == null) return;
        _root.alpha = visible ? 1f : 0f;
        _root.interactable = visible;
        _root.blocksRaycasts = visible;
    }
}
