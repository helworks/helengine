using System.Text.Json.Serialization;

namespace helshader {
    /// <summary>
    /// Describes an entry point within a shader file.
    /// </summary>
    public class ShaderManifestEntryPoint {
        /// <summary>
        /// Gets or sets the shader stage name.
        /// </summary>
        [JsonPropertyName("stage")]
        public string Stage { get; set; }

        /// <summary>
        /// Gets or sets the entry point function name.
        /// </summary>
        [JsonPropertyName("entry")]
        public string Entry { get; set; }
    }
}
