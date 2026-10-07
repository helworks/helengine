namespace helengine.media.tests;
/// <summary>Deterministic output whose consumption is controlled by the test.</summary>
public sealed class FakeCompositionAudioOutput : IAudioOutput {
    /// <summary>Current pull callback retained for stale-generation tests.</summary>
    public Func<long,int,long,AudioBlock> Reader {get;set;}
    /// <summary>Token received by the active output generation.</summary>
    public CancellationToken Token {get;set;}
    /// <summary>Number of frames actually consumed by the fake device.</summary>
    public long ConsumedSampleFrames {get;set;}
    /// <summary>Current seek generation.</summary>
    public long Generation {get;set;}
    /// <summary>Disposal observed by the test.</summary>
    public bool Disposed {get;set;}
    /// <summary>Replaces and flushes the fake PCM stream.</summary>
    public void Reset(AudioFormat format,long generation,Func<long,int,long,AudioBlock> reader,CancellationToken token) {Generation=generation;Reader=reader;Token=token;ConsumedSampleFrames=0;}
    /// <summary>Advances actual consumption independently of picture reads.</summary>
    public void Consume(long count)=>ConsumedSampleFrames+=count;
    /// <summary>Starts the fake output without changing its clock.</summary>
    public void Play() { }
    /// <summary>Pauses the fake output without losing consumed frames.</summary>
    public void Pause() { }
    /// <summary>Records release without blocking callback cancellation.</summary>
    public void Dispose()=>Disposed=true;
}
