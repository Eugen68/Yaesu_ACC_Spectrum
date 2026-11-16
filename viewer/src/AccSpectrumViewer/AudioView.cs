// ACC Spectrum Viewer: shows the scope of a Yaesu FTdx101 from its ACC socket.
// Copyright (C) 2026 Colin Campbell MM5AGM
// SPDX-License-Identifier: GPL-3.0-only (see LICENSE in the top folder)

using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using YaesuAccScope;

namespace AccSpectrumViewer;

/// <summary>
/// One receiver's audio scope: the AF-FFT from 0 to 4 kHz above an audio
/// waterfall made from it, or above the radio's oscilloscope. The radio has one
/// time and attenuator setting for both receivers; they're set on the radio.
/// </summary>
internal sealed class AudioView : Control
{
    private const int FftPoints = AudioScope.FftPointCount;
    private const int Columns = AudioScope.OscColumns;

    /// <summary>The AF-FFT point the trace starts at: point 0 is DC, and on Main always full scale.</summary>
    internal const int FirstPoint = 1;

    /// <summary>How much of each new AF-FFT goes into the trace, to steady it a little.</summary>
    internal const double FftFollow = 0.5;

    /// <summary>The AF-FFT points across the waterfall: all but DC.</summary>
    internal const int FallPoints = FftPoints - FirstPoint;

    /// <summary>
    /// Frames averaged into each waterfall row. At 25 frames a second that's
    /// about 6 rows a second, so 100 pixels of waterfall hold 16 seconds: a
    /// whole FT8 cycle. Averaging also steadies the noise, so weak tones show.
    /// </summary>
    internal const int FramesPerRow = 4;

    /// <summary>How fast the waterfall's colours follow the levels, a share a row: 95% in about 3 seconds.</summary>
    internal const double FallFollow = 0.15;

    private const int FallRows = 400;

    private readonly int[] _fall = new int[FallPoints * FallRows];
    private readonly Bitmap _fallBitmap = new(FallPoints, FallRows, PixelFormat.Format32bppRgb);
    private readonly int[] _rowSum = new int[FallPoints];
    private int _rowFrames;
    private bool _fallChanged;
    private ColourStretch? _stretch;
    private readonly double[] _fft = new double[FftPoints];
    private readonly byte[] _oscMax = new byte[Columns];
    private readonly byte[] _oscMin = new byte[Columns];
    private readonly Pen _fftPen = new(Color.FromArgb(0x4F, 0xC3, 0xF7));
    private readonly Brush _fftFill = new SolidBrush(Color.FromArgb(0x30, 0x4F, 0xC3, 0xF7));
    private readonly Brush _oscBrush = new SolidBrush(Color.FromArgb(0x66, 0xBB, 0x6A));
    private readonly Pen _gridPen = new(Color.FromArgb(0x22, 0x22, 0x22));
    private readonly Pen _edgePen = new(Color.FromArgb(0x33, 0x33, 0x33));
    private readonly Brush _scaleBrush = new SolidBrush(Color.FromArgb(0x88, 0xAA, 0xAA));
    private readonly Font _titleFont;
    private readonly Font _scaleFont;
    private bool _hasData;
    private double? _top;
    private double? _low;
    private int _oscTime, _oscAtt, _fftAtt;

    public AudioView(string name)
    {
        ReceiverName = name;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint |
                 ControlStyles.ResizeRedraw, true);
        BackColor = Color.Black;
        ForeColor = Color.White;
        _titleFont = new Font(DefaultFont, FontStyle.Bold);
        _scaleFont = new Font(DefaultFont.FontFamily, DefaultFont.Size * 0.85f);
    }

    /// <summary>"Main" or "Sub".</summary>
    public string ReceiverName { get; }

    /// <summary>True to draw the radio's oscilloscope under the AF-FFT instead of the audio waterfall.</summary>
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool ShowOscilloscope { get; set; }

    /// <summary>Adds a frame's audio scope. Call <see cref="Control.Invalidate()"/> after.</summary>
    public void AddAudio(AudioScope audio)
    {
        var fft = audio.Fft;
        Smooth(_fft, fft, _hasData ? FftFollow : 1.0);
        AddToRow(fft);
        for (int c = 0; c < Columns; c++)
        {
            _oscMax[c] = audio.OscMax(c);
            _oscMin[c] = audio.OscMin(c);
        }
        var (low, peak) = FftRange(_fft);
        _top = ScopeView.NextTraceTop(_top, (int)Math.Ceiling(peak));
        _low = _low == null ? low : _low.Value + (low - _low.Value) * 0.05;
        _oscTime = audio.OscTime;
        _oscAtt = audio.OscAttenuator;
        _fftAtt = audio.FftAttenuator;
        _hasData = true;
    }

    // Adds a frame's AF-FFT to the waterfall row being built, and when the row
    // has its frames, colours it and puts it at the top.
    private void AddToRow(byte[] fft)
    {
        for (int i = 0; i < FallPoints; i++) _rowSum[i] += fft[i + FirstPoint];
        if (++_rowFrames < FramesPerRow) return;

        var row = AverageRow(_rowSum, _rowFrames);
        Array.Clear(_rowSum);
        _rowFrames = 0;
        var target = ColourStretch.From(row);
        _stretch = _stretch == null ? target : _stretch.Towards(target, FallFollow);
        Array.Copy(_fall, 0, _fall, FallPoints, FallPoints * (FallRows - 1));
        var lookup = _stretch.Lookup;
        for (int i = 0; i < FallPoints; i++) _fall[i] = Palette.Waterfall[lookup[row[i]]];
        _fallChanged = true;
    }

    /// <summary>The average of the frames summed into a row, rounded.</summary>
    internal static byte[] AverageRow(int[] sum, int frames)
    {
        var row = new byte[sum.Length];
        for (int i = 0; i < sum.Length; i++) row[i] = (byte)Math.Clamp((sum[i] + frames / 2) / frames, 0, 255);
        return row;
    }

    /// <summary>
    /// The share of the audio area the AF-FFT takes: more when the oscilloscope
    /// is under it, less with the waterfall, which needs the height to hold a
    /// whole FT8 cycle.
    /// </summary>
    internal static int FftPercent(bool oscilloscope) => oscilloscope ? 60 : 45;

    /// <summary>Moves each smoothed point a share of the way to the new AF-FFT.</summary>
    internal static void Smooth(double[] smoothed, byte[] latest, double follow)
    {
        for (int i = 0; i < smoothed.Length; i++)
            smoothed[i] += (latest[i] - smoothed[i]) * follow;
    }

    /// <summary>
    /// The AF-FFT's floor (the 10th percentile, so a few deep points don't drag
    /// it down) and its peak, leaving out the DC point.
    /// </summary>
    internal static (double Low, double Peak) FftRange(double[] fft)
    {
        var sorted = fft.Skip(FirstPoint).OrderBy(v => v).ToArray();
        return (sorted[sorted.Length / 10], sorted[^1]);
    }

    /// <summary>The oscilloscope time setting as the radio names it, or "t6" and so on if it's new.</summary>
    internal static string OscTimeName(int time) =>
        time >= 0 && time < AudioScope.OscTimeNames.Count ? AudioScope.OscTimeNames[time] : "t" + time;

    /// <summary>The line under the title: the radio's audio scope settings, shared by both receivers.</summary>
    internal static string Settings(int fftAtt, int oscAtt, int oscTime) =>
        $"FFT att {fftAtt}   scope att {oscAtt}   {OscTimeName(oscTime)}";

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(BackColor);
        int titleHeight = _titleFont.Height + _scaleFont.Height + 8;
        const TextFormatFlags flags = TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis;
        TextRenderer.DrawText(g, ReceiverName + " audio", _titleFont, new Rectangle(4, 3, Width - 8, _titleFont.Height), ForeColor, flags);
        if (_hasData)
            TextRenderer.DrawText(g, Settings(_fftAtt, _oscAtt, _oscTime), _scaleFont,
                new Rectangle(4, 4 + _titleFont.Height, Width - 8, _scaleFont.Height), Color.Gray, flags);

        var area = new Rectangle(0, titleHeight, Width, Math.Max(Height - titleHeight, 1));
        int fftHeight = Math.Max(area.Height * FftPercent(ShowOscilloscope) / 100, 30);
        var fft = new Rectangle(area.X, area.Y, area.Width, fftHeight);
        var below = new Rectangle(area.X, fft.Bottom + 4, area.Width, Math.Max(area.Bottom - fft.Bottom - 4, 1));
        DrawFft(g, fft);
        if (ShowOscilloscope) DrawOscilloscope(g, below);
        else DrawWaterfall(g, below);
    }

    // Newest row at the top, one row of pixels each, so its speed doesn't
    // change with the window's height. Lined up with the AF-FFT above it.
    private void DrawWaterfall(Graphics g, Rectangle r)
    {
        g.DrawRectangle(_edgePen, r.Left, r.Top, r.Width - 1, r.Height - 1);
        if (!_hasData) return;
        if (_fallChanged)
        {
            var data = _fallBitmap.LockBits(new Rectangle(0, 0, FallPoints, FallRows), ImageLockMode.WriteOnly, PixelFormat.Format32bppRgb);
            try
            {
                for (int row = 0; row < FallRows; row++)
                    Marshal.Copy(_fall, row * FallPoints, data.Scan0 + row * data.Stride, FallPoints);
            }
            finally
            {
                _fallBitmap.UnlockBits(data);
            }
            _fallChanged = false;
        }
        int rows = Math.Min(FallRows, r.Height);
        int left = (int)Math.Round(FftX(r, FirstPoint));
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        g.DrawImage(_fallBitmap, new Rectangle(left, r.Y, r.Right - left, rows), new Rectangle(0, 0, FallPoints, rows), GraphicsUnit.Pixel);
        g.PixelOffsetMode = PixelOffsetMode.Default;
    }

    // x of an AF-FFT point, with 0 Hz at the left edge and 4 kHz at the right.
    private static float FftX(Rectangle r, double point) => (float)(r.Left + point * r.Width / FftPoints);

    private void DrawFft(Graphics g, Rectangle r)
    {
        // A line each kHz, labelled.
        for (int khz = 0; khz <= 4; khz++)
        {
            float x = FftX(r, AudioScope.PointAt(khz * 1000.0) + (khz == 4 ? 1 : 0));
            x = Math.Min(x, r.Right - 1);
            g.DrawLine(khz == 0 || khz == 4 ? _edgePen : _gridPen, x, r.Top, x, r.Bottom);
            if (khz > 0 && khz < 4)
            {
                using var centred = new StringFormat { Alignment = StringAlignment.Center };
                g.DrawString(khz + " kHz", _scaleFont, _scaleBrush, x, r.Top + 2, centred);
            }
        }
        for (int q = 1; q < 4; q++)
        {
            int y = r.Top + r.Height * q / 4;
            g.DrawLine(_gridPen, r.Left, y, r.Right, y);
        }
        if (!_hasData) return;

        var (low, range) = ScopeView.TraceScale((int)Math.Floor(_low ?? 0), Math.Max(_top ?? 255, (_low ?? 0) + 20));
        int count = FftPoints - FirstPoint;
        var line = new PointF[count];
        for (int j = 0; j < count; j++)
        {
            int i = j + FirstPoint;
            double y = r.Bottom - 1 - (_fft[i] - low) * (r.Height - 2) / range;
            line[j] = new PointF(FftX(r, i + 0.5), (float)Math.Clamp(y, r.Top, r.Bottom - 1));
        }
        var shape = new PointF[count + 2];
        shape[0] = new PointF(line[0].X, r.Bottom - 1);
        Array.Copy(line, 0, shape, 1, count);
        shape[^1] = new PointF(line[^1].X, r.Bottom - 1);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.FillPolygon(_fftFill, shape);
        g.DrawLines(_fftPen, line);
        g.SmoothingMode = SmoothingMode.None;
    }

    private void DrawOscilloscope(Graphics g, Rectangle r)
    {
        g.DrawRectangle(_edgePen, r.Left, r.Top, r.Width - 1, r.Height - 1);
        int mid = r.Top + r.Height / 2;
        g.DrawLine(_gridPen, r.Left, mid, r.Right, mid);
        if (!_hasData) return;

        // Each column is the highest and lowest sample in its time, 128 the middle.
        float columnWidth = (float)r.Width / Columns;
        for (int c = 0; c < Columns; c++)
        {
            float top = OscY(r, _oscMax[c]), bottom = OscY(r, _oscMin[c]);
            g.FillRectangle(_oscBrush, r.Left + c * columnWidth, top, Math.Max(columnWidth, 1f), Math.Max(bottom - top, 1f));
        }
    }

    /// <summary>y of an oscilloscope byte: 255 at the top, 0 at the bottom.</summary>
    internal static float OscY(Rectangle r, byte value) => r.Bottom - 1 - value * (r.Height - 1) / 255f;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _fallBitmap.Dispose();
            _fftPen.Dispose();
            _fftFill.Dispose();
            _oscBrush.Dispose();
            _gridPen.Dispose();
            _edgePen.Dispose();
            _scaleBrush.Dispose();
            _titleFont.Dispose();
            _scaleFont.Dispose();
        }
        base.Dispose(disposing);
    }
}
