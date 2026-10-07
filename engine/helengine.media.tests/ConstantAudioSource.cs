namespace helengine.media.tests;
/// <summary>Deterministic PCM fixture independent of codec or device playback.</summary>
public sealed class ConstantAudioSource : IMediaSource {
    /// <summary>Returns a constant sample level for both channels.</summary>
    public AudioBlock ReadAudio(long first,int count,AudioFormat format) => new(first,Enumerable.Repeat(.75f,count*format.Channels).ToArray(),format);
    /// <summary>An audio fixture cannot provide video.</summary>
    public MediaVideoFrame ReadVideoFrame(MediaTime time) => throw new NotSupportedException();
    /// <summary>No external fixture resources are retained.</summary>
    public void Dispose() {}
}
