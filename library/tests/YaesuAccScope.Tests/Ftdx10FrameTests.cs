namespace YaesuAccScope.Tests;

/// <summary>
/// The FTDX10 captures in captures/ftdx10, taken on 2026-11-20 with the radio in
/// USB and LEVEL +20, each named after what was set on the radio.
/// </summary>
public class Ftdx10FrameTests
{
    [Theory]
    [InlineData("ftdx10-14074-1k", 14_074_000, 1_000, ScopePlacement.Center, 14_074_000)]
    [InlineData("ftdx10-14074-20k", 14_074_000, 20_000, ScopePlacement.Center, 14_074_000)]
    [InlineData("ftdx10-14074-20k-mode2", 14_074_000, 20_000, ScopePlacement.Center, 14_074_000)]
    [InlineData("ftdx10-14074-20k-level30", 14_074_000, 20_000, ScopePlacement.Center, 14_074_000)]
    [InlineData("ftdx10-14074-20k-3dss", 14_074_000, 20_000, ScopePlacement.Center, 14_074_000)]
    [InlineData("ftdx10-14074-100k", 14_074_000, 100_000, ScopePlacement.Center, 14_074_000)]
    [InlineData("ftdx10-14074-1m", 14_074_000, 1_000_000, ScopePlacement.Center, 14_074_000)]
    [InlineData("ftdx10-7074-100k", 7_074_000, 100_000, ScopePlacement.Center, 7_074_000)]
    [InlineData("ftdx10-ton-1k", 863_000, 20_000, ScopePlacement.Center, 863_000)]
    // Cursor and Fix take the centre from the left edge the radio sends (byte 3046).
    [InlineData("ftdx10-14074-20k-cursor", 14_074_000, 20_000, ScopePlacement.Cursor, 14_064_000)]
    [InlineData("ftdx10-14074-20k-fix", 14_074_000, 20_000, ScopePlacement.Fix, 14_256_000)]
    [InlineData("ftdx10-7074-100k-fix", 7_074_000, 100_000, ScopePlacement.Fix, 7_072_000)]
    public void Main_tuning(string capture, long vfo, int span, ScopePlacement placement, long centre)
    {
        foreach (var frame in Captures.Ftdx10(capture))
        {
            Assert.Equal(AccRadio.Ftdx10, frame.Radio);
            Assert.False(frame.HasSub);
            var main = frame.Main;
            Assert.True(main.IsValid);
            Assert.Equal(vfo, main.VfoHz);
            Assert.Equal(span, main.SpanHz);
            Assert.Equal(placement, main.Placement);
            Assert.Equal(centre, main.CentreHz);
        }
    }

    [Fact]
    public void Sub_reads_zero_and_is_not_valid()
    {
        foreach (var frame in Captures.Ftdx10("ftdx10-14074-20k"))
        {
            Assert.False(frame.Sub.IsValid);
            Assert.All(frame.Raw.AsSpan(850, 850).ToArray(), b => Assert.Equal(0, b));
        }
    }

    [Theory]
    [InlineData("ftdx10-14074-20k-3dss", true)]
    [InlineData("ftdx10-14074-20k", false)]
    [InlineData("ftdx10-14074-20k-cursor", false)]
    [InlineData("ftdx10-14074-20k-fix", false)]
    public void Scope_mode(string capture, bool is3dss)
    {
        foreach (var frame in Captures.Ftdx10(capture)) Assert.Equal(is3dss, frame.Main.Is3dss);
    }

    [Fact]
    public void A_1_kHz_tone_shows_on_AF_FFT_point_50()
    {
        // An AM carrier on 864 kHz, with the VFO 1 kHz below in USB.
        var frames = Captures.Ftdx10("ftdx10-ton-1k");
        var sum = new double[AudioScope.FftPointCount];
        foreach (var frame in frames)
        {
            var fft = frame.MainAudio.Fft;
            for (int i = 0; i < sum.Length; i++) sum[i] += fft[i];
        }
        int peak = Enumerable.Range(5, sum.Length - 5).MaxBy(i => sum[i]);
        Assert.InRange(peak, 48, 52);
    }

    [Theory]
    [InlineData("main-20k-place-center")]
    [InlineData("main-20k-place-cursor")]
    [InlineData("main-200k-fix")]
    [InlineData("audio-radio-scotland-810-am")]
    public void FTdx101_frames_still_read_as_FTdx101(string capture)
    {
        foreach (var frame in Captures.Step5(capture))
        {
            Assert.Equal(AccRadio.Ftdx101, frame.Radio);
            Assert.True(frame.HasSub);
        }
    }
}

/// <summary>
/// The FTDX10's audio scope settings, from captures taken with one setting
/// changed at a time.
/// </summary>
public class Ftdx10AudioSettingsTests
{
    [Theory]
    [InlineData("ftdx10-audio-base", 0, 0, 0)]
    [InlineData("ftdx10-audio-fftatt1", 1, 0, 0)]
    [InlineData("ftdx10-audio-fftatt2", 2, 0, 0)]
    [InlineData("ftdx10-audio-oscatt1", 0, 1, 0)]
    [InlineData("ftdx10-audio-time3", 0, 0, 3)]
    // The tuning captures were all taken with the time on 3 ms.
    [InlineData("ftdx10-14074-20k", 0, 0, 1)]
    public void Audio_scope_settings(string capture, int fftAtt, int oscAtt, int time)
    {
        foreach (var frame in Captures.Ftdx10(capture))
        {
            Assert.Equal(AccRadio.Ftdx10, frame.Radio);
            Assert.Equal(fftAtt, frame.MainAudio.FftAttenuator);
            Assert.Equal(oscAtt, frame.MainAudio.OscAttenuator);
            Assert.Equal(time, frame.MainAudio.OscTime);
        }
    }
}
