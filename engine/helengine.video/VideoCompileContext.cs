using helengine.media;

namespace helengine.video {
    /// <summary>
    /// Inputs that shape one compilation besides the edit itself.
    /// </summary>
    public sealed class VideoCompileContext {
        /// <summary>
        /// Gets or sets the engine catalog effects, curves and properties are checked against.
        /// </summary>
        public MediaCapabilities Capabilities { get; set; }

        /// <summary>
        /// Gets or sets whether this is a final render: estimates and missing caption timings become pending issues.
        /// </summary>
        public bool Final { get; set; }
    }
}
