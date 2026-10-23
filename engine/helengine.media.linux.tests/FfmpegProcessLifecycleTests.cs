using System.Diagnostics;
using helengine.media;
using helengine.media.linux;
namespace helengine.media.linux.tests;
/// <summary>Uses explicitly owned Linux helper children to prove deadline, backpressure and process reaping behavior.</summary>
public sealed class FfmpegProcessLifecycleTests {
    /// <summary>Fixed Linux scratch directory; helper scripts and PID records are removed after every test.</summary>
    const string Scratch="/tmp/helengine/linux/encoder-lifecycle";
    /// <summary>Preflight cannot hang forever and reaps the owned child before deleting its partial.</summary>
    [LinuxFact] public async Task HungPreflightTimesOutAndReapsOwnedProcess() {
        string script=CreateScript("preflight","exec sleep 600\n");string target=Path.Combine(Scratch,"preflight.mp4");
        try {
            var elapsed=Stopwatch.StartNew();
            using(var encoder=new FfmpegCompositionEncoder(script,TimeSpan.FromMilliseconds(300))) {
                await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>encoder.BeginAsync(Document(64),Profile(64),target,CancellationToken.None));
            }
            Assert.True(elapsed.Elapsed<TimeSpan.FromSeconds(5));AssertReaped("preflight");
            Assert.False(File.Exists(target));Assert.False(File.Exists(target+".partial"));
        }finally {DeleteScript("preflight");}
    }
    /// <summary>A stalled raw input allows only one frame in flight, times out, and leaves neither an owned child nor a published file.</summary>
    [LinuxFact] public async Task StalledStdinRejectsConcurrentFramesAndTimesOut() {
        string script=CreateScript("stdin","case \" $* \" in *\" -frames:v 1 \"*) cat >/dev/null; exit 0;; esac\nexec sleep 600\n");
        string target=Path.Combine(Scratch,"stdin.mp4");
        try {
            using(var encoder=new FfmpegCompositionEncoder(script,TimeSpan.FromSeconds(1))) {
                await encoder.BeginAsync(Document(1024),Profile(1024),target,CancellationToken.None);
                using var frame=new MediaVideoFrame(new(0,1),new(1,60),new RgbaTestSurface(1024,1024));
                var elapsed=Stopwatch.StartNew();Task blocked=encoder.WriteVideoFrameAsync(frame,0,CancellationToken.None);
                await Assert.ThrowsAsync<InvalidOperationException>(()=>encoder.WriteVideoFrameAsync(frame,0,CancellationToken.None));
                await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>blocked);
                Assert.True(elapsed.Elapsed<TimeSpan.FromSeconds(5));
            }
            AssertReaped("stdin");Assert.False(File.Exists(target));Assert.False(File.Exists(target+".partial"));
        }finally {DeleteScript("stdin");}
    }
    /// <summary>Creates only a fixture-owned script whose shell process is replaced by its owned sleeper child.</summary>
    static string CreateScript(string name,string body) {
        if(!OperatingSystem.IsLinux()) {throw new PlatformNotSupportedException();}
        Directory.CreateDirectory(Scratch);string script=Path.Combine(Scratch,name+".sh");
        Assert.False(File.Exists(script));Assert.False(File.Exists(Path.Combine(Scratch,name+".pid")));
        File.WriteAllText(script,"#!/bin/sh\nprintf '%s\\n' \"$$\" >> '"+Path.Combine(Scratch,name+".pid")+"'\n"+body);
        File.SetUnixFileMode(script,UnixFileMode.UserRead|UnixFileMode.UserWrite|UnixFileMode.UserExecute);return script;
    }
    /// <summary>Verifies that every PID recorded by this test's children has already exited.</summary>
    static void AssertReaped(string name) {
        foreach(string line in File.ReadAllLines(Path.Combine(Scratch,name+".pid"))) {
            try {using var process=Process.GetProcessById(int.Parse(line));Assert.True(process.HasExited,"Owned child is still running: "+line);}
            catch(ArgumentException) { }
        }
    }
    /// <summary>Removes only deterministic files created by the named fixture.</summary>
    static void DeleteScript(string name) {
        string pidFile=Path.Combine(Scratch,name+".pid");
        if(File.Exists(pidFile)) {
            foreach(string line in File.ReadAllLines(pidFile)) {
                try {using var process=Process.GetProcessById(int.Parse(line));if(!process.HasExited) {process.Kill(true);Assert.True(process.WaitForExit(5000));}}
                catch(ArgumentException) { }
            }
        }
        File.Delete(Path.Combine(Scratch,name+".sh"));File.Delete(pidFile);
    }
    /// <summary>Creates a one-frame silent document at the chosen transport stress size.</summary>
    static CompositionDocument Document(int size)=>new(){Width=size,Height=size,FrameRate=new(60,1),Duration=new(1,60),Audio=new(){Enabled=false}};
    /// <summary>Resolves the fixed profile at the transport stress size.</summary>
    static OutputProfile Profile(int size)=>OutputProfile.Resolve("mp4-hevc-silent.v1",size,size);
}
