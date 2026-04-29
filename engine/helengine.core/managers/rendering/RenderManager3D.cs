namespace helengine {
    /// <summary>
    /// Abstract base for 3D rendering backends.
    /// </summary>
    public abstract class RenderManager3D : IDisposable {
        private bool setOneWindow;

        /// <summary>
        /// Gets the primary window size when using a single window setup.
        /// </summary>
        public int2 MainWindowSize { get; private set; }

        /// <summary>
        /// Event raised when a window is resized.
        /// </summary>
        public event Action<IntPtr, int, int> WindowResized;

        /// <summary>
        /// Adds a window to the renderer and tracks its size.
        /// </summary>
        /// <param name="handle">Window handle.</param>
        /// <param name="width">Window width.</param>
        /// <param name="height">Window height.</param>
        public virtual void AddWindow(IntPtr handle, int width, int height) {
            if (!setOneWindow) {
                MainWindowSize = new int2(width, height);
            }

            setOneWindow = true;
        }

        /// <summary>
        /// Builds a runtime model from raw asset data.
        /// </summary>
        /// <param name="data">Raw model data.</param>
        /// <returns>Runtime model instance.</returns>
        public abstract RuntimeModel BuildModelFromRaw(ModelAsset data);

        /// <summary>
        /// Creates a render target that can be assigned to a camera.
        /// </summary>
        /// <param name="width">Width of the render target in pixels.</param>
        /// <param name="height">Height of the render target in pixels.</param>
        /// <returns>Render target instance.</returns>
        public virtual RenderTarget CreateRenderTarget(int width, int height) {
            throw new NotSupportedException("This renderer does not support render target creation.");
        }

        /// <summary>
        /// Builds a runtime material from raw asset data and an associated shader asset.
        /// </summary>
        /// <param name="materialAsset">Raw material asset definition.</param>
        /// <param name="shaderAsset">Shader asset used by the material.</param>
        /// <returns>Runtime material instance.</returns>
        public virtual RuntimeMaterial BuildMaterialFromRaw(MaterialAsset materialAsset, ShaderAsset shaderAsset) {
            throw new NotSupportedException("This renderer does not support material creation.");
        }

        /// <summary>
        /// Invalidates shader resources associated with a compiled shader asset.
        /// </summary>
        /// <param name="shaderAssetId">Shader asset identifier to invalidate.</param>
        /// <param name="shaderAsset">Updated shader asset data.</param>
        public virtual void InvalidateShaderResources(string shaderAssetId, ShaderAsset shaderAsset) {
        }

        /// <summary>
        /// Performs per-frame update for 3D rendering systems.
        /// </summary>
        public virtual void Update() { }

        /// <summary>
        /// Executes the 3D render pass.
        /// </summary>
        public virtual void Draw() { }

        /// <summary>
        /// Releases resources owned by the render manager.
        /// </summary>
        public virtual void Dispose() { }

        /// <summary>
        /// Triggers window resize handling; should be called by the host when resizing.
        /// </summary>
        /// <param name="handle">Window handle.</param>
        /// <param name="newWidth">New width.</param>
        /// <param name="newHeight">New height.</param>
        public virtual void OnWindowResize(IntPtr handle, int newWidth, int newHeight) {
            if (!setOneWindow || (MainWindowSize.X == 0 && MainWindowSize.Y == 0)) {
                MainWindowSize = new int2(newWidth, newHeight);
            }

            WindowResized?.Invoke(handle, newWidth, newHeight);
        }
    }
}
