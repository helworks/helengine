namespace helengine.timeline.tests {
    /// <summary>
    /// Reads the complete example out of <c>docs/helengine-timeline.md</c> so tests keep the documentation honest.
    /// </summary>
    static class TimelineDocumentation {
        /// <summary>
        /// Relative location of the format reference from the repository root.
        /// </summary>
        const string RelativePath = "docs/helengine-timeline.md";

        /// <summary>
        /// Marker placed on the line before the example's fenced block.
        /// </summary>
        const string ExampleMarker = "<!-- timeline-example -->";

        /// <summary>
        /// Finds the documentation file by walking up from the test output directory and returns the JSON of the fenced
        /// block that follows the example marker.
        /// </summary>
        /// <returns>The example JSON text.</returns>
        public static string ReadExampleJson() {
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
