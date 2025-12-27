using System.Text.Json.Serialization;

namespace helshader {
    /// <summary>
    /// Describes a shader input or output signature element in reflection data.
    /// </summary>
    public class ShaderReflectionSignatureElement {
        /// <summary>
        /// Gets or sets the semantic name.
        /// </summary>
        [JsonPropertyName("semantic")]
        public string Semantic { get; set; }

        /// <summary>
        /// Gets or sets the semantic index.
        /// </summary>
        [JsonPropertyName("index")]
        public int Index { get; set; }

        /// <summary>
        /// Gets or sets the format string.
        /// </summary>
        [JsonPropertyName("format")]
        public string Format { get; set; }
    }
}
