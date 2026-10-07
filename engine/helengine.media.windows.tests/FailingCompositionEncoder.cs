using helengine.media;
namespace helengine.media.windows.tests;
/// <summary>Fails after creating a private partial file to verify exporter publication and cleanup.</summary>
public sealed class FailingCompositionEncoder:ICompositionEncoder {
    /// <summary>Creates an incomplete owned encoding output.</summary>
    public Task BeginAsync(CompositionDocument document,OutputProfile profile,string output,CancellationToken token)=>File.WriteAllBytesAsync(output,[1,2,3],token);
    /// <summary>Fails the first video write instead of publishing a false success.</summary>
    public Task WriteVideoFrameAsync(MediaVideoFrame frame,long index,CancellationToken token)=>Task.FromException(new IOException("Injected encoder failure."));
    /// <summary>Accepts no more work after peer cancellation.</summary>
    public Task WriteAudioBlockAsync(AudioBlock block,CancellationToken token) {token.ThrowIfCancellationRequested();return Task.CompletedTask;}
    /// <summary>Cannot complete after the injected write failure.</summary>
    public Task CompleteAsync(CancellationToken token)=>Task.FromException(new IOException("Injected encoder failure."));
    /// <summary>Owns no native handles.</summary>
    public void Dispose() { }
}
