namespace helengine.capture.linux;

/// <summary>Matches the XImage prefix through its public color masks; function pointers are unused.</summary>
[StructLayout(LayoutKind.Sequential)]
struct X11ImageHeader {
    /// <summary>Captured image geometry, horizontal offset and storage format.</summary>
    internal int Width,Height,XOffset,Format;
    /// <summary>Client-side image storage owned by XDestroyImage.</summary>
    internal nint Data;
    /// <summary>Server byte and bitmap layout metadata.</summary>
    internal int ByteOrder,BitmapUnit,BitmapBitOrder,BitmapPad,Depth,BytesPerLine,BitsPerPixel;
    /// <summary>Image masks may be zero for pixmaps; the window visual supplies authoritative masks.</summary>
    internal nuint RedMask,GreenMask,BlueMask;
}
