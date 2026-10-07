namespace helengine.media.windows;
/// <summary>Owns an audio-only file lease and converts bounded PCM intervals on demand.</summary>
public sealed class AudioMediaSource : IMediaSource {
    /// <summary>Verified source file lease.</summary>
    readonly OwnedMediaFile File;
    /// <summary>Codec and format conversion cursor.</summary>
    readonly NativeAudioSource Audio;
    /// <summary>Prevents repeat resource release.</summary>
    bool Disposed;
    /// <summary>Retains the verified bytes and initializes a lazy independent audio cursor.</summary>
    public AudioMediaSource(OwnedMediaFile file) {File=file;Audio=new(file.Path);}
    /// <summary>Audio sources never silently fabricate a visual frame.</summary>
    public MediaVideoFrame ReadVideoFrame(MediaTime sourceTime) => throw new NotSupportedException("Audio-only media has no visual frame.");
    /// <summary>Reads source-indexed audio in the requested common output format.</summary>
    public AudioBlock ReadAudio(long first,int count,AudioFormat target) {if(Disposed) {throw new ObjectDisposedException(nameof(AudioMediaSource));}return Audio.Read(first,count,target);}
    /// <summary>Closes codec state and its immutable file lease exactly once.</summary>
    public void Dispose() {if(!Disposed) {Disposed=true;Audio.Dispose();File.Dispose();}}
}
