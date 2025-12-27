using System.Text.Json.Serialization;

namespace helshader {
    /// <summary>
    /// Describes shader profiles for a specific target backend.
    /// </summary>
    public class ShaderManifestProfile {
        /// <summary>
        /// Gets or sets the vertex shader profile.
        /// </summary>
        [JsonPropertyName("vertex")]
        public string Vertex { get; set; }

        /// <summary>
        /// Gets or sets the pixel shader profile.
        /// </summary>
        [JsonPropertyName("pixel")]
        public string Pixel { get; set; }

        /// <summary>
        /// Gets or sets the geometry shader profile.
        /// </summary>
        [JsonPropertyName("geometry")]
        public string Geometry { get; set; }

        /// <summary>
        /// Gets or sets the hull shader profile.
        /// </summary>
        [JsonPropertyName("hull")]
        public string Hull { get; set; }

        /// <summary>
        /// Gets or sets the domain shader profile.
        /// </summary>
        [JsonPropertyName("domain")]
        public string Domain { get; set; }

        /// <summary>
        /// Gets or sets the compute shader profile.
        /// </summary>
        [JsonPropertyName("compute")]
        public string Compute { get; set; }
    }
}
