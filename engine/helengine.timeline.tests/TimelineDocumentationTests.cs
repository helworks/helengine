namespace helengine.timeline.tests {
    /// <summary>
    /// Keeps <c>docs/helengine-timeline.md</c> honest: the complete example the planner model reads must parse, validate and
    /// round-trip.
    /// </summary>
    public class TimelineDocumentationTests {
        /// <summary>
        /// The documented example parses, validates, keeps its cues and nested definition, and round-trips exactly.
        /// </summary>
        [Fact]
        public void DocumentationExample_parsesValidatesAndRoundTrips() {
            string example = TimelineDocumentation.ReadExampleJson();

            TimelineAsset timeline = TimelineJson.Parse(example, null);
            string written = TimelineJson.Serialize(timeline);

            Assert.Equal("contrast_three_terms", timeline.TimelineId);
            Assert.Equal(3, timeline.Cues.Count);
            Assert.Equal(6, timeline.Slots.Count);
            TimelineNestedTrackAsset nested = Assert.IsType<TimelineNestedTrackAsset>(timeline.Tracks[9]);
            Assert.Equal("pop", nested.Clips[0].Definition.TimelineId);
            Assert.Equal(written, TimelineJson.Serialize(TimelineJson.Parse(written, null)));
            Assert.Contains("≠", written, StringComparison.Ordinal);
        }
    }
}
