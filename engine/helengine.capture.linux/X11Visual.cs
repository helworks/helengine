namespace helengine.capture.linux;

/// <summary>Matches the public TrueColor Visual fields used to decode named pixmap pixels.</summary>
[StructLayout(LayoutKind.Sequential)]
struct X11Visual {
    /// <summary>Extension-owned metadata pointer.</summary>
    internal nint ExtensionData;
    /// <summary>Native visual identifier.</summary>
    internal nuint VisualId;
    /// <summary>X11 visual class; only TrueColor is accepted.</summary>
    internal int Class;
    /// <summary>Native red, green and blue channel masks.</summary>
    internal nuint RedMask,GreenMask,BlueMask;
    /// <summary>Visual component precision and colormap entry count.</summary>
    internal int BitsPerRgb,MapEntries;
}
