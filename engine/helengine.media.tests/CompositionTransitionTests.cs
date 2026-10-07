namespace helengine.media.tests;
/// <summary>Checks explicit transition intervals without modifying audio or timeline duration.</summary>
public sealed class CompositionTransitionTests {
    /// <summary>Crossfade endpoints and midpoint follow the declared interval.</summary>
    [Fact] public void CrossfadeEndpointsAndMidpoint() {
        var transition=new CompositionTransition{Id="cross",FromLayer="a",ToLayer="b",Start=new(1,1),Duration=new(1,1)};
        Assert.Equal(0,TransitionEvaluation.At(transition,new(1,1)).Progress);
        Assert.Equal(.5,TransitionEvaluation.At(transition,new(3,2)).Progress);
        Assert.Equal(1,TransitionEvaluation.At(transition,new(2,1)).Progress);
    }
    /// <summary>A source layer must cover the entire requested overlap.</summary>
    [Fact] public void InsufficientSourceHandlesRejected() {
        var document=Document();document.Layers[0].End=new(3,2);
        Assert.Contains(CompositionValidator.Validate(document,MediaCapabilities.Basic()),error=>error.Code=="transition_source_interval");
    }
    /// <summary>Transition overlap is explicit and never shortens the composition.</summary>
    [Fact] public void TransitionDoesNotShortenTimeline() {
        var document=Document();var clock=new CompositionClock(document.FrameRate,48000,document.Duration);
        Assert.Equal(72,clock.FrameCount);Assert.Equal(new MediaTime(3,1),document.Duration);
    }
    /// <summary>Visual transitions do not invent an audio envelope.</summary>
    [Fact] public void PictureCrossfadeDoesNotForceAudioFade() {
        var document=Document();document.AudioClips.Add(new(){Id="voice",MediaId="source",Start=MediaTime.Zero,End=new(3,1)});
        TransitionEvaluation.At(document.Transitions[0],new(3,2));Assert.Empty(document.AudioClips[0].Envelopes);
    }
    /// <summary>Creates two sources with complete three-second visual coverage.</summary>
    static CompositionDocument Document() => new(){Id="cross",Revision=1,Width=32,Height=32,FrameRate=new(24,1),Duration=new(3,1),Audio=new(){Enabled=false},Media=[new(){Id="source",Kind="image",Path="post.png",Sha256=new string('a',64),Width=32,Height=32}],Layers=[new(){Id="a",MediaId="source",Start=MediaTime.Zero,End=new(3,1)},new(){Id="b",MediaId="source",Start=MediaTime.Zero,End=new(3,1)}],Transitions=[new(){Id="transition",FromLayer="a",ToLayer="b",Start=new(1,1),Duration=new(1,1)}]};
}
