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

        /// <summary>
        /// Gets or sets the graphic templates overlay graphics expand from; required only when an overlay has a graphic,
        /// and expected to match the templates published in <see cref="Capabilities"/>.
        /// </summary>
        public GraphicTemplateCatalog GraphicTemplates { get; set; }

        /// <summary>
        /// Gets or sets the renderer-backed text measurer graphic layout uses; when absent a deterministic estimate is
        /// used and a <c>text_measure_estimated</c> info diagnostic is reported.
        /// </summary>
        public IVideoTextMeasurer TextMeasurer { get; set; }
    }
}
