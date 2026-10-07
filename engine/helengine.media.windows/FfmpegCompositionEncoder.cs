using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
namespace helengine.media.windows;
/// <summary>Streams final RGBA and mixed PCM over bounded Windows named pipes into an encoding-only process.</summary>
public sealed class FfmpegCompositionEncoder : ICompositionEncoder {
    /// <summary>Host-configured trusted encoder executable.</summary>
    readonly string Executable;
    /// <summary>Owned video stream endpoint.</summary>
    NamedPipeServerStream Video;
    /// <summary>Owned optional mixed PCM endpoint.</summary>
    NamedPipeServerStream Audio;
    /// <summary>Connection promises are awaited by their independent bounded producers.</summary>
    Task VideoConnection;
    /// <summary>Optional audio connection promise.</summary>
    Task AudioConnection;
    /// <summary>Owned encoding process.</summary>
    Process Process;
    /// <summary>Cancellation for pipe connections and unexpected encoder exit.</summary>
    CancellationTokenSource Cancellation;
    /// <summary>Caller cancellation kills the process to release blocked pipe writes.</summary>
    CancellationTokenRegistration KillRegistration;
    /// <summary>Bounded encoder diagnostic text.</summary>
    readonly StringBuilder Errors=new();
    /// <summary>Protects the diagnostic ring buffer.</summary>
    readonly object ErrorGate=new();
    /// <summary>Asynchronous bounded stderr drain.</summary>
    Task ErrorDrain;
    /// <summary>Expected output pixels and PCM configuration.</summary>
    OutputProfile Profile;
    /// <summary>Expected audio channels and sample rate.</summary>
    AudioFormat Format;
    /// <summary>Next expected video frame, preventing reordered or duplicate writes.</summary>
    long NextFrame;
    /// <summary>Next expected PCM sample frame.</summary>
    long NextSample;
    /// <summary>Prevents use after process teardown.</summary>
    bool Disposed;
    /// <summary>Retains an explicit host-selected encoder path.</summary>
    public FfmpegCompositionEncoder(string executable) {Executable=executable ?? throw new ArgumentNullException(nameof(executable));}
    /// <summary>Validates codecs before launching bounded raw streams without waiting for data-dependent probing.</summary>
    public async Task BeginAsync(CompositionDocument document,OutputProfile profile,string output,CancellationToken token) {
        if(Disposed || Process!=null) {throw new InvalidOperationException("Encoder is disposed or already begun.");}await ValidateCodecs(profile,document.Audio.Enabled,token);Profile=profile;Format=new(document.Audio.SampleRate,document.Audio.Channels);Cancellation=CancellationTokenSource.CreateLinkedTokenSource(token);string videoName="helengine-video-"+Guid.NewGuid().ToString("N");string audioName="helengine-audio-"+Guid.NewGuid().ToString("N");Video=Pipe(videoName);if(document.Audio.Enabled) {Audio=Pipe(audioName);}VideoConnection=Video.WaitForConnectionAsync(Cancellation.Token);if(Audio!=null) {AudioConnection=Audio.WaitForConnectionAsync(Cancellation.Token);}
        var start=new ProcessStartInfo(Executable){UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true};foreach(string arg in FfmpegEncodeArguments.Build(document,profile,PipePath(videoName),PipePath(audioName),output)) {start.ArgumentList.Add(arg);}Process=new(){StartInfo=start,EnableRaisingEvents=true};Process.Exited+=EncoderExited;
        if(!Process.Start()) {throw new IOException("Unable to start final encoder.");}ErrorDrain=DrainErrors(Process.StandardError);KillRegistration=token.Register(Kill);
    }
    /// <summary>Writes exactly one independently owned composed frame; the producer cannot run ahead.</summary>
    public async Task WriteVideoFrameAsync(MediaVideoFrame frame,long index,CancellationToken token) {
        if(index!=NextFrame || frame.Surface.Width!=Profile.Width || frame.Surface.Height!=Profile.Height) {throw new InvalidDataException("Composed frames are out of order or have incorrect dimensions.");}await VideoConnection.WaitAsync(token);await Video.WriteAsync(frame.Surface.ReadRgba(),token);NextFrame++;
    }
    /// <summary>Writes one already mixed, aligned float PCM block without filters or normalization.</summary>
    public async Task WriteAudioBlockAsync(AudioBlock block,CancellationToken token) {
        if(Audio==null || block.FirstSample!=NextSample || block.Format.SampleRate!=Format.SampleRate || block.Format.Channels!=Format.Channels) {throw new InvalidDataException("PCM blocks are out of order or use another format.");}await AudioConnection.WaitAsync(token);float[] samples=block.Samples.ToArray();byte[] bytes=new byte[samples.Length*4];System.Buffer.BlockCopy(samples,0,bytes,0,bytes.Length);await Audio.WriteAsync(bytes,token);NextSample+=block.SampleCount;
    }
    /// <summary>Signals video EOF independently, preventing a drained stream from blocking audio completion.</summary>
    public Task FinishVideoAsync(CancellationToken token) {token.ThrowIfCancellationRequested();Video?.Dispose();Video=null;return Task.CompletedTask;}
    /// <summary>Signals PCM EOF independently without waiting for the last picture producer.</summary>
    public Task FinishAudioAsync(CancellationToken token) {token.ThrowIfCancellationRequested();Audio?.Dispose();Audio=null;return Task.CompletedTask;}
    /// <summary>Waits for process success after both producers signal EOF.</summary>
    public async Task CompleteAsync(CancellationToken token) {Video?.Dispose();Video=null;Audio?.Dispose();Audio=null;await Process.WaitForExitAsync(token);if(ErrorDrain!=null) {await ErrorDrain;}if(Process.ExitCode!=0) {throw new IOException("Final encoding failed: "+ErrorText());}token.ThrowIfCancellationRequested();}
    /// <summary>Kills blocked encoding and releases owned pipe, process and cancellation resources.</summary>
    public void Dispose() {if(Disposed) {return;}Disposed=true;Cancellation?.Cancel();Kill();Video?.Dispose();Audio?.Dispose();KillRegistration.Dispose();if(Process!=null) {Process.Exited-=EncoderExited;Process.Dispose();}Cancellation?.Dispose();}
    /// <summary>Builds the Windows named-pipe input identity without shell interpolation.</summary>
    static string PipePath(string name)=>@"\\.\pipe\"+name;
    /// <summary>Creates one asynchronous, byte-oriented, write-only endpoint with bounded OS buffering.</summary>
    static NamedPipeServerStream Pipe(string name)=>new(name,PipeDirection.Out,1,PipeTransmissionMode.Byte,PipeOptions.Asynchronous,65536,65536);
    /// <summary>Tests exact required encoder identities before raw media starts flowing.</summary>
    async Task ValidateCodecs(OutputProfile profile,bool audio,CancellationToken token) {
        var start=new ProcessStartInfo(Executable){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};start.ArgumentList.Add("-hide_banner");start.ArgumentList.Add("-encoders");using var process=System.Diagnostics.Process.Start(start) ?? throw new IOException("Unable to inspect encoders.");using var timeout=CancellationTokenSource.CreateLinkedTokenSource(token);timeout.CancelAfter(TimeSpan.FromSeconds(15));using var registration=timeout.Token.Register(()=>{try {if(!process.HasExited) {process.Kill(true);}}catch(InvalidOperationException) {}});Task<string> stdout=process.StandardOutput.ReadToEndAsync(timeout.Token);Task<string> stderr=process.StandardError.ReadToEndAsync(timeout.Token);await process.WaitForExitAsync(timeout.Token);string text=await stdout;await stderr;if(process.ExitCode!=0 || text.Length>262144) {throw new IOException("Unable to inspect codec availability.");}var codecs=text.Split('\n').Select(line=>line.Split(' ',StringSplitOptions.RemoveEmptyEntries)).Where(columns=>columns.Length>1).Select(columns=>columns[1]).ToHashSet(StringComparer.Ordinal);string video=profile.PreserveAlpha?"prores_ks":"libx264";string sound=profile.PreserveAlpha?"pcm_f32le":"aac";if(!codecs.Contains(video) || audio && !codecs.Contains(sound)) {throw new InvalidDataException("Required output codec is unavailable.");}
    }
    /// <summary>Continuously drains stderr while retaining only the last sixteen KiB.</summary>
    async Task DrainErrors(StreamReader reader) {char[] buffer=new char[1024];int count;while((count=await reader.ReadAsync(buffer))>0) {lock(ErrorGate) {Errors.Append(buffer,0,count);if(Errors.Length>16384) {Errors.Remove(0,Errors.Length-16384);}}}}
    /// <summary>Returns the current bounded diagnostic text.</summary>
    string ErrorText() {lock(ErrorGate) {return Errors.ToString();}}
    /// <summary>Unblocks pending pipe connections when the encoder exits unexpectedly.</summary>
    void EncoderExited(object sender,EventArgs args) {try {Cancellation?.Cancel();}catch(ObjectDisposedException) {}}
    /// <summary>Terminates only this encoder process tree to release blocked writers.</summary>
    void Kill() {try {if(Process!=null && !Process.HasExited) {Process.Kill(true);}}catch(InvalidOperationException) {}}
}
