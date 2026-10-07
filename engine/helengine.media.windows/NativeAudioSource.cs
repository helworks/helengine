namespace helengine.media.windows;
/// <summary>Lazily opens a native PCM cursor and preserves exact source-indexed reads.</summary>
public sealed class NativeAudioSource : IDisposable {
    /// <summary>Pinned file path kept immutable by the owner file lease.</summary>
    readonly string Path;
    /// <summary>Codec/resampler cursor owned by this source.</summary>
    IntPtr Handle;
    /// <summary>Output format of the current cursor, or null until first use.</summary>
    AudioFormat Format;
    /// <summary>Whether the source contains an actual audio stream.</summary>
    bool HasAudio;
    /// <summary>Prevents reopening a disposed cursor.</summary>
    bool Disposed;
    /// <summary>Retains a verified media path without decoding or allocating PCM buffers yet.</summary>
    public NativeAudioSource(string path) {Path=path ?? throw new ArgumentNullException(nameof(path));}
    /// <summary>Reads a source interval; absent audio is explicit silence, codec failures are errors.</summary>
    public AudioBlock Read(long first,int count,AudioFormat target) {
        if(Disposed) {throw new ObjectDisposedException(nameof(NativeAudioSource));}
        ArgumentNullException.ThrowIfNull(target);
        if(first<0 || count<0 || count>65536) {throw new ArgumentOutOfRangeException(nameof(count));}
        if(Handle==IntPtr.Zero || Format.SampleRate!=target.SampleRate || Format.Channels!=target.Channels) {
            if(Handle!=IntPtr.Zero) {NativeMediaApi.he_audio_decoder_destroy(Handle);Handle=IntPtr.Zero;}
            Handle=NativeMediaApi.he_audio_decoder_create(Path,target.SampleRate,target.Channels,out var hasAudio);
            if(Handle==IntPtr.Zero) {throw new InvalidDataException("Audio source could not open: "+NativeMediaApi.Error());}
            Format=target;HasAudio=hasAudio!=0;
        }
        var samples=new float[checked(count*target.Channels)];
        if(HasAudio && count>0 && NativeMediaApi.he_audio_decoder_read(Handle,first,count,samples)<0) {throw new InvalidDataException("Audio decode failed: "+NativeMediaApi.Error());}
        return new(first,samples,target,!HasAudio);
    }
    /// <summary>Deterministically releases native codec and resampler state.</summary>
    public void Dispose() {if(Disposed) {return;}Disposed=true;if(Handle!=IntPtr.Zero) {NativeMediaApi.he_audio_decoder_destroy(Handle);Handle=IntPtr.Zero;}}
}
