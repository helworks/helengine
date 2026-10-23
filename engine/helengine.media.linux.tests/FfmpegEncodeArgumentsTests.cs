using helengine.media;
using helengine.media.linux;
namespace helengine.media.linux.tests;
/// <summary>Checks the immutable silent HEVC wire profile independently of installed encoders.</summary>
public sealed class FfmpegEncodeArgumentsTests {
    /// <summary>CPU RGBA input is explicitly mapped into Main 8-bit SDR709 hvc1 with the fixed rate-control contract.</summary>
    [Theory][InlineData("libx265")][InlineData("hevc_nvenc")] public void BuildsPinnedSilentHevcProfile(string encoderName) {
        var document=new CompositionDocument {Width=16,Height=16,FrameRate=new(60,1),Duration=new(1,1),Audio=new(){Enabled=false}};
        var args=FfmpegEncodeArguments.Build(document,OutputProfile.Resolve("mp4-hevc-silent.v1",16,16),"result.partial",encoderName);
        Assert.Contains("pipe:0",args);
        Assert.Equal(encoderName,Value(args,"-c:v"));
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
        Assert.Contains("setparams=range=limited:color_primaries=bt709:color_trc=bt709:colorspace=bt709",Value(args,"-vf"));
        if(encoderName=="libx265") {
            Assert.Equal("fast",Value(args,"-preset"));Assert.Contains("-x265-params",args);Assert.DoesNotContain("-gpu",args);
        }else {
            Assert.Equal("p4",Value(args,"-preset"));Assert.Equal("ll",Value(args,"-tune"));Assert.Equal("vbr",Value(args,"-rc"));
            Assert.Equal("0",Value(args,"-rc-lookahead"));Assert.Equal("0",Value(args,"-delay"));Assert.Equal("4",Value(args,"-surfaces"));
            Assert.Equal("1",Value(args,"-zerolatency"));Assert.Equal("1",Value(args,"-no-scenecut"));Assert.DoesNotContain("-x265-params",args);
        }
        Assert.Contains("-an",args);
        Assert.Equal("result.partial",args[^1]);
    }
    /// <summary>Preserves software defaults while refusing arbitrary names before any executable or driver is opened.</summary>
    [Fact] public void DefaultsToSoftwareAndRejectsUnrecognizedSelection() {
        var document=new CompositionDocument {Width=16,Height=16,FrameRate=new(60,1),Duration=new(1,1),Audio=new(){Enabled=false}};
        var profile=OutputProfile.Resolve("mp4-hevc-silent.v1",16,16);
        Assert.Equal("libx265",Value(FfmpegEncodeArguments.Build(document,profile,"result.partial"),"-c:v"));
        Assert.Throws<ArgumentException>(()=>FfmpegEncodeArguments.Build(document,profile,"result.partial","automatic"));
        Assert.Throws<ArgumentException>(()=>new FfmpegCompositionEncoder("ffmpeg","automatic"));
    }
    /// <summary>Returns the value following a unique FFmpeg option.</summary>
    static string Value(IReadOnlyList<string> args,string name)=>args[args.ToList().IndexOf(name)+1];
}
