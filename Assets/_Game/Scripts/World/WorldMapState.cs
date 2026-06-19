using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Pure, MonoBehaviour-free world-map state. Owns the registry of <see cref="WorldZoneData"/>
/// definitions plus the runtime discovered/POI state, and enforces the rules: a zone is
/// discovered if marked discovered at runtime OR flagged <c>isDiscoveredByDefault</c>;
/// discovery is idempotent (the discovered callback fires once per zone); POIs may only be
/// attached to known zones.
///
/// It is deliberately Unity-runtime-agnostic (it only reads the plain-data fields of
/// <see cref="WorldZoneData"/> and <see cref="POIData"/>) so it can be unit-tested in
/// EditMode without entering play mode. <see cref="WorldMapManager"/> wraps it and forwards
/// the callbacks below to the <see cref="EventBus"/>.
/// </summary>
public sealed class WorldMapState
{
    // zoneId → resolved WorldZoneData (the registry the state reasons over)
    private readonly Dictionary<string, WorldZoneData> _registry =
        new Dictionary<string, WorldZoneData>(StringComparer.Ordinal);

    // zoneIds discovered at runtime (defaults are honoured separately so reset is clean)
    private readonly HashSet<string> _discovered = new HashSet<string>(StringComparer.Ordinal);

    // zoneId → POIs marked on that zone
    private readonly Dictionary<string, List<POIData>> _pois =
        new Dictionary<string, List<POIData>>(StringComparer.Ordinal);

    /// <summary>Invoked with the zone id the first time a zone becomes discovered.</summary>
    public event Action<string> ZoneDiscovered;

    /// <summary>Invoked with (zoneId, poi) when a POI is newly marked on a known zone.</summary>
    public event Action<string, POIData> POIMarked;

    /// <summary>
    /// Create a map state over the supplied zone definitions. Zones flagged
    /// <c>isDiscoveredByDefault</c> are seeded as discovered without firing callbacks.
    /// Duplicate or empty zone ids are ignored.
    /// </summary>
    public WorldMapState(IEnumerable<WorldZoneData> zones)
    {
        if (zones == null) return;
        foreach (var zone in zones)
            Register(zone);
    }

    /// <summary>Add or replace a zone definition, keyed by its zoneId. Honours defaults.</summary>
    public void Register(WorldZoneData zone)
    {
        if (zone == null || string.IsNullOrEmpty(zone.zoneId)) return;
        _registry[zone.zoneId] = zone;
        if (zone.isDiscoveredByDefault)
            _discovered.Add(zone.zoneId);
    }

    // ── Queries ──────────────────────────────────────────────────────────────

    /// <summary>True if the zone is known to the registry.</summary>
    public bool IsZoneRegistered(string zoneId) =>
        !string.IsNullOrEmpty(zoneId) && _registry.ContainsKey(zoneId);

    /// <summary>
    /// True if the zone has been discovered — either marked at runtime or flagged
    /// <c>isDiscoveredByDefault</c>. Unknown zone ids are never discovered.
    /// </summary>
    public bool IsZoneDiscovered(string zoneId)
    {
        if (string.IsNullOrEmpty(zoneId)) return false;
        return _discovered.Contains(zoneId);
    }

    /// <summary>
    /// The resolved <see cref="WorldZoneData"/> for every discovered zone. Undiscovered
    /// zones are omitted (the world map shows only discovered zones; the rest are fog).
    /// </summary>
    public WorldZoneData[] GetDiscoveredZones()
    {
        return _registry.Values
            .Where(z => z != null && _discovered.Contains(z.zoneId))
            .ToArray();
    }

    /// <summary>Every registered zone, discovered or not (for fog-of-war rendering).</summary>
    public WorldZoneData[] GetAllZones() => _registry.Values.ToArray();

    /// <summary>The POIs marked on a zone, or an empty array if none / unknown zone.</summary>
    public POIData[] GetPOIs(string zoneId)
    {
        if (!string.IsNullOrEmpty(zoneId) && _pois.TryGetValue(zoneId, out var list))
            return list.ToArray();
        return Array.Empty<POIData>();
    }

    // ── Mutations ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Mark a zone discovered. Returns false (and fires nothing) if the zone is unknown
    /// or already discovered; otherwise records it and fires <see cref="ZoneDiscovered"/>.
    /// </summary>
    public bool DiscoverZone(string zoneId)
    {
        if (!IsZoneRegistered(zoneId)) return false;
        if (!_discovered.Add(zoneId)) return false; // already discovered

        ZoneDiscovered?.Invoke(zoneId);
        return true;
    }

    /// <summary>
    /// Attach a POI to a known zone. Returns false if the zone is unknown or the POI id is
    /// empty, or if a POI with the same <c>poiId</c> already exists on that zone (idempotent).
    /// On success fires <see cref="POIMarked"/>.
    /// </summary>
    public bool MarkPOI(string zoneId, POIData poi)
    {
        if (!IsZoneRegistered(zoneId)) return false;
        if (string.IsNullOrEmpty(poi.poiId)) return false;

        if (!_pois.TryGetValue(zoneId, out var list))
        {
            list = new List<POIData>();
            _pois[zoneId] = list;
        }

        if (list.Any(p => p.poiId == poi.poiId)) return false; // already marked

        list.Add(poi);
        POIMarked?.Invoke(zoneId, poi);
        return true;
    }

    /// <summary>
    /// Reset all runtime state (discovered + POIs) back to the registry defaults. Does not
    /// fire callbacks. Use on new-game / load before importing saved discovery state.
    /// </summary>
    public void ResetToDefaults()
    {
        _discovered.Clear();
        _pois.Clear();
        foreach (var zone in _registry.Values)
            if (zone != null && zone.isDiscoveredByDefault)
                _discovered.Add(zone.zoneId);
    }

    // ── Persistence ──────────────────────────────────────────────────────────

    /// <summary>
    /// Export the zone ids discovered at runtime beyond the defaults, for the save file.
    /// Default-discovered zones are omitted because re-registering the assets re-seeds them.
    /// </summary>
    public string[] ExportDiscovered()
    {
        return _discovered
            .Where(id => _registry.TryGetValue(id, out var z) && z != null && !z.isDiscoveredByDefault)
            .ToArray();
    }

    /// <summary>
    /// Restore discovery state from a saved id list. Clears runtime discovery back to
    /// defaults first, then marks each saved id discovered. Silent (no callbacks). Unknown
    /// ids are ignored so a save survives a missing asset.
    /// </summary>
    public void ImportDiscovered(string[] discoveredZoneIds)
    {
        ResetToDefaults();
        if (discoveredZoneIds == null) return;

        foreach (var id in discoveredZoneIds)
            if (IsZoneRegistered(id))
                _discovered.Add(id);
    }
}
