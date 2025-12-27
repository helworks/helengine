using System.Text.Json.Serialization;

namespace helshader {
    /// <summary>
    /// Describes a shader module entry in the manifest.
    /// </summary>
    public class ShaderManifestShader {
        /// <summary>
        /// Gets or sets the shader module name.
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the shader source file path relative to the manifest root.
        /// </summary>
        [JsonPropertyName("file")]
        public string File { get; set; }

        /// <summary>
        /// Gets or sets the entry points defined in the shader.
        /// </summary>
        [JsonPropertyName("entries")]
        public ShaderManifestEntryPoint[] Entries { get; set; }

        /// <summary>
        /// Gets or sets the variant definitions for the shader.
        /// </summary>
        [JsonPropertyName("variants")]
        public ShaderManifestVariant[] Variants { get; set; }
    }
}
