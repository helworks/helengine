namespace helengine.capture.linux.tests;

/// <summary>Owns a solid-color X11 fixture window without changing focus or generating input.</summary>
sealed class OwnX11Window : IDisposable {
    /// <summary>Fixture-owned connection on the explicit isolated DISPLAY.</summary>
    readonly nint Display;
    /// <summary>PID property atom belonging to this connection.</summary>
    readonly nuint PidAtom;
    /// <summary>Creates and maps a small test-owned window with a verifiable process identifier.</summary>
    internal OwnX11Window(ulong color) {
        Display=X11Native.XOpenDisplay(Environment.GetEnvironmentVariable("DISPLAY"));
        Assert.NotEqual(0,Display);Root=(ulong)OwnX11WindowNative.XDefaultRootWindow(Display);
        Id=(ulong)OwnX11WindowNative.XCreateSimpleWindow(Display,(nuint)Root,10,10,64,48,0,0,(nuint)color);
        Assert.NotEqual(0ul,Id);PidAtom=X11Native.XInternAtom(Display,"_NET_WM_PID",0);
        SetPid(Environment.ProcessId);OwnX11WindowNative.XMapWindow(Display,(nuint)Id);Sync();
    }
    /// <summary>Explicit fixture window identifier.</summary>
    internal ulong Id {get;private set;}
    /// <summary>Root identifier used only to verify desktop capture rejection.</summary>
    internal ulong Root {get;}
    /// <summary>Fixture connection used to verify that errors from other owned displays are forwarded.</summary>
    internal nint DisplayConnection=>Display;
    /// <summary>Changes the fixture-owned identity property in native-long Xlib format.</summary>
    internal void SetPid(int processId) {
        nint storage=Marshal.AllocHGlobal(IntPtr.Size);
        try {
            Marshal.WriteInt64(storage,processId);OwnX11WindowNative.XChangeProperty(Display,(nuint)Id,PidAtom,6,32,0,storage,1);Sync();
        } finally { Marshal.FreeHGlobal(storage); }
    }
    /// <summary>Removes fixture identity to verify missing PID failure.</summary>
    internal void DeletePid() { OwnX11WindowNative.XDeleteProperty(Display,(nuint)Id,PidAtom);Sync(); }
    /// <summary>Repaints the fixture with the supplied packed TrueColor value.</summary>
    internal void Paint(ulong color) {
        OwnX11WindowNative.XSetWindowBackground(Display,(nuint)Id,(nuint)color);OwnX11WindowNative.XClearWindow(Display,(nuint)Id);Sync();
    }
    /// <summary>Changes fixture size to verify fixed recording geometry.</summary>
    internal void Resize() { OwnX11WindowNative.XResizeWindow(Display,(nuint)Id,80,60);Sync(); }
    /// <summary>Hides the fixture to verify mapped-window requirements.</summary>
    internal void Unmap() { OwnX11WindowNative.XUnmapWindow(Display,(nuint)Id);Sync(); }
    /// <summary>Destroys fixture storage to verify errors do not return stale pixels.</summary>
    internal void Destroy() {
        if(Id==0) { return; }
        OwnX11WindowNative.XDestroyWindow(Display,(nuint)Id);Id=0;Sync();
    }
    /// <summary>Waits until fixture commands are applied before capture uses a different connection.</summary>
    void Sync()=>X11Native.XSync(Display,0);
    /// <summary>Releases only this fixture's windows and connection.</summary>
    public void Dispose() { Destroy();X11Native.XCloseDisplay(Display); }
}
