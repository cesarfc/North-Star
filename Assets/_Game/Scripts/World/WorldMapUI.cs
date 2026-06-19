using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Top-down world-map presentation: one icon per zone, laid out by each zone's
/// <c>mapPosition</c>. Discovered zones show their <c>mapIcon</c> in full colour; undiscovered
/// zones are rendered as fog — either hidden or drawn with a greyed/blanked placeholder
/// depending on <see cref="_showUndiscoveredAsFog"/>. The map is data-driven from a
/// <see cref="WorldMapManager"/> and refreshes when a <see cref="ZoneDiscoveredEvent"/> is
/// published, so it never touches another module directly.
/// </summary>
public class WorldMapUI : MonoBehaviour
{
    [Header("Source")]
    [SerializeField] private WorldMapManager _mapManager;

    [Header("Layout")]
    [Tooltip("Container the zone icons are parented under (RectTransform space).")]
    [SerializeField] private RectTransform _iconContainer;

    [Tooltip("Prefab Image used for each zone marker. Instantiated per zone.")]
    [SerializeField] private Image _zoneIconPrefab;

    [Tooltip("Pixels per mapPosition unit when placing icons in the container.")]
    [SerializeField] private float _positionScale = 1f;

    [Header("Fog of war")]
    [Tooltip("If true, undiscovered zones are drawn greyed/blanked; if false they are hidden.")]
    [SerializeField] private bool _showUndiscoveredAsFog = true;

    [Tooltip("Tint applied to undiscovered (fogged) zone icons.")]
    [SerializeField] private Color _fogTint = new Color(0.18f, 0.18f, 0.18f, 0.65f);

    [Tooltip("Tint applied to discovered zone icons.")]
    [SerializeField] private Color _discoveredTint = Color.white;

    // zoneId → spawned marker image, so a discovery can update one icon without a full rebuild.
    private readonly Dictionary<string, Image> _markers = new Dictionary<string, Image>();

    private void OnEnable()
    {
        EventBus.Subscribe<ZoneDiscoveredEvent>(HandleZoneDiscovered);
        Rebuild();
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<ZoneDiscoveredEvent>(HandleZoneDiscovered);
    }

    /// <summary>
    /// Rebuild the whole map from the manager's zone list: clears existing markers and spawns
    /// one icon per registered zone, styled discovered or fogged. Safe to call repeatedly.
    /// </summary>
    public void Rebuild()
    {
        ClearMarkers();

        if (_mapManager == null || _iconContainer == null || _zoneIconPrefab == null) return;

        foreach (var zone in _mapManager.GetAllZones())
        {
            if (zone == null || string.IsNullOrEmpty(zone.zoneId)) continue;

            bool discovered = _mapManager.IsZoneDiscovered(zone.zoneId);

            // Hidden fog mode: skip undiscovered zones entirely.
            if (!discovered && !_showUndiscoveredAsFog) continue;

            var marker = Instantiate(_zoneIconPrefab, _iconContainer);
            marker.name = $"ZoneIcon_{zone.zoneId}";
            marker.rectTransform.anchoredPosition = zone.mapPosition * _positionScale;
            ApplyStyle(marker, zone, discovered);

            _markers[zone.zoneId] = marker;
        }
    }

    /// <summary>
    /// Update a single zone's marker to its discovered appearance (icon + full tint),
    /// creating the marker if fog mode had hidden it. Used on live discovery.
    /// </summary>
    public void RevealZone(string zoneId)
    {
        if (_mapManager == null || string.IsNullOrEmpty(zoneId)) return;

        if (_markers.TryGetValue(zoneId, out var existing) && existing != null)
        {
            ApplyStyle(existing, FindZone(zoneId), true);
            return;
        }

        // Marker did not exist (hidden fog mode) — a full rebuild adds it.
        Rebuild();
    }

    /// <summary>True if this UI currently has a spawned marker for the given zone.</summary>
    public bool HasMarker(string zoneId) =>
        !string.IsNullOrEmpty(zoneId) && _markers.ContainsKey(zoneId);

    private void HandleZoneDiscovered(ZoneDiscoveredEvent e) => RevealZone(e.zoneId);

    private void ApplyStyle(Image marker, WorldZoneData zone, bool discovered)
    {
        if (marker == null) return;

        // Discovered zones show their real icon; fogged zones keep the prefab's blank sprite.
        if (discovered && zone != null && zone.mapIcon != null)
            marker.sprite = zone.mapIcon;

        marker.color = discovered ? _discoveredTint : _fogTint;
    }

    private WorldZoneData FindZone(string zoneId)
    {
        foreach (var zone in _mapManager.GetAllZones())
            if (zone != null && zone.zoneId == zoneId) return zone;
        return null;
    }

    private void ClearMarkers()
    {
        foreach (var marker in _markers.Values)
            if (marker != null) Destroy(marker.gameObject);
        _markers.Clear();
    }
}
