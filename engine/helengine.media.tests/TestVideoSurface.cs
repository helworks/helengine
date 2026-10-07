namespace helengine.media.tests;
/// <summary>Owned synthetic frame surface for pure session and encoder tests.</summary>
public sealed class TestVideoSurface : IVideoSurface {
    /// <summary>Retains the requested frame dimensions.</summary>
    public TestVideoSurface(RenderSize size) {Width=size.Width;Height=size.Height;}
    /// <summary>Frame width in pixels.</summary>
    public int Width {get;}
    /// <summary>Frame height in pixels.</summary>
    public int Height {get;}
    /// <summary>Returns transparent packed RGBA.</summary>
    public ReadOnlyMemory<byte> ReadRgba()=>new byte[Width*Height*4];
    /// <summary>No native resources are used.</summary>
    public void Dispose() { }
}
