using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Pure, MonoBehaviour-free quest state machine. Holds the active/completed/failed
/// state for every quest and enforces the rules: prerequisite gating, idempotent
/// objective completion, auto-completion when all required objectives are done, and
/// save/load round-tripping via <see cref="QuestFlagEntry"/>[].
///
/// It is deliberately Unity-runtime-agnostic (it only touches the plain-data fields
/// of <see cref="QuestData"/>) so it can be unit-tested in EditMode without entering
/// play mode. <see cref="QuestManager"/> wraps it and forwards the callbacks below to
/// the <see cref="EventBus"/>.
/// </summary>
public sealed class QuestTracker
{
    // questId → resolved QuestData (the registry the tracker reasons over)
    private readonly Dictionary<string, QuestData> _registry =
        new Dictionary<string, QuestData>(StringComparer.Ordinal);

    // questId → live progress
    private readonly Dictionary<string, QuestProgress> _progress =
        new Dictionary<string, QuestProgress>(StringComparer.Ordinal);

    // ── Callbacks (QuestManager forwards these to EventBus) ──────────────────

    /// <summary>Invoked with the quest id when a quest transitions to Active.</summary>
    public event Action<string> QuestStarted;

    /// <summary>Invoked with (questId, objectiveId) when an objective is newly completed.</summary>
    public event Action<string, string> ObjectiveCompleted;

    /// <summary>Invoked with the quest id when a quest transitions to Completed.</summary>
    public event Action<string> QuestCompleted;

    /// <summary>Invoked with the quest id when a quest transitions to Failed.</summary>
    public event Action<string> QuestFailed;

    /// <summary>
    /// Create a tracker over the supplied quest definitions. Duplicate or empty
    /// quest ids are ignored with no entry added.
    /// </summary>
    public QuestTracker(IEnumerable<QuestData> quests)
    {
        if (quests == null) return;
        foreach (var quest in quests)
            Register(quest);
    }

    /// <summary>Add or replace a quest definition in the registry, keyed by its questId.</summary>
    public void Register(QuestData quest)
    {
        if (quest == null || string.IsNullOrEmpty(quest.questId)) return;
        _registry[quest.questId] = quest;
    }

    // ── Queries ──────────────────────────────────────────────────────────────

    /// <summary>Current status of a quest (NotStarted if unknown/untouched).</summary>
    public QuestStatus GetStatus(string questId)
    {
        return _progress.TryGetValue(questId, out var p) ? p.Status : QuestStatus.NotStarted;
    }

    /// <summary>True if the quest is currently Active.</summary>
    public bool IsQuestActive(string questId) => GetStatus(questId) == QuestStatus.Active;

    /// <summary>True if the quest has been Completed.</summary>
    public bool IsQuestComplete(string questId) => GetStatus(questId) == QuestStatus.Completed;

    /// <summary>True if every prerequisite quest of <paramref name="questId"/> is Completed.</summary>
    public bool ArePrerequisitesMet(string questId)
    {
        if (!_registry.TryGetValue(questId, out var quest)) return false;
        if (quest.prerequisiteQuestIds == null) return true;

        foreach (var prereq in quest.prerequisiteQuestIds)
        {
            if (string.IsNullOrEmpty(prereq)) continue;
            if (GetStatus(prereq) != QuestStatus.Completed) return false;
        }
        return true;
    }

    /// <summary>The resolved <see cref="QuestData"/> for every currently Active quest.</summary>
    public QuestData[] GetActiveQuests()
    {
        return _progress.Values
            .Where(p => p.Status == QuestStatus.Active && _registry.ContainsKey(p.QuestId))
            .Select(p => _registry[p.QuestId])
            .ToArray();
    }

    /// <summary>True if the given objective of the given quest is complete.</summary>
    public bool IsObjectiveComplete(string questId, string objectiveId)
    {
        return _progress.TryGetValue(questId, out var p) && p.IsObjectiveComplete(objectiveId);
    }

    // ── Transitions ───────────────────────────────────────────────────────────

    /// <summary>
    /// Start a quest. Fails (returns false) if the quest is unknown, already
    /// active/completed, or its prerequisites are not all completed.
    /// </summary>
    public bool StartQuest(string questId)
    {
        if (!_registry.ContainsKey(questId)) return false;

        var status = GetStatus(questId);
        if (status == QuestStatus.Active || status == QuestStatus.Completed) return false;

        if (!ArePrerequisitesMet(questId)) return false;

        var progress = GetOrCreate(questId);
        progress.Status = QuestStatus.Active;

        QuestStarted?.Invoke(questId);
        return true;
    }

    /// <summary>
    /// Mark an objective complete on an active quest. Returns false if the quest is not
    /// active, the objective id is unknown, or it was already complete. When the final
    /// required (non-optional) objective is completed the quest auto-completes.
    /// </summary>
    public bool CompleteObjective(string questId, string objectiveId)
    {
        if (GetStatus(questId) != QuestStatus.Active) return false;
        if (!_registry.TryGetValue(questId, out var quest)) return false;
        if (!ObjectiveExists(quest, objectiveId)) return false;

        var progress = _progress[questId];
        if (!progress.MarkObjective(objectiveId)) return false; // already complete

        ObjectiveCompleted?.Invoke(questId, objectiveId);

        if (AllRequiredObjectivesComplete(quest, progress))
            CompleteQuest(questId);

        return true;
    }

    /// <summary>
    /// Force a quest to Completed (e.g. on turn-in). Returns false if it is not active.
    /// Idempotent guards prevent double-firing the completed callback.
    /// </summary>
    public bool CompleteQuest(string questId)
    {
        if (GetStatus(questId) != QuestStatus.Active) return false;

        _progress[questId].Status = QuestStatus.Completed;
        QuestCompleted?.Invoke(questId);
        return true;
    }

    /// <summary>Fail an active quest. Returns false if it is not active.</summary>
    public bool FailQuest(string questId)
    {
        if (GetStatus(questId) != QuestStatus.Active) return false;

        _progress[questId].Status = QuestStatus.Failed;
        QuestFailed?.Invoke(questId);
        return true;
    }

    // ── Persistence ───────────────────────────────────────────────────────────

    /// <summary>
    /// Export the completed-quest flags for the save file. Only quests in the
    /// Completed state are emitted (key = questId, value = completed = true), matching
    /// the <see cref="GameSaveData.questFlags"/> contract.
    /// </summary>
    public QuestFlagEntry[] ExportFlags()
    {
        return _progress.Values
            .Where(p => p.Status == QuestStatus.Completed)
            .Select(p => new QuestFlagEntry { questId = p.QuestId, completed = true })
            .ToArray();
    }

    /// <summary>
    /// Restore completed-quest state from a save file's <see cref="QuestFlagEntry"/>[].
    /// Clears existing progress first; entries with <c>completed == true</c> are restored
    /// as Completed quests. Does NOT fire callbacks (loading is silent). Unknown quest
    /// ids are still recorded so flags survive round-trips even before the asset exists.
    /// </summary>
    public void ImportFlags(QuestFlagEntry[] flags)
    {
        _progress.Clear();
        if (flags == null) return;

        foreach (var flag in flags)
        {
            if (string.IsNullOrEmpty(flag.questId)) continue;
            var status = flag.completed ? QuestStatus.Completed : QuestStatus.Active;
            _progress[flag.questId] = new QuestProgress(flag.questId, status);
        }
    }

    // ── Internals ─────────────────────────────────────────────────────────────

    private QuestProgress GetOrCreate(string questId)
    {
        if (!_progress.TryGetValue(questId, out var p))
        {
            p = new QuestProgress(questId, QuestStatus.NotStarted);
            _progress[questId] = p;
        }
        return p;
    }

    private static bool ObjectiveExists(QuestData quest, string objectiveId)
    {
        if (quest.objectives == null) return false;
        foreach (var obj in quest.objectives)
            if (obj.objectiveId == objectiveId) return true;
        return false;
    }

    private static bool AllRequiredObjectivesComplete(QuestData quest, QuestProgress progress)
    {
        if (quest.objectives == null || quest.objectives.Length == 0) return false;

        foreach (var obj in quest.objectives)
        {
            if (obj.isOptional) continue;
            if (!progress.IsObjectiveComplete(obj.objectiveId)) return false;
        }
        return true;
    }
}
