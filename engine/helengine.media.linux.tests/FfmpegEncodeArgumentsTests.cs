using helengine.media;
using helengine.media.linux;
namespace helengine.media.linux.tests;
/// <summary>Checks the immutable silent HEVC wire profile independently of installed encoders.</summary>
public sealed class FfmpegEncodeArgumentsTests {
    /// <summary>CPU RGBA input is explicitly mapped into Main 8-bit SDR709 hvc1 with the fixed rate-control contract.</summary>
    [Fact] public void BuildsPinnedSilentHevcProfile() {
        var document=new CompositionDocument {Width=16,Height=16,FrameRate=new(60,1),Duration=new(1,1),Audio=new(){Enabled=false}};
        var args=FfmpegEncodeArguments.Build(document,OutputProfile.Resolve("mp4-hevc-silent.v1",16,16),"result.partial");
        Assert.Contains("pipe:0",args);
        Assert.Equal("libx265",Value(args,"-c:v"));
        Assert.Equal("main",Value(args,"-profile:v"));
        Assert.Equal("hvc1",Value(args,"-tag:v"));
        Assert.Equal("yuv420p",Value(args,"-pix_fmt"));
        Assert.Equal("60/1",Value(args,"-framerate"));
        Assert.Equal("60/1",Value(args,"-r"));
        Assert.Equal("cfr",Value(args,"-fps_mode"));
        Assert.Equal("40M",Value(args,"-b:v"));
        Assert.Equal("60M",Value(args,"-maxrate"));
        Assert.Equal("120",Value(args,"-g"));
        Assert.Equal("0",Value(args,"-bf"));
        Assert.Equal("bt709",Value(args,"-color_primaries"));
        Assert.Equal("bt709",Value(args,"-color_trc"));
        Assert.Equal("bt709",Value(args,"-colorspace"));
        Assert.Contains("-an",args);
        Assert.Equal("result.partial",args[^1]);
    }
    /// <summary>Returns the value following a unique FFmpeg option.</summary>
    static string Value(IReadOnlyList<string> args,string name)=>args[args.ToList().IndexOf(name)+1];
}
