namespace helengine.capture.linux.tests;

/// <summary>Declares only the Xlib calls needed to create and modify test-owned windows.</summary>
static class OwnX11WindowNative {
    /// <summary>Obtains the root solely as a parent for test windows and a forbidden-target assertion.</summary>
    [DllImport("libX11.so.6")] internal static extern nuint XDefaultRootWindow(nint display);
    /// <summary>Creates one test-owned window on the isolated display.</summary>
    [DllImport("libX11.so.6")] internal static extern nuint XCreateSimpleWindow(nint display,nuint parent,int x,int y,uint width,uint height,uint borderWidth,nuint border,nuint background);
    /// <summary>Publishes the test window's own process identifier.</summary>
    [DllImport("libX11.so.6")] internal static extern int XChangeProperty(nint display,nuint window,nuint property,nuint type,int format,int mode,nint data,int count);
    /// <summary>Removes the test-owned PID property to exercise absent-identity failure.</summary>
    [DllImport("libX11.so.6")] internal static extern int XDeleteProperty(nint display,nuint window,nuint property);
    /// <summary>Maps a newly created test-owned window.</summary>
    [DllImport("libX11.so.6")] internal static extern int XMapWindow(nint display,nuint window);
    /// <summary>Unmaps only the test-owned target.</summary>
    [DllImport("libX11.so.6")] internal static extern int XUnmapWindow(nint display,nuint window);
    /// <summary>Changes the background color used to fill the test-owned client area.</summary>
    [DllImport("libX11.so.6")] internal static extern int XSetWindowBackground(nint display,nuint window,nuint background);
    /// <summary>Repaints the test-owned client area.</summary>
    [DllImport("libX11.so.6")] internal static extern int XClearWindow(nint display,nuint window);
    /// <summary>Resizes only the test-owned target.</summary>
    [DllImport("libX11.so.6")] internal static extern int XResizeWindow(nint display,nuint window,uint width,uint height);
    /// <summary>Destroys only the test-owned target.</summary>
    [DllImport("libX11.so.6")] internal static extern int XDestroyWindow(nint display,nuint window);
}
