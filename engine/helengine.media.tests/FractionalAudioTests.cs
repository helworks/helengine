namespace helengine.media.tests;
/// <summary>Checks exact half-sample source offsets and exclusive fractional timeline boundaries.</summary>
public sealed class FractionalAudioTests {
    /// <summary>A half-sample source offset interpolates adjacent PCM frames instead of rounding its phase.</summary>
    [Fact] public void HalfSampleSourceOffsetInterpolates() {var document=Document();document.AudioClips[0].SourceIn=new(1,96000);document.AudioClips[0].SourceOut=new(96001,96000);using var resolver=new RampAudioResolver();using var mixer=new CompositionAudioMixer(resolver);var block=mixer.Render(document,0,2).Block;Assert.Equal(.5f,block.Samples.Span[0]);Assert.Equal(1.5f,block.Samples.Span[2]);}
    /// <summary>The first sample before a fractional clip start remains silence.</summary>
    [Fact] public void HalfSampleClipStartUsesCeilingBoundary() {var document=Document();document.AudioClips[0].Start=new(1,96000);using var resolver=new RampAudioResolver();using var mixer=new CompositionAudioMixer(resolver);var block=mixer.Render(document,0,2).Block;Assert.Equal(0,block.Samples.Span[0]);Assert.Equal(.5f,block.Samples.Span[2]);}
    /// <summary>Creates one second of resolved ramp sound.</summary>
    static CompositionDocument Document()=>new(){Id="fraction",Revision=1,Width=32,Height=32,Duration=new(1,1),FrameRate=new(24,1),Media=[new(){Id="ramp",Kind="audio",Path="ramp.wav",Sha256=new string('a',64),Duration=new(2,1)}],AudioClips=[new(){Id="clip",MediaId="ramp",Start=MediaTime.Zero,End=new(1,1),SourceOut=new(1,1)}]};
}
