using UnityEngine;

/// <summary>
/// Data definition for a quest — prerequisites, objectives, and rewards.
/// Instances live in ScriptableObjects/Quests/ and are referenced by questId.
/// QuestObjective is defined in Core/SharedTypes.cs.
/// </summary>
[CreateAssetMenu(fileName = "SO_Quest_New", menuName = "Game/Narrative/Quest")]
public class QuestData : ScriptableObject
{
    [Header("Identity")]
    public string          questId;
    public string          displayName;
    [TextArea] public string description;

    [Header("Prerequisites")]
    public string[]        prerequisiteQuestIds;

    [Header("Objectives")]
    public QuestObjective[] objectives;

    [Header("Rewards")]
    public ItemData[]      rewardItems;
    public int             rewardGold;
    public int             rewardExp;
}
