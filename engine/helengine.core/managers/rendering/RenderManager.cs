namespace helengine {
    public abstract class RenderManager : IDisposable {
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

        public abstract RuntimeTexture BuildTextureFromRaw(TextureAsset data);

        public virtual void Update() {
        }

        public virtual void Draw() {
        }

        public virtual void Dispose() {
        }

        public virtual void DrawSprite(ISpriteDrawable2D sprite) {
        }

        public virtual void DrawText(ITextDrawable2D text) {
        }

        /// <summary>
        /// Triggers window resize handling - should be called by forms when resizing
        /// </summary>
        public virtual void OnWindowResize(IntPtr handle, int newWidth, int newHeight) {
            // Update main window size if this is the main window
            if (!setOneWindow || (MainWindowSize.X == 0 && MainWindowSize.Y == 0)) {
                MainWindowSize = new int2(newWidth, newHeight);
            }

            // Trigger the resize event for implementation-specific handling
            WindowResized?.Invoke(handle, newWidth, newHeight);
        }
    }
}
