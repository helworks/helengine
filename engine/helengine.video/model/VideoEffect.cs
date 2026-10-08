using System.Text.Json;

namespace helengine.video {
    /// <summary>
    /// One catalog layer effect.
    /// </summary>
    public sealed class VideoEffect {
        /// <summary>
        /// Catalog effect id.
        /// </summary>
        public string Id { get; set; } = "";

        /// <summary>
        /// Catalog effect version.
        /// </summary>
        public int Version { get; set; } = 1;

        /// <summary>
        /// Effect parameters.
        /// </summary>
        public Dictionary<string, JsonElement> Parameters { get; set; } = new(StringComparer.Ordinal);

        /// <summary>
        /// Extra input roles bound to media ids.
        /// </summary>
        public Dictionary<string, string> Inputs { get; set; } = new(StringComparer.Ordinal);

    }
}
