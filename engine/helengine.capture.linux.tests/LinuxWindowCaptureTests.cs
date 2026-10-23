namespace helengine.capture.linux.tests;

/// <summary>Verifies exact timing, independent frame ownership and capture failure propagation.</summary>
public sealed class LinuxWindowCaptureTests {
    /// <summary>Later captures and source disposal never replace an earlier frame's pixels.</summary>
    [Fact] public void FramesOwnStableStorageAndExactTiming() {
        var source=new FakeLinuxWindowSource();var capture=new LinuxWindowCapture(source);
        using var first=capture.Capture(new(2,3),new(1,60));
        using var second=capture.Capture(new(41,60),new(1,60));
        Assert.Equal(new MediaTime(2,3),first.Timestamp);Assert.Equal(new MediaTime(1,60),first.Duration);
        Assert.Equal(1,first.Surface.ReadRgba().Span[0]);Assert.Equal(2,second.Surface.ReadRgba().Span[0]);
        first.Dispose();Assert.Throws<ObjectDisposedException>(()=>first.Surface.ReadRgba());
        capture.Dispose();capture.Dispose();Assert.True(source.Disposed);
        Assert.Equal(2,second.Surface.ReadRgba().Span[0]);
        Assert.Throws<ObjectDisposedException>(()=>capture.Capture(MediaTime.Zero,new(1,60)));
    }
    /// <summary>Native errors propagate and invalid timing never causes a native readback.</summary>
    [Fact] public void FailuresAreExplicitAndInvalidTimingDoesNotRead() {
        var source=new FakeLinuxWindowSource();using var capture=new LinuxWindowCapture(source);
        Assert.Throws<ArgumentOutOfRangeException>(()=>capture.Capture(MediaTime.Zero,MediaTime.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(()=>capture.Capture(new(-1,1),new(1,60)));
        Assert.Throws<InvalidDataException>(()=>capture.Capture(default,new(1,60)));
        Assert.Equal(0,source.Reads);source.Fail=true;
        Assert.Throws<InvalidOperationException>(()=>capture.Capture(MediaTime.Zero,new(1,60)));
    }
    /// <summary>Invalid explicit identifiers are rejected even before platform or display initialization.</summary>
    [Fact] public void RequiresExplicitWindowAndPid() {
        Assert.Throws<ArgumentOutOfRangeException>(()=>new LinuxWindowCapture(0,123));
        Assert.Throws<ArgumentOutOfRangeException>(()=>new LinuxWindowCapture(123,0));
    }
}
