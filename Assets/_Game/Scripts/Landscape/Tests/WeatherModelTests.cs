using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// EditMode unit tests for the pure weather state machine behind
/// <see cref="EnvironmentManager"/>. The manager delegates SetWeather / GetCurrentWeather
/// and the OnWeatherChanged event to <see cref="WeatherModel"/>, which is MonoBehaviour-free
/// so it runs in EditMode without entering play mode. Covers initial state, transition start
/// (event + immediate intent), gradual progress (no instant snap), completion, no-op
/// same-weather requests, and the pure fog/precipitation blend lookups.
/// </summary>
public class WeatherModelTests
{
    private const float EPS = 1e-3f;

    // ── Initial state ──────────────────────────────────────────────────────────────

    [Test]
    public void Constructor_DefaultsToClearSettled()
    {
        var m = new WeatherModel();
        Assert.AreEqual(WeatherType.Clear, m.CurrentWeather);
        Assert.IsFalse(m.IsTransitioning);
        Assert.AreEqual(1f, m.BlendProgress, EPS);
    }

    [Test]
    public void Constructor_RespectsInitialWeather()
    {
        var m = new WeatherModel(WeatherType.Fog);
        Assert.AreEqual(WeatherType.Fog, m.CurrentWeather);
    }

    // ── BeginTransition ──────────────────────────────────────────────────────────────

    [Test]
    public void BeginTransition_SetsDestinationImmediately_AndFiresEvent()
    {
        var m = new WeatherModel(WeatherType.Clear);
        WeatherType? changed = null;
        m.WeatherChanged += w => changed = w;

        bool started = m.BeginTransition(WeatherType.Rain, 5f);

        Assert.IsTrue(started);
        Assert.AreEqual(WeatherType.Rain, m.CurrentWeather, "intent reflected immediately");
        Assert.AreEqual(WeatherType.Clear, m.PreviousWeather, "previous still the old weather mid-transition");
        Assert.AreEqual(WeatherType.Rain, changed);
        Assert.IsTrue(m.IsTransitioning);
    }

    [Test]
    public void BeginTransition_StartsAtZeroProgress_NotInstant()
    {
        var m = new WeatherModel(WeatherType.Clear);
        m.BeginTransition(WeatherType.HeavyRain, 10f);
        Assert.AreEqual(0f, m.BlendProgress, EPS, "weather must not snap instantly");
    }

    [Test]
    public void BeginTransition_SameWeatherWhileSettled_IsNoOp()
    {
        var m = new WeatherModel(WeatherType.Clear);
        int fires = 0;
        m.WeatherChanged += _ => fires++;

        bool started = m.BeginTransition(WeatherType.Clear, 5f);

        Assert.IsFalse(started);
        Assert.AreEqual(0, fires);
        Assert.IsFalse(m.IsTransitioning);
    }

    [Test]
    public void BeginTransition_ZeroDuration_CompletesImmediately()
    {
        var m = new WeatherModel(WeatherType.Clear);
        WeatherType? completed = null;
        m.TransitionCompleted += w => completed = w;

        bool started = m.BeginTransition(WeatherType.Snow, 0f);

        Assert.IsTrue(started);
        Assert.IsFalse(m.IsTransitioning, "zero-duration settles at once");
        Assert.AreEqual(WeatherType.Snow, m.CurrentWeather);
        Assert.AreEqual(WeatherType.Snow, m.PreviousWeather, "fully settled");
        Assert.AreEqual(WeatherType.Snow, completed);
        Assert.AreEqual(1f, m.BlendProgress, EPS);
    }

    [Test]
    public void BeginTransition_InterruptsInFlight_RetargetsFromCurrentVisual()
    {
        var m = new WeatherModel(WeatherType.Clear);
        m.BeginTransition(WeatherType.Rain, 10f);
        m.Advance(5f); // halfway to rain

        bool started = m.BeginTransition(WeatherType.Fog, 4f);

        Assert.IsTrue(started);
        Assert.AreEqual(WeatherType.Fog, m.CurrentWeather);
        Assert.AreEqual(WeatherType.Rain, m.PreviousWeather, "now fading from rain → fog");
        Assert.AreEqual(0f, m.BlendProgress, EPS);
    }

    // ── Advance / progress ───────────────────────────────────────────────────────────

    [Test]
    public void Advance_ProgressesBlendLinearly()
    {
        var m = new WeatherModel(WeatherType.Clear);
        m.BeginTransition(WeatherType.Rain, 10f);

        m.Advance(2.5f);
        Assert.AreEqual(0.25f, m.BlendProgress, EPS);

        m.Advance(2.5f);
        Assert.AreEqual(0.5f, m.BlendProgress, EPS);
    }

    [Test]
    public void Advance_ReachingDuration_CompletesAndFiresOnce()
    {
        var m = new WeatherModel(WeatherType.Clear);
        int completes = 0;
        m.TransitionCompleted += _ => completes++;
        m.BeginTransition(WeatherType.Overcast, 3f);

        m.Advance(2f);
        Assert.IsTrue(m.IsTransitioning);
        Assert.AreEqual(0, completes);

        m.Advance(2f); // overshoot
        Assert.IsFalse(m.IsTransitioning);
        Assert.AreEqual(1f, m.BlendProgress, EPS);
        Assert.AreEqual(1, completes);

        m.Advance(5f); // no double-fire after settled
        Assert.AreEqual(1, completes);
    }

    [Test]
    public void Advance_WhenNotTransitioning_IsNoOp()
    {
        var m = new WeatherModel(WeatherType.Clear);
        Assert.DoesNotThrow(() => m.Advance(1f));
        Assert.AreEqual(1f, m.BlendProgress, EPS);
    }

    [Test]
    public void Advance_IgnoresNegativeAndNonFiniteDelta()
    {
        var m = new WeatherModel(WeatherType.Clear);
        m.BeginTransition(WeatherType.Rain, 10f);

        m.Advance(-5f);
        m.Advance(float.NaN);
        m.Advance(float.PositiveInfinity);

        Assert.AreEqual(0f, m.BlendProgress, EPS, "bad deltas do not advance the blend");
        Assert.IsTrue(m.IsTransitioning);
    }

    [Test]
    public void Complete_ForcesSettleOnDestination()
    {
        var m = new WeatherModel(WeatherType.Clear);
        m.BeginTransition(WeatherType.Snow, 10f);
        m.Advance(3f);

        m.Complete();

        Assert.IsFalse(m.IsTransitioning);
        Assert.AreEqual(WeatherType.Snow, m.CurrentWeather);
        Assert.AreEqual(WeatherType.Snow, m.PreviousWeather);
        Assert.AreEqual(1f, m.BlendProgress, EPS);
    }

    // ── Pure blend lookups ───────────────────────────────────────────────────────────

    [Test]
    public void FogDensityFor_ClearIsZero_FogIsHighest()
    {
        Assert.AreEqual(0f, WeatherModel.FogDensityFor(WeatherType.Clear), EPS);
        Assert.Greater(WeatherModel.FogDensityFor(WeatherType.Fog),
                       WeatherModel.FogDensityFor(WeatherType.Overcast));
        Assert.Greater(WeatherModel.FogDensityFor(WeatherType.HeavyRain),
                       WeatherModel.FogDensityFor(WeatherType.Rain));
    }

    [Test]
    public void FogDensityFor_AllTypesInUnitRange()
    {
        foreach (WeatherType t in System.Enum.GetValues(typeof(WeatherType)))
        {
            float d = WeatherModel.FogDensityFor(t);
            Assert.GreaterOrEqual(d, 0f);
            Assert.LessOrEqual(d, 1f);
        }
    }

    [Test]
    public void PrecipitationFor_OnlyWetWeatherEmits()
    {
        Assert.AreEqual(0f, WeatherModel.PrecipitationIntensityFor(WeatherType.Clear), EPS);
        Assert.AreEqual(0f, WeatherModel.PrecipitationIntensityFor(WeatherType.Overcast), EPS);
        Assert.AreEqual(0f, WeatherModel.PrecipitationIntensityFor(WeatherType.Fog), EPS);
        Assert.Greater(WeatherModel.PrecipitationIntensityFor(WeatherType.Rain), 0f);
        Assert.Greater(WeatherModel.PrecipitationIntensityFor(WeatherType.HeavyRain),
                       WeatherModel.PrecipitationIntensityFor(WeatherType.Rain));
        Assert.Greater(WeatherModel.PrecipitationIntensityFor(WeatherType.Snow), 0f);
    }

    [Test]
    public void GetBlendedFogDensity_LerpsPreviousToCurrentByProgress()
    {
        var m = new WeatherModel(WeatherType.Clear); // fog 0
        m.BeginTransition(WeatherType.Fog, 10f);      // fog 0.85
        m.Advance(5f);                                // halfway

        float expected = Mathf.Lerp(
            WeatherModel.FogDensityFor(WeatherType.Clear),
            WeatherModel.FogDensityFor(WeatherType.Fog),
            0.5f);
        Assert.AreEqual(expected, m.GetBlendedFogDensity(), EPS);
    }

    [Test]
    public void GetBlendedPrecipitation_RisesAsRainFadesIn()
    {
        var m = new WeatherModel(WeatherType.Clear);
        m.BeginTransition(WeatherType.HeavyRain, 10f);

        float atStart = m.GetBlendedPrecipitation();
        m.Advance(5f);
        float atMid = m.GetBlendedPrecipitation();
        m.Advance(5f);
        float atEnd = m.GetBlendedPrecipitation();

        Assert.AreEqual(0f, atStart, EPS);
        Assert.Greater(atMid, atStart);
        Assert.Greater(atEnd, atMid);
        Assert.AreEqual(WeatherModel.PrecipitationIntensityFor(WeatherType.HeavyRain), atEnd, EPS);
    }

    [Test]
    public void FullSequence_FiresChangeThenComplete()
    {
        var m = new WeatherModel(WeatherType.Clear);
        var log = new List<string>();
        m.WeatherChanged += w => log.Add("changed:" + w);
        m.TransitionCompleted += w => log.Add("done:" + w);

        m.BeginTransition(WeatherType.Rain, 2f);
        m.Advance(1f);
        m.Advance(1f);

        CollectionAssert.AreEqual(new[] { "changed:Rain", "done:Rain" }, log);
    }
}
