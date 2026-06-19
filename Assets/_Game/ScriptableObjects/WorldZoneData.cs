using UnityEngine;

/// <summary>
/// Data definition for a world zone — its scene, connections, and world-map presentation.
/// Instances live in ScriptableObjects/Zones/ and are referenced by zoneId.
/// </summary>
[CreateAssetMenu(fileName = "SO_Zone_New", menuName = "Game/World/Zone")]
public class WorldZoneData : ScriptableObject
{
    [Header("Identity")]
    public string   zoneId;
    public string   displayName;
    public string   sceneId;            // Matches Unity Build Settings scene name
    public ZoneType zoneType;

    [Header("Connections")]
    public string[] connectedZoneIds;

    [Header("Map")]
    public bool     isDiscoveredByDefault;
    public Sprite   mapIcon;
    public Vector2  mapPosition;
}
