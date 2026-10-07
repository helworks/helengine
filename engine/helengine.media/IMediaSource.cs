namespace helengine.media;
/// <summary>Independent source cursor for timed visuals and sample-indexed PCM.</summary>
public interface IMediaSource : IDisposable {
    /// <summary>Returns the visual frame covering the requested normalized source instant.</summary>
    MediaVideoFrame ReadVideoFrame(MediaTime sourceTime);
    /// <summary>Returns a bounded PCM block converted to the requested common output format.</summary>
    AudioBlock ReadAudio(long sourceSampleIndex,int sampleCount,AudioFormat target);
}
