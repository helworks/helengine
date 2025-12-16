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
        public event Action<IntPtr, int, int>? WindowResized;

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
