using helengine.editor;

namespace helengine.editor.tests.testing {
    /// <summary>
    /// Provides a minimal preview source used by panel lifecycle tests.
    /// </summary>
    public class TestPreviewSource : IPreviewSource {
        /// <summary>
        /// Initializes a new test preview source with the supplied texture.
        /// </summary>
        /// <param name="texture">Texture exposed by the preview source.</param>
        public TestPreviewSource(RuntimeTexture texture) {
            if (texture == null) {
                throw new ArgumentNullException(nameof(texture));
            }

            Texture = texture;
        }

        /// <summary>
        /// Gets the preview texture exposed to the panel.
        /// </summary>
        public RuntimeTexture Texture { get; }

        /// <summary>
        /// Gets a value indicating whether the source has been disposed.
        /// </summary>
        public bool IsDisposed { get; private set; }

        /// <summary>
        /// Gets or sets how many times the source was resized.
        /// </summary>
        public int ResizeCount { get; private set; }

        /// <summary>
        /// Gets or sets how many times the source was updated.
        /// </summary>
        public int UpdateCount { get; private set; }

        /// <summary>
        /// Records a resize request from the preview panel.
        /// </summary>
        /// <param name="contentSize">Usable panel content size.</param>
        public void Resize(int2 contentSize) {
            ResizeCount++;
        }

        /// <summary>
        /// Records an update tick from the preview panel.
        /// </summary>
        public void Update() {
            UpdateCount++;
        }

        /// <summary>
        /// Marks the source as disposed.
        /// </summary>
        public void Dispose() {
            IsDisposed = true;
        }
    }
}
