using System.Globalization;

namespace helengine.editor;

/// <summary>
/// Provides shared parsing, formatting, and blending helpers for editor color controls.
/// </summary>
public static class EditorColorUtils {
    /// <summary>
    /// Hue wheel inner radius ratio used by the reusable color picker controls.
    /// </summary>
    const double DefaultHueWheelInnerRadiusRatio = 0.62;

    /// <summary>
    /// Converts one byte-backed RGB color into HSV components.
    /// </summary>
    /// <param name="color">Source RGB color.</param>
    /// <param name="hue">Resolved hue angle in degrees.</param>
    /// <param name="saturation">Resolved saturation from 0 to 1.</param>
    /// <param name="value">Resolved value from 0 to 1.</param>
    public static void RgbToHsv(byte4 color, out double hue, out double saturation, out double value) {
        double red = color.X / 255.0;
        double green = color.Y / 255.0;
        double blue = color.Z / 255.0;
        double max = Math.Max(red, Math.Max(green, blue));
        double min = Math.Min(red, Math.Min(green, blue));
        double delta = max - min;

        value = max;
        if (max <= 0.0 || delta <= 0.0) {
            saturation = 0.0;
            hue = 0.0;
            return;
        }

        saturation = delta / max;

        if (max == red) {
            hue = 60.0 * (((green - blue) / delta) % 6.0);
        } else if (max == green) {
            hue = 60.0 * (((blue - red) / delta) + 2.0);
        } else {
            hue = 60.0 * (((red - green) / delta) + 4.0);
        }

        hue = NormalizeHue(hue);
    }

    /// <summary>
    /// Converts one HSV color plus alpha into a byte-backed RGB color.
    /// </summary>
    /// <param name="hue">Hue angle in degrees.</param>
    /// <param name="saturation">Saturation from 0 to 1.</param>
    /// <param name="value">Value from 0 to 1.</param>
    /// <param name="alpha">Alpha channel to preserve.</param>
    /// <returns>Converted byte-backed RGBA color.</returns>
    public static byte4 HsvToRgb(double hue, double saturation, double value, byte alpha) {
        double normalizedHue = NormalizeHue(hue);
        double clampedSaturation = Clamp01(saturation);
        double clampedValue = Clamp01(value);

        if (clampedValue <= 0.0) {
            return new byte4(0, 0, 0, alpha);
        }

        if (clampedSaturation <= 0.0) {
            byte grayscale = ToByteChannel(clampedValue);
            return new byte4(grayscale, grayscale, grayscale, alpha);
        }

        double chroma = clampedValue * clampedSaturation;
        double hueSector = normalizedHue / 60.0;
        double secondary = chroma * (1.0 - Math.Abs((hueSector % 2.0) - 1.0));
        double match = clampedValue - chroma;

        double red;
        double green;
        double blue;

        if (hueSector < 1.0) {
            red = chroma;
            green = secondary;
            blue = 0.0;
        } else if (hueSector < 2.0) {
            red = secondary;
            green = chroma;
            blue = 0.0;
        } else if (hueSector < 3.0) {
            red = 0.0;
            green = chroma;
            blue = secondary;
        } else if (hueSector < 4.0) {
            red = 0.0;
            green = secondary;
            blue = chroma;
        } else if (hueSector < 5.0) {
            red = secondary;
            green = 0.0;
            blue = chroma;
        } else {
            red = chroma;
            green = 0.0;
            blue = secondary;
        }

        return new byte4(
            ToByteChannel(red + match),
            ToByteChannel(green + match),
            ToByteChannel(blue + match),
            alpha);
    }

    /// <summary>
    /// Builds a reusable hue-wheel texture with a transparent center for the inner triangle.
    /// </summary>
    /// <param name="size">Square texture size in pixels.</param>
    /// <returns>Runtime texture containing the hue wheel.</returns>
    public static RuntimeTexture BuildHueWheelTexture(int size) {
        if (size <= 0) {
            throw new ArgumentOutOfRangeException(nameof(size), "Wheel texture size must be greater than zero.");
        }

        byte[] colors = new byte[size * size * 4];
        double radius = (size - 1) / 2.0;
        double innerRadius = radius * DefaultHueWheelInnerRadiusRatio;

        for (int y = 0; y < size; y++) {
            double dy = y - radius;
            for (int x = 0; x < size; x++) {
                double dx = x - radius;
                double distance = Math.Sqrt((dx * dx) + (dy * dy));
                int pixelIndex = ((y * size) + x) * 4;

                if (distance < innerRadius || distance > radius) {
                    colors[pixelIndex + 3] = 0;
                    continue;
                }

                double hue = NormalizeHue(Math.Atan2(dy, dx) * (180.0 / Math.PI));
                byte4 color = HsvToRgb(hue, 1.0, 1.0, 255);
                colors[pixelIndex] = color.X;
                colors[pixelIndex + 1] = color.Y;
                colors[pixelIndex + 2] = color.Z;
                colors[pixelIndex + 3] = 255;
            }
        }

        return BuildRuntimeTexture(size, size, colors);
    }

    /// <summary>
    /// Builds a reusable saturation/value triangle texture for one hue angle.
    /// </summary>
    /// <param name="size">Square texture size in pixels.</param>
    /// <param name="hue">Hue angle used by the triangle fill.</param>
    /// <returns>Runtime texture containing the triangle.</returns>
    public static RuntimeTexture BuildTriangleTexture(int size, double hue) {
        if (size <= 0) {
            throw new ArgumentOutOfRangeException(nameof(size), "Triangle texture size must be greater than zero.");
        }

        byte[] colors = new byte[size * size * 4];
        float2 topVertex = new float2(size * 0.5f, size * 0.10f);
        float2 leftVertex = new float2(size * 0.12f, size * 0.88f);
        float2 rightVertex = new float2(size * 0.88f, size * 0.88f);
        byte4 hueColor = HsvToRgb(hue, 1.0, 1.0, 255);

        for (int y = 0; y < size; y++) {
            for (int x = 0; x < size; x++) {
                float2 point = new float2(x + 0.5f, y + 0.5f);
                if (!TryResolveTriangleWeights(point, topVertex, leftVertex, rightVertex, out double hueWeight, out double whiteWeight, out double blackWeight)) {
                    continue;
                }

                int pixelIndex = ((y * size) + x) * 4;
                colors[pixelIndex] = ToByteChannel((hueWeight * hueColor.X + (whiteWeight * 255.0)) / 255.0);
                colors[pixelIndex + 1] = ToByteChannel((hueWeight * hueColor.Y + (whiteWeight * 255.0)) / 255.0);
                colors[pixelIndex + 2] = ToByteChannel((hueWeight * hueColor.Z + (whiteWeight * 255.0)) / 255.0);
                colors[pixelIndex + 3] = 255;
            }
        }

        return BuildRuntimeTexture(size, size, colors);
    }

    /// <summary>
    /// Resolves a hue angle from one point inside the hue wheel.
    /// </summary>
    /// <param name="point">Pointer position relative to the wheel bounds.</param>
    /// <param name="size">Square wheel size in pixels.</param>
    /// <returns>Hue angle in degrees.</returns>
    public static double ResolveHueFromWheelPoint(int2 point, int size) {
        if (size <= 0) {
            throw new ArgumentOutOfRangeException(nameof(size), "Wheel size must be greater than zero.");
        }

        double center = (size - 1) / 2.0;
        double dx = point.X - center;
        double dy = point.Y - center;
        return NormalizeHue(Math.Atan2(dy, dx) * (180.0 / Math.PI));
    }

    /// <summary>
    /// Returns whether one point lies inside the hue wheel's visible ring.
    /// </summary>
    /// <param name="point">Pointer position relative to the wheel bounds.</param>
    /// <param name="size">Square wheel size in pixels.</param>
    /// <returns>True when the point is on the visible wheel ring.</returns>
    public static bool IsPointInsideHueWheelRing(int2 point, int size) {
        if (size <= 0) {
            return false;
        }

        double radius = (size - 1) / 2.0;
        double innerRadius = radius * DefaultHueWheelInnerRadiusRatio;
        double center = radius;
        double dx = point.X - center;
        double dy = point.Y - center;
        double distance = Math.Sqrt((dx * dx) + (dy * dy));
        return distance >= innerRadius && distance <= radius;
    }

    /// <summary>
    /// Resolves saturation and value weights from one point inside the triangle.
    /// </summary>
    /// <param name="point">Pointer position relative to the triangle bounds.</param>
    /// <param name="size">Square triangle size in pixels.</param>
    /// <param name="saturation">Resolved saturation from 0 to 1.</param>
    /// <param name="value">Resolved value from 0 to 1.</param>
    /// <returns>True when the point lies inside the triangle.</returns>
    public static bool TryResolveTriangleSelection(int2 point, int size, out double saturation, out double value) {
        saturation = 0.0;
        value = 0.0;
        if (size <= 0) {
            return false;
        }

        float2 topVertex = new float2(size * 0.5f, size * 0.10f);
        float2 leftVertex = new float2(size * 0.12f, size * 0.88f);
        float2 rightVertex = new float2(size * 0.88f, size * 0.88f);
        if (!TryResolveTriangleWeights(new float2(point.X + 0.5f, point.Y + 0.5f), topVertex, leftVertex, rightVertex, out double hueWeight, out double whiteWeight, out double blackWeight)) {
            return false;
        }

        value = Math.Clamp(hueWeight + whiteWeight, 0.0, 1.0);
        saturation = value <= 0.0 ? 0.0 : Math.Clamp(hueWeight / value, 0.0, 1.0);
        return true;
    }

    /// <summary>
    /// Resolves one triangle point from saturation and value values.
    /// </summary>
    /// <param name="saturation">Saturation from 0 to 1.</param>
    /// <param name="value">Value from 0 to 1.</param>
    /// <param name="size">Square triangle size in pixels.</param>
    /// <returns>Resolved triangle point.</returns>
    public static int2 ResolveTrianglePoint(double saturation, double value, int size) {
        if (size <= 0) {
            throw new ArgumentOutOfRangeException(nameof(size), "Triangle size must be greater than zero.");
        }

        double clampedSaturation = Clamp01(saturation);
        double clampedValue = Clamp01(value);
        double hueWeight = clampedValue * clampedSaturation;
        double whiteWeight = clampedValue * (1.0 - clampedSaturation);
        double blackWeight = 1.0 - clampedValue;

        float2 topVertex = new float2(size * 0.5f, size * 0.10f);
        float2 leftVertex = new float2(size * 0.12f, size * 0.88f);
        float2 rightVertex = new float2(size * 0.88f, size * 0.88f);

        double x = (hueWeight * topVertex.X) + (whiteWeight * leftVertex.X) + (blackWeight * rightVertex.X);
        double y = (hueWeight * topVertex.Y) + (whiteWeight * leftVertex.Y) + (blackWeight * rightVertex.Y);
        return new int2((int)Math.Round(x, MidpointRounding.AwayFromZero), (int)Math.Round(y, MidpointRounding.AwayFromZero));
    }

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

    /// <summary>
    /// Normalizes one hue angle into the standard [0, 360) range.
    /// </summary>
    /// <param name="hue">Hue angle in degrees.</param>
    /// <returns>Normalized hue angle.</returns>
    public static double NormalizeHue(double hue) {
        double wrappedHue = hue % 360.0;
        if (wrappedHue < 0.0) {
            wrappedHue += 360.0;
        }

        return wrappedHue;
    }

    /// <summary>
    /// Clamps one normalized scalar into the legal [0, 1] range.
    /// </summary>
    /// <param name="value">Value to clamp.</param>
    /// <returns>Clamped scalar.</returns>
    static double Clamp01(double value) {
        return Math.Clamp(value, 0.0, 1.0);
    }

    /// <summary>
    /// Converts one normalized channel value into a byte-backed color channel.
    /// </summary>
    /// <param name="value">Normalized channel value.</param>
    /// <returns>Rounded byte channel.</returns>
    static byte ToByteChannel(double value) {
        return (byte)Math.Clamp((int)Math.Round(value * 255.0, MidpointRounding.AwayFromZero), 0, 255);
    }

    /// <summary>
    /// Builds one runtime texture from raw RGBA pixel data.
    /// </summary>
    /// <param name="width">Texture width in pixels.</param>
    /// <param name="height">Texture height in pixels.</param>
    /// <param name="colors">Pixel data in RGBA order.</param>
    /// <returns>Runtime texture created by the active 2D renderer.</returns>
    static RuntimeTexture BuildRuntimeTexture(int width, int height, byte[] colors) {
        TextureAsset textureAsset = new TextureAsset {
            Width = (ushort)width,
            Height = (ushort)height,
            Colors = colors
        };

        return Core.Instance.RenderManager2D.BuildTextureFromRaw(textureAsset);
    }

    /// <summary>
    /// Resolves barycentric weights for one point inside a triangle.
    /// </summary>
    /// <param name="point">Point to evaluate.</param>
    /// <param name="topVertex">Triangle top vertex.</param>
    /// <param name="leftVertex">Triangle left vertex.</param>
    /// <param name="rightVertex">Triangle right vertex.</param>
    /// <param name="topWeight">Resolved top vertex weight.</param>
    /// <param name="leftWeight">Resolved left vertex weight.</param>
    /// <param name="rightWeight">Resolved right vertex weight.</param>
    /// <returns>True when the point lies inside the triangle.</returns>
    static bool TryResolveTriangleWeights(
        float2 point,
        float2 topVertex,
        float2 leftVertex,
        float2 rightVertex,
        out double topWeight,
        out double leftWeight,
        out double rightWeight) {
        double denominator = ((leftVertex.Y - rightVertex.Y) * (topVertex.X - rightVertex.X)) + ((rightVertex.X - leftVertex.X) * (topVertex.Y - rightVertex.Y));
        if (Math.Abs(denominator) <= double.Epsilon) {
            topWeight = 0.0;
            leftWeight = 0.0;
            rightWeight = 0.0;
            return false;
        }

        topWeight = (((leftVertex.Y - rightVertex.Y) * (point.X - rightVertex.X)) + ((rightVertex.X - leftVertex.X) * (point.Y - rightVertex.Y))) / denominator;
        leftWeight = (((rightVertex.Y - topVertex.Y) * (point.X - rightVertex.X)) + ((topVertex.X - rightVertex.X) * (point.Y - rightVertex.Y))) / denominator;
        rightWeight = 1.0 - topWeight - leftWeight;

        return topWeight >= 0.0 && leftWeight >= 0.0 && rightWeight >= 0.0;
    }
}
