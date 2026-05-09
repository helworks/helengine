namespace helengine.editor.tests.components.ui;

/// <summary>
/// Verifies the shared color math used by the reusable editor color picker.
/// </summary>
public sealed class EditorColorUtilsTests {
    /// <summary>
    /// Ensures the primary red color round-trips through HSV without changing the authored value.
    /// </summary>
    [Fact]
    public void RgbToHsv_AndBack_ForPrimaryRed_PreservesTheOriginalColor() {
        byte4 source = new byte4(255, 0, 0, 128);

        EditorColorUtils.RgbToHsv(source, out double hue, out double saturation, out double value);
        byte4 result = EditorColorUtils.HsvToRgb(hue, saturation, value, source.W);

        Assert.Equal(source, result);
    }

    /// <summary>
    /// Ensures a neutral gray color produces zero saturation and a matching value channel.
    /// </summary>
    [Fact]
    public void RgbToHsv_ForNeutralGray_ProducesZeroSaturation() {
        EditorColorUtils.RgbToHsv(new byte4(96, 96, 96, 255), out double hue, out double saturation, out double value);

        Assert.Equal(0.0, hue, 6);
        Assert.Equal(0.0, saturation, 6);
        Assert.InRange(value, 0.37, 0.38);
    }

    /// <summary>
    /// Ensures invalid HSV inputs clamp into the legal RGB range instead of overflowing.
    /// </summary>
    [Fact]
    public void HsvToRgb_WhenGivenOutOfRangeValues_ClampsIntoAValidColor() {
        byte4 result = EditorColorUtils.HsvToRgb(420.0, 2.0, -0.5, 64);

        Assert.Equal(new byte4(0, 0, 0, 64), result);
    }

    /// <summary>
    /// Ensures hue normalization treats negative and wrapped angles identically.
    /// </summary>
    [Fact]
    public void HsvToRgb_WhenHueWraps_ReturnsTheSameColor() {
        byte4 wrapped = EditorColorUtils.HsvToRgb(300.0, 1.0, 1.0, 255);
        byte4 negative = EditorColorUtils.HsvToRgb(-60.0, 1.0, 1.0, 255);

        Assert.Equal(wrapped, negative);
    }
}
