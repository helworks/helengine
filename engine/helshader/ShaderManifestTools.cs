using System.Text.Json.Serialization;

namespace helshader {
    /// <summary>
    /// Describes paths to external shader compiler tools.
    /// </summary>
    public class ShaderManifestTools {
        /// <summary>
        /// Gets or sets the path to the fxc compiler.
        /// </summary>
        [JsonPropertyName("fxc")]
        public string Fxc { get; set; }

        /// <summary>
        /// Gets or sets the path to the dxc compiler.
        /// </summary>
        [JsonPropertyName("dxc")]
        public string Dxc { get; set; }

        /// <summary>
        /// Gets or sets the path to the SPIRV-Cross tool.
        /// </summary>
        [JsonPropertyName("spirvCross")]
        public string SpirvCross { get; set; }
    }
}
