using UnityEngine;

/// <summary>
/// Abstraction for any battle participant, implemented by the Battle module's CombatUnit.
/// Defined in Core so cross-module events (BattleStartedEvent, UnitDiedEvent) and systems
/// such as lock-on can refer to combatants without Core taking a hard dependency on Battle
/// (which would create a Core ↔ Battle assembly cycle).
/// </summary>
public interface ICombatant
{
    /// <summary>Display name of the unit.</summary>
    string UnitName { get; }

    /// <summary>True while the unit has HP remaining.</summary>
    bool IsAlive { get; }

    /// <summary>True for player-controlled allies, false for enemies.</summary>
    bool IsPlayerControlled { get; }

    /// <summary>Current hit points.</summary>
    int CurrentHP { get; }

    /// <summary>Maximum hit points.</summary>
    int MaxHP { get; }

    /// <summary>World-space transform used for targeting and camera framing (e.g. lock-on, battle camera).</summary>
    Transform Anchor { get; }
}
