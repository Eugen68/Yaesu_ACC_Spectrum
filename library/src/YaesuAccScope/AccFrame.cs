// YaesuAccScope: reads a Yaesu FTdx101's own scope data from its ACC socket.
// Copyright (C) 2026 Colin Campbell MM5AGM
// SPDX-License-Identifier: LGPL-3.0-only (see COPYING.LESSER and COPYING)

using System;
using System.Collections.Generic;

namespace YaesuAccScope
{
    /// <summary>Where the radio puts the scope window, from its span byte.</summary>
    public enum ScopePlacement
    {
        /// <summary>The byte held a value that isn't known.</summary>
        Unknown = -1,

        /// <summary>The window moves with the cursor.</summary>
        Cursor = 0,

        /// <summary>The window is centred on the VFO.</summary>
        Center = 1,

        /// <summary>The window stays put; the radio keeps a separate span for Fix.</summary>
        Fix = 2,
    }

    /// <summary>Which radio sent a frame, worked out from the frame's layout.</summary>
    public enum AccRadio
    {
        /// <summary>
        /// The FTdx101MP or D: two receivers, and the tuning for both in bytes
        /// 3036 to 3101.
        /// </summary>
        Ftdx101 = 0,

        /// <summary>
        /// The FTDX10: one receiver, and its tuning in bytes 3030 to 3049. Bytes
        /// 3050 on are zero, and the Sub rows and Sub audio are zero.
        /// </summary>
        Ftdx10 = 1,
    }

    /// <summary>
    /// One frame from the FTdx101's ACC socket, decoded. Everything comes from
    /// the frame itself, with nothing from CAT. See docs/protocol.md for the
    /// layout.
    /// </summary>
    public sealed class AccFrame
    {
        /// <summary>
        /// The shortest frame that holds every field decoded here. The radio's
        /// frames are 4096 bytes, with zeros from byte 3102 on.
        /// </summary>
        public const int MinLength = 3102;

        /// <summary>Points in each receiver's scope row.</summary>
        public const int Points = 850;

        /// <summary>
        /// The FTdx101's placement codes, by the span byte's top 2 bits: 0 Cursor,
        /// 1 Center, 2 Fix.
        /// </summary>
        private static readonly ScopePlacement[] Ftdx101Placements = { ScopePlacement.Cursor, ScopePlacement.Center, ScopePlacement.Fix };

        /// <summary>
        /// The FTDX10's placement codes, by the span byte's top 2 bits: 0 Center,
        /// 1 Cursor, 2 Fix. Measured on 2026-11-20.
        /// </summary>
        private static readonly ScopePlacement[] Ftdx10Placements = { ScopePlacement.Center, ScopePlacement.Cursor, ScopePlacement.Fix };

        private AccFrame(byte[] raw) : this(raw, DetectRadio(raw)) { }

        private AccFrame(byte[] raw, AccRadio radio)
        {
            Raw = raw;
            Radio = radio;
            if (Radio == AccRadio.Ftdx10)
            {
                // FTDX10, from captures/ftdx10: the span byte at 3030, the passband
                // marker at 3034, the VFO at 3038 (and again at 3042), the left
                // edge at 3046. There's no Sub receiver: its fields read zero.
                Main = new ReceiverScope(raw, 0, vfoAt: 3038, leftAt: 3046, spanAt: 3030, modeAt: 2917, passbandAt: 3034, Ftdx10Placements);
                Sub = new ReceiverScope(raw, Points, vfoAt: 3094, leftAt: 3098, spanAt: 3084, modeAt: 2918, passbandAt: 3040, Ftdx101Placements);
            }
            else
            {
                Main = new ReceiverScope(raw, 0, vfoAt: 3068, leftAt: 3076, spanAt: 3062, modeAt: 2917, passbandAt: 3036, Ftdx101Placements);
                Sub = new ReceiverScope(raw, Points, vfoAt: 3094, leftAt: 3098, spanAt: 3084, modeAt: 2918, passbandAt: 3040, Ftdx101Placements);
            }
            // The audio scope settings byte: 3056 on the FTdx101, 3026 on the FTDX10,
            // with the same bits (captures/ftdx10/ftdx10-audio-*.bin).
            int settingsAt = Radio == AccRadio.Ftdx10 ? AudioScope.Ftdx10SettingsAt : AudioScope.Ftdx101SettingsAt;
            MainAudio = new AudioScope(raw, fftAt: 1700, oscAt: 1900, settingsAt);
            SubAudio = new AudioScope(raw, fftAt: 2300, oscAt: 2500, settingsAt);
        }

        /// <summary>The frame's bytes, as they came from the radio.</summary>
        public byte[] Raw { get; }

        /// <summary>
        /// Which radio sent the frame, worked out from its layout, or the radio it
        /// was decoded as with <see cref="Parse(byte[], AccRadio)"/>.
        /// </summary>
        public AccRadio Radio { get; }

        /// <summary>
        /// True when the radio has a Sub receiver. The FTDX10 has none, and its
        /// <see cref="Sub"/> and <see cref="SubAudio"/> read zero.
        /// </summary>
        public bool HasSub => Radio != AccRadio.Ftdx10;

        /// <summary>The Main receiver's scope, VFO A.</summary>
        public ReceiverScope Main { get; }

        /// <summary>The Sub receiver's scope, VFO B.</summary>
        public ReceiverScope Sub { get; }

        /// <summary>Main's audio scope: the AF-FFT and the oscilloscope.</summary>
        public AudioScope MainAudio { get; }

        /// <summary>Sub's audio scope: the AF-FFT and the oscilloscope.</summary>
        public AudioScope SubAudio { get; }

        /// <summary>
        /// The Band button on the radio's scope: false for MAIN, true for SUB
        /// (byte 2916).
        /// </summary>
        public bool SubBandSelected => Raw[2916] != 0;

        /// <summary>Decodes a frame. Throws if it's shorter than <see cref="MinLength"/>.</summary>
        public static AccFrame Parse(byte[] frame)
        {
            if (frame == null) throw new ArgumentNullException(nameof(frame));
            if (frame.Length < MinLength)
                throw new ArgumentException($"A frame needs at least {MinLength} bytes; this one has {frame.Length}.", nameof(frame));
            return new AccFrame(frame);
        }

        /// <summary>
        /// Decodes a frame as the given radio's layout, whatever the frame looks
        /// like. <see cref="AccSerialReader"/> uses this with
        /// <see cref="AccRadioSettler"/> so one odd frame can't flip the layout.
        /// Throws if it's shorter than <see cref="MinLength"/>.
        /// </summary>
        public static AccFrame Parse(byte[] frame, AccRadio radio)
        {
            Parse(frame);
            return new AccFrame(frame, radio);
        }

        /// <summary>Decodes a frame, or returns false if it's too short.</summary>
        public static bool TryParse(byte[] frame, out AccFrame? decoded)
        {
            decoded = frame != null && frame.Length >= MinLength ? new AccFrame(frame) : null;
            return decoded != null;
        }

        /// <summary>
        /// An FTDX10 frame ends at byte 3049, so bytes 3050 to 3101, where the
        /// FTdx101 keeps both VFOs, are zero, and its own VFO sits at 3038.
        /// </summary>
        private static AccRadio DetectRadio(byte[] raw)
        {
            for (int i = 3050; i < MinLength; i++)
                if (raw[i] != 0) return AccRadio.Ftdx101;
            long vfo = ReadUInt32BigEndian(raw, 3038);
            return vfo >= 30_000 && vfo <= 75_000_000 ? AccRadio.Ftdx10 : AccRadio.Ftdx101;
        }

        internal static long ReadUInt32BigEndian(byte[] b, int at) =>
            ((long)b[at] << 24) | ((long)b[at + 1] << 16) | ((long)b[at + 2] << 8) | b[at + 3];
    }

    /// <summary>One receiver's scope row and where it sits in frequency.</summary>
    public sealed class ReceiverScope
    {
        /// <summary>
        /// The radio's span list, by the low 4 bits of the span byte.
        /// </summary>
        public static readonly int[] Spans = { 1_000, 2_000, 5_000, 10_000, 20_000, 50_000, 100_000, 200_000, 500_000, 1_000_000 };

        /// <summary>
        /// Points the span covers. The 850 points cover a little more than the
        /// span: the span is the middle 791, with about 30 points beyond each
        /// edge. Measured at every span from 1k to 1M (788 to 795, 791 on average).
        /// </summary>
        public const int SpanPoints = 791;

        /// <summary>
        /// The point the centre frequency falls on. A steady carrier tuned to the
        /// VFO in Center sat on point 425 of 0 to 849.
        /// </summary>
        public const int CentrePoint = 425;

        private readonly byte[] _raw;
        private readonly int _levelsAt;
        private readonly int _spanAt;
        private readonly int _modeAt;
        private readonly ScopePlacement[] _placements;
        private byte[]? _levels;

        internal ReceiverScope(byte[] raw, int levelsAt, int vfoAt, int leftAt, int spanAt, int modeAt, int passbandAt,
                               ScopePlacement[] placements)
        {
            _raw = raw;
            _placements = placements;
            _levelsAt = levelsAt;
            _spanAt = spanAt;
            _modeAt = modeAt;
            VfoHz = AccFrame.ReadUInt32BigEndian(raw, vfoAt);
            LeftEdgeHz = AccFrame.ReadUInt32BigEndian(raw, leftAt);
            PassbandMarkerHz = AccFrame.ReadUInt32BigEndian(raw, passbandAt);
        }

        /// <summary>
        /// The 850 levels, low frequency first, turned the right way up so a
        /// higher number is a stronger signal. The radio sends each byte inverted.
        /// Each step is about 0.42 dB (measured with the radio's attenuator,
        /// within about 10%), with no absolute zero: the radio's scope LEVEL
        /// shifts them, and too low a LEVEL puts the floor on 0 or 1.
        /// </summary>
        public byte[] Levels
        {
            get
            {
                if (_levels == null)
                {
                    var levels = new byte[AccFrame.Points];
                    for (int i = 0; i < levels.Length; i++) levels[i] = (byte)~_raw[_levelsAt + i];
                    _levels = levels;
                }
                return _levels;
            }
        }

        /// <summary>The receiver's VFO in Hz: VFO A for Main, VFO B for Sub.</summary>
        public long VfoHz { get; }

        /// <summary>
        /// The left edge of the span in Hz, as the radio sends it. Right in Cursor
        /// and Fix; in Center the radio doesn't keep it up to date, so use
        /// <see cref="StartHz"/> instead.
        /// </summary>
        public long LeftEdgeHz { get; }

        /// <summary>
        /// Where the radio draws the receiver's passband marker: VFO + 1500 Hz +
        /// IF shift.
        /// </summary>
        public long PassbandMarkerHz { get; }

        /// <summary>The span byte's low 4 bits: the place in <see cref="Spans"/>.</summary>
        public int SpanIndex => _raw[_spanAt] & 0x0F;

        /// <summary>The span the radio shows, in Hz, or 0 if the span byte isn't known.</summary>
        public int SpanHz => SpanIndex < Spans.Length ? Spans[SpanIndex] : 0;

        /// <summary>
        /// Center, Cursor or Fix, from the span byte's top 2 bits. The FTdx101
        /// and the FTDX10 number them differently; this is already sorted out.
        /// </summary>
        public ScopePlacement Placement
        {
            get
            {
                int p = _raw[_spanAt] >> 6;
                return p < _placements.Length ? _placements[p] : ScopePlacement.Unknown;
            }
        }

        /// <summary>
        /// The radio's scope mode byte, its SS P3 character: 0 to 2 are 3DSS, 3
        /// is the waterfall in Center, 6 Cursor and 9 Fix. Sub has shown 5 in
        /// Center too. The FTDX10 sends 0 for 3DSS, 4 Center, 7 Cursor and 10 Fix.
        /// </summary>
        public byte ScopeMode => _raw[_modeAt];

        /// <summary>
        /// True when the radio draws this receiver in 3DSS. The levels are the same
        /// either way; it's only how the radio draws them.
        /// </summary>
        public bool Is3dss => ScopeMode <= 2;

        /// <summary>
        /// True when the VFO, span and placement make sense: the VFO between 30 kHz
        /// and 75 MHz, and a known span and placement.
        /// </summary>
        public bool IsValid =>
            VfoHz >= 30_000 && VfoHz <= 75_000_000 && SpanHz > 0 && Placement != ScopePlacement.Unknown;

        /// <summary>
        /// The centre of the span in Hz: the VFO in Center, the left edge plus half
        /// the span in Cursor and Fix.
        /// </summary>
        public long CentreHz => Placement == ScopePlacement.Center ? VfoHz : LeftEdgeHz + SpanHz / 2;

        /// <summary>The left edge of the span in Hz, in every placement.</summary>
        public long StartHz => CentreHz - SpanHz / 2;

        /// <summary>The right edge of the span in Hz, in every placement.</summary>
        public long EndHz => CentreHz + SpanHz / 2;

        /// <summary>Hz from one point to the next.</summary>
        public double HzPerPoint => (double)SpanHz / SpanPoints;

        /// <summary>
        /// The frequency of a point, 0 to 849, in Hz. Points below about 30 and
        /// above about 820 lie outside the span.
        /// </summary>
        public double FrequencyAt(int point) => CentreHz + (point - CentrePoint) * HzPerPoint;

        /// <summary>The nearest point to a frequency. It can be outside 0 to 849.</summary>
        public int PointAt(double hz) =>
            SpanHz == 0 ? CentrePoint : (int)Math.Round(CentrePoint + (hz - CentreHz) / HzPerPoint);
    }

    /// <summary>
    /// One receiver's audio scope: the AF-FFT and the oscilloscope the radio can
    /// show. Each receiver has 600 bytes: 200 for the AF-FFT, then 400 for the
    /// oscilloscope (Main from byte 1700, Sub from byte 2300). Both come before
    /// the AF gain, so the AF gain knob doesn't change them. The time and the two
    /// attenuators are one setting on the radio, shared by both receivers.
    /// </summary>
    public sealed class AudioScope
    {
        /// <summary>Columns in the oscilloscope, each a max byte and a min byte.</summary>
        public const int OscColumns = 200;

        /// <summary>Points in the AF-FFT.</summary>
        public const int FftPointCount = 200;

        /// <summary>Hz between AF-FFT points: point 0 is 0 Hz and point 199 is 3,980 Hz.</summary>
        public const double HzPerPoint = 20.0;

        /// <summary>The byte holding the shared audio scope settings on the FTdx101.</summary>
        internal const int Ftdx101SettingsAt = 3056;

        /// <summary>
        /// The same byte on the FTDX10, with the same bits. Found on 2026-11-21 by
        /// changing one setting at a time.
        /// </summary>
        internal const int Ftdx10SettingsAt = 3026;

        /// <summary>The oscilloscope time settings t0 to t5, as the radio's CAT manual names them.</summary>
        public static IReadOnlyList<string> OscTimeNames { get; } = new[] { "1 ms", "3 ms", "10 ms", "30 ms", "100 ms", "300 ms" };

        private readonly byte[] _raw;
        private readonly int _fftAt;
        private readonly int _oscAt;
        private readonly int _settingsAt;
        private byte[]? _fft;

        internal AudioScope(byte[] raw, int fftAt, int oscAt, int settingsAt)
        {
            _raw = raw;
            _settingsAt = settingsAt;
            _fftAt = fftAt;
            _oscAt = oscAt;
        }

        /// <summary>Points in the AF-FFT, 200 for both receivers.</summary>
        public int FftPoints => FftPointCount;

        /// <summary>The audio frequency of AF-FFT point <paramref name="point"/>.</summary>
        public static double HzAt(int point) => point * HzPerPoint;

        /// <summary>The nearest AF-FFT point to an audio frequency, kept within 0 to 199.</summary>
        public static int PointAt(double hz) =>
            Math.Max(0, Math.Min(FftPointCount - 1, (int)Math.Round(hz / HzPerPoint)));

        /// <summary>
        /// The AF-FFT, 200 points from 0 Hz to 3,980 Hz, 20 Hz a point, turned the
        /// right way up so higher is stronger. Point 0 is the DC point and always
        /// reads full scale, so a display should skip it. Measured on both
        /// receivers: a 1 kHz tone on point 50 and 2 kHz on point 100.
        /// </summary>
        public byte[] Fft
        {
            get
            {
                if (_fft == null)
                {
                    var fft = new byte[FftPoints];
                    for (int i = 0; i < fft.Length; i++) fft[i] = (byte)~_raw[_fftAt + i];
                    _fft = fft;
                }
                return _fft;
            }
        }

        /// <summary>The top of oscilloscope column <paramref name="column"/>, 0 to 199, centred on 128.</summary>
        public byte OscMax(int column) => _raw[_oscAt + 2 * CheckColumn(column)];

        /// <summary>The bottom of oscilloscope column <paramref name="column"/>, 0 to 199, centred on 128.</summary>
        public byte OscMin(int column) => _raw[_oscAt + 2 * CheckColumn(column) + 1];

        /// <summary>
        /// The oscilloscope time setting, t0 to t5 as 0 to 5 (see
        /// <see cref="OscTimeNames"/>). Measured at t0: about 20 us a column, so
        /// about 4 ms across. Each column holds the highest and lowest sample in
        /// its time, so slower settings show the envelope rather than cycles.
        /// Shared by both receivers.
        /// </summary>
        public int OscTime => _raw[_settingsAt] & 0x07;

        /// <summary>
        /// The oscilloscope attenuator, 0 to 2. At 0 a strong signal clips at
        /// about 2 and 250. Shared by both receivers.
        /// </summary>
        public int OscAttenuator => (_raw[_settingsAt] >> 3) & 0x03;

        /// <summary>The AF-FFT attenuator, 0 to 2. Shared by both receivers.</summary>
        public int FftAttenuator => (_raw[_settingsAt] >> 5) & 0x03;

        private static int CheckColumn(int column)
        {
            if (column < 0 || column >= OscColumns) throw new ArgumentOutOfRangeException(nameof(column));
            return column;
        }
    }
}
