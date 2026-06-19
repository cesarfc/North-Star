using NUnit.Framework;
using NorthStar.Audio;

/// <summary>
/// EditMode tests for <see cref="AudioSourcePool{T}"/> — the pure pooling logic shared by
/// AudioManager's SFX pool and VFXManager's particle pool. Verifies free-slot reuse,
/// oldest-slot stealing under saturation, idempotent return, and the active/free counters.
/// </summary>
public class AudioSourcePoolTests
{
    private static AudioSourcePool<int> MakePool(int size) =>
        new AudioSourcePool<int>(size, i => i); // slot value == its index, for easy asserts

    [Test]
    public void Construct_EagerlyAllocatesEverySlot()
    {
        int created = 0;
        var pool = new AudioSourcePool<int>(5, i => { created++; return i; });

        Assert.AreEqual(5, created, "factory runs once per slot at construction");
        Assert.AreEqual(5, pool.Capacity);
        Assert.AreEqual(0, pool.ActiveCount);
        Assert.AreEqual(5, pool.FreeCount);
    }

    [Test]
    public void Rent_TakesFreeSlots_WithoutStealing()
    {
        var pool = MakePool(3);

        pool.Rent(out int i0, out bool s0);
        pool.Rent(out int i1, out bool s1);

        Assert.IsFalse(s0);
        Assert.IsFalse(s1);
        Assert.AreNotEqual(i0, i1, "distinct free slots handed out");
        Assert.AreEqual(2, pool.ActiveCount);
        Assert.AreEqual(1, pool.FreeCount);
    }

    [Test]
    public void Return_FreesSlotForReuse()
    {
        var pool = MakePool(2);
        pool.Rent(out int a);
        pool.Rent(out int b);
        Assert.AreEqual(0, pool.FreeCount);

        Assert.IsTrue(pool.Return(a));
        Assert.AreEqual(1, pool.FreeCount);

        // Next rent should reuse the freed slot (no steal).
        pool.Rent(out int c, out bool stolen);
        Assert.IsFalse(stolen);
        Assert.AreEqual(a, c, "freed slot is reused before any steal");
    }

    [Test]
    public void Return_IsIdempotent()
    {
        var pool = MakePool(2);
        pool.Rent(out int a);

        Assert.IsTrue(pool.Return(a), "first return frees it");
        Assert.IsFalse(pool.Return(a), "returning again is a no-op");
        Assert.AreEqual(0, pool.ActiveCount);
    }

    [Test]
    public void Rent_WhenSaturated_StealsOldestSlot()
    {
        var pool = MakePool(2);
        pool.Rent(out int first, out _);   // oldest
        pool.Rent(out int second, out _);  // newest

        // Pool is full now; the next rent must steal the oldest (first).
        pool.Rent(out int stolenIdx, out bool stolen);

        Assert.IsTrue(stolen, "saturated pool steals instead of growing");
        Assert.AreEqual(first, stolenIdx, "oldest in-use slot is reclaimed");
        Assert.AreEqual(2, pool.Capacity, "pool never grows past its size");
        Assert.AreEqual(2, pool.ActiveCount, "stealing keeps active count at capacity");
    }

    [Test]
    public void Rent_StealOrder_FollowsRecency()
    {
        var pool = MakePool(3);
        pool.Rent(out int a, out _);
        pool.Rent(out int b, out _);
        pool.Rent(out int c, out _);

        // All busy. First steal takes the oldest (a), second takes the next oldest (b).
        pool.Rent(out int steal1, out _);
        pool.Rent(out int steal2, out _);

        Assert.AreEqual(a, steal1);
        Assert.AreEqual(b, steal2);
        Assert.AreNotEqual(c, steal1);
    }

    [Test]
    public void ReturnAll_FreesEverything()
    {
        var pool = MakePool(4);
        pool.Rent(out _);
        pool.Rent(out _);

        pool.ReturnAll();

        Assert.AreEqual(0, pool.ActiveCount);
        Assert.AreEqual(4, pool.FreeCount);
    }

    [Test]
    public void Constructor_RejectsBadSize()
    {
        Assert.Throws<System.ArgumentOutOfRangeException>(() => new AudioSourcePool<int>(0, i => i));
    }

    // ── Rent stamps / deferred return (drives the timed-recycle correctness) ───────

    [Test]
    public void ReturnIfCurrent_FreesSlot_WhenStampMatches()
    {
        var pool = MakePool(2);
        pool.Rent(out int idx, out _);
        long stamp = pool.RentStamp(idx);

        Assert.IsTrue(pool.ReturnIfCurrent(idx, stamp));
        Assert.AreEqual(0, pool.ActiveCount);
    }

    [Test]
    public void ReturnIfCurrent_NoOp_AfterSlotReRented()
    {
        // Single slot: rent, capture stamp, then force a steal that re-rents the slot.
        var pool = MakePool(1);
        pool.Rent(out int idx, out _);
        long staleStamp = pool.RentStamp(idx);

        // Saturated (size 1) → this rent steals the same slot, bumping its stamp.
        pool.Rent(out int stolenIdx, out bool stolen);
        Assert.IsTrue(stolen);
        Assert.AreEqual(idx, stolenIdx);

        // The original (stale) deferred return must NOT free the freshly-rented slot.
        Assert.IsFalse(pool.ReturnIfCurrent(idx, staleStamp));
        Assert.AreEqual(1, pool.ActiveCount, "slot stays active for its new owner");

        // The current owner can still release it.
        Assert.IsTrue(pool.ReturnIfCurrent(idx, pool.RentStamp(idx)));
        Assert.AreEqual(0, pool.ActiveCount);
    }
}
