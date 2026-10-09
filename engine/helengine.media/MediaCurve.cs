namespace helengine.media;
/// <summary>Versioned curve formulas preserve an authored motion across engine upgrades; the formulas live in the shared <see cref="CurveCatalog"/> so timelines evaluate exactly the same curves.</summary>
public static class MediaCurve {
    /// <summary>Checks whether a named exact curve implementation exists.</summary>
    public static bool Supports(string id) => CurveCatalog.Supports(id);
    /// <summary>Evaluates bounded progress with the selected curve, never substituting a different easing.</summary>
    public static double Evaluate(string id,double progress) => CurveCatalog.Evaluate(id,progress);
}
