namespace helengine.media;
/// <summary>Final encoding boundary accepting only already-composed video and already-mixed PCM.</summary>
public interface ICompositionEncoder : IDisposable {
    /// <summary>Preflights codec support and prepares owned raw stream inputs and a private partial output.</summary>
    Task BeginAsync(CompositionDocument document,OutputProfile profile,string output,CancellationToken token);
    /// <summary>Writes one complete packed RGBA frame with bounded backpressure.</summary>
    Task WriteVideoFrameAsync(MediaVideoFrame frame,long index,CancellationToken token);
    /// <summary>Writes one mixed float PCM block with bounded backpressure.</summary>
    Task WriteAudioBlockAsync(AudioBlock block,CancellationToken token);
    /// <summary>Signals picture EOF independently of the PCM producer.</summary>
    Task FinishVideoAsync(CancellationToken token)=>Task.CompletedTask;
    /// <summary>Signals PCM EOF independently of the picture producer.</summary>
    Task FinishAudioAsync(CancellationToken token)=>Task.CompletedTask;
    /// <summary>Closes stream inputs and succeeds only when encoding and muxing have completed.</summary>
    Task CompleteAsync(CancellationToken token);
}
