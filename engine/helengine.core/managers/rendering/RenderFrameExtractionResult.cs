namespace helengine {
    /// <summary>
    /// Stores the results of extracting one or more render frames from scene data.
    /// </summary>
    public class RenderFrameExtractionResult {
        /// <summary>
        /// Initializes one extraction result.
        /// </summary>
        /// <param name="frames">Extracted frames keyed by camera order.</param>
        /// <param name="backendCapabilities">Backend capability profile that guided extraction.</param>
        public RenderFrameExtractionResult(IReadOnlyList<RenderFrame> frames, RendererBackendCapabilityProfile backendCapabilities) {
            Frames = frames ?? throw new ArgumentNullException(nameof(frames));
            BackendCapabilities = backendCapabilities ?? throw new ArgumentNullException(nameof(backendCapabilities));
        }

        /// <summary>
        /// Gets the extracted frames.
        /// </summary>
        public IReadOnlyList<RenderFrame> Frames { get; }

        /// <summary>
        /// Gets the backend capability profile that guided extraction.
        /// </summary>
        public RendererBackendCapabilityProfile BackendCapabilities { get; }
    }
}
