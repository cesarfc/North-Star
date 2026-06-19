using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NorthStar.Audio
{
    /// <summary>
    /// Pooled particle/VFX spawner. <see cref="Play(ParticleSystem,Vector3)"/> takes a
    /// prefab and a world position, pulls a pooled instance for that prefab, positions it,
    /// and plays it — never <c>Instantiate</c>/<c>Destroy</c> per spawn (CONVENTIONS:
    /// "pool particles, never Instantiate/Destroy per-frame").
    ///
    /// <para>One <see cref="AudioSourcePool{T}"/> is kept per prefab so each effect reuses
    /// its own warmed-up instances; when all are busy the pool steals the oldest. Finished
    /// instances are returned automatically after the effect's duration.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public class VFXManager : MonoBehaviour
    {
        [Tooltip("Pooled instances created per distinct VFX prefab.")]
        [SerializeField] private int _poolSizePerPrefab = 30;

        // Per-prefab pool of ParticleSystem instances.
        private readonly Dictionary<ParticleSystem, AudioSourcePool<ParticleSystem>> _pools =
            new Dictionary<ParticleSystem, AudioSourcePool<ParticleSystem>>();

        /// <summary>
        /// Play a VFX prefab at a world position from its pool, returning the live instance.
        /// Builds the prefab's pool on first use. Returns null if the prefab is null.
        /// </summary>
        public ParticleSystem Play(ParticleSystem prefab, Vector3 position)
        {
            if (prefab == null) return null;

            var pool = GetOrCreatePool(prefab);
            var instance = pool.Rent(out int index, out bool stolen);
            if (stolen) instance.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            instance.transform.position = position;
            instance.gameObject.SetActive(true);
            instance.Clear(true);
            instance.Play(true);

            float life = EstimateLifetime(instance);
            StartCoroutine(CoReturnWhenDone(pool, index, pool.RentStamp(index), instance, life));
            return instance;
        }

        private AudioSourcePool<ParticleSystem> GetOrCreatePool(ParticleSystem prefab)
        {
            if (_pools.TryGetValue(prefab, out var pool)) return pool;

            int size = Mathf.Max(1, _poolSizePerPrefab);
            pool = new AudioSourcePool<ParticleSystem>(size, i =>
            {
                var inst = Instantiate(prefab, transform);
                inst.name = $"{prefab.name}_VFX_{i}";
                var main = inst.main;
                main.playOnAwake = false;
                main.stopAction = ParticleSystemStopAction.None;
                inst.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                inst.gameObject.SetActive(false);
                return inst;
            });
            _pools[prefab] = pool;
            return pool;
        }

        private IEnumerator CoReturnWhenDone(
            AudioSourcePool<ParticleSystem> pool, int index, long stamp, ParticleSystem instance, float life)
        {
            // Unscaled so VFX still recycle while the game is paused.
            yield return new WaitForSecondsRealtime(life);

            // Only retire if this slot is still on the same rent (not stolen/re-rented since).
            if (instance != null && pool.ReturnIfCurrent(index, stamp))
            {
                instance.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                instance.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Estimate how long an effect runs: its main duration plus the longest start
        /// lifetime (constant or curve max), so we recycle only after particles die.
        /// </summary>
        private static float EstimateLifetime(ParticleSystem ps)
        {
            var main = ps.main;
            float lifetime = main.startLifetime.mode == ParticleSystemCurveMode.Constant
                ? main.startLifetime.constant
                : main.startLifetime.constantMax;
            return main.duration + Mathf.Max(0f, lifetime);
        }
    }
}
