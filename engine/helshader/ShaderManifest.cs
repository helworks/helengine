using System.Text.Json.Serialization;

namespace helshader {
    /// <summary>
    /// Represents the root shader manifest configuration.
    /// </summary>
    public class ShaderManifest {
        /// <summary>
        /// Gets or sets the manifest schema version.
        /// </summary>
        [JsonPropertyName("version")]
        public int Version { get; set; }

        /// <summary>
        /// Gets or sets the root folder for shader sources.
        /// </summary>
        [JsonPropertyName("root")]
        public string Root { get; set; }

        /// <summary>
        /// Gets or sets include directories relative to the root.
        /// </summary>
        [JsonPropertyName("includeDirs")]
        public string[] IncludeDirs { get; set; }

        /// <summary>
        /// Gets or sets output configuration for compiled artifacts.
        /// </summary>
        [JsonPropertyName("output")]
        public ShaderManifestOutput Output { get; set; }

        /// <summary>
        /// Gets or sets the list of targets to compile.
        /// </summary>
        [JsonPropertyName("targets")]
        public string[] Targets { get; set; }

        /// <summary>
        /// Gets or sets the profile map for each target.
        /// </summary>
        [JsonPropertyName("profiles")]
        public Dictionary<string, ShaderManifestProfile> Profiles { get; set; }

        /// <summary>
        /// Gets or sets the shader tool paths.
        /// </summary>
        [JsonPropertyName("tools")]
        public ShaderManifestTools Tools { get; set; }

        /// <summary>
        /// Gets or sets the shader entries included in the manifest.
        /// </summary>
        [JsonPropertyName("shaders")]
        public ShaderManifestShader[] Shaders { get; set; }
    }
}
