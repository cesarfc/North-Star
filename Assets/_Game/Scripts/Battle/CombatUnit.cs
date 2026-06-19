using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Represents any unit in combat — player characters and enemies alike.
/// Attach to a prefab. Stats are set by the Battle setup code before StartBattle is called.
/// </summary>
public class CombatUnit : MonoBehaviour
{
    [Header("Identity")]
    public string unitName;
    public bool   isPlayerControlled;

    [Header("Base Stats")]
    public int baseMaxHP  = 100;
    public int baseMaxMP  = 50;
    public int baseAttack = 10;
    public int baseDefense = 5;
    public int baseSpeed  = 8;

    // Runtime values — set on battle init
    public int CurrentHP  { get; private set; }
    public int CurrentMP  { get; private set; }
    public int MaxHP      => baseMaxHP;
    public int MaxMP      => baseMaxMP;
    public int Attack     => baseAttack;
    public int Defense    => baseDefense;
    public int Speed      => baseSpeed;
    public bool IsAlive   => CurrentHP > 0;

    public List<AbilityData>      Abilities     { get; private set; } = new();
    public List<StatusEffectData> ActiveStatuses { get; private set; } = new();

    // Events — subscribe via EventBus instead when possible
    public event System.Action<CombatUnit>       OnDeath;
    public event System.Action<StatusEffectData> OnStatusApplied;
    public event System.Action<StatusEffectData> OnStatusRemoved;

    private void Awake()
    {
        CurrentHP = baseMaxHP;
        CurrentMP = baseMaxMP;
    }

    /// <summary>Apply incoming damage. Returns actual damage dealt after defense.</summary>
    public int TakeDamage(int amount, DamageType type)
    {
        // TODO: Agent implements type resistances and defense reduction formula
        int actual = Mathf.Max(1, amount - Defense);
        CurrentHP  = Mathf.Max(0, CurrentHP - actual);

        EventBus.Publish(new PlayerHPChangedEvent { current = CurrentHP, max = MaxHP });

        if (!IsAlive)
        {
            OnDeath?.Invoke(this);
            EventBus.Publish(new UnitDiedEvent { unit = this, wasAlly = isPlayerControlled });
        }

        return actual;
    }

    /// <summary>Restore HP. Returns actual amount healed.</summary>
    public int Heal(int amount)
    {
        int actual = Mathf.Min(amount, MaxHP - CurrentHP);
        CurrentHP += actual;
        return actual;
    }

    /// <summary>Apply a status effect. Refreshes duration if already active.</summary>
    public void ApplyStatus(StatusEffectData effect)
    {
        ActiveStatuses.RemoveAll(s => s.statusId == effect.statusId);
        ActiveStatuses.Add(effect);
        OnStatusApplied?.Invoke(effect);
    }

    /// <summary>Remove a status effect by ID.</summary>
    public void RemoveStatus(string statusId)
    {
        var removed = ActiveStatuses.Find(s => s.statusId == statusId);
        if (removed != null)
        {
            ActiveStatuses.Remove(removed);
            OnStatusRemoved?.Invoke(removed);
        }
    }

    /// <summary>Attempt to use an ability. Returns false if MP insufficient or unit stunned.</summary>
    public bool UseAbility(AbilityData ability, CombatUnit[] targets)
    {
        if (CurrentMP < ability.mpCost) return false;
        if (IsStunned()) return false;

        CurrentMP -= ability.mpCost;
        // TODO: Agent implements ability resolution and targeting logic
        return true;
    }

    private bool IsStunned() =>
        ActiveStatuses.Exists(s => s.preventsAction);
}
