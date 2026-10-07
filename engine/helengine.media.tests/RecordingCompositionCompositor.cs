namespace helengine.media.tests;
/// <summary>Records exact preview timing without allocating a GPU.</summary>
public sealed class RecordingCompositionCompositor : IMediaCompositor {
    /// <summary>Last requested timeline instant.</summary>
    public MediaTime LastTime {get;set;}=MediaTime.Zero;
    /// <summary>Disposal observed by the test.</summary>
    public bool Disposed {get;set;}
    /// <summary>Returns a tiny independently owned diagnostic surface.</summary>
    public MediaVideoFrame Render(CompositionDocument document,MediaTime time,RenderSize size) {LastTime=time;return new(time,MediaTime.Zero,new TestVideoSurface(size));}
    /// <summary>Records session ownership release.</summary>
    public void Dispose()=>Disposed=true;
}
