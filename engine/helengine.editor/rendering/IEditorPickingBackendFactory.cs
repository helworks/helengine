namespace helengine.editor {
    /// <summary>
    /// Creates the platform-owned picking backend for one viewport camera.
    /// </summary>
    public interface IEditorPickingBackendFactory {
        /// <summary>
        /// Gets whether this host can perform GPU picking.
        /// </summary>
        bool IsSupported { get; }

        /// <summary>
        /// Creates a backend that owns native picking resources for the supplied camera.
        /// </summary>
        /// <param name="camera">Camera whose target may be borrowed by the backend.</param>
        /// <returns>New backend instance.</returns>
        IEditorPickingBackend Create(CameraComponent camera);
    }
}
