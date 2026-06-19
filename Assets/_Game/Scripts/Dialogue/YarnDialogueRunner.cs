// ─────────────────────────────────────────────────────────────────────────────
// Yarn Spinner adapter.
//
// This is the ONLY file in the module that references Yarn Spinner types. It is
// wrapped in `#if YARN_SPINNER` so the project compiles whether or not the package
// is present. After the orchestrator installs Yarn Spinner, add `YARN_SPINNER` to
// Project Settings ▸ Player ▸ Scripting Define Symbols to activate this adapter.
//
// Yarn Spinner's `DialogueRunner` is a MonoBehaviour driven by a `DialogueViewBase`.
// This adapter implements that view contract and translates Yarn's callbacks into
// the engine-agnostic `IDialogueRunner` events the rest of the module consumes.
// ─────────────────────────────────────────────────────────────────────────────

#if YARN_SPINNER
using System;
using UnityEngine;
using Yarn.Unity;

/// <summary>
/// Yarn Spinner implementation of <see cref="IDialogueRunner"/>. Attach alongside a
/// Yarn <c>DialogueRunner</c> on the dialogue rig prefab and register this as a
/// dialogue view. Translates Yarn line/option callbacks into engine-agnostic events.
/// </summary>
[RequireComponent(typeof(DialogueRunner))]
public sealed class YarnDialogueRunner : DialogueViewBase, IDialogueRunner
{
    [SerializeField] private DialogueRunner _runner;

    private Action _continueHandler;
    private Action<int> _selectHandler;

    /// <inheritdoc />
    public bool IsRunning => _runner != null && _runner.IsDialogueRunning;

    /// <inheritdoc />
    public event Action<DialogueLine> OnLine;

    /// <inheritdoc />
    public event Action<DialogueChoice[]> OnChoices;

    /// <inheritdoc />
    public event Action OnComplete;

    private void Awake()
    {
        if (_runner == null) _runner = GetComponent<DialogueRunner>();
        _runner.onDialogueComplete.AddListener(HandleDialogueComplete);
    }

    private void OnDestroy()
    {
        if (_runner != null)
            _runner.onDialogueComplete.RemoveListener(HandleDialogueComplete);
    }

    /// <summary>Start the Yarn node named <paramref name="graphId"/>.</summary>
    public void StartGraph(string graphId) => _runner.StartDialogue(graphId);

    /// <summary>Advance the currently shown Yarn line.</summary>
    public void Continue() => _continueHandler?.Invoke();

    /// <summary>Select a Yarn option by index from the active choice set.</summary>
    public void SelectOption(int choiceIndex) => _selectHandler?.Invoke(choiceIndex);

    /// <summary>Stop the Yarn runner immediately.</summary>
    public void Stop() => _runner.Stop();

    // ── DialogueViewBase overrides ───────────────────────────────────────────

    /// <summary>Yarn delivers a line; map it to a <see cref="DialogueLine"/> and raise OnLine.</summary>
    public override void RunLine(LocalizedLine dialogueLine, Action onDialogueLineFinished)
    {
        _continueHandler = onDialogueLineFinished;

        OnLine?.Invoke(new DialogueLine
        {
            speakerName     = dialogueLine.CharacterName ?? string.Empty,
            text            = dialogueLine.TextWithoutCharacterName.Text,
            speakerPortrait = null // portrait resolution handled by DialogueUI via speaker name/markup
        });
    }

    /// <summary>Yarn presents options; map them to <see cref="DialogueChoice"/>[] and raise OnChoices.</summary>
    public override void RunOptions(DialogueOption[] dialogueOptions, Action<int> onOptionSelected)
    {
        _selectHandler = onOptionSelected;

        var choices = new DialogueChoice[dialogueOptions.Length];
        for (int i = 0; i < dialogueOptions.Length; i++)
        {
            var opt = dialogueOptions[i];
            choices[i] = new DialogueChoice
            {
                index       = opt.DialogueOptionID,
                text        = opt.Line.TextWithoutCharacterName.Text,
                isAvailable = opt.IsAvailable
            };
        }

        OnChoices?.Invoke(choices);
    }

    private void HandleDialogueComplete() => OnComplete?.Invoke();
}
#endif
