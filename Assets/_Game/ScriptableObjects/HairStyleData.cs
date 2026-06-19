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
    public Color[] availableColors;
    public Sprite  previewIcon;
}
