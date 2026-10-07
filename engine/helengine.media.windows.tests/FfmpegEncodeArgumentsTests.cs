using helengine.media;
namespace helengine.media.windows.tests;
/// <summary>Rejects any editorial processing at the final encoding boundary.</summary>
public sealed class FfmpegEncodeArgumentsTests {
    /// <summary>Arguments contain only raw stream declarations, codec conversion and muxing.</summary>
    [Fact] public void NoEditorialFiltersInArguments() {var document=new CompositionDocument{Width=32,Height=32,FrameRate=new(24,1),Duration=new(2,1)};var arguments=FfmpegEncodeArguments.Build(document,OutputProfile.Resolve("mp4-h264-aac.v1",32,32),"video-pipe","audio-pipe","out.mp4");Assert.DoesNotContain(arguments,item=>item is "-vf" or "-af" or "-filter_complex" or "-ss" or "-t" or "-shortest");Assert.Contains("rawvideo",arguments);Assert.Contains("f32le",arguments);Assert.Contains("libx264",arguments);Assert.Contains("aac",arguments);}
}
