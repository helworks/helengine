namespace helengine.editor.tests.testing {
    /// <summary>
    /// Minimal render-target implementation used by preview camera tests.
    /// </summary>
    internal class TestRenderTarget : RenderTarget, IDisposable {
        /// <summary>
        /// Initializes one test render target with post-process-friendly defaults.
        /// </summary>
        public TestRenderTarget() {
            CanSampleAsTexture = true;
            HasDepthBuffer = true;
        }

        /// <summary>
        /// Gets a value indicating whether the test target was disposed.
        /// </summary>
        public bool IsDisposed { get; private set; }

        /// <summary>
        /// Releases the test render target.
        /// </summary>
        public void Dispose() {
            IsDisposed = true;
        }
    }
}
