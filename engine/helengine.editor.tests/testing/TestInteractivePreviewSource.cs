using helengine.editor;

namespace helengine.editor.tests.testing {
    /// <summary>
    /// Provides a preview source double that records wheel and drag forwarding.
    /// </summary>
    internal class TestInteractivePreviewSource : IPreviewSource, IPreviewInteractionSource {
        /// <summary>
        /// Initializes one test preview source with the supplied texture.
        /// </summary>
        /// <param name="texture">Texture exposed by the preview source.</param>
        public TestInteractivePreviewSource(RuntimeTexture texture) {
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
        /// Gets how many times the source was resized.
        /// </summary>
        public int ResizeCount { get; private set; }

        /// <summary>
        /// Gets how many times the source was updated.
        /// </summary>
        public int UpdateCount { get; private set; }

        /// <summary>
        /// Gets how many wheel events were forwarded to the source.
        /// </summary>
        public int WheelCount { get; private set; }

        /// <summary>
        /// Gets how many drag events were forwarded to the source.
        /// </summary>
        public int DragCount { get; private set; }

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
        /// Records a wheel interaction forwarded from the preview panel.
        /// </summary>
        /// <param name="wheelDelta">Raw mouse-wheel delta.</param>
        public void HandleMouseWheel(int wheelDelta) {
            WheelCount++;
        }

        /// <summary>
        /// Records a drag interaction forwarded from the preview panel.
        /// </summary>
        /// <param name="delta">Mouse delta accumulated since the previous frame.</param>
        public void HandleMouseDrag(int2 delta) {
            DragCount++;
        }

        /// <summary>
        /// Marks the source as disposed.
        /// </summary>
        public void Dispose() {
        }
    }
}
