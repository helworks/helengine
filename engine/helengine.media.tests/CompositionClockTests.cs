using helengine.media;
namespace helengine.media.tests;
/// <summary>Checks exact composition timing rather than elapsed playback time.</summary>
public sealed class CompositionClockTests {
    /// <summary>A long fractional-rate render has no accumulated time drift.</summary>
    [Fact] public void NoDriftAt30000Over1001() {
        var clock = new CompositionClock(new MediaTime(30000,1001),48000,new MediaTime(1001,1));
        Assert.Equal(new MediaTime(1001,1),clock.FrameTime(30000));
        Assert.Equal(new MediaTime(1,1),clock.SampleTime(48000));
        Assert.Equal(30000,clock.FrameCount);
        Assert.Equal(48048000,clock.SampleCount);
    }
    /// <summary>A sample or frame exactly at a clip end belongs to the next interval.</summary>
    [Fact] public void IntervalsAreEndExclusive() {
        Assert.True(MediaTime.InInterval(new(0,1),new(0,1),new(1,1)));
        Assert.False(MediaTime.InInterval(new(1,1),new(0,1),new(1,1)));
        var clock = new CompositionClock(new(24,1),48000,new(1,3));
        Assert.Equal(8,clock.FrameCount); Assert.Equal(16000,clock.SampleCount);
    }
    /// <summary>Invalid denominators and non-finite editorial times fail before rendering.</summary>
    [Fact] public void InvalidTimeRejected() {
        Assert.Throws<ArgumentOutOfRangeException>(()=>new MediaTime(1,0));
        Assert.Throws<ArgumentOutOfRangeException>(()=>MediaTime.FromSeconds(double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(()=>MediaTime.FromSeconds(double.PositiveInfinity));
        Assert.Equal(new MediaTime(1,4),new MediaTime(2,8));
    }
}
