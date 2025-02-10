namespace helengine {
    public abstract class RenderManager : IDisposable {

        public RenderManager() {
        }

        public virtual void AddWindow(IntPtr handle, int width, int height) {
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
