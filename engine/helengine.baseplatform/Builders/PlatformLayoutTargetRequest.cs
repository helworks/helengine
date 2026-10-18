using helengine.baseplatform.Definitions;

namespace helengine.baseplatform.Builders {
    /// <summary>Captures effective presentation inputs before scene cooking, independently of the later native build request.</summary>
    public sealed class PlatformLayoutTargetRequest {
        /// <summary>Captures selected profiles, normalized graphics options and an optional explicit Play video mode.</summary>
        public PlatformLayoutTargetRequest(string buildProfileId, string graphicsProfileId, IReadOnlyDictionary<string, string> graphicsOptionValues, PlatformVideoMode videoMode = null) {
            BuildProfileId = buildProfileId ?? throw new ArgumentNullException(nameof(buildProfileId));
            GraphicsProfileId = graphicsProfileId ?? throw new ArgumentNullException(nameof(graphicsProfileId));
            GraphicsOptionValues = new Dictionary<string, string>(graphicsOptionValues ?? throw new ArgumentNullException(nameof(graphicsOptionValues)), StringComparer.OrdinalIgnoreCase);
            VideoMode = videoMode;
        }
        /// <summary>Gets the selected native build profile.</summary>
        public string BuildProfileId { get; }
        /// <summary>Gets the selected graphics profile.</summary>
        public string GraphicsProfileId { get; }
        /// <summary>Gets captured effective values, without reading a mutable settings file during cook.</summary>
        public IReadOnlyDictionary<string, string> GraphicsOptionValues { get; }
        /// <summary>Gets the selected video mode when the backend declares a mode catalog.</summary>
        public PlatformVideoMode VideoMode { get; }
    }
}
