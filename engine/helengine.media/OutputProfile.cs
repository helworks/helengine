namespace helengine.media;
/// <summary>Pinned encoding and alpha profile selected by the host, never model-authored codec arguments.</summary>
public sealed class OutputProfile {
    /// <summary>Exact versioned export profile.</summary>
    public string Id {get;}
    /// <summary>Requested output width.</summary>
    public int Width {get;}
    /// <summary>Requested output height.</summary>
    public int Height {get;}
    /// <summary>Whether the engine keeps transparent canvas pixels.</summary>
    public bool PreserveAlpha {get;}
    /// <summary>Constructs a host-controlled known profile.</summary>
    OutputProfile(string id,int width,int height,bool preserveAlpha) {Id=id;Width=width;Height=height;PreserveAlpha=preserveAlpha;}
    /// <summary>Validates allocation and codec dimensions and rejects unrecognized profile versions.</summary>
    public static OutputProfile Resolve(string id,int width,int height) {if(width<16 || height<16 || width>8192 || height>8192 || (long)width*height>33554432) {throw new InvalidDataException("Invalid export dimensions.");}if(id=="mp4-h264-aac.v1") {if(width%2!=0 || height%2!=0) {throw new InvalidDataException("H264 yuv420p requires even dimensions.");}return new(id,width,height,false);}if(id=="mov-prores4444-pcm.v1") {return new(id,width,height,true);}throw new InvalidDataException("Unknown output profile: "+id);}
}
