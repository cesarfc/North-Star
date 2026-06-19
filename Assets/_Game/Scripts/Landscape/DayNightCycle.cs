using System;
using UnityEngine;

/// <summary>
/// Scene-level day/night driver. Owns a pure <see cref="DayNightModel"/> for the clock /
/// sun math and handles the Unity-side concerns: rotating the directional "sun" light,
/// blending its intensity and color, and blending the skybox/ambient by the daylight
/// factor. The pure model is the EditMode-tested core; this wrapper only ticks it with
/// <c>Time.deltaTime</c> and applies the result to the scene.
///
/// Time scale follows the contract (<c>scale == 1</c> = realtime). The project default of
/// "1 game hour = 2 real minutes" is <see cref="DayNightModel.DefaultTimeScale"/> (= 30),
/// which is also the serialized default below.
///
/// <c>Update()</c> is used here because day/night is a continuous time-based animation —
/// the same exception CONVENTIONS.md grants to "movement logic" — not discrete game logic
/// (which must go through the EventBus).
/// </summary>
public class DayNightCycle : MonoBehaviour
{
    [Header("Clock")]
    [Tooltip("Hour the cycle starts at (0–24).")]
    [SerializeField, Range(0f, 24f)] private float _startHour = 8f;

    [Tooltip("Game-seconds advanced per real-second. 1 = realtime, 30 = 1 game-hour per 2 real-min (default).")]
    [SerializeField] private float _timeScale = DayNightModel.DefaultTimeScale;

    [Tooltip("Advance the clock automatically each frame. Disable to drive time externally via SetHour.")]
    [SerializeField] private bool _autoAdvance = true;

    [Header("Sun (Directional Light)")]
    [Tooltip("Directional light treated as the sun. Rotated and dimmed across the day.")]
    [SerializeField] private Light _sunLight;

    [Tooltip("Compass heading (Y rotation, degrees) the sun travels along.")]
    [SerializeField] private float _sunYaw = 170f;

    [Tooltip("Max sun intensity at solar noon.")]
    [SerializeField] private float _maxSunIntensity = 1.2f;

    [Tooltip("Daylight color ramp from midnight (left) to noon (right).")]
    [SerializeField] private Gradient _sunColorOverDay;

    [Header("Skybox / Ambient")]
    [Tooltip("Optional skybox material with a float '_Blend' property lerped 0(night)→1(day).")]
    [SerializeField] private Material _skyboxMaterial;

    [Tooltip("Ambient light color ramp from midnight (left) to noon (right).")]
    [SerializeField] private Gradient _ambientColorOverDay;

    private static readonly int BLEND_PROPERTY = Shader.PropertyToID("_Blend");

    private DayNightModel _model;

    /// <summary>Raised when the integer hour rolls over, carrying the new whole hour (0–23).</summary>
    public event Action<int> OnHourChanged;

    /// <summary>Raised once when the clock crosses into dawn.</summary>
    public event Action OnDawnStart;

    /// <summary>Raised once when the clock crosses into dusk.</summary>
    public event Action OnDuskStart;

    private void Awake()
    {
        _model = new DayNightModel(_startHour, _timeScale);
        _model.HourChanged += HandleHourChanged;
        _model.DawnStarted += HandleDawnStarted;
        _model.DuskStarted += HandleDuskStarted;
        ApplyVisuals();
    }

    private void OnDestroy()
    {
        if (_model == null) return;
        _model.HourChanged -= HandleHourChanged;
        _model.DawnStarted -= HandleDawnStarted;
        _model.DuskStarted -= HandleDuskStarted;
    }

    private void Update()
    {
        if (!_autoAdvance || _model == null) return;
        _model.Advance(Time.deltaTime);
        ApplyVisuals();
    }

    /// <summary>Current hour of day as a continuous value in the range [0, 24).</summary>
    public float GetCurrentHour()
    {
        EnsureModel();
        return _model.CurrentHour;
    }

    /// <summary>
    /// Jump the clock to <paramref name="hour"/> (wrapped into [0,24)) and re-apply visuals
    /// immediately. Fires <see cref="OnHourChanged"/> if the whole hour changes; does not fire
    /// dawn/dusk (a manual set is not the sun crossing the horizon).
    /// </summary>
    public void SetHour(float hour)
    {
        EnsureModel();
        _model.SetHour(hour);
        ApplyVisuals();
    }

    /// <summary>Set the time scale (game-seconds per real-second). 1 = realtime; negatives clamp to 0.</summary>
    public void SetTimeScale(float scale)
    {
        EnsureModel();
        _model.SetTimeScale(scale);
        _timeScale = _model.TimeScale;
    }

    /// <summary>Daylight factor [0,1] right now (0 midnight → 1 noon). For UI/gameplay hooks.</summary>
    public float GetDaylightFactor()
    {
        EnsureModel();
        return _model.GetDaylightFactor();
    }

    // ── Visual application ────────────────────────────────────────────────────────

    private void ApplyVisuals()
    {
        if (_model == null) return;

        float daylight = _model.GetDaylightFactor();

        if (_sunLight != null)
        {
            _sunLight.transform.rotation = Quaternion.Euler(_model.GetSunPitchDegrees(), _sunYaw, 0f);
            _sunLight.intensity = Mathf.Clamp01(daylight) * _maxSunIntensity;
            if (_sunColorOverDay != null)
                _sunLight.color = _sunColorOverDay.Evaluate(daylight);
        }

        if (_skyboxMaterial != null && _skyboxMaterial.HasProperty(BLEND_PROPERTY))
            _skyboxMaterial.SetFloat(BLEND_PROPERTY, daylight);

        if (_ambientColorOverDay != null)
            RenderSettings.ambientLight = _ambientColorOverDay.Evaluate(daylight);
    }

    private void HandleHourChanged(int hour) => OnHourChanged?.Invoke(hour);
    private void HandleDawnStarted() => OnDawnStart?.Invoke();
    private void HandleDuskStarted() => OnDuskStart?.Invoke();

    private void EnsureModel()
    {
        if (_model != null) return;
        _model = new DayNightModel(_startHour, _timeScale);
        _model.HourChanged += HandleHourChanged;
        _model.DawnStarted += HandleDawnStarted;
        _model.DuskStarted += HandleDuskStarted;
    }
}
