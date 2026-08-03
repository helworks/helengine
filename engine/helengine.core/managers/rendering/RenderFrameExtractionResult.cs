namespace helengine {
    /// <summary>
    /// Stores the results of extracting one or more render frames from scene data.
    /// </summary>
    public class RenderFrameExtractionResult : IDisposable {
        /// <summary>
        /// Stores every extracted frame owned by this result.
        /// </summary>
        [NativeOwnedMember]
        RenderFrame[] FramesValue;

        /// <summary>
        /// Initializes one extraction result.
        /// </summary>
        /// <param name="frames">Extracted frames keyed by camera order.</param>
        /// <param name="backendCapabilities">Backend capability profile that guided extraction.</param>
        public RenderFrameExtractionResult(
            [NativeTakesOwnership] RenderFrame[] frames,
            RendererBackendCapabilityProfile backendCapabilities) {
            FramesValue = frames ?? throw new ArgumentNullException(nameof(frames));
            BackendCapabilities = backendCapabilities ?? throw new ArgumentNullException(nameof(backendCapabilities));
        }

        /// <summary>
        /// Gets the extracted frames.
        /// </summary>
        public IReadOnlyList<RenderFrame> Frames => FramesValue;

        /// <summary>
        /// Gets the backend capability profile that guided extraction.
        /// </summary>
        public RendererBackendCapabilityProfile BackendCapabilities { get; }

        /// <summary>
        /// Disposes and deletes every extracted frame owned by this result.
        /// </summary>
        public void Dispose() {
            NativeOwnership.DisposeItemsAndRelease(ref FramesValue);
        }
    }
}
