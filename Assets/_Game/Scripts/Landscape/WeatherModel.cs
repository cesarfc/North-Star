using System;
using UnityEngine;

/// <summary>
/// Pure, MonoBehaviour-free weather state machine. Tracks the current
/// <see cref="WeatherType"/> (the enum lives in Core/SharedTypes), the weather being
/// transitioned toward, and a normalized [0,1] blend that <see cref="EnvironmentManager"/>
/// uses to lerp fog / particle intensity. It is Unity-runtime-agnostic so it can be
/// unit-tested in EditMode; the manager wraps it and drives <see cref="Advance"/> from a
/// coroutine (weather never snaps instantly — see the contract).
///
/// Blend semantics: <see cref="BlendProgress"/> is 0 at the start of a transition and
/// reaches 1 when complete. <see cref="CurrentWeather"/> reports the destination weather
/// the instant a transition starts (so queries reflect intent immediately), while
/// <see cref="PreviousWeather"/> + <see cref="BlendProgress"/> let visuals cross-fade.
/// </summary>
public class WeatherModel
{
    private WeatherType _current;
    private WeatherType _previous;
    private float _transitionDuration;
    private float _elapsed;
    private bool _transitioning;

    /// <summary>Fired when a transition begins, carrying the destination weather.</summary>
    public event Action<WeatherType> WeatherChanged;

    /// <summary>Fired once when an in-progress transition reaches completion.</summary>
    public event Action<WeatherType> TransitionCompleted;

    /// <summary>Create a model already settled on <paramref name="initial"/> weather.</summary>
    public WeatherModel(WeatherType initial = WeatherType.Clear)
    {
        _current = initial;
        _previous = initial;
        _transitioning = false;
        _elapsed = 0f;
        _transitionDuration = 0f;
    }

    /// <summary>The weather the world is in (or transitioning toward, once a transition starts).</summary>
    public WeatherType CurrentWeather => _current;

    /// <summary>The weather being faded out of during a transition (== current when settled).</summary>
    public WeatherType PreviousWeather => _previous;

    /// <summary>True while a transition is in progress.</summary>
    public bool IsTransitioning => _transitioning;

    /// <summary>
    /// Normalized transition progress in [0,1]. 0 = just started, 1 = fully on the new
    /// weather. Stays at 1 when settled so visual lerps land cleanly on the target.
    /// </summary>
    public float BlendProgress => _transitioning
        ? (_transitionDuration <= 0f ? 1f : Mathf.Clamp01(_elapsed / _transitionDuration))
        : 1f;

    /// <summary>
    /// Begin a transition toward <paramref name="type"/> over <paramref name="duration"/>
    /// seconds. Requesting the weather that is already current (and not mid-transition) is
    /// a no-op and does not refire the event. A non-positive duration completes instantly
    /// on the next <see cref="Advance"/> but still routes through the transition so callers
    /// observe a consistent start→complete sequence. Returns true if a transition began.
    /// </summary>
    public bool BeginTransition(WeatherType type, float duration)
    {
        if (!_transitioning && type == _current)
            return false;

        _previous = _current;   // fade out of whatever we were showing
        _current = type;        // intent is reflected immediately
        _transitionDuration = Mathf.Max(0f, duration);
        _elapsed = 0f;
        _transitioning = true;

        WeatherChanged?.Invoke(_current);

        // Zero-duration request: settle immediately so there is no lingering blend.
        if (_transitionDuration <= 0f)
            Complete();

        return true;
    }

    /// <summary>
    /// Advance an in-progress transition by <paramref name="deltaSeconds"/>. Fires
    /// <see cref="TransitionCompleted"/> exactly once when progress reaches 1. A no-op when
    /// not transitioning or when given a non-finite/negative delta.
    /// </summary>
    public void Advance(float deltaSeconds)
    {
        if (!_transitioning) return;
        if (float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds) || deltaSeconds < 0f) return;

        _elapsed += deltaSeconds;
        if (_elapsed >= _transitionDuration)
            Complete();
    }

    /// <summary>
    /// Force the current transition to finish immediately (snaps blend to 1 on the
    /// destination weather). Safe to call when not transitioning (no-op).
    /// </summary>
    public void Complete()
    {
        if (!_transitioning) return;
        _transitioning = false;
        _previous = _current;
        _elapsed = _transitionDuration;
        TransitionCompleted?.Invoke(_current);
    }

    /// <summary>
    /// Fog density baseline [0,1] for a weather type, used to drive a URP fog/volume
    /// blend. Pure lookup; the manager lerps between previous and current using
    /// <see cref="BlendProgress"/>.
    /// </summary>
    public static float FogDensityFor(WeatherType type)
    {
        switch (type)
        {
            case WeatherType.Clear:     return 0.00f;
            case WeatherType.Overcast:  return 0.15f;
            case WeatherType.Rain:      return 0.30f;
            case WeatherType.HeavyRain: return 0.45f;
            case WeatherType.Fog:       return 0.85f;
            case WeatherType.Snow:      return 0.40f;
            default:                    return 0.00f;
        }
    }

    /// <summary>
    /// Precipitation particle intensity [0,1] for a weather type (drives emission rate of
    /// rain/snow particle systems). Pure lookup.
    /// </summary>
    public static float PrecipitationIntensityFor(WeatherType type)
    {
        switch (type)
        {
            case WeatherType.Rain:      return 0.5f;
            case WeatherType.HeavyRain: return 1.0f;
            case WeatherType.Snow:      return 0.6f;
            default:                    return 0.0f;
        }
    }

    /// <summary>
    /// The fog density that should be displayed right now, lerping from the previous
    /// weather to the current weather by <see cref="BlendProgress"/>. Pure read of state.
    /// </summary>
    public float GetBlendedFogDensity()
    {
        return Mathf.Lerp(FogDensityFor(_previous), FogDensityFor(_current), BlendProgress);
    }

    /// <summary>
    /// The precipitation intensity that should be displayed right now, lerping previous→current
    /// by <see cref="BlendProgress"/>. Pure read of state.
    /// </summary>
    public float GetBlendedPrecipitation()
    {
        return Mathf.Lerp(PrecipitationIntensityFor(_previous), PrecipitationIntensityFor(_current), BlendProgress);
    }
}
