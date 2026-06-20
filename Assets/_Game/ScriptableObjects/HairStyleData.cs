using UnityEngine;

/// <summary>
/// Data definition for a selectable hairstyle (mesh + tintable color options).
/// Instances live in ScriptableObjects/Hair/ and are referenced by styleId.
/// </summary>
[CreateAssetMenu(fileName = "SO_Hair_New", menuName = "Game/Equipment/HairStyle")]
public class HairStyleData : ScriptableObject
{
    public string  styleId;
    public string  displayName;
    public Mesh    mesh;
    [Tooltip("Source mesh bones in bind-pose order (each = a shared-rig bone name). Lets " +
             "CharacterCustomizer rebind this skinned hair mesh onto the character's shared " +
             "skeleton. Empty = legacy sharedMesh-only swap.")]
    public string[] boneNames;
    public Color[] availableColors;
    public Sprite  previewIcon;
}
