namespace helengine.editor.tests.testing {
    /// <summary>Records native picking requests made by actual editor-session viewport construction.</summary>
    public sealed class TestEditorPickingBackendFactory : IEditorPickingBackendFactory {
        /// <summary>Gets cameras that received their own picking backend.</summary>
        public List<CameraComponent> Cameras { get; } = new List<CameraComponent>();

        /// <summary>Gets independently configurable readback backends in viewport creation order.</summary>
        public List<TestEditorPickingBackend> Backends { get; } = new List<TestEditorPickingBackend>();

        /// <summary>Declares that this host supports picking so viewport construction must attach the picker.</summary>
        public bool IsSupported => true;

        /// <summary>Creates one backend for the specified viewport camera and records its ownership.</summary>
        /// <param name="camera">Hidden picker camera created by the viewport controller.</param>
        /// <returns>Independent backend that the viewport must dispose.</returns>
        public IEditorPickingBackend Create(CameraComponent camera) {
            ArgumentNullException.ThrowIfNull(camera);
            TestEditorPickingBackend backend = new TestEditorPickingBackend();
            Cameras.Add(camera);
            Backends.Add(backend);
            return backend;
        }
    }
}
