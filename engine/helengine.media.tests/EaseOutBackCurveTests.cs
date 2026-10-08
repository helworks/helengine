namespace helengine.media.tests;
/// <summary>Pins the back-out pop curve used by kinetic typography so authored overshoot never changes silently.</summary>
public sealed class EaseOutBackCurveTests {
    /// <summary>The curve is published, starts at zero, ends exactly on the target and overshoots in between.</summary>
    [Fact] public void OvershootsAndSettlesOnTarget() {
        Assert.True(MediaCurve.Supports("ease_out_back.v1"));
        Assert.Contains("ease_out_back.v1",MediaCapabilities.Basic().Curves);
        Assert.Equal(0,MediaCurve.Evaluate("ease_out_back.v1",0),9);
        Assert.Equal(1,MediaCurve.Evaluate("ease_out_back.v1",1),9);
        Assert.InRange(MediaCurve.Evaluate("ease_out_back.v1",.6),1.09,1.11);
    }
    /// <summary>The executable composition schema accepts the curve name for keyframes.</summary>
    [Fact] public void CompositionSchemaListsTheCurve() {
        string schema=File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"..","..","..","..","helengine.media","Schemas","helengine.media.composition.v1.schema.json"));
        Assert.Contains("\"ease_out_back.v1\"",schema);
    }
}
