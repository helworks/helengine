namespace helengine.media.tests;
/// <summary>Checks that a video layer can outlast its source by holding the last frame, and only when asked.</summary>
public sealed class HoldLastFrameTests {
    /// <summary>With the flag the layer validates and samples the last source frame after its interval runs out.</summary>
    [Fact] public void HeldLayerSamplesTheLastFrame() {
        var document=Document(true);
        Assert.DoesNotContain(CompositionValidator.Validate(document,MediaCapabilities.Basic()),error=>error.Code=="source_interval");
        var evaluation=Assert.Single(CompositionEvaluator.Evaluate(document,new(5,2)));
        Assert.Equal(new MediaTime(2,1)-new MediaTime(1,24),evaluation.SourceTime);
        Assert.Equal(new MediaTime(1,1),Assert.Single(CompositionEvaluator.Evaluate(document,new(1,1))).SourceTime);
    }
    /// <summary>Without the flag a layer longer than its source is still rejected.</summary>
    [Fact] public void UnheldLayerStillNeedsItsSource() {
        Assert.Contains(CompositionValidator.Validate(Document(false),MediaCapabilities.Basic()),error=>error.Code=="source_interval");
    }
    /// <summary>A three-second layer over a two-second source interval of a ten-second video.</summary>
    static CompositionDocument Document(bool hold) => new(){Id="hold",Revision=1,Width=32,Height=32,FrameRate=new(24,1),Duration=new(3,1),BackgroundColor="#000000FF",Audio=new(){Enabled=false},
        Media=[new(){Id="clip",Kind="video",Path="clip.mp4",Sha256=new string('a',64),Width=32,Height=32,Duration=new(10,1)}],
        Layers=[new(){Id="take",MediaId="clip",Start=MediaTime.Zero,End=new(3,1),SourceIn=MediaTime.Zero,SourceOut=new(2,1),HoldLastFrame=hold}]};
}
