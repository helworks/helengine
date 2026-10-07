namespace helengine.media;
/// <summary>Validated output size for a full render or proportional preview.</summary>
public sealed class RenderSize {
    /// <summary>Checks pixel allocation limits before a render target is created.</summary>
    public RenderSize(int width,int height) {if(width<16 || height<16 || width>8192 || height>8192 || (long)width*height>33554432) {throw new ArgumentOutOfRangeException(nameof(width));}Width=width;Height=height;}
    /// <summary>Requested pixel width.</summary>
    public int Width {get;}
    /// <summary>Requested pixel height.</summary>
    public int Height {get;}
}
