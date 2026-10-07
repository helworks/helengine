namespace helengine.media.tests;
/// <summary>Preserves the product caption travel formulas inside versioned engine animations.</summary>
public sealed class CaptionMotionCurveTests {
    /// <summary>Entrance follows the original cubic ease-out formula.</summary>
    [Fact] public void EntranceKeepsCubicTravel() {Assert.True(MediaCurve.Supports("ease_out_cubic.v1"));Assert.Equal(.875,MediaCurve.Evaluate("ease_out_cubic.v1",.5));}
    /// <summary>Exit follows the original quadratic ease-in formula.</summary>
    [Fact] public void ExitKeepsQuadraticTravel() {Assert.True(MediaCurve.Supports("ease_in_quad.v1"));Assert.Equal(.25,MediaCurve.Evaluate("ease_in_quad.v1",.5));}
}
