namespace helengine.media.tests;
/// <summary>Checks user-visible mixing, envelope timing and reproducible offline blocks.</summary>
public sealed class CompositionAudioMixerTests {
    /// <summary>Two unit-gain sources sum without silently normalizing their volume.</summary>
    [Fact] public void TwoTracksSumWithoutNormalization() {
        var document=Document();document.AudioClips.Add(Clip("second"));using var resolver=new CountingAudioResolver();using var mixer=new CompositionAudioMixer(resolver);
        var result=mixer.Render(document,0,128);Assert.All(result.Block.Samples.ToArray(),sample=>Assert.Equal(1.5f,sample));Assert.Equal(1.5,result.Peaks.Maximum);Assert.True(result.Peaks.ExceedsFullScale);
    }
    /// <summary>A linear fade has exact gain at its indexed midpoint.</summary>
    [Fact] public void FadeAtExactSample() {
        var document=Document();document.AudioClips[0].Envelopes.Add(new(){Type="linear",Start=MediaTime.Zero,Duration=new(1,1),From=0,To=1});using var resolver=new CountingAudioResolver();using var mixer=new CompositionAudioMixer(resolver);
        Assert.Equal(.375f,mixer.Render(document,24000,1).Block.Samples.Span[0]);
    }
    /// <summary>The sample at the exclusive clip end is not played twice.</summary>
    [Fact] public void EndExclusiveDoesNotDuplicateSample() {
        var document=Document();document.AudioClips[0].End=new(1,1);using var resolver=new CountingAudioResolver();using var mixer=new CompositionAudioMixer(resolver);
        Assert.Equal(.75f,mixer.Render(document,47999,1).Block.Samples.Span[0]);Assert.Equal(0,mixer.Render(document,48000,1).Block.Samples.Span[0]);
    }
    /// <summary>Muted tracks do not open media and produce explicit silence.</summary>
    [Fact] public void MutedTrackDoesNotDecode() {
        var document=Document();document.AudioClips[0].Muted=true;using var resolver=new CountingAudioResolver();using var mixer=new CompositionAudioMixer(resolver);
        Assert.All(mixer.Render(document,0,128).Block.Samples.ToArray(),sample=>Assert.Equal(0,sample));Assert.Equal(0,resolver.Opens);
    }
    /// <summary>Random block query order does not alter the final samples.</summary>
    [Fact] public void RandomBlockOrderMatchesWholeRender() {
        var document=Document();document.AudioClips[0].Envelopes.Add(new(){Type="linear",Start=MediaTime.Zero,Duration=new(1,1),From=0,To=1});using var resolver=new CountingAudioResolver();using var mixer=new CompositionAudioMixer(resolver);
        var whole=mixer.Render(document,0,4096).Block.Samples.ToArray();var tail=mixer.Render(document,2048,2048).Block.Samples.ToArray();var first=mixer.Render(document,0,2048).Block.Samples.ToArray();Assert.Equal(whole,first.Concat(tail).ToArray());
    }
    /// <summary>Equal-power crossfade retains combined power at the overlap midpoint.</summary>
    [Fact] public void CrossfadeEqualPowerAvoidsDip() {
        var incoming=new AudioEnvelope{Type="equal_power_in",Start=MediaTime.Zero,Duration=new(1,1)};var outgoing=new AudioEnvelope{Type="equal_power_out",Start=MediaTime.Zero,Duration=new(1,1)};
        double a=incoming.At(new(1,2));double b=outgoing.At(new(1,2));Assert.Equal(1,a*a+b*b,12);
    }
    /// <summary>Creates a resolved stereo timeline and constant source.</summary>
    static CompositionDocument Document() => new(){Id="audio",Revision=1,Width=32,Height=32,FrameRate=new(24,1),Duration=new(3,1),Media=[new(){Id="tone",Kind="audio",Path="tone.wav",Sha256=new string('a',64),Duration=new(3,1)}],AudioClips=[Clip("voice")]};
    /// <summary>Creates a three-second resolved sound item.</summary>
    static AudioClip Clip(string id) => new(){Id=id,MediaId="tone",Start=MediaTime.Zero,End=new(3,1),SourceIn=MediaTime.Zero,SourceOut=new(3,1)};
}
