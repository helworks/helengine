using System.Text.Json.Serialization;

namespace helshader {
    /// <summary>
    /// Describes a shader variant definition in the manifest.
    /// </summary>
    public class ShaderManifestVariant {
        /// <summary>
        /// Gets or sets the variant name.
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets preprocessor defines for the variant.
        /// </summary>
        [JsonPropertyName("defines")]
        public string[] Defines { get; set; }
    }
}
