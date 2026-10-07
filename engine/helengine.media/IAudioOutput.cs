namespace helengine.media;
/// <summary>One bounded PCM device stream whose clock reports actual hardware consumption.</summary>
public interface IAudioOutput : IDisposable {
    /// <summary>Active stream generation; callbacks from earlier seeks must be discarded.</summary>
    long Generation {get;}
    /// <summary>Sample frames consumed since the current stream was reset, excluding queued PCM.</summary>
    long ConsumedSampleFrames {get;}
    /// <summary>Stops and flushes the prior stream, installs a bounded pull reader, and resets consumption.</summary>
    void Reset(AudioFormat format,long generation,Func<long,int,long,AudioBlock> reader,CancellationToken token);
    /// <summary>Starts or resumes the current stream.</summary>
    void Play();
    /// <summary>Pauses consumption while preserving the current stream position.</summary>
    void Pause();
}
