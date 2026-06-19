using System;

namespace NorthStar.Audio
{
    /// <summary>
    /// Pure, MonoBehaviour-free volume math for <see cref="AudioManager"/>. Holds the
    /// three logical channels (master, music, SFX) on a 0–1 linear scale, clamps input,
    /// and computes the effective linear gain for each channel (master * channel).
    ///
    /// Kept separate from the manager so the clamping/composition rules are unit-testable
    /// in EditMode. Also exposes a linear→decibel conversion suitable for an
    /// <c>AudioMixer</c> exposed parameter, should one be wired later.
    /// </summary>
    public sealed class VolumeMixer
    {
        /// <summary>Minimum dB floor used when converting a linear 0 to decibels.</summary>
        public const float MIN_DECIBELS = -80f;

        private float _master = 1f;
        private float _music = 1f;
        private float _sfx = 1f;

        /// <summary>Master channel, 0–1 linear.</summary>
        public float Master => _master;

        /// <summary>Music channel, 0–1 linear (before master is applied).</summary>
        public float Music => _music;

        /// <summary>SFX channel, 0–1 linear (before master is applied).</summary>
        public float Sfx => _sfx;

        /// <summary>Effective music gain actually applied to sources: master * music.</summary>
        public float EffectiveMusic => _master * _music;

        /// <summary>Effective SFX gain actually applied to sources: master * sfx.</summary>
        public float EffectiveSfx => _master * _sfx;

        /// <summary>Set the master channel; input is clamped to 0–1.</summary>
        public void SetMaster(float value) => _master = Clamp01(value);

        /// <summary>Set the music channel; input is clamped to 0–1.</summary>
        public void SetMusic(float value) => _music = Clamp01(value);

        /// <summary>Set the SFX channel; input is clamped to 0–1.</summary>
        public void SetSfx(float value) => _sfx = Clamp01(value);

        /// <summary>Clamp an arbitrary float into the inclusive 0–1 range.</summary>
        public static float Clamp01(float v)
        {
            if (float.IsNaN(v)) return 0f;
            if (v < 0f) return 0f;
            if (v > 1f) return 1f;
            return v;
        }

        /// <summary>
        /// Convert a 0–1 linear amplitude to decibels for an AudioMixer parameter.
        /// 1 → 0 dB, 0 → <see cref="MIN_DECIBELS"/>. Uses 20*log10(linear).
        /// </summary>
        public static float LinearToDecibels(float linear)
        {
            linear = Clamp01(linear);
            if (linear <= 0.0001f) return MIN_DECIBELS;
            return 20f * (float)Math.Log10(linear);
        }
    }
}
