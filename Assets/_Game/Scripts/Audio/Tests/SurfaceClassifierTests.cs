using NUnit.Framework;
using UnityEngine;
using NorthStar.Audio;

/// <summary>
/// EditMode tests for <see cref="SurfaceClassifier"/> and <see cref="ZonePlaylistTable"/> —
/// the pure logic behind FootstepSystem surface detection and AudioManager zone music.
/// </summary>
public class SurfaceClassifierTests
{
    [Test]
    public void Default_ClassifiesKnownSurfaces_CaseInsensitiveSubstring()
    {
        var c = SurfaceClassifier.CreateDefault();
        Assert.AreEqual(SurfaceType.Grass, c.Classify("Grass (Instance)"));
        Assert.AreEqual(SurfaceType.Stone, c.Classify("MAT_STONE_floor"));
        Assert.AreEqual(SurfaceType.Stone, c.Classify("rocky-cliff"));
        Assert.AreEqual(SurfaceType.Wood,  c.Classify("OakWoodPlank"));
        Assert.AreEqual(SurfaceType.Water, c.Classify("DeepWater"));
    }

    [Test]
    public void Classify_Unmatched_ReturnsUnknown()
    {
        var c = SurfaceClassifier.CreateDefault();
        Assert.AreEqual(SurfaceType.Unknown, c.Classify("Concrete"));
    }

    [Test]
    public void Classify_NullOrEmpty_ReturnsUnknown()
    {
        var c = SurfaceClassifier.CreateDefault();
        Assert.AreEqual(SurfaceType.Unknown, c.Classify(null));
        Assert.AreEqual(SurfaceType.Unknown, c.Classify(""));
    }

    [Test]
    public void ClassifyAny_ReturnsFirstNonUnknownInOrder()
    {
        var c = SurfaceClassifier.CreateDefault();
        // material name unmatched, tag matches wood.
        Assert.AreEqual(SurfaceType.Wood, c.ClassifyAny("Concrete", "WoodTag", "grass"));
    }

    [Test]
    public void ClassifyAny_AllUnmatched_ReturnsUnknown()
    {
        var c = SurfaceClassifier.CreateDefault();
        Assert.AreEqual(SurfaceType.Unknown, c.ClassifyAny("Concrete", "Metal", null));
    }

    [Test]
    public void AddRule_FirstMatchWins_InInsertionOrder()
    {
        var c = new SurfaceClassifier();
        c.AddRule("wet", SurfaceType.Water);
        c.AddRule("stone", SurfaceType.Stone);
        // "wetstone" contains both keywords; "wet" was added first.
        Assert.AreEqual(SurfaceType.Water, c.Classify("WetStone"));
    }

    [Test]
    public void AddRule_IgnoresBlankKeyword()
    {
        var c = new SurfaceClassifier();
        c.AddRule("  ", SurfaceType.Grass);
        c.AddRule(null, SurfaceType.Grass);
        Assert.AreEqual(0, c.RuleCount);
    }

    // ── ZonePlaylistTable ──────────────────────────────────────────────────────

    [Test]
    public void ZoneTable_ResolvesByZoneAndPlaylistId()
    {
        var table = new ZonePlaylistTable();
        var pl = ScriptableObject.CreateInstance<MusicPlaylist>();
        pl.playlistId = "forest";

        table.Add("forest-zone-01", pl);

        Assert.AreSame(pl, table.ResolveByZone("forest-zone-01"));
        Assert.AreSame(pl, table.ResolveByPlaylistId("forest"));
        Assert.IsTrue(table.HasZone("forest-zone-01"));
        Assert.AreEqual(1, table.ZoneCount);

        Object.DestroyImmediate(pl);
    }

    [Test]
    public void ZoneTable_UnknownLookups_ReturnNull()
    {
        var table = new ZonePlaylistTable();
        Assert.IsNull(table.ResolveByZone("nope"));
        Assert.IsNull(table.ResolveByZone(null));
        Assert.IsNull(table.ResolveByPlaylistId("nope"));
        Assert.IsFalse(table.HasZone("nope"));
    }

    [Test]
    public void ZoneTable_IgnoresNullPlaylist_LaterEntryReplaces()
    {
        var table = new ZonePlaylistTable();
        table.Add("town", null); // ignored
        Assert.AreEqual(0, table.ZoneCount);

        var a = ScriptableObject.CreateInstance<MusicPlaylist>();
        a.playlistId = "town-a";
        var b = ScriptableObject.CreateInstance<MusicPlaylist>();
        b.playlistId = "town-b";

        table.Add("town", a);
        table.Add("town", b); // replaces a for the zone key

        Assert.AreSame(b, table.ResolveByZone("town"));
        Assert.AreEqual(1, table.ZoneCount);

        Object.DestroyImmediate(a);
        Object.DestroyImmediate(b);
    }
}
