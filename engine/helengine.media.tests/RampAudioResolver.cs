namespace helengine.media.tests;
/// <summary>Provides a simple sample-index ramp to expose fractional source interpolation.</summary>
public sealed class RampAudioResolver : IMediaSourceResolver {
    /// <summary>Creates an independent deterministic ramp cursor.</summary>
    public IMediaSource Open(MediaReference reference)=>new RampAudioSource();
    /// <summary>No resolver-owned native handles exist.</summary>
    public void Dispose() { }
}
