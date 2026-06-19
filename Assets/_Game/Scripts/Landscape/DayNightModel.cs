using System;
using UnityEngine;

/// <summary>
/// Pure, MonoBehaviour-free day/night time model. Owns the clock (hour-of-day in
/// the range [0, 24)), the time scale, and the derived sun direction / daylight
/// factor used to drive the directional light and skybox blend. It is deliberately
/// Unity-runtime-agnostic (it touches no scene objects), so it can be unit-tested in
/// EditMode without entering play mode. <see cref="DayNightCycle"/> wraps it, ticks
/// it with <c>Time.deltaTime</c>, and forwards the callbacks below to C# events.
///
/// Time scale convention matches the contract: <c>scale == 1</c> is realtime
/// (1 real second advances the clock by 1 second of game time). The project default
/// of "1 game hour = 2 real minutes" therefore corresponds to a scale of 30
/// (3600 game seconds / 120 real seconds), exposed as <see cref="DefaultTimeScale"/>.
/// </summary>
public class DayNightModel
{
    /// <summary>Hours in a full day. The clock wraps on this value.</summary>
    public const float HOURS_PER_DAY = 24f;

    /// <summary>Default scale so 1 game hour elapses in 2 real minutes (3600s / 120s).</summary>
    public const float DefaultTimeScale = 30f;

    /// <summary>Hour at which dawn begins (sun rising). Inclusive boundary for the dawn event.</summary>
    public const float DAWN_HOUR = 6f;

    /// <summary>Hour at which dusk begins (sun setting). Inclusive boundary for the dusk event.</summary>
    public const float DUSK_HOUR = 18f;

    private float _hour;       // current hour of day, always in [0, 24)
    private float _timeScale;  // game-seconds advanced per real-second
    private int _lastWholeHour; // last integer hour we reported, for OnHourChanged

    /// <summary>Fired when the integer hour rolls over, carrying the new whole hour (0–23).</summary>
    public event Action<int> HourChanged;

    /// <summary>Fired once when the clock crosses into <see cref="DAWN_HOUR"/>.</summary>
    public event Action DawnStarted;

    /// <summary>Fired once when the clock crosses into <see cref="DUSK_HOUR"/>.</summary>
    public event Action DuskStarted;

    /// <summary>Create a model starting at the given hour and time scale.</summary>
    public DayNightModel(float startHour = 8f, float timeScale = DefaultTimeScale)
    {
        _hour = Wrap(startHour);
        _timeScale = Mathf.Max(0f, timeScale);
        _lastWholeHour = Mathf.FloorToInt(_hour);
    }

    /// <summary>Current hour of day as a continuous value in the range [0, 24).</summary>
    public float CurrentHour => _hour;

    /// <summary>Game-seconds advanced per real-second. 1 = realtime, 30 = default (2 real-min/hour).</summary>
    public float TimeScale => _timeScale;

    /// <summary>True while the clock is between dawn and dusk (sun above the horizon).</summary>
    public bool IsDaytime => _hour >= DAWN_HOUR && _hour < DUSK_HOUR;

    /// <summary>
    /// Set the time scale. Negative values are clamped to 0 (clock frozen). Does not
    /// rewind or skip time, so no hour/dawn/dusk events are fired by this call.
    /// </summary>
    public void SetTimeScale(float scale)
    {
        _timeScale = Mathf.Max(0f, scale);
    }

    /// <summary>
    /// Jump the clock directly to <paramref name="hour"/> (wrapped into [0, 24)).
    /// This is a teleport, not a tick: <see cref="HourChanged"/> fires if the whole
    /// hour differs, but dawn/dusk crossing events are NOT raised (a manual set is not
    /// the sun "crossing" the boundary). Resets the rollover baseline to the new hour.
    /// </summary>
    public void SetHour(float hour)
    {
        _hour = Wrap(hour);
        int whole = Mathf.FloorToInt(_hour);
        if (whole != _lastWholeHour)
        {
            _lastWholeHour = whole;
            HourChanged?.Invoke(whole);
        }
        else
        {
            _lastWholeHour = whole;
        }
    }

    /// <summary>
    /// Advance the clock by <paramref name="realDeltaSeconds"/> of real time, scaled by
    /// the current time scale. Fires <see cref="HourChanged"/> for every whole hour
    /// crossed and <see cref="DawnStarted"/>/<see cref="DuskStarted"/> when the dawn/dusk
    /// boundaries are crossed during this advance. Safe across midnight and across
    /// multi-hour deltas. Negative or non-finite deltas are ignored.
    /// </summary>
    public void Advance(float realDeltaSeconds)
    {
        if (_timeScale <= 0f) return;
        if (!IsFinitePositive(realDeltaSeconds)) return;

        float gameHoursDelta = (realDeltaSeconds * _timeScale) / 3600f;
        if (gameHoursDelta <= 0f) return;

        float previous = _hour;
        float rawNew = previous + gameHoursDelta;

        // Detect dawn/dusk crossings using the unwrapped progression so a single
        // Advance that sweeps past a boundary still fires exactly once per crossing.
        RaiseBoundaryCrossings(previous, rawNew);

        // Fire HourChanged for each integer hour boundary crossed, in order.
        int fromWhole = Mathf.FloorToInt(previous);
        int toWhole = Mathf.FloorToInt(rawNew);
        for (int h = fromWhole + 1; h <= toWhole; h++)
        {
            int wholeWrapped = ((h % 24) + 24) % 24;
            _lastWholeHour = wholeWrapped;
            HourChanged?.Invoke(wholeWrapped);
        }

        _hour = Wrap(rawNew);
    }

    /// <summary>
    /// Daylight factor in [0, 1]: 0 at solar midnight, 1 at solar noon, following a
    /// smooth cosine curve. Useful for blending skybox/ambient intensity. Pure function
    /// of the current hour.
    /// </summary>
    public float GetDaylightFactor()
    {
        return DaylightFactorAt(_hour);
    }

    /// <summary>
    /// Sun elevation as a normalized signed value in [-1, 1]: +1 at noon (overhead),
    /// 0 at the dawn/dusk horizon, -1 at midnight. Pure function of the current hour.
    /// </summary>
    public float GetSunElevation()
    {
        return SunElevationAt(_hour);
    }

    /// <summary>
    /// Euler X rotation (degrees) for a directional "sun" light such that the light
    /// points straight down at noon and lies on the horizon at dawn/dusk. Maps hour to
    /// a full 360° sweep with -90° at midnight and +90° at noon. Pure function.
    /// </summary>
    public float GetSunPitchDegrees()
    {
        // 0h → -90 (below horizon, pointing up = night), 6h → 0 (horizon),
        // 12h → 90 (overhead), 18h → 180 wrapped. A linear sweep over 24h.
        return (_hour / HOURS_PER_DAY) * 360f - 90f;
    }

    // ── Pure static helpers (also handy for tests) ─────────────────────────────

    /// <summary>Daylight factor [0,1] for an arbitrary hour. 0 at midnight, 1 at noon.</summary>
    public static float DaylightFactorAt(float hour)
    {
        // cos peaks at noon (hour 12). Remap from [-1,1] to [0,1].
        float radians = ((Wrap(hour) - 12f) / HOURS_PER_DAY) * 2f * Mathf.PI;
        return Mathf.Clamp01((Mathf.Cos(radians) + 1f) * 0.5f);
    }

    /// <summary>Signed sun elevation [-1,1] for an arbitrary hour. -1 midnight, +1 noon.</summary>
    public static float SunElevationAt(float hour)
    {
        float radians = ((Wrap(hour) - 12f) / HOURS_PER_DAY) * 2f * Mathf.PI;
        return Mathf.Clamp(Mathf.Cos(radians), -1f, 1f);
    }

    /// <summary>Wrap an arbitrary hour value into the canonical range [0, 24).</summary>
    public static float Wrap(float hour)
    {
        float wrapped = hour % HOURS_PER_DAY;
        if (wrapped < 0f) wrapped += HOURS_PER_DAY;
        // Guard the exact-24 case produced by tiny floating point drift.
        if (wrapped >= HOURS_PER_DAY) wrapped -= HOURS_PER_DAY;
        return wrapped;
    }

    // ── Internals ──────────────────────────────────────────────────────────────

    private void RaiseBoundaryCrossings(float fromHour, float toRawHour)
    {
        // Walk each day the advance spans and check the dawn/dusk markers within it.
        // toRawHour can exceed 24 (or span multiple days for huge deltas); iterate the
        // absolute marker positions between fromHour (exclusive) and toRawHour (inclusive).
        int firstDay = Mathf.FloorToInt(fromHour / HOURS_PER_DAY);
        int lastDay = Mathf.FloorToInt(toRawHour / HOURS_PER_DAY);
        for (int day = firstDay; day <= lastDay; day++)
        {
            float dawnMarker = day * HOURS_PER_DAY + DAWN_HOUR;
            float duskMarker = day * HOURS_PER_DAY + DUSK_HOUR;
            if (dawnMarker > fromHour && dawnMarker <= toRawHour) DawnStarted?.Invoke();
            if (duskMarker > fromHour && duskMarker <= toRawHour) DuskStarted?.Invoke();
        }
    }

    private static bool IsFinitePositive(float v)
    {
        return !float.IsNaN(v) && !float.IsInfinity(v) && v > 0f;
    }
}
