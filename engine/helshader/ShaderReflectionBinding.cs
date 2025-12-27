using System.Text.Json.Serialization;

namespace helshader {
    /// <summary>
    /// Describes a single shader resource binding in reflection data.
    /// </summary>
    public class ShaderReflectionBinding {
        /// <summary>
        /// Gets or sets the binding name.
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the binding type string.
        /// </summary>
        [JsonPropertyName("type")]
        public string Type { get; set; }

        /// <summary>
        /// Gets or sets the set index for the binding.
        /// </summary>
        [JsonPropertyName("set")]
        public int Set { get; set; }

        /// <summary>
        /// Gets or sets the binding slot index.
        /// </summary>
        [JsonPropertyName("slot")]
        public int Slot { get; set; }

        /// <summary>
        /// Gets or sets the size in bytes for constant buffers.
        /// </summary>
        [JsonPropertyName("size")]
        public int Size { get; set; }

        /// <summary>
        /// Gets or sets the constant buffer member list.
        /// </summary>
        [JsonPropertyName("members")]
        public ShaderReflectionMember[] Members { get; set; }
    }
}
