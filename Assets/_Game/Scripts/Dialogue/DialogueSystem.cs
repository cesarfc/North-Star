using System;
using UnityEngine;

/// <summary>
/// Entry point for running conversations. Drives an <see cref="IDialogueRunner"/>
/// (Yarn Spinner in the shipping game, a null stub in tests / before install) and
/// bridges it to the rest of the game: it switches the game into
/// <see cref="GameState.Cutscene"/> on start, publishes <see cref="DialogueStartedEvent"/>
/// / <see cref="DialogueEndedEvent"/> on the <see cref="EventBus"/>, and re-exposes the
/// frozen INTERFACE.md dialogue events for the local <see cref="DialogueUI"/>.
///
/// The runner is engine-agnostic, so no Yarn Spinner type ever appears here.
/// </summary>
public class DialogueSystem : MonoBehaviour
{
    [Tooltip("Assign a component implementing IDialogueRunner (e.g. YarnDialogueRunner). " +
             "If left empty, a NullDialogueRunner is used so the system is safe before Yarn is installed.")]
    [SerializeField] private MonoBehaviour _runnerBehaviour;

    private IDialogueRunner _runner;
    private string _activeGraphId;
    private GameState _stateBeforeDialogue = GameState.Exploring;

    /// <summary>True while a conversation is in progress.</summary>
    public bool IsDialogueActive { get; private set; }

    /// <summary>The graph currently running, or null when idle.</summary>
    public string ActiveGraphId => _activeGraphId;

    // ── INTERFACE.md dialogue events ─────────────────────────────────────────

    /// <summary>Raised when a conversation begins, carrying the started graph id.</summary>
    public event Action<string> OnDialogueStart;

    /// <summary>Raised each time the runner delivers a line.</summary>
    public event Action<DialogueLine> OnLineDelivered;

    /// <summary>Raised when the runner presents a set of choices.</summary>
    public event Action<DialogueChoice[]> OnChoicesPresented;

    /// <summary>Raised when the conversation ends.</summary>
    public event Action OnDialogueEnd;

    private void Awake()
    {
        ResolveRunner();
    }

    /// <summary>
    /// Resolve the dialogue runner from the serialized field, falling back to a
    /// <see cref="NullDialogueRunner"/> when none is assigned or it does not implement
    /// <see cref="IDialogueRunner"/>. Subscribes to the runner's callbacks.
    /// </summary>
    private void ResolveRunner()
    {
        if (_runner != null) return;

        _runner = _runnerBehaviour as IDialogueRunner;
        if (_runner == null)
        {
            if (_runnerBehaviour != null)
                Debug.LogWarning($"[DialogueSystem] Assigned runner '{_runnerBehaviour.GetType().Name}' " +
                                 "does not implement IDialogueRunner; using NullDialogueRunner.");
            _runner = new NullDialogueRunner();
        }

        _runner.OnLine     += HandleLine;
        _runner.OnChoices  += HandleChoices;
        _runner.OnComplete += HandleComplete;
    }

    /// <summary>
    /// Begin the conversation identified by <paramref name="yarnGraphId"/>. Switches the
    /// game into the Cutscene state, publishes <see cref="DialogueStartedEvent"/>, and
    /// raises <see cref="OnDialogueStart"/>. No-op if a dialogue is already active.
    /// </summary>
    public void StartDialogue(string yarnGraphId)
    {
        if (IsDialogueActive)
        {
            Debug.LogWarning($"[DialogueSystem] StartDialogue('{yarnGraphId}') ignored — already in dialogue '{_activeGraphId}'.");
            return;
        }

        if (string.IsNullOrEmpty(yarnGraphId))
        {
            Debug.LogError("[DialogueSystem] StartDialogue called with a null/empty graph id.");
            return;
        }

        ResolveRunner();

        IsDialogueActive = true;
        _activeGraphId = yarnGraphId;

        if (GameManager.Instance != null)
        {
            _stateBeforeDialogue = GameManager.Instance.CurrentState;
            GameManager.Instance.ChangeState(GameState.Cutscene);
        }

        EventBus.Publish(new DialogueStartedEvent { graphId = yarnGraphId });
        OnDialogueStart?.Invoke(yarnGraphId);

        _runner.StartGraph(yarnGraphId);
    }

    /// <summary>Advance past the current line to the next piece of content.</summary>
    public void AdvanceDialogue()
    {
        if (!IsDialogueActive) return;
        _runner.Continue();
    }

    /// <summary>Select the choice at <paramref name="choiceIndex"/> from the active choice set.</summary>
    public void SelectChoice(int choiceIndex)
    {
        if (!IsDialogueActive) return;
        _runner.SelectOption(choiceIndex);
    }

    /// <summary>
    /// End the active conversation: stops the runner, restores the prior game state,
    /// publishes <see cref="DialogueEndedEvent"/>, and raises <see cref="OnDialogueEnd"/>.
    /// </summary>
    public void EndDialogue()
    {
        if (!IsDialogueActive) return;

        if (_runner.IsRunning)
        {
            // Stopping triggers HandleComplete, which routes back here once running has cleared.
            _runner.Stop();
            return;
        }

        FinishDialogue();
    }

    private void HandleLine(DialogueLine line) => OnLineDelivered?.Invoke(line);

    private void HandleChoices(DialogueChoice[] choices) => OnChoicesPresented?.Invoke(choices);

    private void HandleComplete()
    {
        if (!IsDialogueActive) return;
        FinishDialogue();
    }

    /// <summary>Shared teardown for both natural completion and explicit EndDialogue.</summary>
    private void FinishDialogue()
    {
        var endedGraph = _activeGraphId;

        IsDialogueActive = false;
        _activeGraphId = null;

        if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Cutscene)
            GameManager.Instance.ChangeState(_stateBeforeDialogue);

        EventBus.Publish(new DialogueEndedEvent { graphId = endedGraph });
        OnDialogueEnd?.Invoke();
    }

    private void OnDestroy()
    {
        if (_runner == null) return;
        _runner.OnLine     -= HandleLine;
        _runner.OnChoices  -= HandleChoices;
        _runner.OnComplete -= HandleComplete;
    }
}
