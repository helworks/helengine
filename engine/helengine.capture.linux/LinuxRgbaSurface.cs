namespace helengine.capture.linux;

/// <summary>Owns one CPU capture allocation independently from its native window and later frames.</summary>
sealed class LinuxRgbaSurface : IVideoSurface {
    /// <summary>Exclusive array backing this frame until disposal.</summary>
    byte[] Pixels;
    /// <summary>Attaches a newly allocated, tightly packed RGBA frame.</summary>
    internal LinuxRgbaSurface(int width,int height,byte[] pixels) {
        if(width<=0 || height<=0 || pixels==null || pixels.Length!=checked(width*height*4)) { throw new ArgumentException("A complete tightly packed RGBA frame is required."); }
        Width=width;Height=height;Pixels=pixels;
    }
    /// <summary>Captured client-area width in pixels.</summary>
    public int Width {get;}
    /// <summary>Captured client-area height in pixels.</summary>
    public int Height {get;}
    /// <summary>Returns the frame's stable CPU allocation; access after disposal is rejected.</summary>
    public ReadOnlyMemory<byte> ReadRgba() {
        ObjectDisposedException.ThrowIf(Pixels==null,this);
        return Pixels;
    }
    /// <summary>Releases this surface's reference without mutating another frame's allocation.</summary>
    public void Dispose() => Pixels=null;
}
