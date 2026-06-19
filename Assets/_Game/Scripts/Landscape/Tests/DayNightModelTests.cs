using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// EditMode unit tests for the pure time math behind <see cref="DayNightCycle"/>. The
/// MonoBehaviour delegates GetCurrentHour / SetHour / SetTimeScale and the hour/dawn/dusk
/// events to <see cref="DayNightModel"/>, which is MonoBehaviour-free so it runs in EditMode
/// without entering play mode. Covers clock wrapping, scaled advance, midnight crossing,
/// hour/dawn/dusk event firing, and the pure sun/daylight curves.
/// </summary>
public class DayNightModelTests
{
    private const float EPS = 1e-3f;

    // ── Construction / SetHour ────────────────────────────────────────────────────

    [Test]
    public void Constructor_StoresStartHourAndScale()
    {
        var m = new DayNightModel(startHour: 9f, timeScale: 5f);
        Assert.AreEqual(9f, m.CurrentHour, EPS);
        Assert.AreEqual(5f, m.TimeScale, EPS);
    }

    [Test]
    public void Constructor_WrapsOutOfRangeStartHour()
    {
        Assert.AreEqual(1f, new DayNightModel(startHour: 25f).CurrentHour, EPS);
        Assert.AreEqual(23f, new DayNightModel(startHour: -1f).CurrentHour, EPS);
    }

    [Test]
    public void DefaultTimeScale_Is30_OneGameHourPerTwoRealMinutes()
    {
        // 1 game hour (3600 game-seconds) in 120 real seconds → scale 30.
        Assert.AreEqual(30f, DayNightModel.DefaultTimeScale, EPS);

        var m = new DayNightModel(startHour: 0f, timeScale: DayNightModel.DefaultTimeScale);
        m.Advance(120f); // 2 real minutes
        Assert.AreEqual(1f, m.CurrentHour, EPS, "2 real minutes at default scale = 1 game hour");
    }

    [Test]
    public void SetHour_WrapsAndUpdatesCurrentHour()
    {
        var m = new DayNightModel(startHour: 8f);
        m.SetHour(26f);
        Assert.AreEqual(2f, m.CurrentHour, EPS);
        m.SetHour(-3f);
        Assert.AreEqual(21f, m.CurrentHour, EPS);
    }

    [Test]
    public void SetHour_FiresHourChangedWhenWholeHourDiffers()
    {
        var m = new DayNightModel(startHour: 8f);
        int? reported = null;
        m.HourChanged += h => reported = h;

        m.SetHour(14.5f);
        Assert.AreEqual(14, reported);
    }

    [Test]
    public void SetHour_DoesNotFireDawnOrDusk()
    {
        var m = new DayNightModel(startHour: 3f);
        int dawn = 0, dusk = 0;
        m.DawnStarted += () => dawn++;
        m.DuskStarted += () => dusk++;

        m.SetHour(7f);   // crosses the dawn marker, but this is a manual jump
        m.SetHour(19f);  // crosses dusk marker

        Assert.AreEqual(0, dawn);
        Assert.AreEqual(0, dusk);
    }

    // ── SetTimeScale ──────────────────────────────────────────────────────────────

    [Test]
    public void SetTimeScale_ClampsNegativeToZero_FreezesClock()
    {
        var m = new DayNightModel(startHour: 10f, timeScale: 10f);
        m.SetTimeScale(-99f);
        Assert.AreEqual(0f, m.TimeScale, EPS);

        m.Advance(1000f);
        Assert.AreEqual(10f, m.CurrentHour, EPS, "frozen clock does not advance");
    }

    [Test]
    public void SetTimeScale_AffectsAdvanceRate()
    {
        var m = new DayNightModel(startHour: 0f, timeScale: 3600f); // 1 game-hour per real-second
        m.Advance(2f);
        Assert.AreEqual(2f, m.CurrentHour, EPS);
    }

    // ── Advance ───────────────────────────────────────────────────────────────────

    [Test]
    public void Advance_RealtimeScale_AdvancesBySeconds()
    {
        var m = new DayNightModel(startHour: 0f, timeScale: 1f);
        m.Advance(3600f); // one real hour at realtime = one game hour
        Assert.AreEqual(1f, m.CurrentHour, EPS);
    }

    [Test]
    public void Advance_WrapsPastMidnight()
    {
        var m = new DayNightModel(startHour: 23f, timeScale: 3600f);
        m.Advance(2f); // +2 game hours → 25 → wraps to 1
        Assert.AreEqual(1f, m.CurrentHour, EPS);
    }

    [Test]
    public void Advance_IgnoresNegativeAndNonFiniteDelta()
    {
        var m = new DayNightModel(startHour: 5f, timeScale: 3600f);
        m.Advance(-10f);
        m.Advance(float.NaN);
        m.Advance(float.PositiveInfinity);
        Assert.AreEqual(5f, m.CurrentHour, EPS);
    }

    [Test]
    public void Advance_FiresHourChangedForEachCrossedHour_InOrder()
    {
        var m = new DayNightModel(startHour: 8f, timeScale: 3600f);
        var hours = new List<int>();
        m.HourChanged += hours.Add;

        m.Advance(3f); // 8 → 11, crossing 9, 10, 11
        CollectionAssert.AreEqual(new[] { 9, 10, 11 }, hours);
    }

    [Test]
    public void Advance_FiresHourChangedWrappedAcrossMidnight()
    {
        var m = new DayNightModel(startHour: 22f, timeScale: 3600f);
        var hours = new List<int>();
        m.HourChanged += hours.Add;

        m.Advance(4f); // 22 → 26 → wraps; crosses 23, 0, 1, 2
        CollectionAssert.AreEqual(new[] { 23, 0, 1, 2 }, hours);
    }

    [Test]
    public void Advance_FiresDawnOnceWhenCrossingSixAM()
    {
        var m = new DayNightModel(startHour: 5f, timeScale: 3600f);
        int dawn = 0;
        m.DawnStarted += () => dawn++;

        m.Advance(2f); // 5 → 7, crosses dawn (6) once
        Assert.AreEqual(1, dawn);
    }

    [Test]
    public void Advance_FiresDuskOnceWhenCrossingSixPM()
    {
        var m = new DayNightModel(startHour: 17f, timeScale: 3600f);
        int dusk = 0;
        m.DuskStarted += () => dusk++;

        m.Advance(2f); // 17 → 19, crosses dusk (18) once
        Assert.AreEqual(1, dusk);
    }

    [Test]
    public void Advance_FullDay_FiresDawnAndDuskExactlyOnceEach()
    {
        var m = new DayNightModel(startHour: 0f, timeScale: 3600f);
        int dawn = 0, dusk = 0;
        m.DawnStarted += () => dawn++;
        m.DuskStarted += () => dusk++;

        m.Advance(24f); // a whole day
        Assert.AreEqual(1, dawn);
        Assert.AreEqual(1, dusk);
    }

    [Test]
    public void Advance_TwoDaysInOneStep_FiresDawnAndDuskTwice()
    {
        var m = new DayNightModel(startHour: 0f, timeScale: 3600f);
        int dawn = 0, dusk = 0;
        m.DawnStarted += () => dawn++;
        m.DuskStarted += () => dusk++;

        m.Advance(48f); // two full days
        Assert.AreEqual(2, dawn);
        Assert.AreEqual(2, dusk);
    }

    // ── IsDaytime ─────────────────────────────────────────────────────────────────

    [Test]
    public void IsDaytime_TrueBetweenDawnAndDusk()
    {
        Assert.IsTrue(new DayNightModel(startHour: 12f).IsDaytime);
        Assert.IsTrue(new DayNightModel(startHour: 6f).IsDaytime, "dawn boundary is day");
        Assert.IsFalse(new DayNightModel(startHour: 18f).IsDaytime, "dusk boundary is night");
        Assert.IsFalse(new DayNightModel(startHour: 3f).IsDaytime);
        Assert.IsFalse(new DayNightModel(startHour: 22f).IsDaytime);
    }

    // ── Pure sun / daylight curves ────────────────────────────────────────────────

    [Test]
    public void DaylightFactor_ZeroAtMidnight_OneAtNoon()
    {
        Assert.AreEqual(0f, DayNightModel.DaylightFactorAt(0f), 1e-2f);
        Assert.AreEqual(1f, DayNightModel.DaylightFactorAt(12f), 1e-2f);
        Assert.AreEqual(0.5f, DayNightModel.DaylightFactorAt(6f), 1e-2f, "horizon at dawn ≈ half light");
    }

    [Test]
    public void DaylightFactor_AlwaysInUnitRange()
    {
        for (float h = 0f; h < 24f; h += 0.5f)
        {
            float f = DayNightModel.DaylightFactorAt(h);
            Assert.GreaterOrEqual(f, 0f);
            Assert.LessOrEqual(f, 1f);
        }
    }

    [Test]
    public void SunElevation_PositiveAtNoon_NegativeAtMidnight()
    {
        Assert.AreEqual(1f, DayNightModel.SunElevationAt(12f), 1e-2f);
        Assert.AreEqual(-1f, DayNightModel.SunElevationAt(0f), 1e-2f);
        Assert.AreEqual(0f, DayNightModel.SunElevationAt(6f), 1e-2f, "sun on horizon at dawn");
        Assert.AreEqual(0f, DayNightModel.SunElevationAt(18f), 1e-2f, "sun on horizon at dusk");
    }

    [Test]
    public void SunPitch_NoonPointsDown_DawnOnHorizon()
    {
        var m = new DayNightModel(startHour: 12f);
        Assert.AreEqual(90f, m.GetSunPitchDegrees(), EPS);

        m.SetHour(6f);
        Assert.AreEqual(0f, m.GetSunPitchDegrees(), EPS);

        m.SetHour(0f);
        Assert.AreEqual(-90f, m.GetSunPitchDegrees(), EPS);
    }

    // ── Wrap helper ───────────────────────────────────────────────────────────────

    [Test]
    public void Wrap_NormalizesIntoZeroTo24()
    {
        Assert.AreEqual(0f, DayNightModel.Wrap(24f), EPS);
        Assert.AreEqual(1f, DayNightModel.Wrap(25f), EPS);
        Assert.AreEqual(23f, DayNightModel.Wrap(-1f), EPS);
        Assert.AreEqual(12f, DayNightModel.Wrap(12f), EPS);
        Assert.AreEqual(0f, DayNightModel.Wrap(48f), EPS);
    }
}
