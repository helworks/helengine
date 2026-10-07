using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Security.Cryptography;
using System.Text.Json;
using helengine.media;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
namespace helengine.media.windows.tests;
/// <summary>Encodes real composed RGBA and native PCM through both final output profiles.</summary>
public sealed class FfmpegCompositionIntegrationTests {
    /// <summary>Owned test material directory.</summary>
    const string Root="C:/dev/helworks/builds/helengine/media-composition/fixtures";
    /// <summary>Installed trusted encoder used only for codec/mux work in the feature.</summary>
    const string Ffmpeg="C:/env/ffmpeg-8.1.1-full_build-shared/bin/ffmpeg.exe";
    /// <summary>Installed diagnostic probe.</summary>
    const string Probe="C:/env/ffmpeg-8.1.1-full_build-shared/bin/ffprobe.exe";
    /// <summary>Both profiles receive exact pre-encode counts; MP4 is opaque and MOV preserves alpha.</summary>
    [Theory] [InlineData("mp4-h264-aac.v1","mp4")] [InlineData("mov-prores4444-pcm.v1","mov")]
    public async Task RealEncodingPreservesProfileAndPcmCounts(string profile,string extension) {
        string name="encode-alpha-"+Guid.NewGuid().ToString("N")+".png";using(var bitmap=new Bitmap(16,16)) {using(var graphics=Graphics.FromImage(bitmap)) {graphics.Clear(Color.FromArgb(128,255,0,0));}bitmap.Save(Path.Combine(Root,name),ImageFormat.Png);}string output=Path.Combine(Root,"encoded-"+Guid.NewGuid().ToString("N")+"."+extension);
        var document=new CompositionDocument{Id="encoded",Revision=1,Width=32,Height=32,FrameRate=new(24,1),Duration=new(2,1),BackgroundColor="#00000000",Media=[Reference(name,"image"),Reference("sync-impulse.wav","audio")],Layers=[new(){Id="image",MediaId=name,Start=MediaTime.Zero,End=new(2,1)}],AudioClips=[new(){Id="voice",MediaId="sync-impulse.wav",Start=MediaTime.Zero,End=new(2,1),SourceOut=new(2,1)}]};
        using var device=new Device(DriverType.Warp,DeviceCreationFlags.BgraSupport);using var resolver=new WindowsMediaSourceResolver(Root,device);using var compositor=new DirectX11MediaCompositor(device,resolver);using var mixer=new CompositionAudioMixer(resolver);using var encoder=new FfmpegCompositionEncoder(Ffmpeg);using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(30));var exporter=new CompositionExporter(compositor,mixer,encoder,WindowsMediaCapabilities.Describe());
        try {var report=await exporter.ExportAsync(document,OutputProfile.Resolve(profile,32,32),output,timeout.Token);Assert.Equal(48,report.FrameCount);Assert.Equal(96000,report.SampleCount);Assert.Equal(.75,report.AudioPeak);using var probe=JsonDocument.Parse(await RunText(Probe,["-v","error","-show_streams","-of","json",output]));var streams=probe.RootElement.GetProperty("streams").EnumerateArray().ToList();var video=streams.Single(stream=>stream.GetProperty("codec_type").GetString()=="video");Assert.Equal("48",video.GetProperty("nb_frames").GetString());Assert.Contains(streams,stream=>stream.GetProperty("codec_type").GetString()=="audio");byte[] pixels=await RunBytes(Ffmpeg,["-v","error","-i",output,"-frames:v","1","-f","rawvideo","-pix_fmt","rgba","pipe:1"]);Assert.Equal(4096,pixels.Length);if(extension=="mov") {Assert.InRange(pixels[3],126,130);Assert.InRange(pixels[0],252,255);}else {Assert.Equal(255,pixels[3]);Assert.InRange(pixels[0],183,190);}}
        finally {encoder.Dispose();compositor.Dispose();mixer.Dispose();resolver.Dispose();File.Delete(output);File.Delete(output+".report.json");File.Delete(Path.Combine(Root,name));}
    }
    /// <summary>Pins source dimensions, bytes and duration.</summary>
    static MediaReference Reference(string name,string kind)=>new(){Id=name,Kind=kind,Path=name,Width=kind=="image"?16:0,Height=kind=="image"?16:0,Duration=kind=="audio"?new(2,1):MediaTime.Zero,Sha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(Root,name)))).ToLowerInvariant()};
    /// <summary>Runs a bounded diagnostic text probe without shell argument interpolation.</summary>
    static async Task<string> RunText(string executable,string[] args)=>System.Text.Encoding.UTF8.GetString(await RunBytes(executable,args));
    /// <summary>Downloads only this fixture's diagnostic stream and asserts the tool succeeds.</summary>
    static async Task<byte[]> RunBytes(string executable,string[] args) {var start=new ProcessStartInfo(executable){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};foreach(string arg in args) {start.ArgumentList.Add(arg);}using var process=Process.Start(start);using var memory=new MemoryStream();var copy=process.StandardOutput.BaseStream.CopyToAsync(memory);var error=process.StandardError.ReadToEndAsync();await Task.WhenAll(copy,process.WaitForExitAsync());Assert.True(process.ExitCode==0,await error);return memory.ToArray();}
}
