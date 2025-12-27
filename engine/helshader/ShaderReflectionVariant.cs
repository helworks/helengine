using System.Text.Json.Serialization;

namespace helshader {
    /// <summary>
    /// Describes a variant entry in reflection data.
    /// </summary>
    public class ShaderReflectionVariant {
        /// <summary>
        /// Gets or sets the variant name.
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the defines for this variant.
        /// </summary>
        [JsonPropertyName("defines")]
        public string[] Defines { get; set; }
    }
}
