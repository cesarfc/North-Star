using System;
using UnityEngine;

/// <summary>
/// Fallback <see cref="IDialogueRunner"/> used when the Yarn Spinner package is not
/// installed (no <c>YARN_SPINNER</c> scripting define). It satisfies the interface so
/// <see cref="DialogueSystem"/> and the EditMode tests compile and run, but it cannot
/// actually run a Yarn graph. Once the orchestrator installs Yarn Spinner and adds the
/// <c>YARN_SPINNER</c> define, <c>YarnDialogueRunner</c> is wired in instead.
/// </summary>
public sealed class NullDialogueRunner : IDialogueRunner
{
    /// <summary>Always false — this stub never runs a real graph.</summary>
    public bool IsRunning => false;

    /// <summary>Logs a warning that no dialogue backend is installed.</summary>
    public void StartGraph(string graphId)
    {
        Debug.LogWarning(
            $"[NullDialogueRunner] StartGraph('{graphId}') ignored — Yarn Spinner is not installed. " +
            "Install the package and add the YARN_SPINNER scripting define to enable dialogue.");
        // Immediately report completion so callers that wait on OnComplete do not hang.
        OnComplete?.Invoke();
    }

    /// <summary>No-op — nothing is running.</summary>
    public void Continue() { }

    /// <summary>No-op — nothing is running.</summary>
    public void SelectOption(int choiceIndex) { }

    /// <summary>No-op — nothing is running.</summary>
    public void Stop() => OnComplete?.Invoke();

    /// <summary>Never raised by the null runner.</summary>
    public event Action<DialogueLine> OnLine;

    /// <summary>Never raised by the null runner.</summary>
    public event Action<DialogueChoice[]> OnChoices;

    /// <summary>Raised once from <see cref="StartGraph"/> and <see cref="Stop"/>.</summary>
    public event Action OnComplete;
}
