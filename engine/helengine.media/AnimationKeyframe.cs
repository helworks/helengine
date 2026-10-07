namespace helengine.media;
/// <summary>One property value at a local layer instant, with an outgoing segment curve.</summary>
public sealed class AnimationKeyframe {
    /// <summary>Local layer time, sorted strictly increasingly.</summary>
    [JsonRequired] public MediaTime Time {get;set;} = MediaTime.Zero;
    /// <summary>Finite catalog value at this instant.</summary>
    [JsonRequired] public double Value {get;set;}
    /// <summary>Curve applied from this keyframe to the next; the last curve is ignored.</summary>
    public string Curve {get;set;} = "linear.v1";
}
