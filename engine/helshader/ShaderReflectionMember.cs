using System.Text.Json.Serialization;

namespace helshader {
    /// <summary>
    /// Describes a constant buffer member in reflection data.
    /// </summary>
    public class ShaderReflectionMember {
        /// <summary>
        /// Gets or sets the member name.
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the member type name.
        /// </summary>
        [JsonPropertyName("type")]
        public string Type { get; set; }

        /// <summary>
        /// Gets or sets the byte offset within the constant buffer.
        /// </summary>
        [JsonPropertyName("offset")]
        public int Offset { get; set; }

        /// <summary>
        /// Gets or sets the member size in bytes.
        /// </summary>
        [JsonPropertyName("size")]
        public int Size { get; set; }
    }
}
