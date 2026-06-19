using System;

/// <summary>
/// Per-element incoming-damage multiplier for a <see cref="CombatUnit"/>.
/// 1 = neutral, &lt;1 = resistant, &gt;1 = weak, 0 = immune, &lt;0 = absorb (heals).
/// </summary>
[Serializable]
public struct ElementalResistance
{
    public DamageType type;
    public float      multiplier;
}
