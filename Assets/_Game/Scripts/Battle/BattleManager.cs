using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Drives the turn-based combat loop: builds the initiative order (Speed + d6, ties re-rolled),
/// advances turns/rounds, watches win/lose conditions, and bridges battle state to the rest of
/// the game via <see cref="GameManager"/> and the <see cref="EventBus"/>.
/// Cross-module listeners should subscribe to the EventBus battle events; the C# events here are
/// for local listeners (BattleUI, BattleCamera) that already hold a reference.
/// </summary>
public class BattleManager : MonoBehaviour
{
    /// <summary>Raised when a battle begins, carrying the participating allies and enemies.</summary>
    public event Action<CombatUnit[], CombatUnit[]> OnBattleStart;
    /// <summary>Raised when a battle ends, carrying the resolved <see cref="BattleResult"/>.</summary>
    public event Action<BattleResult> OnBattleEnd;
    /// <summary>Raised at the start of a unit's turn.</summary>
    public event Action<CombatUnit> OnTurnStart;
    /// <summary>Raised at the end of a unit's turn.</summary>
    public event Action<CombatUnit> OnTurnEnd;

    /// <summary>True while a battle is in progress.</summary>
    public bool IsBattleActive { get; private set; }
    /// <summary>1-based round counter; increments each time the initiative order cycles.</summary>
    public int RoundNumber { get; private set; }
    /// <summary>The unit whose turn is currently active, or null when no battle is running.</summary>
    public CombatUnit ActiveUnit =>
        (_turnOrder.Count > 0 && _turnIndex >= 0 && _turnIndex < _turnOrder.Count)
            ? _turnOrder[_turnIndex]
            : null;

    private CombatUnit[] _allies = Array.Empty<CombatUnit>();
    private CombatUnit[] _enemies = Array.Empty<CombatUnit>();
    private readonly List<CombatUnit> _turnOrder = new();
    private int _turnIndex = -1;

    /// <summary>
    /// Begin a battle with the given allies and enemies. Switches game state to
    /// <see cref="GameState.Battle"/>, publishes <see cref="BattleStartedEvent"/>, builds the
    /// initiative order and starts the first turn.
    /// </summary>
    /// <param name="allies">Player-controlled combatants.</param>
    /// <param name="enemies">Enemy combatants.</param>
    public void StartBattle(CombatUnit[] allies, CombatUnit[] enemies)
    {
        if (IsBattleActive) return;

        _allies  = allies  ?? Array.Empty<CombatUnit>();
        _enemies = enemies ?? Array.Empty<CombatUnit>();

        IsBattleActive = true;
        RoundNumber = 1;

        if (GameManager.Instance != null)
            GameManager.Instance.ChangeState(GameState.Battle);

        BuildTurnOrder();

        OnBattleStart?.Invoke(_allies, _enemies);
        EventBus.Publish(new BattleStartedEvent
        {
            allies  = ToCombatants(_allies),
            enemies = ToCombatants(_enemies)
        });

        BeginTurnAt(0);
    }

    /// <summary>
    /// End the current battle with the given result. Restores game state to
    /// <see cref="GameState.Exploring"/>, fires <see cref="OnBattleEnd"/> and publishes
    /// <see cref="BattleEndedEvent"/>. Safe to call once; subsequent calls are no-ops.
    /// </summary>
    /// <param name="result">Outcome, rewards and loot IDs for this battle.</param>
    public void EndBattle(BattleResult result)
    {
        if (!IsBattleActive) return;

        IsBattleActive = false;
        _turnOrder.Clear();
        _turnIndex = -1;

        OnBattleEnd?.Invoke(result);
        EventBus.Publish(new BattleEndedEvent { result = result });

        if (GameManager.Instance != null)
            GameManager.Instance.ChangeState(GameState.Exploring);
    }

    /// <summary>
    /// Advance to the next living unit's turn. Skips dead units, increments the round when the
    /// order wraps, and ends the battle if a win/lose condition is met. Call after the active
    /// unit has finished acting.
    /// </summary>
    public void AdvanceTurn()
    {
        if (!IsBattleActive) return;

        var finished = ActiveUnit;
        if (finished != null) OnTurnEnd?.Invoke(finished);

        if (CheckEndConditions()) return;

        int next = NextLivingIndex(_turnIndex);
        if (next == -1)
        {
            // Everyone is down — defensive guard; CheckEndConditions normally catches this first.
            EndBattle(new BattleResult { outcome = BattleOutcome.Defeat });
            return;
        }

        if (next <= _turnIndex)
            RoundNumber++;

        BeginTurnAt(next);
    }

    /// <summary>
    /// Build the initiative order: each unit rolls Speed + d6 (1–6); ties are broken by re-rolling
    /// the d6 for the tied units until distinct. Highest total acts first. Pure ordering is delegated
    /// to <see cref="OrderByInitiative"/> so it is unit-testable without play mode.
    /// </summary>
    private void BuildTurnOrder()
    {
        var participants = new List<CombatUnit>();
        participants.AddRange(_allies);
        participants.AddRange(_enemies);

        _turnOrder.Clear();
        _turnOrder.AddRange(OrderByInitiative(participants, RollD6));
        _turnIndex = -1;
    }

    /// <summary>
    /// Order combatants by initiative (Speed + d6), highest first, re-rolling the die for any
    /// tied group until totals are distinct. <paramref name="rollD6"/> is injected so tests can
    /// feed a deterministic die.
    /// </summary>
    /// <param name="units">Combatants to order.</param>
    /// <param name="rollD6">Function returning a value in [1, 6].</param>
    public static List<CombatUnit> OrderByInitiative(IList<CombatUnit> units, Func<int> rollD6)
    {
        var entries = new List<(CombatUnit unit, int total)>();
        foreach (var u in units)
            entries.Add((u, u.Speed + rollD6()));

        // Re-roll ties: while any two entries share a total, re-roll the whole tied group.
        // Bounded so a pathological injected die can never spin forever.
        const int MAX_REROLLS = 64;
        for (int guard = 0; guard < MAX_REROLLS; guard++)
        {
            var tiedTotals = new HashSet<int>();
            var seen = new HashSet<int>();
            foreach (var e in entries)
            {
                if (!seen.Add(e.total)) tiedTotals.Add(e.total);
            }
            if (tiedTotals.Count == 0) break;

            for (int i = 0; i < entries.Count; i++)
            {
                if (tiedTotals.Contains(entries[i].total))
                    entries[i] = (entries[i].unit, entries[i].unit.Speed + rollD6());
            }
        }

        entries.Sort((a, b) => b.total.CompareTo(a.total));

        var ordered = new List<CombatUnit>(entries.Count);
        foreach (var e in entries) ordered.Add(e.unit);
        return ordered;
    }

    private void BeginTurnAt(int index)
    {
        _turnIndex = index;
        var unit = ActiveUnit;
        if (unit == null) return;

        // Resolve damage-over-time / expiry at the start of the acting unit's turn.
        unit.TickStatuses();

        if (CheckEndConditions()) return;

        // A unit killed by its own DoT (or stunned) yields its turn immediately.
        if (!unit.IsAlive)
        {
            AdvanceTurn();
            return;
        }

        OnTurnStart?.Invoke(unit);
    }

    /// <summary>
    /// Resolve win/lose conditions: all enemies down → Victory; all allies down → Defeat.
    /// Ends the battle and returns true if either side has been eliminated.
    /// </summary>
    private bool CheckEndConditions()
    {
        if (!IsBattleActive) return false;

        bool anyAllyAlive  = AnyAlive(_allies);
        bool anyEnemyAlive = AnyAlive(_enemies);

        if (!anyEnemyAlive)
        {
            EndBattle(new BattleResult { outcome = BattleOutcome.Victory });
            return true;
        }
        if (!anyAllyAlive)
        {
            EndBattle(new BattleResult { outcome = BattleOutcome.Defeat });
            return true;
        }
        return false;
    }

    /// <summary>Find the next index (cyclically) holding a living unit; -1 if none remain.</summary>
    private int NextLivingIndex(int from)
    {
        if (_turnOrder.Count == 0) return -1;
        for (int step = 1; step <= _turnOrder.Count; step++)
        {
            int idx = (from + step) % _turnOrder.Count;
            if (_turnOrder[idx] != null && _turnOrder[idx].IsAlive)
                return idx;
        }
        return -1;
    }

    private static bool AnyAlive(CombatUnit[] units)
    {
        foreach (var u in units)
            if (u != null && u.IsAlive) return true;
        return false;
    }

    private static ICombatant[] ToCombatants(CombatUnit[] units)
    {
        var result = new ICombatant[units.Length];
        for (int i = 0; i < units.Length; i++) result[i] = units[i];
        return result;
    }

    private static int RollD6() => UnityEngine.Random.Range(1, 7); // [1, 7) → 1..6
}
