namespace helengine.media.windows;
/// <summary>Samples video by presentation timestamps and embeds a separate aligned PCM cursor.</summary>
public sealed class VideoMediaSource : IMediaSource {
    /// <summary>Verified immutable media file lease.</summary>
    readonly OwnedMediaFile File;
    /// <summary>Graphics device borrowed from the render session.</summary>
    readonly Device Device;
    /// <summary>Independently owned video decode cursor.</summary>
    readonly DirectX11VideoDecoder Decoder;
    /// <summary>Independently owned embedded-audio cursor using the same video origin.</summary>
    readonly NativeAudioSource Audio;
    /// <summary>Current held frame covering the requested source time.</summary>
    VideoFrame Current;
    /// <summary>One look-ahead frame, keeping decode memory bounded.</summary>
    VideoFrame Next;
    /// <summary>Marks the source as deterministically disposed.</summary>
    bool Disposed;
    /// <summary>Verifies stream dimensions before opening the playback decoder.</summary>
    public VideoMediaSource(OwnedMediaFile file,MediaReference reference,Device device) {
        File=file;Device=device;var info=VideoFileProbe.Probe(file.Path);
        if(info.Width!=reference.Width || info.Height!=reference.Height || (long)info.Width*info.Height>33554432) {throw new InvalidDataException("Video dimensions do not match the pinned source.");}
        Decoder=new(new(device,file.Path,VideoDecoderHardwareMode.PreferHardware));Audio=new(file.Path);
    }
    /// <summary>Returns the last presentation frame at or before the requested source instant.</summary>
    public MediaVideoFrame ReadVideoFrame(MediaTime sourceTime) {
        if(Disposed) {throw new ObjectDisposedException(nameof(VideoMediaSource));}
        sourceTime.Validate();var time=TimeSpan.FromSeconds(sourceTime.ToSeconds());
        if(time<TimeSpan.Zero || time>=Decoder.StreamInfo.Duration) {throw new InvalidDataException("Requested video time is outside the source.");}
        if(Current!=null && time<Current.Timestamp) {ReleaseHeld();Decoder.Seek(time);}
        if(Current==null && !Decoder.TryGetNextFrame(out Current)) {throw new InvalidDataException("Video source has no frame at the requested instant.");}
        while(true) {
            if(Next==null && !Decoder.TryGetNextFrame(out Next)) {break;}
            if(Next.Timestamp>time) {break;}Current.Dispose();Current=Next;Next=null;
        }
        var texture=new Texture2D(Current.CopyRgbaTexture());
        return new(new(Current.Timestamp.Ticks,TimeSpan.TicksPerSecond),new(Current.Duration.Ticks,TimeSpan.TicksPerSecond),new DirectX11VideoSurface(Device,texture,false),Decoder.SourceRotationDegrees);
    }
    /// <summary>Reads embedded sound against the same normalized primary-video source origin.</summary>
    public AudioBlock ReadAudio(long first,int count,AudioFormat target) {if(Disposed) {throw new ObjectDisposedException(nameof(VideoMediaSource));}return Audio.Read(first,count,target);}
    /// <summary>Releases held frames before their owner decoder and closes embedded audio and source bytes.</summary>
    public void Dispose() {if(!Disposed) {Disposed=true;ReleaseHeld();Decoder.Dispose();Audio.Dispose();File.Dispose();}}
    /// <summary>Releases at most two retained native decode frames.</summary>
    void ReleaseHeld() {Current?.Dispose();Next?.Dispose();Current=null;Next=null;}
}
