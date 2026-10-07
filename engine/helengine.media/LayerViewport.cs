namespace helengine.media;
/// <summary>Normalized presentation rectangle in the composition canvas.</summary>
public sealed class LayerViewport {
    /// <summary>Canvas-relative left edge.</summary>
    public double X {get;set;}
    /// <summary>Canvas-relative top edge.</summary>
    public double Y {get;set;}
    /// <summary>Canvas-relative width.</summary>
    public double Width {get;set;} = 1;
    /// <summary>Canvas-relative height.</summary>
    public double Height {get;set;} = 1;
    /// <summary>Checks a finite nonempty rectangle lying in the canvas.</summary>
    public bool IsValid() => double.IsFinite(X+Y+Width+Height) && X>=0 && Y>=0 && Width>0 && Height>0 && X+Width<=1.000000001 && Y+Height<=1.000000001;
}
