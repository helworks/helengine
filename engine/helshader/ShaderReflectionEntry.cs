using System.Text.Json.Serialization;

namespace helshader {
    /// <summary>
    /// Represents reflection data for a single shader entry point and variant.
    /// </summary>
    public class ShaderReflectionEntry {
        /// <summary>
        /// Gets or sets the shader program name.
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the shader stage name.
        /// </summary>
        [JsonPropertyName("stage")]
        public string Stage { get; set; }

        /// <summary>
        /// Gets or sets the entry point name.
        /// </summary>
        [JsonPropertyName("entryPoint")]
        public string EntryPoint { get; set; }

        /// <summary>
        /// Gets or sets the list of targets included in this reflection entry.
        /// </summary>
        [JsonPropertyName("targets")]
        public string[] Targets { get; set; }

        /// <summary>
        /// Gets or sets the resource bindings.
        /// </summary>
        [JsonPropertyName("bindings")]
        public ShaderReflectionBinding[] Bindings { get; set; }

        /// <summary>
        /// Gets or sets the input signature elements.
        /// </summary>
        [JsonPropertyName("inputs")]
        public ShaderReflectionSignatureElement[] Inputs { get; set; }

        /// <summary>
        /// Gets or sets the output signature elements.
        /// </summary>
        [JsonPropertyName("outputs")]
        public ShaderReflectionSignatureElement[] Outputs { get; set; }

        /// <summary>
        /// Gets or sets the variants list.
        /// </summary>
        [JsonPropertyName("variants")]
        public ShaderReflectionVariant[] Variants { get; set; }
    }
}
