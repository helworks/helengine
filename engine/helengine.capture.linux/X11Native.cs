namespace helengine.capture.linux;

/// <summary>Declares the installed Xlib and XComposite C ABI without an additional native package.</summary>
static class X11Native {
    /// <summary>Opens only the display explicitly supplied by the caller's environment.</summary>
    [DllImport("libX11.so.6")] internal static extern nint XOpenDisplay(string display);
    /// <summary>Closes a display and releases all resources and automatic window redirections owned by it.</summary>
    [DllImport("libX11.so.6")] internal static extern int XCloseDisplay(nint display);
    /// <summary>Reads the selected window's current geometry and visual.</summary>
    [DllImport("libX11.so.6")] internal static extern int XGetWindowAttributes(nint display,nuint window,out X11WindowAttributes attributes);
    /// <summary>Finds an existing property atom without creating a new server property name.</summary>
    [DllImport("libX11.so.6")] internal static extern nuint XInternAtom(nint display,string name,int onlyIfExists);
    /// <summary>Reads a bounded property while preserving it on the selected window.</summary>
    [DllImport("libX11.so.6")] internal static extern int XGetWindowProperty(nint display,nuint window,nuint property,nint offset,nint length,int delete,nuint requestedType,out nuint actualType,out int format,out nuint count,out nuint bytesAfter,out nint value);
    /// <summary>Releases Xlib-allocated property memory.</summary>
    [DllImport("libX11.so.6")] internal static extern int XFree(nint data);
    /// <summary>Retrieves pixels from a named window pixmap, never from the desktop drawable.</summary>
    [DllImport("libX11.so.6")] internal static extern nint XGetImage(nint display,nuint drawable,int x,int y,uint width,uint height,nuint planeMask,int format);
    /// <summary>Destroys an XImage and its client-side pixel allocation.</summary>
    [DllImport("libX11.so.6")] internal static extern int XDestroyImage(nint image);
    /// <summary>Releases the temporary reference to redirected window storage.</summary>
    [DllImport("libX11.so.6")] internal static extern int XFreePixmap(nint display,nuint pixmap);
    /// <summary>Flushes requests and delivers asynchronous protocol errors before validation proceeds.</summary>
    [DllImport("libX11.so.6")] internal static extern int XSync(nint display,int discard);
    /// <summary>Temporarily replaces the process-wide Xlib protocol error handler.</summary>
    [DllImport("libX11.so.6")] internal static extern nint XSetErrorHandler(nint handler);
    /// <summary>Checks the Composite protocol version available on the chosen display.</summary>
    [DllImport("libXcomposite.so.1")] internal static extern int XCompositeQueryVersion(nint display,out int major,out int minor);
    /// <summary>Retains offscreen storage for the one selected window while preserving automatic presentation.</summary>
    [DllImport("libXcomposite.so.1")] internal static extern void XCompositeRedirectWindow(nint display,nuint window,int update);
    /// <summary>Names only the chosen window's current redirected pixmap.</summary>
    [DllImport("libXcomposite.so.1")] internal static extern nuint XCompositeNameWindowPixmap(nint display,nuint window);
}
