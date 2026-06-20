using UnityEngine;

/// <summary>
/// Data definition for a single armor piece (mesh + stats) occupying one equipment slot.
/// Instances live in ScriptableObjects/Armor/ and are referenced by itemId.
/// </summary>
[CreateAssetMenu(fileName = "SO_Armor_New", menuName = "Game/Equipment/Armor")]
public class ArmorData : ScriptableObject
{
    [Header("Identity")]
    public string        itemId;
    public string        displayName;
    public EquipmentSlot slot;

    [Header("Visuals")]
    public Mesh          mesh;
    public Material[]    materials;
    [Tooltip("Source mesh bones in bind-pose order (each = a shared-rig bone name). Lets " +
             "CharacterCustomizer rebind this skinned mesh onto the character's shared skeleton. " +
             "Empty = legacy sharedMesh-only swap. Populate via SkeletonRebinder.ExtractBoneNames " +
             "from the imported FBX's SkinnedMeshRenderer.")]
    public string[]      boneNames;
    public Sprite        icon;

    [Header("Stats")]
    public int           defenseBonus;
    [Range(1, 3)]
    public int           weightClass;    // 1=Light, 2=Medium, 3=Heavy
}
