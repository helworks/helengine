namespace helengine.media.tests;
/// <summary>Counts actual cursor opens to prove muted audio does not decode.</summary>
public sealed class CountingAudioResolver : IMediaSourceResolver {
    /// <summary>Number of source cursors actually opened.</summary>
    public int Opens {get;private set;}
    /// <summary>Creates a deterministic source while recording the open.</summary>
    public IMediaSource Open(MediaReference reference) {Opens++;return new ConstantAudioSource();}
    /// <summary>The fixture retains no source resources.</summary>
    public void Dispose() {}
}
