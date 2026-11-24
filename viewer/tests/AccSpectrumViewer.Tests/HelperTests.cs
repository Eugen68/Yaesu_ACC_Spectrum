using AccFirmwareFlasher;

namespace AccSpectrumViewer.Tests;

public class ColourStretchTests
{
    [Fact]
    public void Black_is_the_median_and_white_the_strongest()
    {
        var levels = Enumerable.Repeat((byte)40, 800).Concat(Enumerable.Repeat((byte)120, 50)).ToArray();

        var s = ColourStretch.From(levels);

        Assert.Equal(40, s.Low);
        Assert.Equal(120, s.High);
        Assert.Equal(0, s.Lookup[40]);
        Assert.Equal(0, s.Lookup[10]);
        Assert.Equal(255, s.Lookup[120]);
        Assert.Equal(255, s.Lookup[200]);
        Assert.InRange(s.Lookup[80], 120, 135);
    }

    [Fact]
    public void A_flat_trace_still_has_a_range()
    {
        var s = ColourStretch.From(Enumerable.Repeat((byte)50, 850).ToArray());

        Assert.Equal(50, s.Low);
        Assert.Equal(70, s.High);
    }

    [Fact]
    public void A_trace_at_the_top_stays_in_range()
    {
        var s = ColourStretch.From(Enumerable.Repeat((byte)255, 850).ToArray());

        Assert.True(s.Low < s.High);
        Assert.Equal(255, s.High);
    }

    [Fact]
    public void The_trace_bottom_is_below_the_noise()
    {
        var levels = Enumerable.Range(0, 850).Select(i => (byte)(i % 100)).ToArray();

        var s = ColourStretch.From(levels);

        Assert.True(s.TraceLow <= s.Low);
        Assert.InRange(s.TraceLow, 0, 10);
    }

    [Fact]
    public void Following_moves_part_of_the_way_each_frame()
    {
        // Switched on: flat, low levels first, then real noise and signals.
        var startup = ColourStretch.From(Enumerable.Repeat((byte)5, 850).ToArray());
        var real = ColourStretch.From(Enumerable.Repeat((byte)60, 800).Concat(Enumerable.Repeat((byte)160, 50)).ToArray());

        var s = startup.Towards(real, 0.04);
        Assert.InRange(s.Low, 6, 7);

        for (int i = 0; i < 125; i++) s = s.Towards(real, 0.04);   // 5 seconds at 25 a second
        Assert.InRange(s.Low, 58, 60);
        Assert.InRange(s.High, 156, 160);
        Assert.True(s.Low < s.High);
    }

    [Fact]
    public void Following_the_same_levels_stays_put()
    {
        var a = ColourStretch.From(Enumerable.Range(0, 850).Select(i => (byte)(40 + i % 60)).ToArray());
        var b = a.Towards(a, 0.02);

        Assert.Equal(a.Low, b.Low);
        Assert.Equal(a.High, b.High);
        Assert.Equal(a.Lookup, b.Lookup);
    }

    [Fact]
    public void The_waterfall_runs_black_to_white()
    {
        Assert.Equal(unchecked((int)0xFF000000), Palette.Waterfall[0]);
        Assert.Equal(unchecked((int)0xFFFFFFFF), Palette.Waterfall[255]);
        Assert.Equal(unchecked((int)0xFF00008C), Palette.Waterfall[60]);
        Assert.All(Palette.Waterfall, c => Assert.Equal(0xFF, (c >> 24) & 0xFF));
    }
}

public class BoardPortsTests
{
    [Theory]
    [InlineData("VID_1A86&PID_55D3", 0x1A86, 0x55D3)]
    [InlineData("VID_303A&PID_1001&MI_00", 0x303A, 0x1001)]
    [InlineData("vid_10c4&pid_ea60", 0x10C4, 0xEA60)]
    public void Reads_usb_ids(string device, int vid, int pid)
    {
        Assert.True(BoardPorts.TryParseIds(device, out int v, out int p));
        Assert.Equal(vid, v);
        Assert.Equal(pid, p);
    }

    [Theory]
    [InlineData("ROOT_HUB30")]
    [InlineData("VID_1A86")]
    [InlineData("VID_ZZZZ&PID_55D3")]
    public void Ignores_other_names(string device) => Assert.False(BoardPorts.TryParseIds(device, out _, out _));

    [Theory]
    [InlineData(0x1A86, 0x55D3, true)]     // the board's CH343
    [InlineData(0x303A, 0x1001, true)]     // the ESP32-S3's own port
    [InlineData(0x10C4, 0xEA60, false)]    // CP210x: the FTdx101's CAT ports
    [InlineData(0x1A86, 0x7523, false)]    // a CH340 on something else
    [InlineData(0x303A, 0x0002, false)]
    public void Only_the_boards_ids_are_board_ports(int vid, int pid, bool board) =>
        Assert.Equal(board, BoardPorts.IsBoardId(vid, pid));

    [Fact]
    public void Says_which_socket()
    {
        Assert.Equal("COM5: the board's COM socket (CH343)", new BoardPort("COM5", 0x1A86, 0x55D3, "x").ToString());
        Assert.Equal("COM6: the board's USB socket (ESP32-S3)", new BoardPort("COM6", 0x303A, 0x1001, "x").ToString());
    }
}

public class EspToolTests
{
    [Fact]
    public void Writes_the_merged_file_at_zero()
    {
        var args = EspTool.WriteFlashArguments("COM5", @"C:\f\yaesu-acc-firmware-2.0.1-merged.bin");

        Assert.Equal(new[] { "--chip", "esp32s3", "--port", "COM5", "--baud", "460800", "write-flash", "0x0",
                             @"C:\f\yaesu-acc-firmware-2.0.1-merged.bin" }, args);
    }

    [Fact]
    public void Writes_several_pieces_each_at_its_address()
    {
        var args = EspTool.WriteFlashArguments("COM5", new[] { (0, @"C:\t\part-0x0.bin"), (0x10000, @"C:\t\part-0x10000.bin") });

        Assert.Equal(new[] { "--chip", "esp32s3", "--port", "COM5", "--baud", "460800", "write-flash",
                             "0x0", @"C:\t\part-0x0.bin", "0x10000", @"C:\t\part-0x10000.bin" }, args);
    }

    [Theory]
    [InlineData("Writing at 0x00010000 [===>        ]  35.2% 65536/185344 bytes...", 35.2)]
    [InlineData("Writing at 0x0002d4a0 (100 %)", 100)]
    [InlineData("Writing at 0x00000000... (7 %)", 7)]
    public void Reads_progress(string line, double expected) => Assert.Equal(expected, EspTool.ProgressIn(line));

    [Theory]
    [InlineData("Connecting....")]
    [InlineData("Chip type: ESP32-S3 (QFN56) (revision v0.2)")]
    [InlineData("Wrote 1234 bytes at 0x00000000 in 1.2 seconds (500 %)")]
    public void Other_lines_have_no_progress(string line) => Assert.Null(EspTool.ProgressIn(line));
}

public class MergedImageTests
{
    // A merged file laid out like the board's: bootloader at 0, the partition
    // table at 0x8000, NVS at 0x9000 (0x5000), otadata at 0xE000, app at 0x10000.
    private static byte[] Image(int length = 0x20000)
    {
        var b = new byte[length];
        Array.Fill(b, (byte)0xFF);
        int at = MergedImage.TableOffset;
        void Entry(byte type, byte subtype, int offset, int size, string label)
        {
            b[at] = 0xAA; b[at + 1] = 0x50; b[at + 2] = type; b[at + 3] = subtype;
            BitConverter.GetBytes(offset).CopyTo(b, at + 4);
            BitConverter.GetBytes(size).CopyTo(b, at + 8);
            System.Text.Encoding.ASCII.GetBytes(label).CopyTo(b, at + 12);
            at += 32;
        }
        Entry(1, 2, 0x9000, 0x5000, "nvs");
        Entry(1, 0, 0xE000, 0x2000, "otadata");
        Entry(0, 0x10, 0x10000, 0x640000, "app0");
        Entry(1, 0x82, 0xC90000, 0x360000, "spiffs");
        b[at] = 0xEB; b[at + 1] = 0xEB;
        return b;
    }

    [Fact]
    public void Leaves_out_the_nvs_partition()
    {
        var pieces = MergedImage.Pieces(Image());

        Assert.Equal(new[] { new MergedImage.Piece(0, 0x9000), new MergedImage.Piece(0xE000, 0x12000) }, pieces);
    }

    [Fact]
    public void A_file_without_a_partition_table_is_one_piece()
    {
        var b = new byte[0x20000];
        Array.Fill(b, (byte)0xFF);

        Assert.Equal(new[] { new MergedImage.Piece(0, 0x20000) }, MergedImage.Pieces(b));
    }

    [Fact]
    public void A_file_that_ends_before_the_nvs_is_one_piece()
    {
        var b = Image(0x8000 + 0xC00);
        Array.Resize(ref b, 0x9000);

        Assert.Equal(new[] { new MergedImage.Piece(0, 0x9000) }, MergedImage.Pieces(b));
    }

    [Fact]
    public void Split_writes_the_pieces_and_keeps_their_bytes()
    {
        var image = Image();
        image[0] = 0xE9; image[0x10000] = 0xE9; image[0xE000] = 0x01;
        var folder = Path.Combine(Path.GetTempPath(), "MergedImageTests-" + Guid.NewGuid().ToString("N"));
        var file = Path.Combine(folder, "fw-merged.bin");
        Directory.CreateDirectory(folder);
        try
        {
            File.WriteAllBytes(file, image);

            var parts = MergedImage.Split(file, Path.Combine(folder, "parts"));

            Assert.Equal(new[] { 0, 0xE000 }, parts.Select(p => p.Offset));
            Assert.Equal(image.AsSpan(0, 0x9000).ToArray(), File.ReadAllBytes(parts[0].Path));
            Assert.Equal(image.AsSpan(0xE000).ToArray(), File.ReadAllBytes(parts[1].Path));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void The_real_firmware_keeps_bootloader_table_and_app()
    {
        var release = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "firmware", "release");
        var file = Directory.Exists(release) ? Directory.GetFiles(release, "*-merged.bin").OrderBy(f => f).LastOrDefault() : null;
        if (file == null) return;   // release/ isn't in git; CI has no firmware here

        var pieces = MergedImage.Pieces(File.ReadAllBytes(file));

        Assert.Equal(0, pieces[0].Offset);
        Assert.Equal(0x9000, pieces[0].Length);
        Assert.Equal(0xE000, pieces[1].Offset);
    }
}

public class FormattingTests
{
    [Theory]
    [InlineData(14_074_000, "14,074")]
    [InlineData(14_074_500, "14,074.5")]
    [InlineData(7_074_250, "7,074.25")]
    [InlineData(20_000, "20")]
    public void Khz(long hz, string text) => Assert.Equal(text, ScopeView.Khz(hz));

    [Theory]
    [InlineData(49, "49 s")]
    [InlineData(119, "119 s")]
    [InlineData(1440, "24 min")]
    [InlineData(7200, "2 h 0 min")]
    [InlineData(9000, "2 h 30 min")]
    public void Uptime(long seconds, string text) => Assert.Equal(text, MainForm.Uptime(seconds));
}

public class TraceTopTests
{
    [Fact]
    public void A_stronger_peak_moves_the_top_up_at_once()
    {
        Assert.Equal(180, ScopeView.NextTraceTop(150, 180));
        Assert.Equal(120, ScopeView.NextTraceTop(null, 120));
    }

    [Fact]
    public void A_weaker_peak_lets_the_top_down_slowly()
    {
        double top = 200;
        top = ScopeView.NextTraceTop(top, 100);
        Assert.Equal(199, top, 6);
        for (int i = 0; i < 25 * 10; i++) top = ScopeView.NextTraceTop(top, 100);   // 10 seconds
        Assert.InRange(top, 100, 110);
    }

    [Fact]
    public void The_strongest_signal_stays_below_the_top_of_the_trace()
    {
        var (low, range) = ScopeView.TraceScale(20, 220);

        Assert.Equal(20, low);
        Assert.True((220 - low) / range < 1);
        Assert.InRange((220 - low) / range, 0.9, 0.92);
    }
}

public class RadioNameTests
{
    [Theory]
    [InlineData(YaesuAccScope.AccRadio.Ftdx10, "FTDX10")]
    [InlineData(YaesuAccScope.AccRadio.Ftdx101, "FTdx101")]
    public void The_title_bar_names_the_radio(YaesuAccScope.AccRadio radio, string name) =>
        Assert.Equal(name, MainForm.RadioName(radio));
}
