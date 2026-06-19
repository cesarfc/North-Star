using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// EditMode unit tests for the discovery/query logic that backs <see cref="WorldMapManager"/>.
/// The manager delegates every public method (DiscoverZone / IsZoneDiscovered / MarkPOI /
/// GetDiscoveredZones) to a pure <see cref="WorldMapState"/>, which is MonoBehaviour-free so
/// it runs in EditMode without play mode. These cover discovery (incl. defaults and
/// idempotency), querying discovered zones, POI marking, and the save/load round-trip.
/// </summary>
public class WorldMapManagerTests
{
    // Track every WorldZoneData built so TearDown can dispose them precisely.
    private readonly List<WorldZoneData> _created = new List<WorldZoneData>();

    // ── Test data builders ────────────────────────────────────────────────────

    private WorldZoneData MakeZone(
        string zoneId,
        bool discoveredByDefault = false,
        ZoneType type = ZoneType.Wilderness,
        params string[] connections)
    {
        var zone = ScriptableObject.CreateInstance<WorldZoneData>();
        zone.zoneId = zoneId;
        zone.displayName = zoneId;
        zone.sceneId = "SCN_" + zoneId;
        zone.zoneType = type;
        zone.isDiscoveredByDefault = discoveredByDefault;
        zone.connectedZoneIds = connections ?? new string[0];
        _created.Add(zone);
        return zone;
    }

    private static POIData MakePOI(string id) =>
        new POIData { poiId = id, label = id, mapPosition = Vector2.zero };

    [TearDown]
    public void TearDown()
    {
        foreach (var so in _created)
            if (so != null) Object.DestroyImmediate(so);
        _created.Clear();
    }

    // ── DiscoverZone / IsZoneDiscovered ────────────────────────────────────────

    [Test]
    public void DiscoverZone_MarksDiscoveredAndFiresCallback()
    {
        var state = new WorldMapState(new[] { MakeZone("hub") });

        string discovered = null;
        state.ZoneDiscovered += id => discovered = id;

        bool result = state.DiscoverZone("hub");

        Assert.IsTrue(result);
        Assert.AreEqual("hub", discovered);
        Assert.IsTrue(state.IsZoneDiscovered("hub"));
    }

    [Test]
    public void DiscoverZone_UnknownId_ReturnsFalseAndDoesNotFire()
    {
        var state = new WorldMapState(new[] { MakeZone("hub") });
        int fires = 0;
        state.ZoneDiscovered += _ => fires++;

        Assert.IsFalse(state.DiscoverZone("does-not-exist"));
        Assert.AreEqual(0, fires);
        Assert.IsFalse(state.IsZoneDiscovered("does-not-exist"));
    }

    [Test]
    public void DiscoverZone_Twice_IsIdempotent()
    {
        var state = new WorldMapState(new[] { MakeZone("hub") });
        int fires = 0;
        state.ZoneDiscovered += _ => fires++;

        Assert.IsTrue(state.DiscoverZone("hub"));
        Assert.IsFalse(state.DiscoverZone("hub")); // already discovered
        Assert.AreEqual(1, fires);
    }

    [Test]
    public void IsZoneDiscovered_RespectsDiscoveredByDefault()
    {
        var state = new WorldMapState(new[]
        {
            MakeZone("hub", discoveredByDefault: true),
            MakeZone("forest", discoveredByDefault: false)
        });

        Assert.IsTrue(state.IsZoneDiscovered("hub"), "default-discovered zone is discovered at start");
        Assert.IsFalse(state.IsZoneDiscovered("forest"));
    }

    [Test]
    public void DiscoverZone_OnDefaultDiscovered_ReturnsFalse_NoRefire()
    {
        var state = new WorldMapState(new[] { MakeZone("hub", discoveredByDefault: true) });
        int fires = 0;
        state.ZoneDiscovered += _ => fires++;

        Assert.IsFalse(state.DiscoverZone("hub"), "already discovered via default");
        Assert.AreEqual(0, fires);
    }

    [Test]
    public void IsZoneDiscovered_NullOrEmpty_ReturnsFalse()
    {
        var state = new WorldMapState(new[] { MakeZone("hub") });
        Assert.IsFalse(state.IsZoneDiscovered(null));
        Assert.IsFalse(state.IsZoneDiscovered(string.Empty));
    }

    // ── GetDiscoveredZones / GetAllZones ───────────────────────────────────────

    [Test]
    public void GetDiscoveredZones_ReturnsOnlyDiscovered()
    {
        var state = new WorldMapState(new[]
        {
            MakeZone("hub", discoveredByDefault: true),
            MakeZone("forest"),
            MakeZone("ruins")
        });

        state.DiscoverZone("forest");

        var ids = state.GetDiscoveredZones().Select(z => z.zoneId).ToArray();
        CollectionAssert.AreEquivalent(new[] { "hub", "forest" }, ids);
        CollectionAssert.DoesNotContain(ids, "ruins");
    }

    [Test]
    public void GetAllZones_ReturnsEveryRegisteredZone_DiscoveredOrNot()
    {
        var state = new WorldMapState(new[]
        {
            MakeZone("hub", discoveredByDefault: true),
            MakeZone("forest"),
            MakeZone("ruins")
        });

        var ids = state.GetAllZones().Select(z => z.zoneId).ToArray();
        CollectionAssert.AreEquivalent(new[] { "hub", "forest", "ruins" }, ids);
    }

    [Test]
    public void GetDiscoveredZones_EmptyWhenNothingDiscovered()
    {
        var state = new WorldMapState(new[] { MakeZone("forest"), MakeZone("ruins") });
        Assert.AreEqual(0, state.GetDiscoveredZones().Length);
    }

    // ── Registration edge cases ────────────────────────────────────────────────

    [Test]
    public void Constructor_IgnoresNullAndEmptyIdZones()
    {
        var good = MakeZone("hub");
        var blank = MakeZone(string.Empty); // empty id → ignored
        var state = new WorldMapState(new WorldZoneData[] { good, blank, null });

        Assert.IsTrue(state.IsZoneRegistered("hub"));
        Assert.AreEqual(1, state.GetAllZones().Length);
    }

    [Test]
    public void Register_ReplacesExistingZoneById()
    {
        var state = new WorldMapState(new[] { MakeZone("hub") });
        var replacement = MakeZone("hub", discoveredByDefault: true);

        state.Register(replacement);

        Assert.AreEqual(1, state.GetAllZones().Length);
        Assert.IsTrue(state.IsZoneDiscovered("hub"), "replacement carried the default flag");
    }

    // ── MarkPOI / GetPOIs ──────────────────────────────────────────────────────

    [Test]
    public void MarkPOI_OnKnownZone_AddsAndFires()
    {
        var state = new WorldMapState(new[] { MakeZone("forest") });

        var fired = new List<(string, string)>();
        state.POIMarked += (z, p) => fired.Add((z, p.poiId));

        bool result = state.MarkPOI("forest", MakePOI("shrine"));

        Assert.IsTrue(result);
        Assert.AreEqual(1, fired.Count);
        Assert.AreEqual(("forest", "shrine"), fired[0]);
        Assert.AreEqual(1, state.GetPOIs("forest").Length);
        Assert.AreEqual("shrine", state.GetPOIs("forest")[0].poiId);
    }

    [Test]
    public void MarkPOI_OnUnknownZone_ReturnsFalse()
    {
        var state = new WorldMapState(new[] { MakeZone("forest") });
        Assert.IsFalse(state.MarkPOI("nowhere", MakePOI("shrine")));
    }

    [Test]
    public void MarkPOI_DuplicatePoiId_IsIdempotent()
    {
        var state = new WorldMapState(new[] { MakeZone("forest") });
        int fires = 0;
        state.POIMarked += (_, __) => fires++;

        Assert.IsTrue(state.MarkPOI("forest", MakePOI("shrine")));
        Assert.IsFalse(state.MarkPOI("forest", MakePOI("shrine"))); // same id
        Assert.AreEqual(1, fires);
        Assert.AreEqual(1, state.GetPOIs("forest").Length);
    }

    [Test]
    public void MarkPOI_EmptyPoiId_ReturnsFalse()
    {
        var state = new WorldMapState(new[] { MakeZone("forest") });
        Assert.IsFalse(state.MarkPOI("forest", MakePOI(string.Empty)));
    }

    [Test]
    public void GetPOIs_UnknownZone_ReturnsEmpty()
    {
        var state = new WorldMapState(new[] { MakeZone("forest") });
        Assert.AreEqual(0, state.GetPOIs("nowhere").Length);
    }

    // ── Reset / persistence round-trip ──────────────────────────────────────────

    [Test]
    public void ResetToDefaults_RestoresDefaultDiscoveryOnly()
    {
        var state = new WorldMapState(new[]
        {
            MakeZone("hub", discoveredByDefault: true),
            MakeZone("forest")
        });

        state.DiscoverZone("forest");
        state.MarkPOI("forest", MakePOI("shrine"));

        state.ResetToDefaults();

        Assert.IsTrue(state.IsZoneDiscovered("hub"));
        Assert.IsFalse(state.IsZoneDiscovered("forest"));
        Assert.AreEqual(0, state.GetPOIs("forest").Length);
    }

    [Test]
    public void ExportDiscovered_EmitsOnlyNonDefaultDiscoveredZones()
    {
        var state = new WorldMapState(new[]
        {
            MakeZone("hub", discoveredByDefault: true),
            MakeZone("forest"),
            MakeZone("ruins")
        });

        state.DiscoverZone("forest"); // runtime discovery
        // hub is default; ruins untouched

        var exported = state.ExportDiscovered();
        CollectionAssert.AreEquivalent(new[] { "forest" }, exported);
    }

    [Test]
    public void ImportDiscovered_RestoresRuntimeDiscovery()
    {
        var state = new WorldMapState(new[]
        {
            MakeZone("hub", discoveredByDefault: true),
            MakeZone("forest"),
            MakeZone("ruins")
        });

        state.ImportDiscovered(new[] { "ruins" });

        Assert.IsTrue(state.IsZoneDiscovered("hub"), "default still discovered");
        Assert.IsTrue(state.IsZoneDiscovered("ruins"), "imported runtime discovery");
        Assert.IsFalse(state.IsZoneDiscovered("forest"));
    }

    [Test]
    public void SaveLoadRoundTrip_PreservesRuntimeDiscovery()
    {
        var hub = MakeZone("hub", discoveredByDefault: true);
        var forest = MakeZone("forest");
        var ruins = MakeZone("ruins");

        var session1 = new WorldMapState(new[] { hub, forest, ruins });
        session1.DiscoverZone("forest");
        session1.DiscoverZone("ruins");
        string[] saved = session1.ExportDiscovered();

        var session2 = new WorldMapState(new[] { hub, forest, ruins });
        session2.ImportDiscovered(saved);

        Assert.IsTrue(session2.IsZoneDiscovered("forest"));
        Assert.IsTrue(session2.IsZoneDiscovered("ruins"));
        Assert.IsTrue(session2.IsZoneDiscovered("hub"));
        CollectionAssert.AreEquivalent(
            new[] { "hub", "forest", "ruins" },
            session2.GetDiscoveredZones().Select(z => z.zoneId));
    }

    [Test]
    public void ImportDiscovered_Null_ResetsToDefaults_NoThrow()
    {
        var state = new WorldMapState(new[]
        {
            MakeZone("hub", discoveredByDefault: true),
            MakeZone("forest")
        });
        state.DiscoverZone("forest");

        Assert.DoesNotThrow(() => state.ImportDiscovered(null));
        Assert.IsTrue(state.IsZoneDiscovered("hub"));
        Assert.IsFalse(state.IsZoneDiscovered("forest"));
    }

    [Test]
    public void ImportDiscovered_UnknownIds_AreIgnored()
    {
        var state = new WorldMapState(new[] { MakeZone("forest") });
        state.ImportDiscovered(new[] { "ghost-zone", "forest" });

        Assert.IsTrue(state.IsZoneDiscovered("forest"));
        Assert.IsFalse(state.IsZoneDiscovered("ghost-zone"));
        Assert.AreEqual(1, state.GetDiscoveredZones().Length);
    }
}
