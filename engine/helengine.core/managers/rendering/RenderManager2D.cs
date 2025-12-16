namespace helengine {
    /// <summary>
    /// Abstract base for 2D rendering backends.
    /// </summary>
    public abstract class RenderManager2D : IDisposable {
        /// <summary>
        /// Builds a runtime texture from raw texture data.
        /// </summary>
        /// <param name="data">Raw texture data.</param>
        /// <returns>Runtime texture instance.</returns>
        public abstract RuntimeTexture BuildTextureFromRaw(TextureAsset data);

        /// <summary>
        /// Performs per-frame update for 2D rendering systems.
        /// </summary>
        public virtual void Update() { }

        /// <summary>
        /// Executes the 2D render pass.
        /// </summary>
        public virtual void Draw() { }

        /// <summary>
        /// Releases resources owned by the render manager.
        /// </summary>
        public virtual void Dispose() { }

        /// <summary>
        /// Draws a sprite component.
        /// </summary>
        /// <param name="sprite">Sprite to draw.</param>
        public abstract void DrawSprite(ISpriteDrawable2D sprite);

        /// <summary>
        /// Draws text for a text drawable.
        /// </summary>
        /// <param name="text">Text drawable.</param>
        public abstract void DrawText(ITextDrawable2D text);

        /// <summary>
        /// Draws a rounded rectangle.
        /// </summary>
        /// <param name="shape">Rounded rectangle drawable.</param>
        public abstract void DrawRoundedRect(IRoundedRectDrawable2D shape);
    }
}
