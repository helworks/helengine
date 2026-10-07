namespace helengine.media.tests;
/// <summary>Records composed stream boundaries and simulates bounded encoding backpressure.</summary>
public sealed class FakeCompositionEncoder : ICompositionEncoder {
    /// <summary>Frames observed in order.</summary>
    public List<long> Frames {get;}=[];
    /// <summary>Total sample frames received.</summary>
    public long Samples {get;set;}
    /// <summary>Maximum concurrent outstanding video writes.</summary>
    public int MaximumWrites {get;set;}
    /// <summary>Current outstanding writes.</summary>
    int Writes;
    /// <summary>Optional artificial latency applied to each video frame.</summary>
    public int DelayMilliseconds {get;set;}
    /// <summary>Destination owned by the exporter until successful completion.</summary>
    string Path;
    /// <summary>Captures the encoding-only stream configuration.</summary>
    public Task BeginAsync(CompositionDocument document,OutputProfile profile,string output,CancellationToken token) {token.ThrowIfCancellationRequested();Path=output;return Task.CompletedTask;}
    /// <summary>Records one complete RGBA frame under bounded backpressure.</summary>
    public async Task WriteVideoFrameAsync(MediaVideoFrame frame,long index,CancellationToken token) {int pending=Interlocked.Increment(ref Writes);MaximumWrites=Math.Max(MaximumWrites,pending);try {if(DelayMilliseconds>0) {await Task.Delay(DelayMilliseconds,token);}Frames.Add(index);Assert.Equal(frame.Surface.Width*frame.Surface.Height*4,frame.Surface.ReadRgba().Length);}finally {Interlocked.Decrement(ref Writes);}}
    /// <summary>Records one already mixed PCM block.</summary>
    public Task WriteAudioBlockAsync(AudioBlock block,CancellationToken token) {token.ThrowIfCancellationRequested();Samples+=block.SampleCount;return Task.CompletedTask;}
    /// <summary>Publishes a fake encoded stream only after both producers finish.</summary>
    public Task CompleteAsync(CancellationToken token)=>File.WriteAllBytesAsync(Path,[1,2,3],token);
    /// <summary>Releases no native resources in this fake.</summary>
    public void Dispose() { }
}
