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
        /// Builds a runtime model from one platform-owned cooked model payload.
        /// </summary>
        /// <param name="cookedAssetPath">Absolute path to the cooked model payload.</param>
        /// <returns>Runtime model instance.</returns>
        public virtual RuntimeModel BuildModelFromCooked(string cookedAssetPath) {
            if (string.IsNullOrWhiteSpace(cookedAssetPath)) {
                throw new ArgumentException("Cooked model asset path must be provided.", nameof(cookedAssetPath));
            }

            throw new NotSupportedException("This renderer does not support platform-owned cooked model creation.");
        }

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
        /// Builds a runtime material from one raw material asset using the active renderer's asset-loading strategy.
        /// </summary>
        /// <param name="assetContentManager">Content manager that can load companion assets needed by the renderer.</param>
        /// <param name="contentRootPath">Absolute packaged content root that owns the material asset.</param>
        /// <param name="materialAssetPath">Absolute path to the serialized material asset.</param>
        /// <returns>Runtime material instance.</returns>
        public virtual RuntimeMaterial BuildMaterialFromRawAsset(
            ContentManager assetContentManager,
            string contentRootPath,
            string materialAssetPath) {
            if (assetContentManager == null) {
                throw new ArgumentNullException(nameof(assetContentManager));
            }
            if (string.IsNullOrWhiteSpace(contentRootPath)) {
                throw new ArgumentException("Content root path must be provided.", nameof(contentRootPath));
            }
            if (string.IsNullOrWhiteSpace(materialAssetPath)) {
                throw new ArgumentException("Material asset path must be provided.", nameof(materialAssetPath));
            }

            throw new NotSupportedException("This renderer does not support material creation.");
        }

        /// <summary>
        /// Releases one runtime model previously created by this renderer.
        /// </summary>
        /// <param name="model">Runtime model that should release any renderer-owned resources.</param>
        public virtual void ReleaseModel(RuntimeModel model) {
            if (model == null) {
                throw new ArgumentNullException(nameof(model));
            }
        }

        /// <summary>
        /// Releases one runtime material previously created by this renderer.
        /// </summary>
        /// <param name="material">Runtime material that should release any renderer-owned resources.</param>
        public virtual void ReleaseMaterial(RuntimeMaterial material) {
            if (material == null) {
                throw new ArgumentNullException(nameof(material));
            }
        }

        /// <summary>
        /// Flushes any renderer-owned runtime asset releases that were deferred until the renderer reached a safe point.
        /// </summary>
        public virtual void FlushReleasedAssets() {
        }

#if HELENGINE_RUNTIME_MATERIAL_RESOLUTION_COOKED_PLATFORM_OWNED
        /// <summary>
        /// Builds a runtime material from one builder-owned cooked material payload.
        /// </summary>
        /// <param name="materialAsset">Builder-owned cooked material payload.</param>
        /// <returns>Runtime material instance.</returns>
        public virtual RuntimeMaterial BuildMaterialFromCooked(PlatformMaterialAsset materialAsset) {
            throw new NotSupportedException("This renderer does not support platform-owned cooked material creation.");
        }

        /// <summary>
        /// Builds a runtime material directly from one platform-owned cooked material payload path.
        /// </summary>
        /// <param name="cookedAssetPath">Absolute path to the cooked material payload.</param>
        /// <returns>Runtime material instance.</returns>
        public virtual RuntimeMaterial BuildMaterialFromCooked(string cookedAssetPath) {
            if (string.IsNullOrWhiteSpace(cookedAssetPath)) {
                throw new ArgumentException("Cooked material asset path must be provided.", nameof(cookedAssetPath));
            }

            throw new NotSupportedException("This renderer does not support opaque platform-owned cooked material creation.");
        }
#endif

        /// <summary>
        /// Gets the backend capability profile published by this renderer.
        /// </summary>
        /// <returns>Renderer capability profile used by shared extraction and planning systems.</returns>
        public virtual RendererBackendCapabilityProfile GetCapabilityProfile() {
            return new RendererBackendCapabilityProfile(true, false, false, false, 0, 0);
        }

        /// <summary>
        /// Gets the draw-call count recorded by the most recent completed draw.
        /// </summary>
        public virtual int LastDrawCallCount => 0;

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
