using System.Text.Json;
using helengine.vfx.captions;
using helengine.video;

namespace helengine.media.windows.tests;

/// <summary>
/// Checks that graphic layout measures text with the caption renderer's own font, case conversion and word spacing.
/// </summary>
public sealed class WindowsVideoTextMeasurerTests {
    /// <summary>
    /// Widths follow the style's case conversion and equal the renderer font's word advances plus spaces.
    /// </summary>
    [Fact]
    public void MeasuresLikeTheCaptionRenderer() {
        using WindowsVideoTextMeasurer measurer = new WindowsVideoTextMeasurer(null);
        JsonElement upper = JsonSerializer.SerializeToElement(new { FontFamily = "Arial", Bold = true, Uppercase = true });
        JsonElement lower = JsonSerializer.SerializeToElement(new { FontFamily = "Arial", Bold = true, Uppercase = false });

        VideoTextExtent shouted = measurer.Measure(upper, "não é crime", 40);
        VideoTextExtent plain = measurer.Measure(lower, "não é crime", 40);

        Assert.True(shouted.Width > plain.Width);
        Assert.Equal(40 * VideoTextStyles.LineHeight, shouted.Height, 6);
        using CaptionFont font = new CaptionFont(new CaptionStyle { FontFamily = "Arial", Bold = true });
        Assert.Equal(font.MeasureLine("NÃO É CRIME", 40), shouted.Width, 3);
        Assert.InRange(measurer.Measure(upper, "não é crime", 80).Width / shouted.Width, 1.9, 2.1);
    }

    /// <summary>
    /// A style font outside the assets root is rejected like it is for rendering.
    /// </summary>
    [Fact]
    public void RejectsFontsOutsideTheAssetsRoot() {
        using WindowsVideoTextMeasurer measurer = new WindowsVideoTextMeasurer("C:/dev/helworks/builds/helengine/media-composition/fixtures");
        JsonElement style = JsonSerializer.SerializeToElement(new { FontFile = "../fonts/escape.ttf" });

        Assert.Throws<InvalidDataException>(() => measurer.Measure(style, "x", 20));
    }
}
