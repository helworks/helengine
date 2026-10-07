namespace helengine.media;
/// <summary>Renders complete visual composition frames from the same timeline used by playback.</summary>
public interface IMediaCompositor : IDisposable {
    /// <summary>Produces an independently owned frame for one exact time and requested resolution.</summary>
    MediaVideoFrame Render(CompositionDocument document,MediaTime time,RenderSize size);
}
