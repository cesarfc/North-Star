using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// EditMode unit tests for the pure quest-state logic in <see cref="QuestTracker"/>,
/// which backs <see cref="QuestManager"/>. These cover the four required areas:
/// start/complete, objective completion, prerequisite gating, and quest-flag
/// save/load round-trip. The tracker is MonoBehaviour-free so no play mode is needed.
/// </summary>
public class QuestTrackerTests
{
    // Track every QuestData built in a test so TearDown can dispose them precisely.
    private readonly List<QuestData> _created = new List<QuestData>();

    // ── Test data builders ────────────────────────────────────────────────────

    private QuestData MakeQuest(
        string questId,
        string[] prerequisites = null,
        params (string id, bool optional)[] objectives)
    {
        var quest = ScriptableObject.CreateInstance<QuestData>();
        quest.questId = questId;
        quest.displayName = questId;
        quest.prerequisiteQuestIds = prerequisites ?? new string[0];
        quest.objectives = objectives.Select(o => new QuestObjective
        {
            objectiveId = o.id,
            description = o.id,
            isOptional = o.optional
        }).ToArray();
        _created.Add(quest);
        return quest;
    }

    private QuestData SimpleQuest(string id) =>
        MakeQuest(id, null, ("obj-1", false));

    [TearDown]
    public void TearDown()
    {
        // Dispose only the ScriptableObjects this test created.
        foreach (var so in _created)
            if (so != null) Object.DestroyImmediate(so);
        _created.Clear();
    }

    // ── Start / Complete ──────────────────────────────────────────────────────

    [Test]
    public void StartQuest_ActivatesAndFiresStartedCallback()
    {
        var quest = SimpleQuest("quest-a");
        var tracker = new QuestTracker(new[] { quest });

        string started = null;
        tracker.QuestStarted += id => started = id;

        bool result = tracker.StartQuest("quest-a");

        Assert.IsTrue(result);
        Assert.AreEqual("quest-a", started);
        Assert.IsTrue(tracker.IsQuestActive("quest-a"));
        Assert.AreEqual(QuestStatus.Active, tracker.GetStatus("quest-a"));
    }

    [Test]
    public void StartQuest_UnknownId_ReturnsFalse()
    {
        var tracker = new QuestTracker(new[] { SimpleQuest("quest-a") });
        Assert.IsFalse(tracker.StartQuest("quest-does-not-exist"));
    }

    [Test]
    public void StartQuest_AlreadyActive_ReturnsFalseAndDoesNotRefire()
    {
        var tracker = new QuestTracker(new[] { SimpleQuest("quest-a") });
        int startCount = 0;
        tracker.QuestStarted += _ => startCount++;

        Assert.IsTrue(tracker.StartQuest("quest-a"));
        Assert.IsFalse(tracker.StartQuest("quest-a"));
        Assert.AreEqual(1, startCount);
    }

    [Test]
    public void CompleteQuest_FromActive_MarksCompleteAndFires()
    {
        var tracker = new QuestTracker(new[] { SimpleQuest("quest-a") });
        string completed = null;
        tracker.QuestCompleted += id => completed = id;

        tracker.StartQuest("quest-a");
        bool result = tracker.CompleteQuest("quest-a");

        Assert.IsTrue(result);
        Assert.AreEqual("quest-a", completed);
        Assert.IsTrue(tracker.IsQuestComplete("quest-a"));
        Assert.IsFalse(tracker.IsQuestActive("quest-a"));
    }

    [Test]
    public void CompleteQuest_WhenNotActive_ReturnsFalse()
    {
        var tracker = new QuestTracker(new[] { SimpleQuest("quest-a") });
        Assert.IsFalse(tracker.CompleteQuest("quest-a")); // never started
    }

    [Test]
    public void FailQuest_FromActive_MarksFailed()
    {
        var tracker = new QuestTracker(new[] { SimpleQuest("quest-a") });
        string failed = null;
        tracker.QuestFailed += id => failed = id;

        tracker.StartQuest("quest-a");
        bool result = tracker.FailQuest("quest-a");

        Assert.IsTrue(result);
        Assert.AreEqual("quest-a", failed);
        Assert.AreEqual(QuestStatus.Failed, tracker.GetStatus("quest-a"));
        Assert.IsFalse(tracker.IsQuestActive("quest-a"));
    }

    [Test]
    public void GetActiveQuests_ReturnsOnlyActiveOnes()
    {
        var a = SimpleQuest("quest-a");
        var b = SimpleQuest("quest-b");
        var c = SimpleQuest("quest-c");
        var tracker = new QuestTracker(new[] { a, b, c });

        tracker.StartQuest("quest-a");
        tracker.StartQuest("quest-b");
        tracker.CompleteQuest("quest-b");

        var active = tracker.GetActiveQuests();
        Assert.AreEqual(1, active.Length);
        Assert.AreEqual("quest-a", active[0].questId);
    }

    // ── Objective completion ──────────────────────────────────────────────────

    [Test]
    public void CompleteObjective_OnActiveQuest_FiresObjectiveCallback()
    {
        var quest = MakeQuest("quest-a", null, ("obj-1", false), ("obj-2", false));
        var tracker = new QuestTracker(new[] { quest });

        var fired = new List<(string, string)>();
        tracker.ObjectiveCompleted += (q, o) => fired.Add((q, o));

        tracker.StartQuest("quest-a");
        bool result = tracker.CompleteObjective("quest-a", "obj-1");

        Assert.IsTrue(result);
        Assert.AreEqual(1, fired.Count);
        Assert.AreEqual(("quest-a", "obj-1"), fired[0]);
        Assert.IsTrue(tracker.IsObjectiveComplete("quest-a", "obj-1"));
        Assert.IsFalse(tracker.IsObjectiveComplete("quest-a", "obj-2"));
    }

    [Test]
    public void CompleteObjective_Twice_IsIdempotent()
    {
        var quest = MakeQuest("quest-a", null, ("obj-1", false), ("obj-2", false));
        var tracker = new QuestTracker(new[] { quest });
        int objFires = 0;
        tracker.ObjectiveCompleted += (_, __) => objFires++;

        tracker.StartQuest("quest-a");
        Assert.IsTrue(tracker.CompleteObjective("quest-a", "obj-1"));
        Assert.IsFalse(tracker.CompleteObjective("quest-a", "obj-1")); // already done
        Assert.AreEqual(1, objFires);
    }

    [Test]
    public void CompleteObjective_UnknownObjective_ReturnsFalse()
    {
        var quest = MakeQuest("quest-a", null, ("obj-1", false));
        var tracker = new QuestTracker(new[] { quest });
        tracker.StartQuest("quest-a");

        Assert.IsFalse(tracker.CompleteObjective("quest-a", "no-such-objective"));
    }

    [Test]
    public void CompleteObjective_OnInactiveQuest_ReturnsFalse()
    {
        var quest = MakeQuest("quest-a", null, ("obj-1", false));
        var tracker = new QuestTracker(new[] { quest });
        // not started
        Assert.IsFalse(tracker.CompleteObjective("quest-a", "obj-1"));
    }

    [Test]
    public void CompletingAllRequiredObjectives_AutoCompletesQuest()
    {
        var quest = MakeQuest("quest-a", null,
            ("obj-1", false),
            ("obj-2", false),
            ("obj-opt", true)); // optional, not required for completion
        var tracker = new QuestTracker(new[] { quest });

        string completed = null;
        tracker.QuestCompleted += id => completed = id;

        tracker.StartQuest("quest-a");
        tracker.CompleteObjective("quest-a", "obj-1");
        Assert.IsNull(completed, "should not complete until all required objectives are done");

        tracker.CompleteObjective("quest-a", "obj-2");
        Assert.AreEqual("quest-a", completed, "completing all required objectives auto-completes");
        Assert.IsTrue(tracker.IsQuestComplete("quest-a"));
    }

    [Test]
    public void OptionalObjectiveAlone_DoesNotCompleteQuest()
    {
        var quest = MakeQuest("quest-a", null,
            ("obj-req", false),
            ("obj-opt", true));
        var tracker = new QuestTracker(new[] { quest });
        tracker.StartQuest("quest-a");

        tracker.CompleteObjective("quest-a", "obj-opt");
        Assert.IsTrue(tracker.IsQuestActive("quest-a"));
        Assert.IsFalse(tracker.IsQuestComplete("quest-a"));
    }

    // ── Prerequisite gating ────────────────────────────────────────────────────

    [Test]
    public void StartQuest_WithUnmetPrerequisite_IsBlocked()
    {
        var a = SimpleQuest("quest-a");
        var b = MakeQuest("quest-b", new[] { "quest-a" }, ("obj-1", false));
        var tracker = new QuestTracker(new[] { a, b });

        Assert.IsFalse(tracker.ArePrerequisitesMet("quest-b"));
        Assert.IsFalse(tracker.StartQuest("quest-b"));
        Assert.AreEqual(QuestStatus.NotStarted, tracker.GetStatus("quest-b"));
    }

    [Test]
    public void StartQuest_WithMetPrerequisite_Succeeds()
    {
        var a = SimpleQuest("quest-a");
        var b = MakeQuest("quest-b", new[] { "quest-a" }, ("obj-1", false));
        var tracker = new QuestTracker(new[] { a, b });

        tracker.StartQuest("quest-a");
        tracker.CompleteQuest("quest-a");

        Assert.IsTrue(tracker.ArePrerequisitesMet("quest-b"));
        Assert.IsTrue(tracker.StartQuest("quest-b"));
        Assert.IsTrue(tracker.IsQuestActive("quest-b"));
    }

    [Test]
    public void StartQuest_PrerequisiteActiveButNotComplete_IsBlocked()
    {
        var a = SimpleQuest("quest-a");
        var b = MakeQuest("quest-b", new[] { "quest-a" }, ("obj-1", false));
        var tracker = new QuestTracker(new[] { a, b });

        tracker.StartQuest("quest-a"); // active, not completed
        Assert.IsFalse(tracker.StartQuest("quest-b"));
    }

    [Test]
    public void Prerequisites_AllMustBeComplete()
    {
        var a = SimpleQuest("quest-a");
        var b = SimpleQuest("quest-b");
        var c = MakeQuest("quest-c", new[] { "quest-a", "quest-b" }, ("obj-1", false));
        var tracker = new QuestTracker(new[] { a, b, c });

        tracker.StartQuest("quest-a");
        tracker.CompleteQuest("quest-a");
        Assert.IsFalse(tracker.StartQuest("quest-c"), "only one of two prerequisites met");

        tracker.StartQuest("quest-b");
        tracker.CompleteQuest("quest-b");
        Assert.IsTrue(tracker.StartQuest("quest-c"), "both prerequisites met");
    }

    // ── Quest-flag save/load round-trip ─────────────────────────────────────────

    [Test]
    public void ExportFlags_EmitsOnlyCompletedQuests()
    {
        var a = SimpleQuest("quest-a");
        var b = SimpleQuest("quest-b");
        var c = SimpleQuest("quest-c");
        var tracker = new QuestTracker(new[] { a, b, c });

        tracker.StartQuest("quest-a");
        tracker.CompleteQuest("quest-a");
        tracker.StartQuest("quest-b"); // active only
        // quest-c untouched

        QuestFlagEntry[] flags = tracker.ExportFlags();

        Assert.AreEqual(1, flags.Length);
        Assert.AreEqual("quest-a", flags[0].questId);
        Assert.IsTrue(flags[0].completed);
    }

    [Test]
    public void ImportFlags_RestoresCompletedState()
    {
        var a = SimpleQuest("quest-a");
        var b = SimpleQuest("quest-b");
        var tracker = new QuestTracker(new[] { a, b });

        var flags = new[]
        {
            new QuestFlagEntry { questId = "quest-a", completed = true }
        };
        tracker.ImportFlags(flags);

        Assert.IsTrue(tracker.IsQuestComplete("quest-a"));
        Assert.AreEqual(QuestStatus.NotStarted, tracker.GetStatus("quest-b"));
    }

    [Test]
    public void SaveLoadRoundTrip_PreservesCompletedQuestsAndUnblocksDependents()
    {
        var a = SimpleQuest("quest-a");
        var b = MakeQuest("quest-b", new[] { "quest-a" }, ("obj-1", false));

        // First session: complete quest-a, export.
        var session1 = new QuestTracker(new[] { a, b });
        session1.StartQuest("quest-a");
        session1.CompleteQuest("quest-a");
        QuestFlagEntry[] saved = session1.ExportFlags();

        // Round-trip through the GameSaveData shape (array, not Dictionary).
        var save = new GameSaveData { questFlags = saved };

        // Second session: fresh tracker, import the loaded flags.
        var session2 = new QuestTracker(new[] { a, b });
        session2.ImportFlags(save.questFlags);

        Assert.IsTrue(session2.IsQuestComplete("quest-a"), "completed state survived round-trip");
        Assert.IsTrue(session2.ArePrerequisitesMet("quest-b"), "prereq satisfied after load");
        Assert.IsTrue(session2.StartQuest("quest-b"), "dependent quest now startable");
    }

    [Test]
    public void ImportFlags_Null_ClearsState_NoThrow()
    {
        var tracker = new QuestTracker(new[] { SimpleQuest("quest-a") });
        tracker.StartQuest("quest-a");

        Assert.DoesNotThrow(() => tracker.ImportFlags(null));
        Assert.AreEqual(QuestStatus.NotStarted, tracker.GetStatus("quest-a"));
    }

    [Test]
    public void ExportThenImport_IsStable()
    {
        var a = SimpleQuest("quest-a");
        var b = SimpleQuest("quest-b");
        var tracker = new QuestTracker(new[] { a, b });
        tracker.StartQuest("quest-a");
        tracker.CompleteQuest("quest-a");
        tracker.StartQuest("quest-b");
        tracker.CompleteQuest("quest-b");

        var flags = tracker.ExportFlags();
        var reloaded = new QuestTracker(new[] { a, b });
        reloaded.ImportFlags(flags);

        var reExported = reloaded.ExportFlags();
        CollectionAssert.AreEquivalent(
            flags.Select(f => f.questId),
            reExported.Select(f => f.questId));
    }
}
