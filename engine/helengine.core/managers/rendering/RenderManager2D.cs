namespace helengine {
    public abstract class RenderManager2D : IDisposable {
        public abstract RuntimeTexture BuildTextureFromRaw(TextureAsset data);

        public virtual void Update() { }

        public virtual void Draw() { }

        public virtual void Dispose() { }

        public abstract void DrawSprite(ISpriteDrawable2D sprite);

        public abstract void DrawText(ITextDrawable2D text);

        public abstract void DrawRoundedRect(IRoundedRectDrawable2D shape);
    }
}

