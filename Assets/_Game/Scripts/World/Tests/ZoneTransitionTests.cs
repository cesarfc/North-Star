using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// EditMode unit tests for <see cref="ZoneTransition.FindSpawnPoint"/> — the
/// <c>SpawnPoint_[id]</c> lookup convention the zone gate uses to place the player after an
/// additive load. Objects are created in the active editor scene and destroyed in TearDown.
/// </summary>
public class ZoneTransitionTests
{
    private GameObject _rootA;
    private GameObject _rootB;

    [TearDown]
    public void TearDown()
    {
        if (_rootA != null) Object.DestroyImmediate(_rootA);
        if (_rootB != null) Object.DestroyImmediate(_rootB);
    }

    [Test]
    public void FindSpawnPoint_FindsRootObjectByConvention()
    {
        _rootA = new GameObject("SpawnPoint_spawn-outpost");

        Transform found = ZoneTransition.FindSpawnPoint(SceneManager.GetActiveScene(), "spawn-outpost");

        Assert.IsNotNull(found);
        Assert.AreSame(_rootA.transform, found);
    }

    [Test]
    public void FindSpawnPoint_FindsNestedChild()
    {
        _rootA = new GameObject("ZoneContent");
        var mid = new GameObject("Spawns");
        mid.transform.SetParent(_rootA.transform);
        var spawn = new GameObject("SpawnPoint_spawn-cave");
        spawn.transform.SetParent(mid.transform);

        Transform found = ZoneTransition.FindSpawnPoint(SceneManager.GetActiveScene(), "spawn-cave");

        Assert.IsNotNull(found);
        Assert.AreSame(spawn.transform, found);
    }

    [Test]
    public void FindSpawnPoint_ReturnsNullWhenIdMissing()
    {
        _rootA = new GameObject("SpawnPoint_spawn-outpost");

        Assert.IsNull(ZoneTransition.FindSpawnPoint(SceneManager.GetActiveScene(), "spawn-nowhere"));
    }

    [Test]
    public void FindSpawnPoint_ReturnsNullForEmptyId()
    {
        _rootB = new GameObject("SpawnPoint_");

        Assert.IsNull(ZoneTransition.FindSpawnPoint(SceneManager.GetActiveScene(), null));
        Assert.IsNull(ZoneTransition.FindSpawnPoint(SceneManager.GetActiveScene(), ""));
    }
}
