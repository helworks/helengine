namespace helengine.capture.linux.tests;

/// <summary>Exercises native XComposite against exclusively test-owned windows on an opted-in isolated display.</summary>
public sealed class LinuxWindowCaptureIntegrationTests {
    /// <summary>Another owned window may occlude the target without contaminating captured target pixels.</summary>
    [LinuxCaptureFact] public void CapturesOnlySelectedWindowDespiteOcclusion() {
        using var target=new OwnX11Window(0xff0000);
        using var capture=new LinuxWindowCapture(target.Id,Environment.ProcessId);
        target.Paint(0xff0000);
        using var cover=new OwnX11Window(0x0000ff);
        using var red=capture.Capture(MediaTime.Zero,new(1,60));
        Assert.Equal(64,capture.Width);Assert.Equal(48,capture.Height);AssertSolid(red,[255,0,0,255]);
        target.Paint(0x00ff00);
        using var green=capture.Capture(new(1,60),new(1,60));
        AssertSolid(green,[0,255,0,255]);AssertSolid(red,[255,0,0,255]);
    }
    /// <summary>Window identity and root exclusion are validated before any readback occurs.</summary>
    [LinuxCaptureFact] public void RejectsWrongPidMissingPidAndDesktop() {
        using var target=new OwnX11Window(0xff0000);
        Assert.Throws<InvalidOperationException>(()=>new LinuxWindowCapture(target.Id,Environment.ProcessId+1));
        Assert.Throws<ArgumentException>(()=>new LinuxWindowCapture(target.Root,Environment.ProcessId));
        target.DeletePid();Assert.Throws<InvalidOperationException>(()=>new LinuxWindowCapture(target.Id,Environment.ProcessId));
    }
    /// <summary>A resized selected window cannot silently change the encoder's fixed dimensions.</summary>
    [LinuxCaptureFact] public void RejectsResize() {
        using var target=new OwnX11Window(0xff0000);using var capture=new LinuxWindowCapture(target.Id,Environment.ProcessId);
        target.Resize();Assert.Throws<InvalidOperationException>(()=>capture.Capture(MediaTime.Zero,new(1,60)));
    }
    /// <summary>Destroyed windows produce a managed error instead of fatal Xlib termination or stale frames.</summary>
    [LinuxCaptureFact] public void RejectsDestroyedWindow() {
        using var target=new OwnX11Window(0xff0000);using var capture=new LinuxWindowCapture(target.Id,Environment.ProcessId);
        target.Destroy();Assert.Throws<InvalidOperationException>(()=>capture.Capture(MediaTime.Zero,new(1,60)));
    }
    /// <summary>Hidden windows are rejected even if their old named pixmap remains allocated server-side.</summary>
    [LinuxCaptureFact] public void RejectsUnmappedWindow() {
        using var target=new OwnX11Window(0xff0000);using var capture=new LinuxWindowCapture(target.Id,Environment.ProcessId);
        target.Unmap();Assert.Throws<InvalidOperationException>(()=>capture.Capture(MediaTime.Zero,new(1,60)));
    }
    /// <summary>Changed identity is checked on every frame rather than only during construction.</summary>
    [LinuxCaptureFact] public void RejectsChangedPid() {
        using var target=new OwnX11Window(0xff0000);using var capture=new LinuxWindowCapture(target.Id,Environment.ProcessId);
        target.SetPid(Environment.ProcessId+1);Assert.Throws<InvalidOperationException>(()=>capture.Capture(MediaTime.Zero,new(1,60)));
    }
    /// <summary>Checks every client pixel so borders and occluding pixels cannot pass a center-only assertion.</summary>
    static void AssertSolid(MediaVideoFrame frame,byte[] rgba) {
        var pixels=frame.Surface.ReadRgba().Span;
        Assert.Equal(64*48*4,pixels.Length);
        for(int i=0;i<pixels.Length;i++) { Assert.Equal(rgba[i%4],pixels[i]); }
    }
}
