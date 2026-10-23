using helengine.media;
namespace helengine.media.linux.tests;
/// <summary>Owns a deterministic packed RGBA fixture, optionally malformed for boundary validation.</summary>
sealed class RgbaTestSurface : IVideoSurface {
    /// <summary>Owned fixture pixel bytes.</summary>
    readonly byte[] Pixels;
    /// <summary>Creates an opaque gray fixture with independently configurable byte length.</summary>
    public RgbaTestSurface(int width,int height,byte gray=80,int length=-1) {Width=width;Height=height;Pixels=new byte[length<0?width*height*4:length];for(int i=0;i+3<Pixels.Length;i+=4) {Pixels[i]=gray;Pixels[i+1]=gray;Pixels[i+2]=gray;Pixels[i+3]=255;}}
    /// <summary>Fixture pixel width.</summary>
    public int Width {get;}
    /// <summary>Fixture pixel height.</summary>
    public int Height {get;}
    /// <summary>Returns the exact fixture storage without copying.</summary>
    public ReadOnlyMemory<byte> ReadRgba()=>Pixels;
    /// <summary>The fixture owns no native resources.</summary>
    public void Dispose() { }
}
