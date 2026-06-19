using System;
using System.Collections.Generic;

namespace NorthStar.Audio
{
    /// <summary>
    /// Pure, MonoBehaviour-free ring/free-list pool of generic handles. Backs
    /// <see cref="AudioManager"/>'s SFX <c>AudioSource</c> pool and
    /// <see cref="VFXManager"/>'s particle pool so the reuse policy is unit-testable
    /// in EditMode without play mode.
    ///
    /// The pool owns a fixed array of slots created by the supplied factory. Callers
    /// <see cref="Rent"/> a slot to play something and <see cref="Return"/> it when done.
    /// When every slot is busy, <see cref="Rent"/> steals the least-recently-rented slot
    /// (the oldest in use) so a burst of requests never allocates beyond the pool size —
    /// "never Instantiate/Destroy per-frame" (CONVENTIONS performance rules).
    /// </summary>
    /// <typeparam name="T">Pooled resource type (e.g. <c>AudioSource</c>, particle wrapper).</typeparam>
    public sealed class AudioSourcePool<T>
    {
        private readonly T[] _slots;
        private readonly bool[] _inUse;
        // Monotonically increasing rent stamp per slot; used to find the oldest in-use slot.
        private readonly long[] _rentStamp;
        private long _clock;

        /// <summary>Total number of slots the pool manages (fixed at construction).</summary>
        public int Capacity => _slots.Length;

        /// <summary>How many slots are currently rented out.</summary>
        public int ActiveCount { get; private set; }

        /// <summary>How many slots are free to rent without stealing.</summary>
        public int FreeCount => Capacity - ActiveCount;

        /// <summary>
        /// Build a pool of <paramref name="size"/> slots. The <paramref name="factory"/>
        /// is invoked once per slot at construction (eager allocation — no per-frame churn).
        /// </summary>
        /// <param name="size">Number of pooled slots (must be &gt;= 1).</param>
        /// <param name="factory">Creates a slot given its index.</param>
        public AudioSourcePool(int size, Func<int, T> factory)
        {
            if (size < 1) throw new ArgumentOutOfRangeException(nameof(size), "Pool size must be >= 1.");
            if (factory == null) throw new ArgumentNullException(nameof(factory));

            _slots = new T[size];
            _inUse = new bool[size];
            _rentStamp = new long[size];
            for (int i = 0; i < size; i++)
                _slots[i] = factory(i);
        }

        /// <summary>
        /// Rent a slot. Returns a free slot if one exists; otherwise steals the
        /// least-recently-rented (oldest) active slot so the call always succeeds
        /// without growing the pool. <paramref name="stolen"/> is true when an active
        /// slot was reclaimed, letting the caller stop whatever was playing on it first.
        /// </summary>
        /// <param name="index">Index of the rented slot.</param>
        /// <param name="stolen">True if an in-use slot was reclaimed to satisfy the rent.</param>
        /// <returns>The rented resource.</returns>
        public T Rent(out int index, out bool stolen)
        {
            // Prefer a free slot.
            for (int i = 0; i < _slots.Length; i++)
            {
                if (!_inUse[i])
                {
                    index = i;
                    stolen = false;
                    Mark(i);
                    return _slots[i];
                }
            }

            // All busy: steal the oldest by rent stamp.
            int oldest = 0;
            long best = long.MaxValue;
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_rentStamp[i] < best)
                {
                    best = _rentStamp[i];
                    oldest = i;
                }
            }

            index = oldest;
            stolen = true;
            // Stealing reuses an already-active slot, so ActiveCount is unchanged.
            _rentStamp[oldest] = ++_clock;
            return _slots[oldest];
        }

        /// <summary>Convenience overload that ignores the steal flag.</summary>
        public T Rent(out int index) => Rent(out index, out _);

        /// <summary>
        /// Return a previously rented slot to the free list. Idempotent: returning a
        /// slot that is already free is a no-op. Returns true if the slot transitioned
        /// from in-use to free.
        /// </summary>
        public bool Return(int index)
        {
            if (index < 0 || index >= _slots.Length) return false;
            if (!_inUse[index]) return false;
            _inUse[index] = false;
            ActiveCount--;
            return true;
        }

        /// <summary>
        /// Unique stamp assigned to a slot's most recent <see cref="Rent"/>. Capture this
        /// when you rent and pass it to <see cref="ReturnIfCurrent"/> so a deferred return
        /// (e.g. a timed coroutine) only retires the slot if it has not been re-rented/stolen
        /// in the meantime.
        /// </summary>
        public long RentStamp(int index) =>
            index >= 0 && index < _slots.Length ? _rentStamp[index] : -1;

        /// <summary>
        /// Return a slot only if it is still on the same rent identified by
        /// <paramref name="stamp"/> (i.e. it was not stolen/re-rented since). Returns true
        /// only if the slot was actually freed by this call.
        /// </summary>
        public bool ReturnIfCurrent(int index, long stamp)
        {
            if (index < 0 || index >= _slots.Length) return false;
            if (!_inUse[index] || _rentStamp[index] != stamp) return false;
            return Return(index);
        }

        /// <summary>True if the slot at <paramref name="index"/> is currently rented.</summary>
        public bool IsActive(int index) =>
            index >= 0 && index < _slots.Length && _inUse[index];

        /// <summary>Direct read access to a slot's resource (active or not).</summary>
        public T Get(int index) => _slots[index];

        /// <summary>Free every slot without destroying the underlying resources.</summary>
        public void ReturnAll()
        {
            for (int i = 0; i < _inUse.Length; i++) _inUse[i] = false;
            ActiveCount = 0;
        }

        /// <summary>Enumerate the underlying resources for teardown / iteration.</summary>
        public IReadOnlyList<T> Slots => _slots;

        private void Mark(int i)
        {
            _inUse[i] = true;
            _rentStamp[i] = ++_clock;
            ActiveCount++;
        }
    }
}
