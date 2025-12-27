using System.Text.Json.Serialization;

namespace helshader {
    /// <summary>
    /// Describes output paths for compiled shader artifacts.
    /// </summary>
    public class ShaderManifestOutput {
        /// <summary>
        /// Gets or sets the binary output directory.
        /// </summary>
        [JsonPropertyName("binaryDir")]
        public string BinaryDir { get; set; }

        /// <summary>
        /// Gets or sets the reflection output directory.
        /// </summary>
        [JsonPropertyName("reflectionDir")]
        public string ReflectionDir { get; set; }

        /// <summary>
        /// Gets or sets the generated code output directory.
        /// </summary>
        [JsonPropertyName("codegenDir")]
        public string CodegenDir { get; set; }

        /// <summary>
        /// Gets or sets the compiled module output directory.
        /// </summary>
        [JsonPropertyName("moduleDir")]
        public string ModuleDir { get; set; }

        /// <summary>
        /// Gets or sets the Metal output directory.
        /// </summary>
        [JsonPropertyName("mslDir")]
        public string MslDir { get; set; }

        /// <summary>
        /// Gets or sets the debug output directory.
        /// </summary>
        [JsonPropertyName("debugDir")]
        public string DebugDir { get; set; }
    }
}
