using System;

/// <summary>
/// Thin abstraction over the underlying dialogue engine (Yarn Spinner).
/// Nothing in the Dialogue module talks to Yarn Spinner types directly — they go
/// through this interface so the testable dialogue/quest state logic does not
/// hard-depend on the (orchestrator-installed) Yarn Spinner package.
///
/// The concrete Yarn-backed implementation lives in <c>YarnDialogueRunner.cs</c>
/// and is compiled only when the <c>YARN_SPINNER</c> scripting define is present.
/// When Yarn is absent, <c>NullDialogueRunner</c> is used so the rest of the
/// module still compiles and runs in EditMode tests.
/// </summary>
public interface IDialogueRunner
{
    /// <summary>True while a dialogue graph is currently running.</summary>
    bool IsRunning { get; }

    /// <summary>Begin running the Yarn node/graph identified by <paramref name="graphId"/>.</summary>
    void StartGraph(string graphId);

    /// <summary>Advance past the currently displayed line to the next content.</summary>
    void Continue();

    /// <summary>Select the option at <paramref name="choiceIndex"/> from the active choice set.</summary>
    void SelectOption(int choiceIndex);

    /// <summary>Force-stop the running graph (e.g. dialogue skipped or interrupted).</summary>
    void Stop();

    /// <summary>Raised when the engine delivers a line of dialogue to display.</summary>
    event Action<DialogueLine> OnLine;

    /// <summary>Raised when the engine presents a set of selectable choices.</summary>
    event Action<DialogueChoice[]> OnChoices;

    /// <summary>Raised when the running graph reaches its end (or is stopped).</summary>
    event Action OnComplete;
}
