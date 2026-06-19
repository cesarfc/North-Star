using UnityEngine;

/// <summary>
/// Base data definition for any inventory item. Subclassed by ConsumableData, WeaponData, etc.
/// Instances live in ScriptableObjects/Items/ and are referenced by itemId.
/// </summary>
[CreateAssetMenu(fileName = "SO_Item_New", menuName = "Game/Items/Item")]
public class ItemData : ScriptableObject
{
    [Header("Identity")]
    public string   itemId;
    public string   displayName;
    [TextArea] public string description;
    public ItemType itemType;

    [Header("Economy")]
    public int      buyPrice;
    public int      sellPrice;

    [Header("Stacking")]
    public bool     isStackable;
    public int      maxStackSize;

    [Header("Visuals")]
    public Sprite   icon;
}
