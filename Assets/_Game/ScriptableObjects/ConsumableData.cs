using UnityEngine;

/// <summary>
/// Consumable item (potions, food, cure items). Restores HP/MP or applies/cures status effects.
/// </summary>
[CreateAssetMenu(fileName = "SO_Item_Consumable_New", menuName = "Game/Items/Consumable")]
public class ConsumableData : ItemData
{
    public int hpRestore;
    public int mpRestore;
    public StatusEffectData appliedEffect;
    public bool curesAllStatus;
}
