namespace helengine.capture.linux.tests;

/// <summary>Verifies native error trapping preserves preexisting handlers for unrelated owned display connections.</summary>
public sealed class X11ErrorForwardingTests {
    /// <summary>Represents a handler declared independently from the capture backend's managed delegate type.</summary>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate int ForeignErrorHandler(nint display,nint error);
    /// <summary>Counts errors delivered to the previous handler in this test process.</summary>
    int Forwarded;
    /// <summary>Tracks the display observed by the previous handler.</summary>
    nint ForwardedDisplay;
    /// <summary>A protocol error on another owned display is delivered to its prior custom handler.</summary>
    [LinuxCaptureFact] public void ForwardsOtherDisplayErrorsAndRestoresPreviousHandler() {
        using var target=new OwnX11Window(0xff0000);using var other=new OwnX11Window(0x0000ff);
        var previousHandler=new ForeignErrorHandler(HandleForeign);
        nint pointer=Marshal.GetFunctionPointerForDelegate(previousHandler);
        nint original=X11Native.XSetErrorHandler(pointer);
        ulong destroyed=other.Id;other.Destroy();
        try {
            lock(X11ErrorTrap.Gate) {
                using(var trap=new X11ErrorTrap(target.DisplayConnection)) {
                    X11Native.XGetWindowAttributes(other.DisplayConnection,(nuint)destroyed,out _);
                    X11Native.XSync(other.DisplayConnection,0);trap.Check();
                    Assert.Equal(1,Forwarded);Assert.Equal(other.DisplayConnection,ForwardedDisplay);
                }
                nint restored=X11Native.XSetErrorHandler(pointer);Assert.Equal(pointer,restored);
            }
        } finally { X11Native.XSetErrorHandler(original);GC.KeepAlive(previousHandler); }
    }
    /// <summary>Records only the deliberately generated error on the test-owned foreign display.</summary>
    int HandleForeign(nint display,nint error) { Forwarded++;ForwardedDisplay=display;return 0; }
}
