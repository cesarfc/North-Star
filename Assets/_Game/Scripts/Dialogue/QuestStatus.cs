/// <summary>
/// Lifecycle state of a quest within the <see cref="QuestTracker"/>.
/// </summary>
public enum QuestStatus
{
    /// <summary>Not yet started (and not previously completed/failed).</summary>
    NotStarted,

    /// <summary>Currently active; objectives may be in progress.</summary>
    Active,

    /// <summary>All required objectives done and the quest was turned in.</summary>
    Completed,

    /// <summary>Quest failed and can no longer be completed.</summary>
    Failed
}
