namespace helengine.media.tests;
/// <summary>Tests pure timeline animation evaluation and presentation fitting.</summary>
public sealed class CompositionEvaluationTests {
    /// <summary>A frame reached through a later seek has the same transform as a direct query.</summary>
    [Fact] public void RepeatedSeekIsDeterministic() {
        var document=Document();var layer=document.Layers[0];
        layer.Animations.Add(new(){Property="zoom",Keyframes=[new(){Time=new(1,1),Value=1,Curve="smoothstep.v1"},new(){Time=new(2,1),Value=1.35}]});
        var before=CompositionEvaluator.Evaluate(document,new(3,2))[0];
        CompositionEvaluator.Evaluate(document,new(5,2));
        var repeated=CompositionEvaluator.Evaluate(document,new(3,2))[0];
        Assert.Equal(1.175,before.Transform.Zoom,10);Assert.Equal(before.Transform.Zoom,repeated.Transform.Zoom);
        Assert.Equal(new MediaTime(3,2),before.LocalTime);
    }
    /// <summary>The existing editorial smoothstep curve is preserved rather than replaced by quadratic easing.</summary>
    [Fact] public void SmoothstepQuarterEquals015625() => Assert.Equal(.15625,MediaCurve.Evaluate("smoothstep.v1",.25),12);
    /// <summary>Contain padding remains part of the animated viewport so a near-full-width phrase stays visible.</summary>
    [Fact] public void FitBeforeZoomPreservesPostEdges() {
        var mapping=PresentationTransform.Resolve(685,898,540,540,"contain",1.35,.5,.5);
        Assert.InRange(mapping.MapSourcePoint(.03,.3).X,0,540);
        Assert.InRange(mapping.MapSourcePoint(.97,.3).X,0,540);
        Assert.True(mapping.FittedWidth<540);Assert.Equal(400,mapping.CropWidth,8);
    }
    /// <summary>Image zoom never changes a sibling text layer's own scale.</summary>
    [Fact] public void TextLayerDoesNotInheritImageScale() {
        var document=Document();document.Layers[0].Transform.Zoom=1.35;
        document.Layers.Add(new(){Id="text",MediaId="post",Start=MediaTime.Zero,End=new(3,1),Order=1});
        var layers=CompositionEvaluator.Evaluate(document,new(1,1));
        Assert.Equal(1.35,layers[0].Transform.Zoom);Assert.Equal(1,layers[1].Transform.Zoom);
    }
    /// <summary>Pure evaluation only exposes active layers and excludes the exact end instant.</summary>
    [Fact] public void InactiveLayerNotSampled() {
        var document=Document();document.Layers[0].Start=new(1,1);document.Layers[0].End=new(2,1);
        Assert.Empty(CompositionEvaluator.Evaluate(document,MediaTime.Zero));
        Assert.Single(CompositionEvaluator.Evaluate(document,new(1,1)));
        Assert.Empty(CompositionEvaluator.Evaluate(document,new(2,1)));
    }
    /// <summary>Ambiguous duplicate keyframe times are rejected before interpolation.</summary>
    [Fact] public void KeyframesRequireStrictOrder() {
        var document=Document();document.Layers[0].Animations.Add(new(){Property="zoom",Keyframes=[new(){Time=MediaTime.Zero,Value=1},new(){Time=MediaTime.Zero,Value=2}]});
        var errors=CompositionValidator.Validate(document,MediaCapabilities.Basic());
        Assert.Contains(errors,error=>error.Code=="invalid_animation");
    }
    /// <summary>Creates a pinned still composition with a three-second local timeline.</summary>
    static CompositionDocument Document() => new(){Id="animation",Revision=1,Width=540,Height=960,FrameRate=new(24,1),Duration=new(3,1),Audio=new(){Enabled=false},Media=[new(){Id="post",Kind="image",Path="post.png",Sha256=new string('a',64),Width=685,Height=898}],Layers=[new(){Id="image",MediaId="post",Start=MediaTime.Zero,End=new(3,1)}]};
}
