// ─────────────────────────────────────────────────────────────────────────────
// Yarn Spinner adapter.
//
// This is the ONLY file in the module that references Yarn Spinner types. It is
// wrapped in `#if YARN_SPINNER` so the project compiles whether or not the package
// is present; the define is set automatically by the asmdef's versionDefines when
// the dev.yarnspinner.unity package is installed.
//
// Yarn Spinner 3.x replaced the 2.x `DialogueViewBase` callbacks with the async
// `DialoguePresenterBase` contract (RunLineAsync / RunOptionsAsync returning
// YarnTasks). This adapter bridges that async world back to the engine-agnostic
// `IDialogueRunner` events the rest of the module consumes: each presenter task is
// held open on a YarnTaskCompletionSource until DialogueSystem calls Continue() /
// SelectOption(i) in response to player input.
// ─────────────────────────────────────────────────────────────────────────────

#if YARN_SPINNER
using System;
using UnityEngine;
using Yarn.Unity;

/// <summary>
/// Yarn Spinner implementation of <see cref="IDialogueRunner"/>. Attach alongside a
/// Yarn <c>DialogueRunner</c> and register this component in the runner's dialogue
/// presenters list. Translates Yarn's async presenter callbacks into engine-agnostic
/// events, advancing only when <see cref="Continue"/> / <see cref="SelectOption"/> are called.
/// </summary>
[RequireComponent(typeof(DialogueRunner))]
public sealed class YarnDialogueRunner : DialoguePresenterBase, IDialogueRunner
{
    [SerializeField] private DialogueRunner _runner;

    // Held open while a line / option set is on screen; completed by Continue/SelectOption.
    private YarnTaskCompletionSource _lineFinished;
    private YarnTaskCompletionSource<DialogueOption> _optionSelected;
    private DialogueOption[] _currentOptions = Array.Empty<DialogueOption>();

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
        _runner.onDialogueComplete?.AddListener(HandleDialogueComplete);
    }

    private void OnDestroy()
    {
        if (_runner != null)
            _runner.onDialogueComplete?.RemoveListener(HandleDialogueComplete);
    }

    /// <summary>Start the Yarn node named <paramref name="graphId"/>.</summary>
    public void StartGraph(string graphId) => _runner.StartDialogue(graphId);

    /// <summary>Advance the currently shown Yarn line.</summary>
    public void Continue() => _lineFinished?.TrySetResult();

    /// <summary>Select a Yarn option by id from the active choice set.</summary>
    public void SelectOption(int choiceIndex)
    {
        if (_optionSelected == null) return;
        foreach (DialogueOption option in _currentOptions)
        {
            if (option.DialogueOptionID == choiceIndex)
            {
                _optionSelected.TrySetResult(option);
                return;
            }
        }
        Debug.LogWarning($"[YarnDialogueRunner] SelectOption({choiceIndex}) matches no active option.");
    }

    /// <summary>Stop the Yarn runner immediately.</summary>
    public void Stop() => _runner.Stop();

    // ── DialoguePresenterBase overrides ──────────────────────────────────────

    /// <summary>Yarn signals dialogue start; nothing to prepare (UI reacts to OnLine).</summary>
    public override YarnTask OnDialogueStartedAsync() => YarnTask.CompletedTask;

    /// <summary>Yarn signals dialogue end; completion is routed via onDialogueComplete.</summary>
    public override YarnTask OnDialogueCompleteAsync() => YarnTask.CompletedTask;

    /// <summary>
    /// Yarn delivers a line: map it to a <see cref="DialogueLine"/>, raise <see cref="OnLine"/>,
    /// then hold the presenter task open until <see cref="Continue"/> (or cancellation, e.g.
    /// <see cref="Stop"/>) releases it.
    /// </summary>
    public override async YarnTask RunLineAsync(LocalizedLine line, LineCancellationToken token)
    {
        var finished = new YarnTaskCompletionSource();
        _lineFinished = finished;

        OnLine?.Invoke(new DialogueLine
        {
            speakerName     = line.CharacterName ?? string.Empty,
            text            = line.TextWithoutCharacterName.Text,
            speakerPortrait = null // portrait resolution handled by the UI via speaker name/markup
        });

        using (token.NextContentToken.Register(() => finished.TrySetResult()))
        {
            await finished.Task;
        }
        if (_lineFinished == finished) _lineFinished = null;
    }

    /// <summary>
    /// Yarn presents options: map them to <see cref="DialogueChoice"/>[], raise
    /// <see cref="OnChoices"/>, and hold the presenter task open until
    /// <see cref="SelectOption"/> resolves it (or cancellation returns no selection).
    /// </summary>
    public override async YarnTask<DialogueOption> RunOptionsAsync(
        DialogueOption[] dialogueOptions, LineCancellationToken cancellationToken)
    {
        var selected = new YarnTaskCompletionSource<DialogueOption>();
        _optionSelected = selected;
        _currentOptions = dialogueOptions;

        var choices = new DialogueChoice[dialogueOptions.Length];
        for (int i = 0; i < dialogueOptions.Length; i++)
        {
            DialogueOption opt = dialogueOptions[i];
            choices[i] = new DialogueChoice
            {
                index       = opt.DialogueOptionID,
                text        = opt.Line.TextWithoutCharacterName.Text,
                isAvailable = opt.IsAvailable
            };
        }
        OnChoices?.Invoke(choices);

        DialogueOption picked;
        using (cancellationToken.NextContentToken.Register(() => selected.TrySetResult(null)))
        {
            picked = await selected.Task;
        }
        if (_optionSelected == selected)
        {
            _optionSelected = null;
            _currentOptions = Array.Empty<DialogueOption>();
        }
        return picked;
    }

    private void HandleDialogueComplete() => OnComplete?.Invoke();
}
#endif
