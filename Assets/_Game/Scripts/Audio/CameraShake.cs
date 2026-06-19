using Unity.Cinemachine;
using UnityEngine;

namespace NorthStar.Audio
{
    /// <summary>
    /// Camera shake driven by Cinemachine's impulse system (<see cref="CinemachineImpulseSource"/>),
    /// not manual camera-transform jitter. A <c>CinemachineImpulseListener</c> on the live
    /// <c>CinemachineCamera</c> receives the impulse and shakes the view.
    ///
    /// <para>Designers fire calibrated presets via <see cref="Shake(ShakePreset)"/>; gameplay
    /// can fire arbitrary shakes via <see cref="Shake(float,float)"/>. The intensity scales the
    /// impulse velocity; duration is applied to the impulse definition's time envelope.</para>
    /// </summary>
    [RequireComponent(typeof(CinemachineImpulseSource))]
    [DisallowMultipleComponent]
    public class CameraShake : MonoBehaviour
    {
        [System.Serializable]
        private struct PresetConfig
        {
            public ShakePreset preset;
            [Tooltip("Impulse velocity magnitude (how hard the kick is).")]
            public float intensity;
            [Tooltip("How long the shake envelope lasts, seconds.")]
            public float duration;
        }

        [Header("Impulse")]
        [SerializeField] private CinemachineImpulseSource _impulseSource;
        [Tooltip("Direction the impulse kicks the camera before the source's signal shapes it.")]
        [SerializeField] private Vector3 _impulseDirection = new Vector3(0f, -1f, 0f);

        [Header("Presets")]
        [SerializeField] private PresetConfig[] _presets =
        {
            new PresetConfig { preset = ShakePreset.Light,   intensity = 0.3f, duration = 0.2f },
            new PresetConfig { preset = ShakePreset.Medium,  intensity = 0.6f, duration = 0.3f },
            new PresetConfig { preset = ShakePreset.Heavy,   intensity = 1.0f, duration = 0.4f },
            new PresetConfig { preset = ShakePreset.BossHit, intensity = 1.6f, duration = 0.6f },
        };

        private void Awake()
        {
            if (_impulseSource == null)
                _impulseSource = GetComponent<CinemachineImpulseSource>();
        }

        /// <summary>
        /// Fire a camera shake with explicit intensity and duration. Intensity scales the
        /// impulse velocity; duration sets the impulse signal's time envelope.
        /// </summary>
        public void Shake(float intensity, float duration)
        {
            if (_impulseSource == null) return;

            // Apply the requested duration to the impulse definition's envelope.
            var def = _impulseSource.ImpulseDefinition;
            if (def != null) def.ImpulseDuration = Mathf.Max(0.01f, duration);

            Vector3 velocity = _impulseDirection.normalized * Mathf.Max(0f, intensity);
            _impulseSource.GenerateImpulseWithVelocity(velocity);
        }

        /// <summary>Fire a designer-calibrated shake preset.</summary>
        public void Shake(ShakePreset preset)
        {
            var cfg = ResolvePreset(preset);
            Shake(cfg.intensity, cfg.duration);
        }

        private PresetConfig ResolvePreset(ShakePreset preset)
        {
            foreach (var p in _presets)
                if (p.preset == preset) return p;
            // Fallback if a preset row is missing in the inspector.
            return new PresetConfig { preset = preset, intensity = 0.6f, duration = 0.3f };
        }
    }
}
