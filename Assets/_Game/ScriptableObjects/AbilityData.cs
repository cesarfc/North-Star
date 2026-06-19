using UnityEngine;

/// <summary>
/// Data definition for a combat ability — cost, damage profile, presentation, and applied effects.
/// Instances live in ScriptableObjects/Abilities/ and are referenced by abilityId.
/// </summary>
[CreateAssetMenu(fileName = "SO_Ability_New", menuName = "Game/Battle/Ability")]
public class AbilityData : ScriptableObject
{
    [Header("Identity")]
    public string       abilityId;
    public string       displayName;
    [TextArea] public string description;

    [Header("Battle")]
    public int          mpCost;
    public DamageType   damageType;
    [Range(0f, 10f)]
    public float        damageMultiplier;
    public bool         isMultiTarget;

    [Header("Presentation")]
    public string       animationTrigger;   // Must match Animator parameter name
    public Sprite       icon;

    [Header("Effects")]
    public StatusEffectData[] appliedEffects;
}
