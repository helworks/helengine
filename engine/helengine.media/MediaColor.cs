using System.Globalization;
namespace helengine.media;
/// <summary>Explicit sRGB straight-alpha color for canvas and presentation padding.</summary>
public sealed class MediaColor {
    /// <summary>Stores normalized sRGB color values and linear coverage.</summary>
    public MediaColor(double red,double green,double blue,double alpha) {Red=red;Green=green;Blue=blue;Alpha=alpha;}
    /// <summary>Encoded red component.</summary>
    public double Red {get;}
    /// <summary>Encoded green component.</summary>
    public double Green {get;}
    /// <summary>Encoded blue component.</summary>
    public double Blue {get;}
    /// <summary>Linear opacity component.</summary>
    public double Alpha {get;}
    /// <summary>Parses #RRGGBB or #RRGGBBAA; caption styles use their existing color convention separately.</summary>
    public static MediaColor Parse(string text) {
        if(text==null || text.Length is not (7 or 9) || text[0]!='#' || text.Skip(1).Any(value=>!Uri.IsHexDigit(value))) {throw new InvalidDataException("Color requires #RRGGBB or #RRGGBBAA.");}
        return new(byte.Parse(text.Substring(1,2),NumberStyles.HexNumber)/255d,byte.Parse(text.Substring(3,2),NumberStyles.HexNumber)/255d,byte.Parse(text.Substring(5,2),NumberStyles.HexNumber)/255d,text.Length==9?byte.Parse(text.Substring(7,2),NumberStyles.HexNumber)/255d:1);
    }
    /// <summary>Converts an encoded channel to linear light for correct alpha composition.</summary>
    public static double ToLinear(double value) => value<=.04045 ? value/12.92 : Math.Pow((value+.055)/1.055,2.4);
}
