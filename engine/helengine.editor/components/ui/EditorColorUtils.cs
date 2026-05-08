using System.Globalization;

namespace helengine.editor;

/// <summary>
/// Provides shared parsing, formatting, and blending helpers for editor color controls.
/// </summary>
public static class EditorColorUtils {
    /// <summary>
    /// Parses one HTML-style color string into a byte-backed color value.
    /// </summary>
    /// <param name="text">Color text in <c>#RRGGBB</c> or <c>#RRGGBBAA</c> form.</param>
    /// <param name="color">Parsed color value when parsing succeeds.</param>
    /// <returns>True when the text was parsed successfully.</returns>
    public static bool TryParseHtmlColor(string text, out byte4 color) {
        color = new byte4(0, 0, 0, 255);
        if (string.IsNullOrWhiteSpace(text)) {
            return false;
        }

        string trimmed = text.Trim();
        if (trimmed.StartsWith("#", StringComparison.Ordinal)) {
            trimmed = trimmed.Substring(1);
        }

        if (trimmed.Length != 6 && trimmed.Length != 8) {
            return false;
        }

        byte red;
        byte green;
        byte blue;
        byte alpha = 255;
        if (!TryParseHexByte(trimmed, 0, out red) ||
            !TryParseHexByte(trimmed, 2, out green) ||
            !TryParseHexByte(trimmed, 4, out blue)) {
            return false;
        }

        if (trimmed.Length == 8 && !TryParseHexByte(trimmed, 6, out alpha)) {
            return false;
        }

        color = new byte4(red, green, blue, alpha);
        return true;
    }

    /// <summary>
    /// Formats one color as an HTML hex string.
    /// </summary>
    /// <param name="color">Color to format.</param>
    /// <returns>Formatted color string.</returns>
    public static string FormatHtmlColor(byte4 color) {
        string red = color.X.ToString("x2", CultureInfo.InvariantCulture);
        string green = color.Y.ToString("x2", CultureInfo.InvariantCulture);
        string blue = color.Z.ToString("x2", CultureInfo.InvariantCulture);
        if (color.W == 255) {
            return $"#{red}{green}{blue}";
        }

        string alpha = color.W.ToString("x2", CultureInfo.InvariantCulture);
        return $"#{red}{green}{blue}{alpha}";
    }

    /// <summary>
    /// Linearly blends two colors using the supplied interpolation factor.
    /// </summary>
    /// <param name="fromColor">Starting color.</param>
    /// <param name="toColor">Destination color.</param>
    /// <param name="amount">Blend factor from 0 to 1.</param>
    /// <returns>Blended color.</returns>
    public static byte4 Mix(byte4 fromColor, byte4 toColor, double amount) {
        double clampedAmount = Math.Clamp(amount, 0.0, 1.0);
        return new byte4(
            MixChannel(fromColor.X, toColor.X, clampedAmount),
            MixChannel(fromColor.Y, toColor.Y, clampedAmount),
            MixChannel(fromColor.Z, toColor.Z, clampedAmount),
            MixChannel(fromColor.W, toColor.W, clampedAmount));
    }

    /// <summary>
    /// Parses one two-character hex fragment into a byte value.
    /// </summary>
    /// <param name="text">Hex string to inspect.</param>
    /// <param name="startIndex">Starting offset of the fragment.</param>
    /// <param name="value">Parsed byte value when parsing succeeds.</param>
    /// <returns>True when the fragment was parsed successfully.</returns>
    static bool TryParseHexByte(string text, int startIndex, out byte value) {
        value = 0;
        if (startIndex < 0 || startIndex + 1 >= text.Length) {
            return false;
        }

        string fragment = text.Substring(startIndex, 2);
        return byte.TryParse(fragment, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
    }

    /// <summary>
    /// Interpolates one byte channel between two colors.
    /// </summary>
    /// <param name="fromValue">Starting channel value.</param>
    /// <param name="toValue">Destination channel value.</param>
    /// <param name="amount">Blend factor from 0 to 1.</param>
    /// <returns>Interpolated channel value.</returns>
    static byte MixChannel(byte fromValue, byte toValue, double amount) {
        double blended = (fromValue * (1.0 - amount)) + (toValue * amount);
        return (byte)Math.Clamp((int)Math.Round(blended, MidpointRounding.AwayFromZero), 0, 255);
    }
}
