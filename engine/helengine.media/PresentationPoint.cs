namespace helengine.media;
/// <summary>Double-precision pixel location in a presentation viewport.</summary>
public sealed class PresentationPoint {
    /// <summary>Stores one mapped presentation location.</summary>
    public PresentationPoint(double x,double y) {X=x;Y=y;}
    /// <summary>Horizontal pixel coordinate.</summary>
    public double X {get;}
    /// <summary>Vertical pixel coordinate.</summary>
    public double Y {get;}
}
