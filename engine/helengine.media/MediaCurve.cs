namespace helengine.media;
/// <summary>Versioned curve formulas preserve an authored motion across engine upgrades.</summary>
public static class MediaCurve {
    /// <summary>Checks whether a named exact curve implementation exists.</summary>
    public static bool Supports(string id) => id is "linear.v1" or "smoothstep.v1" or "ease_out_cubic.v1" or "ease_in_quad.v1" or "ease_out_back.v1";
    /// <summary>Evaluates bounded progress with the selected curve, never substituting a different easing.</summary>
    public static double Evaluate(string id,double progress) {
        if(!double.IsFinite(progress)) {throw new ArgumentOutOfRangeException(nameof(progress));}
        double value=Math.Clamp(progress,0,1);
        return id switch {"linear.v1"=>value,"smoothstep.v1"=>value*value*(3-2*value),"ease_out_cubic.v1"=>1-Math.Pow(1-value,3),"ease_in_quad.v1"=>value*value,"ease_out_back.v1"=>EaseOutBack(value),_=>throw new InvalidDataException("Unsupported curve: "+id)};
    }
    /// <summary>Back-out easing: travels about ten percent past the target near 60% progress and settles on it, giving pops a soft overshoot.</summary>
    /// <param name="value">Progress already clamped to zero..one.</param>
    /// <returns>Eased progress; exceeds one between roughly 0.4 and 1.</returns>
    static double EaseOutBack(double value) {
        const double overshoot=1.70158;
        double shifted=value-1;
        return 1+(overshoot+1)*shifted*shifted*shifted+overshoot*shifted*shifted;
    }
}
