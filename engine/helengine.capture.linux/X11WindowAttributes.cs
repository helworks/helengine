namespace helengine.capture.linux;

/// <summary>Matches XWindowAttributes on Linux's native-long ABI.</summary>
[StructLayout(LayoutKind.Sequential)]
struct X11WindowAttributes {
    /// <summary>Client origin, dimensions, border and drawable depth.</summary>
    internal int X,Y,Width,Height,BorderWidth,Depth;
    /// <summary>Window visual descriptor.</summary>
    internal nint Visual;
    /// <summary>Screen root drawable used solely to reject desktop capture.</summary>
    internal nuint Root;
    /// <summary>Window class, gravity and backing store policy.</summary>
    internal int Class,BitGravity,WinGravity,BackingStore;
    /// <summary>Native unsigned-long backing plane and pixel values.</summary>
    internal nuint BackingPlanes,BackingPixel;
    /// <summary>Server save-under flag.</summary>
    internal int SaveUnder;
    /// <summary>Installed colormap identifier.</summary>
    internal nuint Colormap;
    /// <summary>Colormap installation and mapping state.</summary>
    internal int MapInstalled,MapState;
    /// <summary>Native-long input event selections; capture never changes them.</summary>
    internal nint AllEventMasks,YourEventMask,DoNotPropagateMask;
    /// <summary>Window-manager redirection policy.</summary>
    internal int OverrideRedirect;
    /// <summary>Owning screen pointer.</summary>
    internal nint Screen;
}
