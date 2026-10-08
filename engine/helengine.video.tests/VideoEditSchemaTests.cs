using System.Text.Json;

namespace helengine.video.tests {
    /// <summary>
    /// Verifies the generated JSON Schema is closed and carries the fixed vocabularies the AI must respect.
    /// </summary>
    public class VideoEditSchemaTests {
        /// <summary>
        /// The root and nested objects reject unknown properties, and duration modes are an enum.
        /// </summary>
        [Fact]
        public void Create_ClosesObjectsAndListsDurationModes() {
            JsonElement schema = VideoEditSchema.Create();

            Assert.False(schema.GetProperty("additionalProperties").GetBoolean());
            JsonElement scene = schema.GetProperty("properties").GetProperty("scenes").GetProperty("items");
            Assert.False(scene.GetProperty("additionalProperties").GetBoolean());
            JsonElement mode = scene.GetProperty("properties").GetProperty("duration").GetProperty("properties").GetProperty("mode");
            Assert.Contains("from_take", mode.GetProperty("enum").EnumerateArray().Select(value => value.ValueKind == JsonValueKind.Null ? null : value.GetString()));
        }

        /// <summary>
        /// Lock markers only accept human or ai.
        /// </summary>
        [Fact]
        public void Create_LockMarkersAreAnEnum() {
            JsonElement scene = VideoEditSchema.Create().GetProperty("properties").GetProperty("scenes").GetProperty("items");
            JsonElement by = scene.GetProperty("properties").GetProperty("entry").GetProperty("properties").GetProperty("by");
            Assert.Equal(["human", "ai", null], by.GetProperty("enum").EnumerateArray().Select(value => value.ValueKind == JsonValueKind.Null ? null : value.GetString()));
        }
    }
}
