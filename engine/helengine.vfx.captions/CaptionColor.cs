using System.Globalization;

namespace helengine.vfx.captions;

/// <summary>Converts portable preset colors to drawing colors with explicit alpha support.</summary>
public static class CaptionColor {
    /// <summary>Parses #RRGGBB as opaque or #AARRGGBB with its supplied transparency.</summary>
    public static Color Parse(string value) {
        if (value == null || !value.StartsWith('#') || (value.Length != 7 && value.Length != 9)
            || !uint.TryParse(value.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint bits)) {
            throw new FormatException($"Invalid caption color '{value}'; use #RRGGBB or #AARRGGBB.");
        }
        return Color.FromArgb(unchecked((int)(value.Length == 7 ? bits | 0xFF000000u : bits)));
    }
}
