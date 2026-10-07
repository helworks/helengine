using System.Diagnostics;
namespace helengine.media;
/// <summary>Owns preview composition and one mixed PCM stream, with audio-driven timing and seek isolation.</summary>
public sealed class CompositionPlaybackSession : IDisposable {
    /// <summary>Immutable executable snapshot retained for this session.</summary>
    readonly CompositionDocument Document;
    /// <summary>Owned compositor shared by preview and offline rendering semantics.</summary>
    readonly IMediaCompositor Compositor;
    /// <summary>Owned sample-accurate mixer.</summary>
    readonly CompositionAudioMixer Mixer;
    /// <summary>Owned bounded output stream.</summary>
    readonly IAudioOutput Output;
    /// <summary>Cancellation reaches the output before mixer source release.</summary>
    readonly CancellationTokenSource Cancellation=new();
    /// <summary>Protects mixer reads from cursor disposal and concurrent callbacks.</summary>
    readonly object MixerGate=new();
    /// <summary>Protects compositor reads from disposal.</summary>
    readonly object PictureGate=new();
    /// <summary>Monotonic clock used only when no audio stream exists.</summary>
    readonly Stopwatch SilentClock=new();
    /// <summary>Whether the timeline contains an enabled audible source.</summary>
    readonly bool HasAudio;
    /// <summary>Current seek generation, atomically visible to output callbacks.</summary>
    long Generation;
    /// <summary>Exact clock anchor for the current stream.</summary>
    MediaTime Anchor=MediaTime.Zero;
    /// <summary>First timeline sample of the current seek.</summary>
    long FirstSample;
    /// <summary>Whether playback currently advances.</summary>
    bool Playing;
    /// <summary>Prevents access after teardown.</summary>
    volatile bool Disposed;
    /// <summary>Takes ownership of explicit composition, render, mix and output dependencies.</summary>
    public CompositionPlaybackSession(CompositionDocument document,IMediaCompositor compositor,CompositionAudioMixer mixer,IAudioOutput output) {
        ArgumentNullException.ThrowIfNull(document);Document=CompositionJson.Parse(CompositionJson.Serialize(document));Compositor=compositor ?? throw new ArgumentNullException(nameof(compositor));Mixer=mixer ?? throw new ArgumentNullException(nameof(mixer));Output=output ?? throw new ArgumentNullException(nameof(output));HasAudio=Document.Audio.Enabled && Document.AudioClips.Any(clip=>!clip.Muted && clip.Gain>0);Reset(MediaTime.Zero);
    }
    /// <summary>Starts the existing stream without resetting already consumed audio.</summary>
    public void Play() {Check();if(Playing) {return;}if(HasAudio) {Output.Play();}else {SilentClock.Start();}Playing=true;}
    /// <summary>Pauses both clock and consumption without replaying queued old samples.</summary>
    public void Pause() {Check();if(!Playing) {return;}if(HasAudio) {Output.Pause();}else {SilentClock.Stop();}Playing=false;}
    /// <summary>Flushes old PCM and repositions to the first sample at or after the requested instant.</summary>
    public void Seek(MediaTime time) {Check();time.Validate();if(time<MediaTime.Zero || time>Document.Duration) {throw new ArgumentOutOfRangeException(nameof(time));}bool resume=Playing;Pause();Reset(time);if(resume) {Play();}}
    /// <summary>Reports the audio-consumption clock or monotonic silent clock clamped to the timeline.</summary>
    public PlaybackSnapshot Snapshot() {Check();var elapsed=HasAudio?new MediaTime(Output.Generation==Interlocked.Read(ref Generation)?Output.ConsumedSampleFrames:0,Document.Audio.SampleRate):MediaTime.FromSeconds(SilentClock.Elapsed.TotalSeconds);var time=Anchor+elapsed;if(time>Document.Duration) {time=Document.Duration;}return new(time,Playing && time<Document.Duration,Interlocked.Read(ref Generation));}
    /// <summary>Renders the same executable timeline at the current exact clock instant.</summary>
    public MediaVideoFrame ReadPreviewFrame(RenderSize size) {Check();var snapshot=Snapshot();lock(PictureGate) {Check();var time=snapshot.Time;if(time==Document.Duration){var clock=new CompositionClock(Document.FrameRate,Document.Audio.SampleRate,Document.Duration);time=clock.FrameTime(clock.FrameCount-1);}return Compositor.Render(Document,time,size);}}
    /// <summary>Cancels output pulls before releasing stream, source cursors and compositor.</summary>
    public void Dispose() {if(Disposed) {return;}Disposed=true;Cancellation.Cancel();Interlocked.Increment(ref Generation);Output.Dispose();lock(MixerGate) {Mixer.Dispose();}lock(PictureGate) {Compositor.Dispose();}SilentClock.Stop();}
    /// <summary>Publishes a new generation before flushing the output so old callbacks become silence.</summary>
    void Reset(MediaTime time) {long generation;lock(MixerGate) {generation=Interlocked.Increment(ref Generation);FirstSample=(time*new MediaTime(Document.Audio.SampleRate,1)).Ceiling();Anchor=HasAudio?new(FirstSample,Document.Audio.SampleRate):time;SilentClock.Reset();}if(HasAudio) {Output.Reset(new(Document.Audio.SampleRate,Document.Audio.Channels),generation,ReadAudio,Cancellation.Token);}}
    /// <summary>Supplies bounded PCM and refuses callbacks from an earlier stream generation.</summary>
    AudioBlock ReadAudio(long offset,int count,long generation) {
        if(count<0 || count>32768 || offset<0) {throw new ArgumentOutOfRangeException(nameof(count));}var format=new AudioFormat(Document.Audio.SampleRate,Document.Audio.Channels);
        lock(MixerGate) {if(Disposed || Cancellation.IsCancellationRequested || generation!=Interlocked.Read(ref Generation)) {return new(0,new float[count*format.Channels],format,true);}return Mixer.Render(Document,checked(FirstSample+offset),count).Block;}
    }
    /// <summary>Rejects calls after owned resources have been released.</summary>
    void Check() {if(Disposed) {throw new ObjectDisposedException(nameof(CompositionPlaybackSession));}}
}
