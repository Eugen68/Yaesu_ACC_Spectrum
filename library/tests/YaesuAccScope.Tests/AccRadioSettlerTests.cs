namespace YaesuAccScope.Tests;

/// <summary>
/// One odd frame mustn't flip the layout: a stream stays on its radio until
/// several frames in a row read as the other one.
/// </summary>
public class AccRadioSettlerTests
{
    [Fact]
    public void The_first_frame_is_taken_at_once()
    {
        var settler = new AccRadioSettler();
        Assert.Null(settler.Radio);
        Assert.Equal(AccRadio.Ftdx10, settler.Settle(AccRadio.Ftdx10));
        Assert.Equal(AccRadio.Ftdx10, settler.Radio);
    }

    [Fact]
    public void A_few_odd_frames_change_nothing()
    {
        var settler = new AccRadioSettler();
        settler.Settle(AccRadio.Ftdx101);
        for (int i = 1; i < AccRadioSettler.FramesToChange; i++)
            Assert.Equal(AccRadio.Ftdx101, settler.Settle(AccRadio.Ftdx10));
        Assert.Equal(AccRadio.Ftdx101, settler.Settle(AccRadio.Ftdx101));
        // The count starts again after a frame from the settled radio.
        for (int i = 1; i < AccRadioSettler.FramesToChange; i++)
            Assert.Equal(AccRadio.Ftdx101, settler.Settle(AccRadio.Ftdx10));
    }

    [Fact]
    public void Enough_frames_in_a_row_change_the_radio()
    {
        var settler = new AccRadioSettler();
        settler.Settle(AccRadio.Ftdx101);
        for (int i = 1; i < AccRadioSettler.FramesToChange; i++) settler.Settle(AccRadio.Ftdx10);
        Assert.Equal(AccRadio.Ftdx10, settler.Settle(AccRadio.Ftdx10));
    }

    [Fact]
    public void Reset_forgets_the_radio()
    {
        var settler = new AccRadioSettler();
        settler.Settle(AccRadio.Ftdx101);
        settler.Reset();
        Assert.Null(settler.Radio);
        Assert.Equal(AccRadio.Ftdx10, settler.Settle(AccRadio.Ftdx10));
    }

    [Fact]
    public void A_frame_can_be_decoded_as_either_radio()
    {
        var raw = Captures.Ftdx10("ftdx10-14074-20k")[0].Raw;
        Assert.Equal(AccRadio.Ftdx10, AccFrame.Parse(raw).Radio);
        Assert.Equal(AccRadio.Ftdx101, AccFrame.Parse(raw, AccRadio.Ftdx101).Radio);
        var asTen = AccFrame.Parse(raw, AccRadio.Ftdx10);
        Assert.Equal(14_074_000, asTen.Main.VfoHz);
        Assert.False(asTen.HasSub);
        Assert.Throws<ArgumentException>(() => AccFrame.Parse(new byte[100], AccRadio.Ftdx10));
    }

    /// <summary>
    /// An FTDX10 stream with one FTdx101 frame in the middle: settled, every
    /// frame still decodes as the FTDX10, the way AccSerialReader does it.
    /// </summary>
    [Fact]
    public void An_odd_frame_in_an_ftdx10_stream_keeps_the_ftdx10_layout()
    {
        var stream = Captures.Ftdx10("ftdx10-14074-20k").Take(20).Select(f => f.Raw).ToList();
        var odd = Captures.Step5(Directory.GetFiles(Captures.PathOf("step5"), "*.bin")
            .Select(Path.GetFileNameWithoutExtension).First()!)[0];
        Assert.Equal(AccRadio.Ftdx101, odd.Radio);
        stream.Insert(10, odd.Raw);

        var settler = new AccRadioSettler();
        foreach (var raw in stream)
        {
            var frame = AccFrame.Parse(raw);
            var radio = settler.Settle(frame.Radio);
            if (radio != frame.Radio) frame = AccFrame.Parse(raw, radio);
            Assert.Equal(AccRadio.Ftdx10, frame.Radio);
            Assert.False(frame.HasSub);
        }
    }
}
