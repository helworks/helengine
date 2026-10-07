namespace helengine.media;
/// <summary>Owns a rendered or decoded surface without exposing platform types to the timeline.</summary>
public interface IVideoSurface : IDisposable {
    /// <summary>Stored surface pixel width.</summary>
    int Width {get;}
    /// <summary>Stored surface pixel height.</summary>
    int Height {get;}
    /// <summary>Explicit readback for encoding or pixel verification; normal GPU composition does not call this.</summary>
    ReadOnlyMemory<byte> ReadRgba();
}
