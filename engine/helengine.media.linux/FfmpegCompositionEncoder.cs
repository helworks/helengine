using System.Diagnostics;
using System.Globalization;
using helengine.media;
namespace helengine.media.linux;
/// <summary>Encodes ordered CPU RGBA frames through bounded stdin, publishing only a finalized and decoded silent HEVC file.</summary>
public sealed class FfmpegCompositionEncoder : ICompositionEncoder {
    /// <summary>Host-selected executable; the backend never searches for downloads or installs codecs.</summary>
    readonly string Executable;
    /// <summary>Explicit allowlisted encoder requested by the host, with no capability-based fallback.</summary>
    readonly string RequestedEncoder;
    /// <summary>Upper bound for a stalled write, child exit, preflight or verification operation.</summary>
    readonly TimeSpan OperationTimeout;
    /// <summary>Rejects overlapping producers instead of retaining an unbounded queue of frames.</summary>
    readonly SemaphoreSlim Writer=new(1,1);
    /// <summary>Bounded stderr suffix drained continuously throughout encoding.</summary>
    readonly BoundedProcessDiagnostics Errors=new();
    /// <summary>Owned encoding child, never an externally discovered process.</summary>
    Process Child;
    /// <summary>Asynchronous drain which prevents stderr backpressure.</summary>
    Task ErrorDrain;
    /// <summary>Lifetime cancellation inherited from the recording operation.</summary>
    CancellationTokenSource Lifetime;
    /// <summary>Terminates the owned child when recording cancellation occurs.</summary>
    CancellationTokenRegistration KillRegistration;
    /// <summary>Output contract accepted at begin.</summary>
    OutputProfile Profile;
    /// <summary>Exclusive final pathname supplied by the host.</summary>
    string Target;
    /// <summary>Fixed partial pathname reserved with CreateNew before launching FFmpeg.</summary>
    string Partial;
    /// <summary>Whether this instance owns the partial reservation and may remove it.</summary>
    bool OwnsPartial;
    /// <summary>Expected total number of exact CFR frames.</summary>
    long ExpectedFrames;
    /// <summary>Index and timestamp of the next accepted frame.</summary>
    long NextFrame;
    /// <summary>Whether video EOF has been signaled.</summary>
    bool VideoEnded;
    /// <summary>Whether output was verified and exclusively published.</summary>
    bool Completed;
    /// <summary>Whether an operation failed after starting the child.</summary>
    bool Failed;
    /// <summary>Prevents any use after teardown.</summary>
    bool Disposed;
    /// <summary>Uses a thirty-second limit for stalled operations with no queued frame buffering.</summary>
    public FfmpegCompositionEncoder(string executable):this(executable,FfmpegEncodeArguments.EncoderName,TimeSpan.FromSeconds(30)) { }
    /// <summary>Accepts a bounded host timeout, permitting shorter deadlines in lifecycle tests.</summary>
    public FfmpegCompositionEncoder(string executable,TimeSpan operationTimeout):this(executable,FfmpegEncodeArguments.EncoderName,operationTimeout) { }
    /// <summary>Selects software libx265 or NVIDIA HEVC explicitly, requiring an actual initialization preflight.</summary>
    public FfmpegCompositionEncoder(string executable,string encoderName):this(executable,encoderName,TimeSpan.FromSeconds(30)) { }
    /// <summary>Combines an explicit encoder selection with the bounded operation deadline used by lifecycle tests.</summary>
    public FfmpegCompositionEncoder(string executable,string encoderName,TimeSpan operationTimeout) {
        if(string.IsNullOrWhiteSpace(executable)) {throw new ArgumentException("A trusted FFmpeg executable is required.",nameof(executable));}
        if(operationTimeout<=TimeSpan.Zero || operationTimeout>TimeSpan.FromMinutes(5)) {throw new ArgumentOutOfRangeException(nameof(operationTimeout));}
        FfmpegEncodeArguments.ValidateEncoder(encoderName);
        Executable=executable;RequestedEncoder=encoderName;OperationTimeout=operationTimeout;
    }
    /// <summary>Exact selected encoder identity, populated only after an actual initialization preflight succeeds.</summary>
    public string SelectedEncoder {get;private set;}="";
    /// <summary>Reports hardware encoding only after the requested NVIDIA encoder has successfully initialized.</summary>
    public bool IsHardwareAccelerated=>SelectedEncoder==FfmpegEncodeArguments.NvidiaEncoderName;
    /// <summary>Validates the silent profile, reserves a fixed partial, and preflights actual Main HEVC initialization.</summary>
    public async Task BeginAsync(CompositionDocument document,OutputProfile profile,string output,CancellationToken token) {
        if(Disposed || Lifetime!=null) {throw new InvalidOperationException("Encoder is disposed or already begun.");}
        token.ThrowIfCancellationRequested();FfmpegEncodeArguments.Validate(document,profile);
        Target=Path.GetFullPath(output);Partial=Target+".partial";
        if(File.Exists(Target) || Directory.Exists(Target) || File.Exists(Partial) || Directory.Exists(Partial)) {throw new IOException("Recording destination or fixed partial already exists: "+Target);}
        Directory.CreateDirectory(Path.GetDirectoryName(Target));
        Profile=profile;ExpectedFrames=(document.Duration*document.FrameRate).Ceiling();
        Lifetime=CancellationTokenSource.CreateLinkedTokenSource(token);
        try {
            using(var reservation=new FileStream(Partial,FileMode.CreateNew,FileAccess.Write,FileShare.None)) {OwnsPartial=true;}
            await PreflightAsync(document,profile,Lifetime.Token);SelectedEncoder=RequestedEncoder;
            var start=StartInfo(FfmpegEncodeArguments.Build(document,profile,Partial,RequestedEncoder),true);
            Child=Process.Start(start)??throw new IOException("Unable to start HEVC encoder.");
            ErrorDrain=Errors.DrainAsync(Child.StandardError);
            KillRegistration=Lifetime.Token.Register(KillOwnedChild);
        }catch {Abort();throw;}
    }
    /// <summary>Writes one complete CPU RGBA frame directly to stdin with exact index, PTS, size and byte-count validation.</summary>
    public async Task WriteVideoFrameAsync(MediaVideoFrame frame,long index,CancellationToken token) {
        RequireActive();Lifetime.Token.ThrowIfCancellationRequested();token.ThrowIfCancellationRequested();
        if(!await Writer.WaitAsync(0,token)) {throw new InvalidOperationException("Concurrent frame writes are not allowed.");}
        try {
            if(VideoEnded || frame==null || index!=NextFrame || index>=ExpectedFrames || frame.Timestamp!=new MediaTime(index,60)
                || frame.Duration!=new MediaTime(1,60) || frame.RotationDegrees!=0 || frame.Surface.Width!=Profile.Width || frame.Surface.Height!=Profile.Height) {
                throw new InvalidDataException("HEVC requires sequential unrotated CFR60 frames at the declared dimensions and timestamps.");
            }
            ReadOnlyMemory<byte> pixels=frame.Surface.ReadRgba();
            if(pixels.Length!=checked(Profile.Width*Profile.Height*4)) {throw new InvalidDataException("Expected exactly width*height*4 packed RGBA bytes.");}
            using var deadline=Deadline(token);using var registration=deadline.Token.Register(KillOwnedChild);
            try {await Child.StandardInput.BaseStream.WriteAsync(pixels,deadline.Token);NextFrame++;}
            catch {Abort();deadline.Token.ThrowIfCancellationRequested();throw;}
        }finally {Writer.Release();}
    }
    /// <summary>Audio is explicitly rejected; no PCM block is silently discarded by this silent profile.</summary>
    public Task WriteAudioBlockAsync(AudioBlock block,CancellationToken token)=>Task.FromException(new InvalidDataException("The silent HEVC profile does not accept audio blocks."));
    /// <summary>Closes stdin only after every declared frame has been accepted, allowing the muxer to finalize.</summary>
    public async Task FinishVideoAsync(CancellationToken token) {
        RequireActive();Lifetime.Token.ThrowIfCancellationRequested();token.ThrowIfCancellationRequested();
        if(!await Writer.WaitAsync(0,token)) {throw new InvalidOperationException("Cannot finish during a frame write.");}
        try {
            if(NextFrame!=ExpectedFrames) {throw new InvalidDataException("HEVC frame count is incomplete: "+NextFrame+"/"+ExpectedFrames+".");}
            if(!VideoEnded) {Child.StandardInput.Close();VideoEnded=true;}
        }finally {Writer.Release();}
    }
    /// <summary>No audio input is opened for the silent profile.</summary>
    public Task FinishAudioAsync(CancellationToken token) {RequireActive();token.ThrowIfCancellationRequested();return Task.CompletedTask;}
    /// <summary>Finalizes the muxer, independently decodes exactly the expected frame count, then publishes without overwriting.</summary>
    public async Task CompleteAsync(CancellationToken token) {
        RequireActive();Lifetime.Token.ThrowIfCancellationRequested();token.ThrowIfCancellationRequested();
        try {
            await FinishVideoAsync(token);
            using(var deadline=Deadline(token)) {
                using var registration=deadline.Token.Register(KillOwnedChild);
                await Child.WaitForExitAsync(deadline.Token);await ErrorDrain.WaitAsync(deadline.Token);
            }
            if(Child.ExitCode!=0) {throw new IOException("HEVC encoding failed: "+Errors);}
            if(!File.Exists(Partial) || new FileInfo(Partial).Length==0) {throw new IOException("HEVC encoder produced no finalized output.");}
            await VerifyAsync(token);Lifetime.Token.ThrowIfCancellationRequested();token.ThrowIfCancellationRequested();
            File.Move(Partial,Target,false);OwnsPartial=false;Completed=true;
        }catch {Abort();throw;}
    }
    /// <summary>Stops and reaps only this instance's child, deleting its unverified partial while preserving unrelated files.</summary>
    public void Dispose() {
        if(Disposed) {return;}Disposed=true;
        try {if(!Completed) {Abort();}}
        finally {KillRegistration.Dispose();Child?.Dispose();Lifetime?.Dispose();Writer.Dispose();}
    }
    /// <summary>Rejects operations outside one live recording.</summary>
    void RequireActive() {if(Disposed || Child==null || Failed || Completed) {throw new InvalidOperationException("Encoder has no active recording.");}}
    /// <summary>Creates a deadline linked to both the recording and the current caller.</summary>
    CancellationTokenSource Deadline(CancellationToken token) {var deadline=CancellationTokenSource.CreateLinkedTokenSource(token,Lifetime.Token);deadline.CancelAfter(OperationTimeout);return deadline;}
    /// <summary>Creates a process with separate arguments and redirected bounded diagnostic streams.</summary>
    ProcessStartInfo StartInfo(IReadOnlyList<string> arguments,bool input) {
        var start=new ProcessStartInfo(Executable){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=input,RedirectStandardError=true,RedirectStandardOutput=!input};
        foreach(string arg in arguments) {start.ArgumentList.Add(arg);}return start;
    }
    /// <summary>Actually initializes the requested codec and driver with one raw RGBA frame instead of trusting a listed name.</summary>
    async Task PreflightAsync(CompositionDocument document,OutputProfile profile,CancellationToken token) {
        var arguments=FfmpegEncodeArguments.Build(document,profile,"-",RequestedEncoder).ToList();
        int container=arguments.IndexOf("-movflags");arguments.RemoveRange(container,arguments.Count-container);
        arguments.AddRange(["-frames:v","1","-f","null","-"]);
        await InspectAsync(arguments,new byte[checked(profile.Width*profile.Height*4)],true,token);
    }
    /// <summary>Decodes the complete finalized file with no timestamp resampling and checks the exact accepted count.</summary>
    async Task VerifyAsync(CancellationToken token) {
        string progress=await InspectAsync(["-hide_banner","-loglevel","error","-nostdin","-xerror","-i",Partial,"-map","0:v:0","-an","-fps_mode","passthrough","-progress","pipe:1","-nostats","-f","null","-"],ReadOnlyMemory<byte>.Empty,false,token);
        string last=progress.Split('\n').LastOrDefault(line=>line.StartsWith("frame=",StringComparison.Ordinal));
        if(last==null || !long.TryParse(last.AsSpan(6).Trim(),NumberStyles.None,CultureInfo.InvariantCulture,out long count) || count!=ExpectedFrames) {throw new InvalidDataException("Final HEVC decode did not produce the exact declared frame count.");}
    }
    /// <summary>Runs a bounded inspection child, retaining only bounded diagnostics and always killing and reaping it on failure.</summary>
    async Task<string> InspectAsync(IReadOnlyList<string> arguments,ReadOnlyMemory<byte> input,bool hasInput,CancellationToken token) {
        using var deadline=Deadline(token);using var process=Process.Start(StartInfo(arguments,hasInput))??throw new IOException("Unable to start FFmpeg preflight or verification.");
        using var registration=deadline.Token.Register(()=>KillProcess(process));
        var errors=new BoundedProcessDiagnostics();var output=new BoundedProcessDiagnostics();
        Task errorDrain=errors.DrainAsync(process.StandardError);Task outputDrain=hasInput?Task.CompletedTask:output.DrainAsync(process.StandardOutput);
        try {
            if(hasInput) {await process.StandardInput.BaseStream.WriteAsync(input,deadline.Token);process.StandardInput.Close();}
            await process.WaitForExitAsync(deadline.Token);await Task.WhenAll(errorDrain,outputDrain).WaitAsync(deadline.Token);
            if(process.ExitCode!=0) {throw new IOException("FFmpeg HEVC preflight/verification failed: "+errors);}
            return output.ToString();
        }catch {
            KillProcess(process);if(!process.WaitForExit(5000)) {throw new IOException("FFmpeg inspection child did not terminate.");}
            await Task.WhenAll(errorDrain,outputDrain).WaitAsync(TimeSpan.FromSeconds(5));deadline.Token.ThrowIfCancellationRequested();throw;
        }
    }
    /// <summary>Terminates only the process started by this encoder instance.</summary>
    void KillOwnedChild() {if(Child!=null) {KillProcess(Child);}}
    /// <summary>Terminates one explicitly owned process tree while tolerating its normal exit race.</summary>
    static void KillProcess(Process process) {try {if(!process.HasExited) {process.Kill(true);}}catch(InvalidOperationException) {}}
    /// <summary>Reaps a failed child before removing the partial; no failed prefix is published as a valid recording.</summary>
    void Abort() {
        Failed=true;KillOwnedChild();
        if(Child!=null && !Child.WaitForExit(5000)) {throw new IOException("HEVC child did not terminate; its partial was retained.");}
        if(ErrorDrain!=null && !ErrorDrain.Wait(5000)) {throw new IOException("HEVC diagnostic stream did not close; its partial was retained.");}
        if(OwnsPartial) {File.Delete(Partial);OwnsPartial=false;}
    }
}
