using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Scene-level manager for quest state. Owns a pure <see cref="QuestTracker"/> for the
/// actual logic and is responsible for the Unity-side concerns: holding the QuestData
/// registry, republishing tracker transitions on the <see cref="EventBus"/> and via the
/// frozen INTERFACE.md events, and persisting completed-quest flags through
/// <see cref="SaveSystem"/> as a <see cref="QuestFlagEntry"/>[] (never a Dictionary).
/// </summary>
public class QuestManager : MonoBehaviour
{
    [Tooltip("Every QuestData asset the game knows about. Resolved by questId at runtime.")]
    [SerializeField] private QuestData[] _questDatabase = Array.Empty<QuestData>();

    private QuestTracker _tracker;

    // ── INTERFACE.md quest events ────────────────────────────────────────────

    /// <summary>Raised when a quest becomes active, carrying its resolved <see cref="QuestData"/>.</summary>
    public event Action<QuestData> OnQuestStarted;

    /// <summary>Raised when a quest is completed, carrying its resolved <see cref="QuestData"/>.</summary>
    public event Action<QuestData> OnQuestCompleted;

    /// <summary>Raised when an objective is completed, carrying (questId, objectiveId).</summary>
    public event Action<string, string> OnObjectiveCompleted;

    private void Awake()
    {
        BuildTracker();
    }

    /// <summary>
    /// Construct the underlying <see cref="QuestTracker"/> from the serialized database
    /// and subscribe to its callbacks. Safe to call again (re-wires cleanly).
    /// </summary>
    private void BuildTracker()
    {
        if (_tracker != null) UnsubscribeTracker();

        _tracker = new QuestTracker(_questDatabase);
        _tracker.QuestStarted       += HandleQuestStarted;
        _tracker.ObjectiveCompleted += HandleObjectiveCompleted;
        _tracker.QuestCompleted     += HandleQuestCompleted;
    }

    private void UnsubscribeTracker()
    {
        _tracker.QuestStarted       -= HandleQuestStarted;
        _tracker.ObjectiveCompleted -= HandleObjectiveCompleted;
        _tracker.QuestCompleted     -= HandleQuestCompleted;
    }

    // ── Public API (INTERFACE.md) ─────────────────────────────────────────────

    /// <summary>Start the quest with the given lowercase-hyphen id (respects prerequisites).</summary>
    public void StartQuest(string questId) => _tracker?.StartQuest(questId);

    /// <summary>Mark an objective complete; may auto-complete the quest.</summary>
    public void CompleteObjective(string questId, string objectiveId) =>
        _tracker?.CompleteObjective(questId, objectiveId);

    /// <summary>Force a quest to the completed state (e.g. on turn-in).</summary>
    public void CompleteQuest(string questId) => _tracker?.CompleteQuest(questId);

    /// <summary>Fail an active quest.</summary>
    public void FailQuest(string questId) => _tracker?.FailQuest(questId);

    /// <summary>True if the quest is currently active.</summary>
    public bool IsQuestActive(string questId) => _tracker != null && _tracker.IsQuestActive(questId);

    /// <summary>True if the quest has been completed.</summary>
    public bool IsQuestComplete(string questId) => _tracker != null && _tracker.IsQuestComplete(questId);

    /// <summary>All currently active quests as resolved <see cref="QuestData"/>.</summary>
    public QuestData[] GetActiveQuests() =>
        _tracker != null ? _tracker.GetActiveQuests() : Array.Empty<QuestData>();

    // ── Persistence ───────────────────────────────────────────────────────────

    /// <summary>
    /// Write the current completed-quest flags into <paramref name="data"/> as a
    /// <see cref="QuestFlagEntry"/>[] so they ride along in the next
    /// <see cref="SaveSystem.Save"/>. Returns the populated array.
    /// </summary>
    public QuestFlagEntry[] CaptureFlags(GameSaveData data)
    {
        var flags = _tracker != null ? _tracker.ExportFlags() : Array.Empty<QuestFlagEntry>();
        if (data != null) data.questFlags = flags;
        return flags;
    }

    /// <summary>
    /// Restore quest state from a loaded <see cref="GameSaveData"/>'s questFlags array.
    /// Null-safe: a missing save or null flags resets quest progress to empty.
    /// </summary>
    public void RestoreFromSave(GameSaveData data)
    {
        if (_tracker == null) BuildTracker();
        _tracker.ImportFlags(data != null ? data.questFlags : null);
    }

    // ── Tracker callback → EventBus / public events ───────────────────────────

    private void HandleQuestStarted(string questId)
    {
        EventBus.Publish(new QuestStartedEvent { questId = questId });
        OnQuestStarted?.Invoke(Resolve(questId));
    }

    private void HandleObjectiveCompleted(string questId, string objectiveId)
    {
        EventBus.Publish(new ObjectiveCompletedEvent { questId = questId, objectiveId = objectiveId });
        OnObjectiveCompleted?.Invoke(questId, objectiveId);
    }

    private void HandleQuestCompleted(string questId)
    {
        EventBus.Publish(new QuestCompletedEvent { questId = questId });
        OnQuestCompleted?.Invoke(Resolve(questId));
    }

    private QuestData Resolve(string questId)
    {
        if (_questDatabase == null) return null;
        foreach (var quest in _questDatabase)
            if (quest != null && quest.questId == questId) return quest;
        return null;
    }

    private void OnDestroy()
    {
        if (_tracker != null) UnsubscribeTracker();
    }
}
