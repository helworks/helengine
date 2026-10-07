namespace helengine.media;
/// <summary>Independent properties of one visual layer, evaluated without changing its siblings.</summary>
public sealed class LayerTransform {
    /// <summary>Viewport-relative horizontal offset.</summary>
    public double PositionX {get;set;} = 0;
    /// <summary>Viewport-relative vertical offset.</summary>
    public double PositionY {get;set;} = 0;
    /// <summary>Independent horizontal layer scale.</summary>
    public double ScaleX {get;set;} = 1;
    /// <summary>Independent vertical layer scale.</summary>
    public double ScaleY {get;set;} = 1;
    /// <summary>Clockwise rotation of this layer in screen coordinates.</summary>
    public double RotationDegrees {get;set;} = 0;
    /// <summary>Layer opacity independent of image zoom.</summary>
    public double Opacity {get;set;} = 1;
    /// <summary>Magnification of the already-fitted presentation viewport.</summary>
    public double Zoom {get;set;} = 1;
    /// <summary>Normalized focus in the oriented source image.</summary>
    public double FocusX {get;set;} = .5;
    /// <summary>Normalized focus in the oriented source image.</summary>
    public double FocusY {get;set;} = .5;
    /// <summary>Creates a separate property snapshot so evaluation never modifies the authored transform.</summary>
    public LayerTransform Copy() => new() {PositionX=PositionX, PositionY=PositionY, ScaleX=ScaleX, ScaleY=ScaleY, RotationDegrees=RotationDegrees, Opacity=Opacity, Zoom=Zoom, FocusX=FocusX, FocusY=FocusY};
    /// <summary>Assigns a catalog property after interpolation.</summary>
    public void Set(string property,double value) {
        switch(property) {
            case "position_x": PositionX=value;break;
            case "position_y": PositionY=value;break;
            case "scale_x": ScaleX=value;break;
            case "scale_y": ScaleY=value;break;
            case "rotation_deg": RotationDegrees=value;break;
            case "opacity": Opacity=value;break;
            case "zoom": Zoom=value;break;
            case "focus_x": FocusX=value;break;
            case "focus_y": FocusY=value;break;
            default: throw new InvalidDataException("Unknown animated layer property: "+property);
        }
    }
    /// <summary>Checks a property value against explicit executable catalog limits.</summary>
    public static bool IsValid(string property,double value) {
        if(!double.IsFinite(value)) {return false;}
        return property switch {
            "position_x" or "position_y" => value>=-4 && value<=4,
            "scale_x" or "scale_y" => value>=.01 && value<=16,
            "rotation_deg" => value>=-36000 && value<=36000,
            "opacity" or "focus_x" or "focus_y" => value>=0 && value<=1,
            "zoom" => value>=1 && value<=2.5,
            _ => false
        };
    }
    /// <summary>Checks all static values using the same limits as animated values.</summary>
    public bool IsValid() => IsValid("position_x",PositionX) && IsValid("position_y",PositionY) && IsValid("scale_x",ScaleX) && IsValid("scale_y",ScaleY) && IsValid("rotation_deg",RotationDegrees) && IsValid("opacity",Opacity) && IsValid("zoom",Zoom) && IsValid("focus_x",FocusX) && IsValid("focus_y",FocusY);
}
