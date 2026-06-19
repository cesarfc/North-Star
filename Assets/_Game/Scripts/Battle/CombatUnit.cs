using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Represents any unit in combat — player characters and enemies alike.
/// Attach to a prefab. Stats are set by the Battle setup code before StartBattle is called.
/// Pure combat math (damage, ability resolution, status ticking) is kept free of play-mode
/// dependencies so it can be exercised in EditMode tests.
/// </summary>
public class CombatUnit : MonoBehaviour, ICombatant
{
    [Header("Identity")]
    public string unitName;
    public bool   isPlayerControlled;

    // ICombatant implementation (abstraction consumed by Core events + lock-on)
    public string    UnitName           => unitName;
    public bool      IsPlayerControlled => isPlayerControlled;
    public Transform Anchor             => transform;

    [Header("Base Stats")]
    public int baseMaxHP  = 100;
    public int baseMaxMP  = 50;
    public int baseAttack = 10;
    public int baseDefense = 5;
    public int baseSpeed  = 8;

    [Header("Elemental Resistances")]
    [Tooltip("Per-element multipliers applied to incoming damage of that type. " +
             "1 = neutral, <1 = resistant, >1 = weak, 0 = immune, <0 = absorb (heals).")]
    public ElementalResistance[] resistances = Array.Empty<ElementalResistance>();

    // Runtime values — set on battle init
    public int CurrentHP  { get; private set; }
    public int CurrentMP  { get; private set; }
    public int MaxHP      => baseMaxHP;
    public int MaxMP      => baseMaxMP;

    /// <summary>Effective Attack after status multipliers (e.g. debuffs).</summary>
    public int Attack  => Mathf.Max(0, Mathf.RoundToInt(baseAttack  * StatMultiplierFor("Attack")));
    /// <summary>Effective Defense after status multipliers.</summary>
    public int Defense => Mathf.Max(0, Mathf.RoundToInt(baseDefense * StatMultiplierFor("Defense")));
    /// <summary>Effective Speed after status multipliers (also feeds turn-order rolls).</summary>
    public int Speed   => Mathf.Max(0, Mathf.RoundToInt(baseSpeed   * StatMultiplierFor("Speed")));

    public bool IsAlive => CurrentHP > 0;

    public List<AbilityData>      Abilities      { get; private set; } = new();
    public List<StatusEffectData> ActiveStatuses { get; private set; } = new();

    // Events — subscribe via EventBus instead when possible
    public event System.Action<CombatUnit>       OnDeath;
    public event System.Action<StatusEffectData> OnStatusApplied;
    public event System.Action<StatusEffectData> OnStatusRemoved;

    // Remaining duration (in turns) for each active status, keyed by statusId.
    private readonly Dictionary<string, int> _statusTurnsRemaining = new();

    private void Awake()
    {
        ResetRuntimeStats();
    }

    /// <summary>
    /// Reset HP/MP to full and clear all statuses. Called by Awake and by battle setup
    /// so a reused unit starts each encounter clean. Exposed for tests and pooling.
    /// </summary>
    public void ResetRuntimeStats()
    {
        CurrentHP = baseMaxHP;
        CurrentMP = baseMaxMP;
        ActiveStatuses.Clear();
        _statusTurnsRemaining.Clear();
    }

    /// <summary>
    /// Apply incoming damage after defense reduction and elemental resistance.
    /// Returns the actual HP delta applied (positive = damage taken; negative = absorbed/healed).
    /// Heal-type damage restores HP instead of subtracting it.
    /// </summary>
    /// <param name="amount">Raw incoming amount before defense/resistance.</param>
    /// <param name="type">Element of the incoming hit; selects the resistance multiplier.</param>
    public int TakeDamage(int amount, DamageType type)
    {
        // Heal-type "damage" is restorative — route through Heal so the formula stays in one place.
        if (type == DamageType.Heal)
            return -Heal(amount);

        int actual = ComputeDamage(amount, Defense, ResistanceFor(type));

        if (actual >= 0)
            CurrentHP = Mathf.Max(0, CurrentHP - actual);
        else
            // Negative result = absorption: the element heals this unit.
            CurrentHP = Mathf.Min(MaxHP, CurrentHP - actual);

        PublishHPChanged();

        if (!IsAlive)
        {
            OnDeath?.Invoke(this);
            EventBus.Publish(new UnitDiedEvent { unit = this, wasAlly = isPlayerControlled });
        }

        return actual;
    }

    /// <summary>
    /// Pure damage formula: <c>(amount - defense)</c> floored at 1, then scaled by the
    /// elemental resistance multiplier and rounded. A multiplier of 0 yields 0 (immune);
    /// a negative multiplier yields a negative result (absorb). Static so tests can call it
    /// without instantiating a MonoBehaviour.
    /// </summary>
    public static int ComputeDamage(int amount, int defense, float resistanceMultiplier)
    {
        int afterDefense = Mathf.Max(1, amount - defense);
        return Mathf.RoundToInt(afterDefense * resistanceMultiplier);
    }

    /// <summary>Restore HP, clamped to MaxHP. Returns actual amount healed.</summary>
    public int Heal(int amount)
    {
        if (amount <= 0) return 0;
        int actual = Mathf.Min(amount, MaxHP - CurrentHP);
        CurrentHP += actual;
        if (actual > 0) PublishHPChanged();
        return actual;
    }

    /// <summary>Restore MP, clamped to MaxMP. Returns actual amount restored.</summary>
    public int RestoreMP(int amount)
    {
        if (amount <= 0) return 0;
        int actual = Mathf.Min(amount, MaxMP - CurrentMP);
        CurrentMP += actual;
        return actual;
    }

    /// <summary>Apply a status effect. Refreshes duration if already active.</summary>
    public void ApplyStatus(StatusEffectData effect)
    {
        if (effect == null) return;

        ActiveStatuses.RemoveAll(s => s.statusId == effect.statusId);
        ActiveStatuses.Add(effect);
        _statusTurnsRemaining[effect.statusId] = Mathf.Max(1, effect.durationTurns);
        OnStatusApplied?.Invoke(effect);
    }

    /// <summary>Remove a status effect by ID.</summary>
    public void RemoveStatus(string statusId)
    {
        var removed = ActiveStatuses.Find(s => s.statusId == statusId);
        if (removed != null)
        {
            ActiveStatuses.Remove(removed);
            _statusTurnsRemaining.Remove(statusId);
            OnStatusRemoved?.Invoke(removed);
        }
    }

    /// <summary>
    /// Advance all active statuses by one turn: apply per-turn damage (e.g. poison/burn),
    /// then decrement durations and expire any that have run out. Call once per this unit's turn.
    /// </summary>
    public void TickStatuses()
    {
        // Snapshot so removal during iteration is safe.
        var current = ActiveStatuses.ToArray();
        foreach (var status in current)
        {
            if (status.damagePerTurn > 0 && IsAlive)
                TakeDamage(status.damagePerTurn, status.damageType);
        }

        var expired = new List<string>();
        foreach (var status in current)
        {
            if (!_statusTurnsRemaining.TryGetValue(status.statusId, out int turns))
                continue;

            turns--;
            if (turns <= 0) expired.Add(status.statusId);
            else            _statusTurnsRemaining[status.statusId] = turns;
        }

        foreach (var id in expired)
            RemoveStatus(id);
    }

    /// <summary>
    /// Attempt to use an ability against the given targets. Returns false if MP is
    /// insufficient or the user is stunned. On success, spends MP, then resolves the
    /// ability against each target (damage/heal by type, plus any applied status effects).
    /// </summary>
    /// <param name="ability">The ability definition to resolve.</param>
    /// <param name="targets">Targets selected by the caller (BattleManager/AI/UI).</param>
    public bool UseAbility(AbilityData ability, CombatUnit[] targets)
    {
        if (ability == null) return false;
        if (CurrentMP < ability.mpCost) return false;
        if (IsStunned()) return false;

        CurrentMP -= ability.mpCost;

        if (targets == null) return true;

        // Single-target abilities only affect the first valid target even if more are passed.
        int count = ability.isMultiTarget ? targets.Length : Mathf.Min(1, targets.Length);
        for (int i = 0; i < count; i++)
        {
            var target = targets[i];
            if (target == null || !target.IsAlive) continue;

            ResolveAgainst(ability, target);
        }

        return true;
    }

    /// <summary>
    /// Apply this ability's effect to a single target: heals route through Heal, all other
    /// elements scale this unit's Attack by the ability's multiplier and route through TakeDamage,
    /// then any applied status effects are added to the target.
    /// </summary>
    private void ResolveAgainst(AbilityData ability, CombatUnit target)
    {
        if (ability.damageType == DamageType.Heal)
        {
            int healAmount = Mathf.RoundToInt(Attack * ability.damageMultiplier);
            target.Heal(healAmount);
        }
        else if (ability.damageType != DamageType.Buff && ability.damageType != DamageType.Debuff)
        {
            int rawDamage = Mathf.RoundToInt(Attack * ability.damageMultiplier);
            target.TakeDamage(rawDamage, ability.damageType);
        }

        if (ability.appliedEffects != null)
        {
            foreach (var effect in ability.appliedEffects)
                target.ApplyStatus(effect);
        }
    }

    /// <summary>True when any active status prevents this unit from acting (e.g. stun).</summary>
    public bool IsStunned() => ActiveStatuses.Exists(s => s.preventsAction);

    /// <summary>Resistance multiplier for a damage type; 1 (neutral) when none is configured.</summary>
    public float ResistanceFor(DamageType type)
    {
        if (resistances != null)
        {
            foreach (var r in resistances)
                if (r.type == type) return r.multiplier;
        }
        return 1f;
    }

    /// <summary>Product of all active status multipliers that target the given stat name.</summary>
    private float StatMultiplierFor(string statName)
    {
        float multiplier = 1f;
        foreach (var s in ActiveStatuses)
        {
            if (s.statMultiplier > 0f && s.affectedStat == statName)
                multiplier *= s.statMultiplier;
        }
        return multiplier;
    }

    private void PublishHPChanged()
    {
        // Per-unit event drives every HP bar (allies + enemies).
        EventBus.Publish(new UnitHPChangedEvent { unit = this, current = CurrentHP, max = MaxHP });

        // PlayerHPChangedEvent is reserved for the player HUD — fix for the known stub bug
        // where it fired for every unit including enemies.
        if (isPlayerControlled)
            EventBus.Publish(new PlayerHPChangedEvent { current = CurrentHP, max = MaxHP });
    }
}
