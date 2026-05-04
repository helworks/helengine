namespace helengine.editor.tests.testing {
    /// <summary>
    /// Minimal render-target implementation used by preview camera tests.
    /// </summary>
    internal class TestRenderTarget : RenderTarget, IDisposable {
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
