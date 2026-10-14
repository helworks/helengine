namespace helengine.media.tests;
/// <summary>Checks playback rate on visual layers and audio clips: source timing, validation, JSON and pitch-preserving audio.</summary>
public sealed class PlaybackRateTests {
    /// <summary>A layer at 1.5x reads its source at SourceIn plus one and a half times its local time.</summary>
    [Fact] public void LayerSourceTimeAdvancesAtItsRate() {
        var document=Video(1.5,new(1,1),new(6,1));
        Assert.Equal(new MediaTime(4,1),Assert.Single(CompositionEvaluator.Evaluate(document,new(2,1))).SourceTime);
        Assert.DoesNotContain(CompositionValidator.Validate(document,MediaCapabilities.Basic()),error=>error.Code is "source_interval" or "invalid_rate");
    }
    /// <summary>A long layer at a rate with a nanosecond-precision fraction (4/3, 1.234567891) evaluates late frames and validates without overflowing the exact time fraction.</summary>
    [Theory] [InlineData(1.333333333)] [InlineData(1.1111111111)] [InlineData(1.234567891)] [InlineData(0.666666667)]
    public void LongLayerAtAnIrregularRateDoesNotOverflow(double rate) {
        var start=new MediaTime(1,1000000000);var end=new MediaTime(600,1);
        var document=new CompositionDocument{Id="speed",Revision=1,Width=32,Height=32,FrameRate=new(30,1),Duration=end,BackgroundColor="#000000FF",Audio=new(){Enabled=false},
            Media=[new(){Id="clip",Kind="video",Path="clip.mp4",Sha256=new string('a',64),Width=32,Height=32,Duration=new(3000,1)}],
            Layers=[new(){Id="take",MediaId="clip",Start=start,End=end,SourceIn=MediaTime.Zero,SourceOut=new(2500,1),Rate=rate}]};
        foreach(long frame in new long[]{271,9001,17999}) {
            var time=new MediaTime(frame,30);var source=Assert.Single(CompositionEvaluator.Evaluate(document,time)).SourceTime;
            Assert.InRange(source.ToSeconds()-(time-start).ToSeconds()*rate,-1e-6,1e-6);Assert.True(source.Denominator<=1000000000);
        }
        Assert.DoesNotContain(CompositionValidator.Validate(document,MediaCapabilities.Basic()),error=>error.Code is "source_interval" or "invalid_rate");
    }
    /// <summary>A faster layer needs proportionally more source: three timeline seconds at 2x need six source seconds.</summary>
    [Fact] public void FasterLayerNeedsMoreSource() {
        Assert.Contains(CompositionValidator.Validate(Video(2,MediaTime.Zero,new(5,1)),MediaCapabilities.Basic()),error=>error.Code=="source_interval");
        Assert.DoesNotContain(CompositionValidator.Validate(Video(2,MediaTime.Zero,new(6,1)),MediaCapabilities.Basic()),error=>error.Code=="source_interval");
    }
    /// <summary>Rates outside the supported range are rejected on layers and clips.</summary>
    [Fact] public void InvalidRatesAreReported() {
        var document=Video(0,MediaTime.Zero,new(6,1));document.Media.Add(new(){Id="tone",Kind="audio",Path="tone.wav",Sha256=new string('b',64),Duration=new(10,1)});
        document.AudioClips.Add(new(){Id="voice",MediaId="tone",Start=MediaTime.Zero,End=new(3,1),SourceIn=MediaTime.Zero,SourceOut=new(9,1),Rate=double.NaN});
        var errors=CompositionValidator.Validate(document,MediaCapabilities.Basic());
        Assert.Contains(errors,error=>error.Code=="invalid_rate" && error.Path=="layers[0].rate");Assert.Contains(errors,error=>error.Code=="invalid_rate" && error.Path=="audio_clips[0].rate");
    }
    /// <summary>The rate is written only when it differs from real time and parses back.</summary>
    [Fact] public void RateRoundTripsAndIsOmittedAtOne() {
        var plain=Video(1,MediaTime.Zero,new(3,1));Assert.DoesNotContain("\"rate\":",CompositionJson.Serialize(plain));
        var fast=Video(1.08,MediaTime.Zero,new(4,1));string json=CompositionJson.Serialize(fast);Assert.Contains("\"rate\": 1.08",json);
        Assert.Equal(1.08,CompositionJson.Parse(json).Layers[0].Rate);Assert.Equal(1,CompositionJson.Parse(CompositionJson.Serialize(plain)).Layers[0].Rate);
    }
    /// <summary>At 1.08x a 440 Hz tone stays at 440 Hz while the audio gets 1.08 times shorter.</summary>
    [Fact] public void StretchKeepsPitchAndShortensLength() {
        const int rate=48000;int frames=rate*2;var input=new float[frames*2];
        for(int index=0;index<frames;index++) {input[index*2]=input[index*2+1]=(float)(0.5*Math.Sin(2*Math.PI*440*index/rate));}
        var output=AudioTimeStretch.Stretch(input,2,rate,1.08);
        Assert.Equal((int)Math.Round(frames/1.08),output.Length/2);
        Assert.InRange(Frequency(output,2,rate,4800,output.Length/2-4800),436,444);
        Assert.InRange(MaxStep(output,2,0,output.Length/2-2400),0,0.06);
    }
    /// <summary>A 1.08x clip mixes the same samples whatever the block size, keeps its pitch and has no clicks.</summary>
    [Fact] public void RateChangedClipIsBlockIndependentAndKeepsPitch() {
        var document=Tone();document.AudioClips.Add(new(){Id="voice",MediaId="tone",Start=MediaTime.Zero,End=new(2,1),SourceIn=new(1,10),SourceOut=new MediaTime(1,10)+new MediaTime(216,100),Rate=1.08});
        Assert.Empty(CompositionValidator.Validate(document,MediaCapabilities.Basic()));
        using var resolver=new SineAudioResolver();using var mixer=new CompositionAudioMixer(resolver);
        var whole=Render(mixer,document,96000,32768);var small=Render(mixer,document,96000,1000);
        Assert.Equal(whole,small);
        Assert.InRange(Frequency(whole,2,48000,4800,86400),436,444);
        Assert.InRange(MaxStep(whole,2,0,96000-2400),0,0.06);
    }
    /// <summary>Two jump-cut segments joined by a 10 ms equal-power crossfade leave no discontinuity at the join.</summary>
    [Fact] public void CrossfadedSegmentJoinHasNoSpike() {
        var document=Tone();var join=new MediaTime(1,1);var half=new MediaTime(5,1000);
        // The second segment starts at an unrelated source phase, which would click as a hard cut.
        document.AudioClips.Add(new(){Id="first",MediaId="tone",Start=MediaTime.Zero,End=join+half,SourceIn=MediaTime.Zero,SourceOut=(join+half).AtRate(1.08),Rate=1.08,Envelopes=[new(){Type="equal_power_out",Start=join-half,Duration=half+half}]});
        document.AudioClips.Add(new(){Id="second",MediaId="tone",Start=join-half,End=new(2,1),SourceIn=new(30013,10000),SourceOut=new MediaTime(30013,10000)+(new MediaTime(2,1)-join+half).AtRate(1.08),Rate=1.08,Envelopes=[new(){Type="equal_power_in",Start=MediaTime.Zero,Duration=half+half}]});
        Assert.Empty(CompositionValidator.Validate(document,MediaCapabilities.Basic()));
        using var resolver=new SineAudioResolver();using var mixer=new CompositionAudioMixer(resolver);
        var samples=Render(mixer,document,96000,4096);
        Assert.InRange(MaxStep(samples,2,46800,49200),0,0.06);
    }
    /// <summary>Renders a whole timeline in blocks of the given size.</summary>
    static float[] Render(CompositionAudioMixer mixer,CompositionDocument document,int frames,int block) {
        var result=new List<float>();for(int first=0;first<frames;first+=block) {result.AddRange(mixer.Render(document,first,Math.Min(block,frames-first)).Block.Samples.ToArray());}return result.ToArray();
    }
    /// <summary>Estimates the frequency of the left channel from rising zero crossings between two frames.</summary>
    static double Frequency(float[] samples,int channels,int rate,int from,int to) {
        double first=-1,last=-1;int crossings=0;
        for(int index=from+1;index<to;index++) {float previous=samples[(index-1)*channels],current=samples[index*channels];if(previous<0 && current>=0) {double exact=index-1+previous/(previous-current);if(first<0) {first=exact;}else {crossings++;}last=exact;}}
        return crossings/((last-first)/rate);
    }
    /// <summary>Largest change between consecutive left-channel samples in a frame range.</summary>
    static double MaxStep(float[] samples,int channels,int from,int to) {double step=0;for(int index=from+1;index<to;index++) {step=Math.Max(step,Math.Abs(samples[index*channels]-samples[(index-1)*channels]));}return step;}
    /// <summary>A two-second audio timeline over a ten-second tone.</summary>
    static CompositionDocument Tone() => new(){Id="speed",Revision=1,Width=32,Height=32,FrameRate=new(24,1),Duration=new(2,1),BackgroundColor="#000000FF",Media=[new(){Id="tone",Kind="audio",Path="tone.wav",Sha256=new string('a',64),Duration=new(10,1)}]};
    /// <summary>A three-second video layer at a rate over a ten-second clip.</summary>
    static CompositionDocument Video(double rate,MediaTime sourceIn,MediaTime sourceOut) => new(){Id="speed",Revision=1,Width=32,Height=32,FrameRate=new(24,1),Duration=new(3,1),BackgroundColor="#000000FF",Audio=new(){Enabled=false},
        Media=[new(){Id="clip",Kind="video",Path="clip.mp4",Sha256=new string('a',64),Width=32,Height=32,Duration=new(10,1)}],
        Layers=[new(){Id="take",MediaId="clip",Start=MediaTime.Zero,End=new(3,1),SourceIn=sourceIn,SourceOut=sourceOut,Rate=rate}]};
}
