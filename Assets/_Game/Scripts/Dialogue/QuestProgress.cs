using System.Collections.Generic;

/// <summary>
/// Runtime progress for a single quest tracked by <see cref="QuestTracker"/>.
/// Pure C# (no Unity types) so it is fully unit-testable in EditMode. Holds the
/// quest's lifecycle <see cref="QuestStatus"/> and the set of completed objective ids.
/// </summary>
public sealed class QuestProgress
{
    /// <summary>The quest's lowercase-hyphen id this progress belongs to.</summary>
    public string QuestId { get; }

    /// <summary>Current lifecycle status of the quest.</summary>
    public QuestStatus Status { get; internal set; }

    private readonly HashSet<string> _completedObjectiveIds = new HashSet<string>();

    /// <summary>Create progress for <paramref name="questId"/>, defaulting to Active.</summary>
    public QuestProgress(string questId, QuestStatus status = QuestStatus.Active)
    {
        QuestId = questId;
        Status = status;
    }

    /// <summary>True if the given objective has been marked complete.</summary>
    public bool IsObjectiveComplete(string objectiveId) =>
        _completedObjectiveIds.Contains(objectiveId);

    /// <summary>Mark an objective complete. Returns false if it was already complete.</summary>
    public bool MarkObjective(string objectiveId) =>
        _completedObjectiveIds.Add(objectiveId);

    /// <summary>Number of objectives completed so far.</summary>
    public int CompletedObjectiveCount => _completedObjectiveIds.Count;

    /// <summary>Snapshot of completed objective ids (copy; safe to mutate by caller).</summary>
    public IReadOnlyCollection<string> CompletedObjectiveIds => _completedObjectiveIds;
}
