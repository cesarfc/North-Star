using UnityEngine;

/// <summary>
/// Bridges dialogue to quests for the slice: when the quest-giver conversation ends
/// (published as <see cref="DialogueEndedEvent"/> on the EventBus), starts the configured
/// quest in <see cref="QuestManager"/> and completes its "talk to the giver" objective.
/// Idempotent — re-running the conversation never restarts a live or finished quest.
/// Composition-root glue (NorthStar.Game); modules stay decoupled (event in, module API out).
/// </summary>
public class DialogueQuestBridge : MonoBehaviour
{
    [Tooltip("The scene's QuestManager (Dialogue module).")]
    [SerializeField] private QuestManager _quests;

    [Tooltip("Conversation that grants the quest when it ends.")]
    [SerializeField] private string _giverGraphId = "ElderVane_Intro";

    [Tooltip("Quest granted by the conversation.")]
    [SerializeField] private string _questId = "quest-find-the-spark";

    [Tooltip("Objective completed by having had the conversation (empty = none).")]
    [SerializeField] private string _talkObjectiveId = "talk-to-elder-vane";

    private void OnEnable()
    {
        EventBus.Subscribe<DialogueEndedEvent>(HandleDialogueEnded);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<DialogueEndedEvent>(HandleDialogueEnded);
    }

    private void HandleDialogueEnded(DialogueEndedEvent evt)
    {
        if (_quests == null || evt.graphId != _giverGraphId) return;
        if (_quests.IsQuestActive(_questId) || _quests.IsQuestComplete(_questId)) return;

        _quests.StartQuest(_questId);
        if (!string.IsNullOrEmpty(_talkObjectiveId))
            _quests.CompleteObjective(_questId, _talkObjectiveId);
    }
}
