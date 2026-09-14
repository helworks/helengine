using helengine.directx11;

namespace helengine.editor {
    /// <summary>
    /// Creates DirectX11 editor picking backends for Windows viewports.
    /// </summary>
    public sealed class DirectX11EditorPickingBackendFactory : IEditorPickingBackendFactory {
        readonly DirectX11Renderer3D Renderer;

        /// <summary>
        /// Initializes a factory over the caller-owned DirectX11 renderer.
        /// </summary>
        /// <param name="renderer">Renderer shared by editor viewport passes.</param>
        public DirectX11EditorPickingBackendFactory(DirectX11Renderer3D renderer) {
            Renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
        }

        /// <summary>
        /// Gets whether DirectX11 picking is available.
        /// </summary>
        public bool IsSupported => true;

        /// <summary>
        /// Creates one backend whose target resources belong to the supplied viewport camera.
        /// </summary>
        /// <param name="camera">Viewport camera that receives the backend-owned render target.</param>
        /// <returns>New DirectX11 picking backend.</returns>
        public IEditorPickingBackend Create(CameraComponent camera) {
            if (camera == null) {
                throw new ArgumentNullException(nameof(camera));
            }

            return new DirectX11EditorPickingBackend(Renderer, camera);
        }
    }
}
