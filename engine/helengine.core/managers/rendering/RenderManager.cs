namespace helengine {
    public abstract class RenderManager : IDisposable {
        private bool setOneWindow;

        public int2 MainWindowSize { get; private set; }

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
    }
}
