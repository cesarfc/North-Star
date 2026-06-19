using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Scene-level weather manager. Owns a pure <see cref="WeatherModel"/> for the state /
/// blend math and handles the Unity-side concerns: running a coroutine that fades weather
/// over time (never an instant snap), driving the scene fog and a precipitation
/// <see cref="ParticleSystem"/>, and publishing the weather change so other modules can
/// react without referencing this assembly.
///
/// Fog: URP honors the built-in <see cref="RenderSettings"/> fog (color/mode/density), so we
/// drive <c>RenderSettings.fogDensity</c>/<c>fogColor</c> rather than a Volume override. URP
/// has no Fog VolumeComponent (that is HDRP), so this is the correct cross-pipeline-safe path
/// and keeps this assembly free of a hard URP package dependency.
///
/// Cross-module note: the contract only specifies the C# <see cref="OnWeatherChanged"/> event
/// below. There is currently no weather struct in Core/GameEvents.cs, so a global EventBus
/// broadcast is not yet possible — see the orchestrator note in the module report. Once Core
/// adds a <c>WeatherChangedEvent</c>, this manager should also publish it.
/// </summary>
public class EnvironmentManager : MonoBehaviour
{
    [Header("Initial State")]
    [Tooltip("Weather the scene starts in before any SetWeather call.")]
    [SerializeField] private WeatherType _startWeather = WeatherType.Clear;

    [Header("Fog Driving")]
    [Tooltip("Drive RenderSettings fog from weather. Turn off if a Volume/other system owns fog.")]
    [SerializeField] private bool _driveRenderSettingsFog = true;

    [Tooltip("Max exponential fog density at full fog (e.g. WeatherType.Fog).")]
    [SerializeField] private float _maxFogDensity = 0.05f;

    [Tooltip("Fog tint applied while fog is active.")]
    [SerializeField] private Color _fogColor = new Color(0.7f, 0.72f, 0.75f, 1f);

    [Header("Precipitation")]
    [Tooltip("Particle system emitting rain/snow. Emission rate is scaled by precipitation intensity.")]
    [SerializeField] private ParticleSystem _precipitation;

    [Tooltip("Max emission (particles/sec) at full precipitation intensity.")]
    [SerializeField] private float _maxPrecipitationEmission = 800f;

    private WeatherModel _model;
    private Coroutine _transitionRoutine;

    /// <summary>Raised when the weather begins changing, carrying the new (destination) weather.</summary>
    public event Action<WeatherType> OnWeatherChanged;

    private void Awake()
    {
        EnsureModel();
        ApplyVisuals(); // settle visuals to the starting weather
    }

    private void OnDestroy()
    {
        if (_model != null)
            _model.WeatherChanged -= HandleModelWeatherChanged;
    }

    /// <summary>
    /// Begin transitioning to <paramref name="type"/> over <paramref name="transitionDuration"/>
    /// seconds. Always coroutine-driven so the change is gradual; requesting the current
    /// weather (while settled) is a no-op. A new request interrupts any in-flight transition.
    /// </summary>
    public void SetWeather(WeatherType type, float transitionDuration)
    {
        EnsureModel();

        bool started = _model.BeginTransition(type, transitionDuration);
        if (!started)
            return;

        if (_transitionRoutine != null)
        {
            StopCoroutine(_transitionRoutine);
            _transitionRoutine = null;
        }

        // If the model already completed (zero/negative duration) just apply once.
        if (!_model.IsTransitioning)
        {
            ApplyVisuals();
            return;
        }

        if (isActiveAndEnabled)
            _transitionRoutine = StartCoroutine(CoTransitionWeather());
        else
            ApplyVisuals(); // cannot run a coroutine while disabled; settle immediately
    }

    /// <summary>Return the current (or destination, mid-transition) weather.</summary>
    public WeatherType GetCurrentWeather()
    {
        EnsureModel();
        return _model.CurrentWeather;
    }

    /// <summary>Normalized [0,1] blend progress of the active transition (1 when settled). For UI/debug.</summary>
    public float GetTransitionProgress()
    {
        EnsureModel();
        return _model.BlendProgress;
    }

    // ── Coroutine ───────────────────────────────────────────────────────────────

    private IEnumerator CoTransitionWeather()
    {
        while (_model.IsTransitioning)
        {
            _model.Advance(Time.deltaTime);
            ApplyVisuals();
            yield return null;
        }

        ApplyVisuals(); // final settle on the destination weather
        _transitionRoutine = null;
    }

    // ── Visual driving ──────────────────────────────────────────────────────────

    private void ApplyVisuals()
    {
        if (_model == null) return;

        float fog = _model.GetBlendedFogDensity();      // [0,1]
        float precip = _model.GetBlendedPrecipitation(); // [0,1]

        if (_driveRenderSettingsFog)
        {
            // URP reads RenderSettings fog. Map our [0,1] density onto an exponential
            // fog density; enable fog only when there is something to show.
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fog = fog > 0.0001f;
            RenderSettings.fogDensity = fog * _maxFogDensity;
            RenderSettings.fogColor = _fogColor;
        }

        if (_precipitation != null)
        {
            var emission = _precipitation.emission;
            emission.rateOverTime = precip * _maxPrecipitationEmission;
            if (precip > 0f)
            {
                if (!_precipitation.isPlaying) _precipitation.Play();
            }
            else if (_precipitation.isPlaying)
            {
                _precipitation.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }
    }

    private void HandleModelWeatherChanged(WeatherType type)
    {
        OnWeatherChanged?.Invoke(type);
    }

    private void EnsureModel()
    {
        // Awake may not have run yet if SetWeather is called before the object is active
        // (or from a non-play-mode context); construct lazily so the API never NREs.
        if (_model != null) return;
        _model = new WeatherModel(_startWeather);
        _model.WeatherChanged += HandleModelWeatherChanged;
    }
}
