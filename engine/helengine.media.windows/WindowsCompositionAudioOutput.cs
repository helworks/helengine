using NAudio.Wave;
namespace helengine.media.windows;
/// <summary>One Windows PCM output with bounded latency and a hardware-consumption clock.</summary>
public sealed class WindowsCompositionAudioOutput : IAudioOutput {
    /// <summary>Current generation's output device.</summary>
    WaveOutEvent Output;
    /// <summary>Bytes per consumed PCM frame.</summary>
    int FrameBytes;
    /// <summary>Prevents reinitialization after release.</summary>
    bool Disposed;
    /// <summary>Current stream identity used to discard old seeks.</summary>
    public long Generation {get;set;}
    /// <summary>Actual wave-device position, excluding PCM merely requested into buffers.</summary>
    public long ConsumedSampleFrames=>Output==null?0:Output.GetPosition()/FrameBytes;
    /// <summary>Stops and disposes the previous device before installing the new PCM stream.</summary>
    public void Reset(AudioFormat format,long generation,Func<long,int,long,AudioBlock> reader,CancellationToken token) {if(Disposed) {throw new ObjectDisposedException(nameof(WindowsCompositionAudioOutput));}Output?.Stop();Output?.Dispose();Output=null;Generation=generation;FrameBytes=format.Channels*4;var provider=new WindowsCompositionPcmProvider(format,generation,reader,token);var output=new WaveOutEvent{DesiredLatency=60,NumberOfBuffers=3};try {output.Init(provider);Output=output;}catch {output.Dispose();throw;}}
    /// <summary>Starts or resumes the current seek generation.</summary>
    public void Play()=>Output?.Play();
    /// <summary>Pauses hardware consumption without discarding the current stream position.</summary>
    public void Pause()=>Output?.Pause();
    /// <summary>Stops callbacks and releases native output resources.</summary>
    public void Dispose() {if(Disposed) {return;}Disposed=true;Output?.Stop();Output?.Dispose();Output=null;}
}
