using UnityEngine;

/// <summary>
/// Data definition for a status effect (poison, stun, burn, buffs/debuffs) applied to a CombatUnit.
/// Instances live in ScriptableObjects/StatusEffects/ and are referenced by statusId.
/// </summary>
[CreateAssetMenu(fileName = "SO_Status_New", menuName = "Game/Battle/StatusEffect")]
public class StatusEffectData : ScriptableObject
{
    public string     statusId;
    public string     displayName;
    public Sprite     icon;
    public int        durationTurns;
    public DamageType damageType;
    public int        damagePerTurn;
    public bool       preventsAction;   // e.g. stun
    public float      statMultiplier;   // e.g. 0.5 = halved ATK
    public string     affectedStat;     // "Attack", "Defense", "Speed"
}
