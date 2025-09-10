namespace helengine {
    public abstract class RenderManager3D : IDisposable {
        private bool setOneWindow;

        public int2 MainWindowSize { get; private set; }

        // Event for window resize notifications
        public event Action<IntPtr, int, int>? WindowResized;

        public virtual void AddWindow(IntPtr handle, int width, int height) {
            if (!setOneWindow) {
                MainWindowSize = new int2(width, height);
            }

            setOneWindow = true;
        }

        public abstract RuntimeModel BuildModelFromRaw(ModelAsset data);

        public virtual void Update() { }

        public virtual void Draw() { }

        public virtual void Dispose() { }

        /// <summary>
        /// Triggers window resize handling - should be called by forms when resizing
        /// </summary>
        public virtual void OnWindowResize(IntPtr handle, int newWidth, int newHeight) {
            if (!setOneWindow || (MainWindowSize.X == 0 && MainWindowSize.Y == 0)) {
                MainWindowSize = new int2(newWidth, newHeight);
            }

            WindowResized?.Invoke(handle, newWidth, newHeight);
        }
    }
}

