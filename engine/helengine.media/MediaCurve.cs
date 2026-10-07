namespace helengine.media;
/// <summary>Versioned curve formulas preserve an authored motion across engine upgrades.</summary>
public static class MediaCurve {
    /// <summary>Checks whether a named exact curve implementation exists.</summary>
    public static bool Supports(string id) => id is "linear.v1" or "smoothstep.v1";
    /// <summary>Evaluates bounded progress with the selected curve, never substituting a different easing.</summary>
    public static double Evaluate(string id,double progress) {
        if(!double.IsFinite(progress)) {throw new ArgumentOutOfRangeException(nameof(progress));}
        double value=Math.Clamp(progress,0,1);
        return id switch {"linear.v1"=>value,"smoothstep.v1"=>value*value*(3-2*value),_=>throw new InvalidDataException("Unsupported curve: "+id)};
    }
}
