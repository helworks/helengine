namespace helengine.editor {
    /// <summary>
    /// Represents one active preview source that can supply a runtime texture to the preview panel.
    /// </summary>
    public interface IPreviewSource : IDisposable {
        /// <summary>
        /// Gets the runtime texture currently exposed by the preview source.
        /// </summary>
        RuntimeTexture Texture { get; }

        /// <summary>
        /// Updates the source for the current frame.
        /// </summary>
        void Update();

        /// <summary>
        /// Resizes the source to match the preview panel content area.
        /// </summary>
        /// <param name="contentSize">Usable panel content size in pixels.</param>
        void Resize(int2 contentSize);
    }
}
