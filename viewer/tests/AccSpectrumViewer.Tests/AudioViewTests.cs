using System.Drawing;
using YaesuAccScope;

namespace AccSpectrumViewer.Tests;

public class AudioViewTests
{
    [Fact]
    public void Smoothing_moves_part_of_the_way()
    {
        var smoothed = new double[] { 100, 100 };

        AudioView.Smooth(smoothed, new byte[] { 200, 0 }, 0.5);

        Assert.Equal(new double[] { 150, 50 }, smoothed);
    }

    [Fact]
    public void The_first_frame_is_taken_whole()
    {
        var smoothed = new double[3];

        AudioView.Smooth(smoothed, new byte[] { 10, 20, 30 }, 1.0);

        Assert.Equal(new double[] { 10, 20, 30 }, smoothed);
    }

    [Fact]
    public void The_range_leaves_out_the_dc_point()
    {
        var fft = Enumerable.Repeat(60.0, AudioScope.FftPointCount).ToArray();
        fft[0] = 254;      // Main's DC point, always full scale
        fft[100] = 180;    // a 2 kHz tone

        var (low, peak) = AudioView.FftRange(fft);

        Assert.Equal(60, low);
        Assert.Equal(180, peak);
    }

    [Fact]
    public void The_floor_ignores_a_few_deep_points()
    {
        var fft = Enumerable.Repeat(80.0, AudioScope.FftPointCount).ToArray();
        for (int i = 1; i < 10; i++) fft[i] = 5;

        Assert.Equal(80, AudioView.FftRange(fft).Low);
    }

    [Theory]
    [InlineData(0, "1 ms")]
    [InlineData(4, "100 ms")]
    [InlineData(5, "300 ms")]
    [InlineData(6, "t6")]
    public void Time_names(int time, string name) => Assert.Equal(name, AudioView.OscTimeName(time));

    [Fact]
    public void Settings_line() =>
        Assert.Equal("FFT att 1   scope att 2   100 ms", AudioView.Settings(1, 2, 4));

    [Fact]
    public void Oscilloscope_bytes_fill_the_height()
    {
        var r = new Rectangle(0, 10, 100, 256);

        Assert.Equal(265f, AudioView.OscY(r, 0));
        Assert.Equal(10f, AudioView.OscY(r, 255));
    }

    [Fact]
    public void A_waterfall_row_is_the_average_of_its_frames()
    {
        var row = AudioView.AverageRow(new[] { 0, 400, 1020, 6 }, 4);

        Assert.Equal(new byte[] { 0, 100, 255, 2 }, row);
    }

    [Fact]
    public void The_waterfall_holds_a_whole_ft8_cycle_in_100_pixels()
    {
        // 25 frames a second; an FT8 cycle is 15 seconds.
        double secondsIn100Rows = 100.0 * AudioView.FramesPerRow / 25;

        Assert.True(secondsIn100Rows >= 15, $"{secondsIn100Rows} s");
        Assert.Equal(199, AudioView.FallPoints);
    }

    [Fact]
    public void The_waterfall_gets_more_height_than_the_oscilloscope()
    {
        Assert.True(AudioView.FftPercent(oscilloscope: false) < AudioView.FftPercent(oscilloscope: true));
    }

    [Fact]
    public void The_waterfall_is_shown_unless_the_oscilloscope_is_asked_for()
    {
        using var form = new MainForm();
        var views = AllControls(form).OfType<AudioView>().ToArray();
        var osc = AllControls(form).OfType<CheckBox>().Single(c => c.Text == "&Oscilloscope");

        Assert.All(views, v => Assert.False(v.ShowOscilloscope));
        osc.Checked = true;
        Assert.All(views, v => Assert.True(v.ShowOscilloscope));
    }

    [Fact]
    public void Each_receiver_has_an_audio_view()
    {
        using var form = new MainForm();
        var views = AllControls(form).OfType<AudioView>().Select(v => v.ReceiverName).ToArray();

        Assert.Equal(new[] { "Main", "Sub" }, views.OrderBy(n => n).ToArray());
    }

    private static IEnumerable<Control> AllControls(Control parent)
    {
        foreach (Control c in parent.Controls)
        {
            yield return c;
            foreach (var child in AllControls(c)) yield return child;
        }
    }
}
