using System.Diagnostics;
using System.Text.Json;
using helengine.media;
using helengine.media.linux;
namespace helengine.media.linux.tests;
/// <summary>Exercises actual host FFmpeg encoding, boundary failures and exclusive output publication.</summary>
public sealed class FfmpegCompositionEncoderTests {
    /// <summary>Host-provided trusted FFmpeg executable; never downloaded or installed by the suite.</summary>
    static string Executable=>Environment.GetEnvironmentVariable("HELENGINE_TEST_FFMPEG")??"ffmpeg";
    /// <summary>One fixed platform-specific scratch directory overwritten by this suite.</summary>
    static string Scratch=>OperatingSystem.IsWindows()?@"C:\temp\helengine\linux\encoder-fixture":"/tmp/helengine/linux/encoder-fixture";
    /// <summary>Boundary validation runs before attempting to launch even a nonexistent executable.</summary>
    [Fact] public async Task RejectsAudioAndNonSixtyFpsBeforeStartingProcess() {
        using var encoder=new FfmpegCompositionEncoder("nonexistent-ffmpeg");var document=Document(1);document.Audio.Enabled=true;
        await Assert.ThrowsAsync<InvalidDataException>(()=>encoder.BeginAsync(document,Profile(),"unused.mp4",CancellationToken.None));
        document.Audio.Enabled=false;document.FrameRate=new(30,1);
        await Assert.ThrowsAsync<InvalidDataException>(()=>encoder.BeginAsync(document,Profile(),"unused.mp4",CancellationToken.None));
        document.FrameRate=new(60,1);document.AudioClips.Add(new());
        await Assert.ThrowsAsync<InvalidDataException>(()=>encoder.BeginAsync(document,Profile(),"unused.mp4",CancellationToken.None));
    }
    /// <summary>Pre-cancelled jobs do not create outputs or start a child process.</summary>
    [Fact] public async Task PreCancelledBeginDoesNotCreatePartial() {
        string target=Target("cancelled.mp4");using var cancelled=new CancellationTokenSource();cancelled.Cancel();
        using var encoder=new FfmpegCompositionEncoder("nonexistent-ffmpeg");
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>encoder.BeginAsync(Document(1),Profile(),target,cancelled.Token));
        Assert.False(File.Exists(target));Assert.False(File.Exists(target+".partial"));
    }
    /// <summary>Rejected ordering, resizing, timing and byte layout never alter the accepted frame count.</summary>
    [Fact] public async Task ValidatesFramesThenPublishesExactOrderedDecodableHevc() {
        string target=Target("ordered.mp4");using var encoder=new FfmpegCompositionEncoder(Executable);
        await encoder.BeginAsync(Document(3),Profile(),target,CancellationToken.None);
        Assert.Equal("libx265",encoder.SelectedEncoder);Assert.False(encoder.IsHardwareAccelerated);
        using var first=Frame(0,40);
        await Assert.ThrowsAsync<InvalidDataException>(()=>encoder.WriteVideoFrameAsync(first,1,CancellationToken.None));
        using var resized=new MediaVideoFrame(new(0,1),new(1,60),new RgbaTestSurface(66,64));
        await Assert.ThrowsAsync<InvalidDataException>(()=>encoder.WriteVideoFrameAsync(resized,0,CancellationToken.None));
        using var malformed=new MediaVideoFrame(new(0,1),new(1,60),new RgbaTestSurface(64,64,40,16383));
        await Assert.ThrowsAsync<InvalidDataException>(()=>encoder.WriteVideoFrameAsync(malformed,0,CancellationToken.None));
        using var wrongTime=Frame(1,40);
        await Assert.ThrowsAsync<InvalidDataException>(()=>encoder.WriteVideoFrameAsync(wrongTime,0,CancellationToken.None));
        await encoder.WriteVideoFrameAsync(first,0,CancellationToken.None);
        using var second=Frame(1,120);await encoder.WriteVideoFrameAsync(second,1,CancellationToken.None);
        using var third=Frame(2,200);await encoder.WriteVideoFrameAsync(third,2,CancellationToken.None);
        await Assert.ThrowsAsync<InvalidDataException>(()=>encoder.WriteVideoFrameAsync(third,3,CancellationToken.None));
        await Assert.ThrowsAsync<InvalidDataException>(()=>encoder.WriteAudioBlockAsync(null,CancellationToken.None));
        Assert.False(File.Exists(target));Assert.True(File.Exists(target+".partial"));
        await encoder.FinishVideoAsync(CancellationToken.None);await encoder.CompleteAsync(CancellationToken.None);
        Assert.True(File.Exists(target));Assert.False(File.Exists(target+".partial"));
        byte[] decoded=await DecodeAsync(target);
        Assert.Equal(3*64*64*4,decoded.Length);
        Assert.InRange((int)decoded[0],36,44);Assert.InRange((int)decoded[16384],116,124);Assert.InRange((int)decoded[32768],196,204);
        await AssertMetadataAsync(target);
        File.Delete(target);
    }
    /// <summary>A preexisting final or stale partial file belongs to its caller and is never overwritten or deleted.</summary>
    [Theory][InlineData(false)][InlineData(true)] public async Task PreservesUnrelatedDestination(bool partial) {
        string target=Target(partial?"existing-partial.mp4":"existing-final.mp4");string existing=target+(partial?".partial":"");await File.WriteAllTextAsync(existing,"unrelated");
        using(var encoder=new FfmpegCompositionEncoder("nonexistent-ffmpeg")) {await Assert.ThrowsAsync<IOException>(()=>encoder.BeginAsync(Document(1),Profile(),target,CancellationToken.None));}
        Assert.Equal("unrelated",await File.ReadAllTextAsync(existing));File.Delete(existing);
    }
    /// <summary>Incomplete recording and explicit cancellation remove only the owned partial and never publish success.</summary>
    [Fact] public async Task IncompleteAndCancelledJobsDoNotPublish() {
        string target=Target("incomplete.mp4");
        using(var encoder=new FfmpegCompositionEncoder(Executable)) {
            await encoder.BeginAsync(Document(2),Profile(),target,CancellationToken.None);
            using var frame=Frame(0,80);await encoder.WriteVideoFrameAsync(frame,0,CancellationToken.None);
            await Assert.ThrowsAsync<InvalidDataException>(()=>encoder.CompleteAsync(CancellationToken.None));
        }
        Assert.False(File.Exists(target));Assert.False(File.Exists(target+".partial"));
        using(var lifetime=new CancellationTokenSource()) {
            using(var encoder=new FfmpegCompositionEncoder(Executable)) {
                await encoder.BeginAsync(Document(1),Profile(),target,lifetime.Token);lifetime.Cancel();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>encoder.CompleteAsync(CancellationToken.None));
            }
        }
        Assert.False(File.Exists(target));Assert.False(File.Exists(target+".partial"));
    }
    /// <summary>Publication remains exclusive even when another writer creates the destination after recording begins.</summary>
    [Fact] public async Task PreservesDestinationCreatedDuringEncoding() {
        string target=Target("publication-race.mp4");
        using(var encoder=new FfmpegCompositionEncoder(Executable)) {
            await encoder.BeginAsync(Document(1),Profile(),target,CancellationToken.None);
            using var frame=Frame(0,80);await encoder.WriteVideoFrameAsync(frame,0,CancellationToken.None);
            await File.WriteAllTextAsync(target,"other writer");
            await Assert.ThrowsAsync<IOException>(()=>encoder.CompleteAsync(CancellationToken.None));
        }
        Assert.Equal("other writer",await File.ReadAllTextAsync(target));Assert.False(File.Exists(target+".partial"));File.Delete(target);
    }
    /// <summary>Returns an exact three-component document for the encoder boundary, without source assets.</summary>
    static CompositionDocument Document(int count)=>new(){Width=64,Height=64,FrameRate=new(60,1),Duration=new(count,60),Audio=new(){Enabled=false}};
    /// <summary>Returns the fixed silent HEVC profile at the fixture resolution.</summary>
    static OutputProfile Profile()=>OutputProfile.Resolve("mp4-hevc-silent.v1",64,64);
    /// <summary>Creates one opaque grayscale frame at its exact CFR timestamp.</summary>
    static MediaVideoFrame Frame(long index,byte gray)=>new(new(index,60),new(1,60),new RgbaTestSurface(64,64,gray));
    /// <summary>Returns a deterministic scratch target, rejecting stale evidence from an interrupted test.</summary>
    static string Target(string name) {Directory.CreateDirectory(Scratch);string target=Path.Combine(Scratch,name);Assert.False(File.Exists(target));Assert.False(File.Exists(target+".partial"));return target;}
    /// <summary>Decodes all output frames through actual FFmpeg for independent count and ordering assertions.</summary>
    static async Task<byte[]> DecodeAsync(string target) {
        var start=new ProcessStartInfo(Executable){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};
        foreach(string arg in new[]{"-v","error","-i",target,"-f","rawvideo","-pix_fmt","rgba","-"}) {start.ArgumentList.Add(arg);}
        using var process=Process.Start(start);using var output=new MemoryStream();using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(15));
        Task errors=process.StandardError.ReadToEndAsync(timeout.Token);await process.StandardOutput.BaseStream.CopyToAsync(output,timeout.Token);await process.WaitForExitAsync(timeout.Token);await errors;
        Assert.Equal(0,process.ExitCode);return output.ToArray();
    }
    /// <summary>Inspects independent stream and frame metadata to verify HEVC, color, timing, silence and the absence of B frames.</summary>
    static async Task AssertMetadataAsync(string target) {
        string executable=Environment.GetEnvironmentVariable("HELENGINE_TEST_FFPROBE")??Path.Combine(Path.GetDirectoryName(Executable)??"",OperatingSystem.IsWindows()?"ffprobe.exe":"ffprobe");
        var start=new ProcessStartInfo(executable){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};
        foreach(string arg in new[]{"-v","error","-show_streams","-show_frames","-of","json",target}) {start.ArgumentList.Add(arg);}
        using var process=Process.Start(start);using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(15));
        Task<string> output=process.StandardOutput.ReadToEndAsync(timeout.Token);Task errors=process.StandardError.ReadToEndAsync(timeout.Token);
        await process.WaitForExitAsync(timeout.Token);await errors;Assert.Equal(0,process.ExitCode);
        using var json=JsonDocument.Parse(await output);var streams=json.RootElement.GetProperty("streams");Assert.Equal(1,streams.GetArrayLength());
        var stream=streams[0];Assert.Equal("hevc",stream.GetProperty("codec_name").GetString());Assert.Equal("hvc1",stream.GetProperty("codec_tag_string").GetString());
        Assert.Equal("Main",stream.GetProperty("profile").GetString());Assert.Equal("yuv420p",stream.GetProperty("pix_fmt").GetString());
        Assert.Equal("60/1",stream.GetProperty("avg_frame_rate").GetString());Assert.Equal("tv",stream.GetProperty("color_range").GetString());
        Assert.Equal("bt709",stream.GetProperty("color_space").GetString());Assert.Equal("bt709",stream.GetProperty("color_transfer").GetString());Assert.Equal("bt709",stream.GetProperty("color_primaries").GetString());
        var frames=json.RootElement.GetProperty("frames");Assert.Equal(3,frames.GetArrayLength());
        for(int index=0;index<3;index++) {Assert.NotEqual("B",frames[index].GetProperty("pict_type").GetString());Assert.Equal(index*1000,frames[index].GetProperty("best_effort_timestamp").GetInt64());}
    }
}
