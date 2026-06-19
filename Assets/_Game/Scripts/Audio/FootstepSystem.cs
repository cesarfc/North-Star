using System;
using UnityEngine;

namespace NorthStar.Audio
{
    /// <summary>
    /// Detects the ground surface under a mover with a downward <c>Physics.Raycast</c> and
    /// plays the matching footstep SFX through <see cref="AudioManager"/>. Surface →
    /// clipId mapping is configured per surface; the raw classification (physic-material
    /// name / tag → <see cref="SurfaceType"/>) is delegated to the pure
    /// <see cref="SurfaceClassifier"/> so it stays EditMode-testable.
    ///
    /// <para>Footsteps are driven by an animation event or movement system calling
    /// <see cref="Step"/> — there is no per-frame <c>Update</c> footstep polling, per
    /// CONVENTIONS ("never use Update() for non-movement logic").</para>
    /// </summary>
    [DisallowMultipleComponent]
    public class FootstepSystem : MonoBehaviour
    {
        [Serializable]
        public struct SurfaceSfx
        {
            public SurfaceType surface;
            [Tooltip("clipId resolved by AudioManager, e.g. sfx-footstep-grass.")]
            public string clipId;
        }

        [Header("References")]
        [Tooltip("AudioManager that plays the resolved footstep clipId.")]
        [SerializeField] private AudioManager _audioManager;
        [Tooltip("Origin the raycast is cast down from; defaults to this transform.")]
        [SerializeField] private Transform _rayOrigin;

        [Header("Raycast")]
        [Tooltip("Layers considered walkable ground.")]
        [SerializeField] private LayerMask _groundMask = ~0;
        [Tooltip("How far down to probe for ground from the origin.")]
        [SerializeField] private float _rayDistance = 1.5f;
        [Tooltip("Small upward offset so the ray starts above the feet.")]
        [SerializeField] private float _rayUpOffset = 0.1f;

        [Header("Surface SFX")]
        [SerializeField] private SurfaceSfx[] _surfaceClips = Array.Empty<SurfaceSfx>();

        private readonly SurfaceClassifier _classifier = SurfaceClassifier.CreateDefault();
        private SurfaceType _lastSurface = SurfaceType.Unknown;

        /// <summary>The surface detected on the most recent <see cref="Step"/>.</summary>
        public SurfaceType LastSurface => _lastSurface;

        private void Awake()
        {
            if (_rayOrigin == null) _rayOrigin = transform;
        }

        /// <summary>
        /// Detect the current ground surface and play its footstep SFX. Call from an
        /// animation event or the movement system on each foot plant. No-op if no ground
        /// is hit or the surface has no configured clip.
        /// </summary>
        public void Step()
        {
            var surface = DetectSurface();
            _lastSurface = surface;
            if (surface == SurfaceType.Unknown) return;

            string clipId = ResolveClipId(surface);
            if (string.IsNullOrEmpty(clipId) || _audioManager == null) return;

            _audioManager.PlaySFX(clipId, _rayOrigin.position);
        }

        /// <summary>
        /// Cast down and classify the surface hit. Returns <see cref="SurfaceType.Unknown"/>
        /// when nothing is hit. The hit's <c>PhysicMaterial</c> name is preferred, then the
        /// collider's tag, then the GameObject name — first match wins.
        /// </summary>
        public SurfaceType DetectSurface()
        {
            Vector3 origin = _rayOrigin.position + Vector3.up * _rayUpOffset;
            if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit,
                    _rayDistance + _rayUpOffset, _groundMask, QueryTriggerInteraction.Ignore))
            {
                return SurfaceType.Unknown;
            }

            string materialName = null;
            var col = hit.collider;
            if (col != null && col.sharedMaterial != null) materialName = col.sharedMaterial.name;
            string tag = col != null ? SafeTag(col) : null;
            string objName = col != null ? col.gameObject.name : null;

            return _classifier.ClassifyAny(materialName, tag, objName);
        }

        /// <summary>Look up the configured clipId for a surface, or null if unmapped.</summary>
        public string ResolveClipId(SurfaceType surface)
        {
            foreach (var entry in _surfaceClips)
                if (entry.surface == surface) return entry.clipId;
            return null;
        }

        private static string SafeTag(Collider col)
        {
            // Untagged colliders carry the "Untagged" tag; treat it as no signal.
            string t = col.tag;
            return t == "Untagged" ? null : t;
        }
    }
}
