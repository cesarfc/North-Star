using System;
using UnityEngine;

/// <summary>
/// Scene-level manager for world-map state. Owns a pure <see cref="WorldMapState"/> for the
/// discovery/POI logic and handles the Unity-side concerns: holding the WorldZoneData
/// registry, listening for <see cref="ZoneEnteredEvent"/> on the <see cref="EventBus"/> to
/// auto-discover the zone a player walks into, and republishing discovery as
/// <see cref="ZoneDiscoveredEvent"/>. Persists discovered zones via the save data's
/// currentZone/flags pipeline (see <see cref="ExportDiscovered"/> / <see cref="RestoreFromSave"/>).
/// </summary>
public class WorldMapManager : MonoBehaviour
{
    [Tooltip("Every WorldZoneData asset the game knows about. Resolved by zoneId at runtime.")]
    [SerializeField] private WorldZoneData[] _zoneDatabase = Array.Empty<WorldZoneData>();

    private WorldMapState _state;

    /// <summary>Raised when a zone becomes discovered, carrying its resolved <see cref="WorldZoneData"/>.</summary>
    public event Action<WorldZoneData> OnZoneDiscovered;

    /// <summary>Raised when a POI is marked, carrying (zoneId, poi).</summary>
    public event Action<string, POIData> OnPOIMarked;

    private void Awake()
    {
        BuildState();
    }

    private void OnEnable()
    {
        EventBus.Subscribe<ZoneEnteredEvent>(HandleZoneEntered);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<ZoneEnteredEvent>(HandleZoneEntered);
    }

    /// <summary>
    /// Construct the underlying <see cref="WorldMapState"/> from the serialized database and
    /// wire its callbacks. Safe to call again (re-wires cleanly).
    /// </summary>
    private void BuildState()
    {
        if (_state != null) UnsubscribeState();

        _state = new WorldMapState(_zoneDatabase);
        _state.ZoneDiscovered += HandleStateZoneDiscovered;
        _state.POIMarked      += HandleStatePOIMarked;
    }

    private void UnsubscribeState()
    {
        _state.ZoneDiscovered -= HandleStateZoneDiscovered;
        _state.POIMarked      -= HandleStatePOIMarked;
    }

    // ── Public API (INTERFACE.md) ─────────────────────────────────────────────

    /// <summary>Mark the zone with the given lowercase-hyphen id as discovered.</summary>
    public void DiscoverZone(string zoneId) => _state?.DiscoverZone(zoneId);

    /// <summary>True if the zone has been discovered (runtime or discovered-by-default).</summary>
    public bool IsZoneDiscovered(string zoneId) => _state != null && _state.IsZoneDiscovered(zoneId);

    /// <summary>Attach a point-of-interest marker to a known zone.</summary>
    public void MarkPOI(string zoneId, POIData poi) => _state?.MarkPOI(zoneId, poi);

    /// <summary>All currently discovered zones as resolved <see cref="WorldZoneData"/>.</summary>
    public WorldZoneData[] GetDiscoveredZones() =>
        _state != null ? _state.GetDiscoveredZones() : Array.Empty<WorldZoneData>();

    /// <summary>Every registered zone, discovered or not (for fog-of-war rendering).</summary>
    public WorldZoneData[] GetAllZones() =>
        _state != null ? _state.GetAllZones() : Array.Empty<WorldZoneData>();

    /// <summary>The POIs marked on a zone, or an empty array if none / unknown.</summary>
    public POIData[] GetPOIs(string zoneId) =>
        _state != null ? _state.GetPOIs(zoneId) : Array.Empty<POIData>();

    // ── Persistence ───────────────────────────────────────────────────────────

    /// <summary>The runtime-discovered (non-default) zone ids, for inclusion in a save.</summary>
    public string[] ExportDiscovered() =>
        _state != null ? _state.ExportDiscovered() : Array.Empty<string>();

    /// <summary>
    /// Restore discovery state from a saved zone-id list. Null-safe: a null list resets
    /// discovery to the registry defaults.
    /// </summary>
    public void RestoreFromSave(string[] discoveredZoneIds)
    {
        if (_state == null) BuildState();
        _state.ImportDiscovered(discoveredZoneIds);
    }

    // ── EventBus / state callbacks ────────────────────────────────────────────

    /// <summary>Auto-discover a zone the moment the player enters it.</summary>
    private void HandleZoneEntered(ZoneEnteredEvent e)
    {
        _state?.DiscoverZone(e.zoneId);
    }

    private void HandleStateZoneDiscovered(string zoneId)
    {
        EventBus.Publish(new ZoneDiscoveredEvent { zoneId = zoneId });
        OnZoneDiscovered?.Invoke(Resolve(zoneId));
    }

    private void HandleStatePOIMarked(string zoneId, POIData poi)
    {
        OnPOIMarked?.Invoke(zoneId, poi);
    }

    private WorldZoneData Resolve(string zoneId)
    {
        if (_zoneDatabase == null) return null;
        foreach (var zone in _zoneDatabase)
            if (zone != null && zone.zoneId == zoneId) return zone;
        return null;
    }

    private void OnDestroy()
    {
        if (_state != null) UnsubscribeState();
    }
}
