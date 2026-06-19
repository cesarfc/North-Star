using NUnit.Framework;
using NorthStar.Audio;

/// <summary>
/// EditMode tests for <see cref="VolumeMixer"/> — AudioManager's volume math. Covers
/// clamping, master*channel composition for effective gains, and linear→dB conversion.
/// </summary>
public class VolumeMixerTests
{
    private const float EPS = 1e-4f;

    [Test]
    public void Defaults_AreFullVolume()
    {
        var m = new VolumeMixer();
        Assert.AreEqual(1f, m.Master, EPS);
        Assert.AreEqual(1f, m.Music, EPS);
        Assert.AreEqual(1f, m.Sfx, EPS);
        Assert.AreEqual(1f, m.EffectiveMusic, EPS);
        Assert.AreEqual(1f, m.EffectiveSfx, EPS);
    }

    [Test]
    public void Set_ClampsToZeroOne()
    {
        var m = new VolumeMixer();
        m.SetMaster(2f);
        m.SetMusic(-1f);
        m.SetSfx(0.5f);

        Assert.AreEqual(1f, m.Master, EPS, "above-range master clamps to 1");
        Assert.AreEqual(0f, m.Music, EPS, "below-range music clamps to 0");
        Assert.AreEqual(0.5f, m.Sfx, EPS);
    }

    [Test]
    public void EffectiveGains_MultiplyMasterByChannel()
    {
        var m = new VolumeMixer();
        m.SetMaster(0.5f);
        m.SetMusic(0.8f);
        m.SetSfx(0.25f);

        Assert.AreEqual(0.40f, m.EffectiveMusic, EPS); // 0.5 * 0.8
        Assert.AreEqual(0.125f, m.EffectiveSfx, EPS);  // 0.5 * 0.25
    }

    [Test]
    public void MasterZero_SilencesEverything()
    {
        var m = new VolumeMixer();
        m.SetMusic(1f);
        m.SetSfx(1f);
        m.SetMaster(0f);

        Assert.AreEqual(0f, m.EffectiveMusic, EPS);
        Assert.AreEqual(0f, m.EffectiveSfx, EPS);
    }

    [Test]
    public void Clamp01_HandlesNaN()
    {
        Assert.AreEqual(0f, VolumeMixer.Clamp01(float.NaN), EPS);
    }

    [Test]
    public void LinearToDecibels_UnityIsZeroDb()
    {
        Assert.AreEqual(0f, VolumeMixer.LinearToDecibels(1f), 1e-3f);
    }

    [Test]
    public void LinearToDecibels_HalfIsAboutMinusSix()
    {
        // 20*log10(0.5) ≈ -6.0206 dB
        Assert.AreEqual(-6.0206f, VolumeMixer.LinearToDecibels(0.5f), 1e-2f);
    }

    [Test]
    public void LinearToDecibels_ZeroIsFloor()
    {
        Assert.AreEqual(VolumeMixer.MIN_DECIBELS, VolumeMixer.LinearToDecibels(0f), EPS);
    }
}
