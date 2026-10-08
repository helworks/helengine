using System.Text.Json;

namespace helengine.video.tests {
    /// <summary>
    /// Verifies the edit validator rules for overlay graphics against the published template descriptors.
    /// </summary>
    public class VideoGraphicValidatorTests {
        /// <summary>
        /// The sample graphic edit is valid, even though its overlay leaves <c>at</c> empty.
        /// </summary>
        [Fact]
        public void Validate_Sample_HasNoErrors() {
            Assert.Empty(VideoEditValidator.Validate(GraphicEditSamples.ContrastChain(), GraphicEditSamples.Catalog()));
        }

        /// <summary>
        /// Each mutation reports the expected code at the expected path.
        /// </summary>
        /// <param name="mutation">Name of the mutation to apply.</param>
        /// <param name="code">Expected diagnostic code.</param>
        /// <param name="path">Expected path suffix.</param>
        [Theory]
        [InlineData("unknown_template", "unknown_graphic_template", "graphic.template")]
        [InlineData("wrong_version", "unknown_graphic_template", "graphic.template")]
        [InlineData("too_few_items", "invalid_graphic", "graphic.items")]
        [InlineData("too_many_items", "invalid_graphic", "graphic.items")]
        [InlineData("item_too_long", "invalid_graphic", "graphic.items[1]")]
        [InlineData("blank_item", "invalid_graphic", "graphic.items[0]")]
        [InlineData("at_count", "invalid_graphic", "graphic.at")]
        [InlineData("bad_moment", "invalid_moment", "graphic.at[0]")]
        [InlineData("unknown_parameter", "invalid_graphic", "graphic.parameters.glow")]
        [InlineData("parameter_type", "invalid_graphic", "graphic.parameters.dim_previous")]
        [InlineData("parameter_range", "invalid_graphic", "graphic.parameters.dim_opacity")]
        [InlineData("separator_too_long", "invalid_graphic", "graphic.separator")]
        [InlineData("accent_without_slot", "invalid_graphic", "graphic.accent_item")]
        [InlineData("accent_missing", "invalid_graphic", "graphic.accent_item")]
        [InlineData("accent_out_of_range", "invalid_graphic", "graphic.accent_item")]
        [InlineData("unknown_layout", "invalid_graphic", "graphic.layout")]
        [InlineData("plain_overlay_without_at", "invalid_moment", "overlays[0].at")]
        public void Validate_ReportsGraphicErrors(string mutation, string code, string path) {
            VideoEdit edit = GraphicEditSamples.ContrastChain();
            VideoOverlay overlay = edit.Scenes[0].Overlays[0];
            VideoGraphic graphic = overlay.Graphic;
            switch (mutation) {
                case "unknown_template": graphic.Template = "spinning_cube"; break;
                case "wrong_version": graphic.Version = 2; break;
                case "too_few_items": graphic.Items = ["Só"]; graphic.At = [new VideoMoment { Sec = 1 }]; break;
                case "too_many_items": graphic.Items = ["A", "B", "C", "D", "E"]; graphic.At = Enumerable.Range(0, 5).Select(index => new VideoMoment { Sec = index }).ToList(); break;
                case "item_too_long": graphic.Items[1] = new string('x', 29); break;
                case "blank_item": graphic.Items[0] = " "; break;
                case "at_count": graphic.At.RemoveAt(2); break;
                case "bad_moment": graphic.At[0] = new VideoMoment { Sec = 1, Word = "legalizar" }; break;
                case "unknown_parameter": graphic.Parameters["glow"] = JsonSerializer.SerializeToElement(1); break;
                case "parameter_type": graphic.Parameters["dim_previous"] = JsonSerializer.SerializeToElement("yes"); break;
                case "parameter_range": graphic.Parameters["dim_opacity"] = JsonSerializer.SerializeToElement(3); break;
                case "separator_too_long": graphic.Separator = "versus"; break;
                case "accent_without_slot": graphic.AccentItem = 0; break;
                case "accent_missing": graphic.Template = "highlight_word"; break;
                case "accent_out_of_range": graphic.Template = "highlight_word"; graphic.AccentItem = 3; break;
                case "unknown_layout": graphic.Layout = "diagonal"; break;
                case "plain_overlay_without_at": overlay.Graphic = null; break;
                default: throw new ArgumentException(mutation);
            }

            IReadOnlyList<VideoDiagnostic> errors = VideoEditValidator.Validate(edit, GraphicEditSamples.Catalog());

            Assert.Contains(errors, diagnostic => diagnostic.Code == code && diagnostic.Path.EndsWith(path, StringComparison.Ordinal));
        }

        /// <summary>
        /// A graphic overlay round-trips through the edit JSON with snake_case names.
        /// </summary>
        [Fact]
        public void Json_RoundTripsGraphic() {
            VideoEdit edit = GraphicEditSamples.ContrastChain();
            edit.Scenes[0].Overlays[0].Graphic.AccentItem = 2;
            edit.Scenes[0].Overlays[0].Graphic.Parameters["dim_opacity"] = JsonSerializer.SerializeToElement(0.5);

            string json = VideoEditJson.Serialize(edit);
            VideoGraphic graphic = VideoEditJson.Parse(json).Scenes[0].Overlays[0].Graphic;

            Assert.Contains("\"accent_item\": 2", json);
            Assert.Equal("contrast_chain", graphic.Template);
            Assert.Equal(["Legalizar", "Descriminalizar", "Tratar"], graphic.Items);
            Assert.Equal("tratar", graphic.At[2].Word);
            Assert.Equal(0.5, graphic.Parameters["dim_opacity"].GetDouble());
        }

        /// <summary>
        /// The generated edit schema closes the graphic object and lists the layout vocabulary.
        /// </summary>
        [Fact]
        public void Schema_DescribesGraphicLayouts() {
            string schema = VideoEditSchema.Create().GetRawText();

            Assert.Contains("\"auto\"", schema);
            Assert.Contains("\"accent_item\"", schema);
        }
    }
}
