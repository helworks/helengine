using System.Text.Json;

namespace helengine.video.tests {
    /// <summary>
    /// Keeps the overlay timeline example of <c>docs/helengine-media-composition.md</c> honest: it parses as an overlay,
    /// validates and compiles in a stack scene.
    /// </summary>
    public class VideoTimelineDocumentationTests {
        /// <summary>
        /// Document holding the example.
        /// </summary>
        const string RelativePath = "docs/helengine-media-composition.md";

        /// <summary>
        /// Marker placed right before the example's JSON block.
        /// </summary>
        const string ExampleMarker = "<!-- overlay-timeline-example -->";

        /// <summary>
        /// The documented overlay validates and compiles into a group with one layer per slot.
        /// </summary>
        [Fact]
        public void DocumentedExample_ValidatesAndCompiles() {
            VideoOverlay overlay = JsonSerializer.Deserialize<VideoOverlay>(ReadExample(), VideoEditJson.Options);
            VideoEdit edit = TimelineEditSamples.Contrast();
            edit.Scenes[0].Overlays = [overlay];

            Assert.Empty(VideoEditValidator.Validate(edit, GraphicEditSamples.Catalog()));
            VideoCompileResult result = VideoEditCompiler.Compile(edit, TimelineEditSamples.Context(new FixedAdvanceTextMeasurer()));

            Assert.False(result.HasErrors, string.Join("; ", result.Diagnostics.Select(item => item.Code + " " + item.Path + " " + item.Message)));
            Assert.Equal(4, result.Composition.Layers.Single(layer => layer.Id == "contrast-overlay-contrast").Members.Count);
        }

        /// <summary>
        /// Reads the example JSON block that follows the marker.
        /// </summary>
        /// <returns>The example JSON text.</returns>
        static string ReadExample() {
            DirectoryInfo directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, RelativePath))) {
                directory = directory.Parent;
            }
            if (directory == null) {
                throw new FileNotFoundException("Could not find " + RelativePath + " above " + AppContext.BaseDirectory + ".");
            }
            string text = File.ReadAllText(Path.Combine(directory.FullName, RelativePath));
            int marker = text.IndexOf(ExampleMarker, StringComparison.Ordinal);
            if (marker < 0) {
                throw new InvalidDataException(RelativePath + " has no " + ExampleMarker + " marker.");
            }
            int start = text.IndexOf("```json", marker, StringComparison.Ordinal);
            int bodyStart = text.IndexOf('\n', start) + 1;
            int end = text.IndexOf("```", bodyStart, StringComparison.Ordinal);
            return text.Substring(bodyStart, end - bodyStart);
        }
    }
}
