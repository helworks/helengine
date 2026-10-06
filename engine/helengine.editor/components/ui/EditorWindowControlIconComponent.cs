namespace helengine.editor {
    /// <summary>Owns a generated window-control texture and regenerates it when the glyph or display size changes.</summary>
    public sealed class EditorWindowControlIconComponent : Component {
        /// <summary>Renderer that owns the uploaded glyph texture.</summary>
        readonly RenderManager2D Renderer;
        /// <summary>Currently uploaded glyph mask, released on replacement or component disposal.</summary>
        RuntimeTexture OwnedTexture;

        /// <summary>Creates the sprite and uploads the first SVG mask at its final display size.</summary>
        /// <param name="renderer">Session renderer that owns the icon resources.</param>
        /// <param name="kind">Initial window-control glyph.</param>
        /// <param name="pixelSize">Final square canvas size in physical pixels.</param>
        public EditorWindowControlIconComponent(RenderManager2D renderer, EditorWindowControlIconKind kind, int pixelSize) {
            Renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
            Sprite = new SpriteComponent();
            ApplyGlyph(kind, pixelSize);
        }

        /// <summary>Gets the current glyph so hosts and layout tests can inspect maximize/restore state.</summary>
        public EditorWindowControlIconKind Kind { get; private set; }
        /// <summary>Gets the drawable sprite; its tint follows the title bar theme.</summary>
        public SpriteComponent Sprite { get; }

        /// <summary>Updates the glyph while reusing its texture whenever kind and pixel dimensions are unchanged.</summary>
        /// <param name="kind">Requested window-control glyph.</param>
        /// <param name="pixelSize">Final square canvas size in physical pixels.</param>
        public void ApplyGlyph(EditorWindowControlIconKind kind, int pixelSize) {
            ThrowIfDisposed();
            if (OwnedTexture != null && Kind == kind && OwnedTexture.Width == pixelSize && OwnedTexture.Height == pixelSize) {
                return;
            }
            TextureAsset asset = EditorWindowControlIconBuilder.CreateTextureAsset(kind, pixelSize);
            RuntimeTexture replacement;
            try {
                replacement = Renderer.BuildTextureFromRaw(asset);
            } finally {
                NativeOwnership.DisposeAndDelete(asset);
            }
            replacement.IsEngineOwned = true;
            RuntimeTexture previous = OwnedTexture;
            OwnedTexture = replacement;
            Kind = kind;
            Sprite.Texture = replacement;
            Sprite.Size = new int2(pixelSize, pixelSize);
            if (previous != null) {
                Renderer.ReleaseTexture(previous);
            }
        }

        /// <summary>Releases the icon texture exactly once during title-bar hierarchy teardown.</summary>
        public override void Dispose() {
            if (OwnedTexture != null) {
                Sprite.Texture = null;
                Renderer.ReleaseTexture(OwnedTexture);
                OwnedTexture = null;
            }
            base.Dispose();
        }
    }
}
