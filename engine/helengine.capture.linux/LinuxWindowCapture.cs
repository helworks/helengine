namespace helengine.capture.linux;

/// <summary>Captures only an explicitly identified X11/Xwayland client window using XComposite CPU readback.</summary>
public sealed class LinuxWindowCapture : IDisposable {
    /// <summary>Serializes capture and disposal for this window connection.</summary>
    readonly object Gate=new();
    /// <summary>Native window resources, cleared on disposal.</summary>
    ILinuxWindowSource Source;
    /// <summary>Opens the supplied window on the explicit DISPLAY and requires its _NET_WM_PID to match.</summary>
    public LinuxWindowCapture(ulong windowId,int expectedProcessId) : this(new X11WindowSource(windowId,expectedProcessId)) { }
    /// <summary>Attaches a validated native source while keeping frame ownership independent.</summary>
    internal LinuxWindowCapture(ILinuxWindowSource source) {
        Source=source ?? throw new ArgumentNullException(nameof(source));Width=source.Width;Height=source.Height;
    }
    /// <summary>Fixed client-area width; a resize fails capture instead of changing encoder geometry.</summary>
    public int Width {get;}
    /// <summary>Fixed client-area height; minimized or destroyed windows fail capture explicitly.</summary>
    public int Height {get;}
    /// <summary>Returns an independently owned frame with caller-supplied exact presentation timing.</summary>
    public MediaVideoFrame Capture(MediaTime timestamp,MediaTime duration) {
        timestamp.Validate();duration.Validate();
        if(timestamp<MediaTime.Zero || duration<=MediaTime.Zero) { throw new ArgumentOutOfRangeException(nameof(duration),"Capture requires nonnegative timestamps and a positive frame duration."); }
        lock(Gate) {
            ObjectDisposedException.ThrowIf(Source==null,this);
            return new MediaVideoFrame(timestamp,duration,new LinuxRgbaSurface(Width,Height,Source.ReadRgba()));
        }
    }
    /// <summary>Closes this capture connection without invalidating previously returned frames.</summary>
    public void Dispose() {
        lock(Gate) {
            if(Source==null) { return; }
            Source.Dispose();Source=null;
        }
    }
}
