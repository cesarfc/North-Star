using NUnit.Framework;

/// <summary>
/// EditMode tests for the pure GPU-instancing batch math in <see cref="ProceduralFoliage"/>.
/// Graphics.DrawMeshInstanced is capped at 1023 matrices per call, so the scatter must be
/// sliced into ceil(count / 1023) batches. This verifies the cap constant and the slicing
/// arithmetic without needing a scene, mesh, or GPU.
/// </summary>
public class ProceduralFoliageTests
{
    [Test]
    public void MaxInstancesPerBatch_MatchesUnityCap()
    {
        Assert.AreEqual(1023, ProceduralFoliage.MAX_INSTANCES_PER_BATCH);
    }

    [Test]
    public void BatchesNeeded_ZeroOrNegative_IsZero()
    {
        Assert.AreEqual(0, ProceduralFoliage.BatchesNeeded(0));
        Assert.AreEqual(0, ProceduralFoliage.BatchesNeeded(-100));
    }

    [Test]
    public void BatchesNeeded_UnderCap_IsOne()
    {
        Assert.AreEqual(1, ProceduralFoliage.BatchesNeeded(1));
        Assert.AreEqual(1, ProceduralFoliage.BatchesNeeded(1023));
    }

    [Test]
    public void BatchesNeeded_CrossingCap_RoundsUp()
    {
        Assert.AreEqual(2, ProceduralFoliage.BatchesNeeded(1024));
        Assert.AreEqual(2, ProceduralFoliage.BatchesNeeded(2046));
        Assert.AreEqual(3, ProceduralFoliage.BatchesNeeded(2047));
    }

    [Test]
    public void BatchesNeeded_LargeCount_IsCeilingDivision()
    {
        // 5000 blades / 1023 = 4.88… → 5 draw calls.
        Assert.AreEqual(5, ProceduralFoliage.BatchesNeeded(5000));
    }
}
