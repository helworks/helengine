namespace helengine.video.tests {
    /// <summary>
    /// Locks the <c>helengine.video.edit.v1</c> JSON contract: stable round trips, snake_case names and strict parsing.
    /// </summary>
    public class VideoEditJsonTests {
        /// <summary>
        /// Writing, reading and writing again produces the same text, so the product can diff stored revisions.
        /// </summary>
        [Fact]
        public void Serialize_Parse_Serialize_IsStable() {
            string first = VideoEditJson.Serialize(VideoEditSamples.TwoScenes());
            string second = VideoEditJson.Serialize(VideoEditJson.Parse(first));

            Assert.Equal(first, second);
            Assert.Contains("\"mode\": \"from_take\"", first);
            Assert.Contains("\"by\": \"human\"", first);
            Assert.Contains("\"word\": \"lei\"", first);
            Assert.Contains("\"audio_fade_sec\": 0.15", first);
        }

        /// <summary>
        /// A misspelled property is an error, never silently ignored.
        /// </summary>
        [Fact]
        public void Parse_UnknownProperty_Throws() {
            string json = VideoEditJson.Serialize(VideoEditSamples.TwoScenes()).Replace("\"project_profile\"", "\"project_profil\"");
            Assert.ThrowsAny<Exception>(() => VideoEditJson.Parse(json));
        }

        /// <summary>
        /// A repeated key is an error, never resolved by keeping the last value.
        /// </summary>
        [Fact]
        public void Parse_DuplicateKey_Throws() {
            string json = VideoEditJson.Serialize(VideoEditSamples.TwoScenes()).Replace("\"revision\": 3,", "\"revision\": 3, \"revision\": 4,");
            Assert.Throws<InvalidDataException>(() => VideoEditJson.Parse(json));
        }

        /// <summary>
        /// Another schema id is rejected.
        /// </summary>
        [Fact]
        public void Parse_OtherSchema_Throws() {
            string json = VideoEditJson.Serialize(VideoEditSamples.TwoScenes()).Replace(VideoEditJson.SchemaId, "helengine.video.edit.v0");
            Assert.Throws<InvalidDataException>(() => VideoEditJson.Parse(json));
        }
    }
}
