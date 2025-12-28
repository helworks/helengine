namespace helengine.editor {
    /// <summary>
    /// Dockable panel that previews texture assets.
    /// </summary>
    public class PreviewPanel : DockableEntity {
        /// <summary>
        /// Padding applied around the preview image.
        /// </summary>
        const int ContentPadding = 8;

        /// <summary>
        /// Render order used for the preview sprite.
        /// </summary>
        readonly byte spriteOrder;
        /// <summary>
        /// Root entity hosting preview content.
        /// </summary>
        readonly EditorEntity contentRoot;
        /// <summary>
        /// Entity that positions the preview sprite.
        /// </summary>
        readonly EditorEntity textureHost;
        /// <summary>
        /// Sprite used to draw the preview texture.
        /// </summary>
        readonly SpriteComponent textureSprite;
        /// <summary>
        /// Tracks the current texture size.
        /// </summary>
        int2 textureSize;
        /// <summary>
        /// Tracks whether a texture is currently displayed.
        /// </summary>
        bool hasTexture;
        /// <summary>
        /// Tracks whether the panel finished initialization.
        /// </summary>
        bool isInitialized;

        /// <summary>
        /// Initializes a new preview panel with the provided font.
        /// </summary>
        /// <param name="font">Font used for the title bar.</param>
        public PreviewPanel(FontAsset font) : base(font) {
            if (font == null) {
                throw new ArgumentNullException(nameof(font));
            }

            Title = "Preview";
            MinSize = new int2(220, 160);

            spriteOrder = Core.Instance.ObjectManager.GetRenderOrderForLayer2D(2);

            contentRoot = new EditorEntity();
            contentRoot.LayerMask = LayerMask;
            contentRoot.Position = new float3(0, TitleBarHeight, 0.05f);
            AddChild(contentRoot);

            textureHost = new EditorEntity();
            textureHost.LayerMask = LayerMask;
            textureHost.Position = new float3(ContentPadding, ContentPadding, 0.2f);
            contentRoot.AddChild(textureHost);

            textureSprite = new SpriteComponent();
            textureSprite.RenderOrder2D = spriteOrder;
            textureSprite.Color = new byte4(255, 255, 255, 255);
            textureSprite.Size = new int2(1, 1);
            textureHost.AddComponent(textureSprite);

            ClearPreview();
            isInitialized = true;
        }

        /// <summary>
        /// Displays a texture asset at its native size.
        /// </summary>
        /// <param name="asset">Texture asset to preview.</param>
        public void ShowTexture(TextureAsset asset) {
            if (asset == null) {
                throw new ArgumentNullException(nameof(asset));
            }

            RuntimeTexture runtimeTexture = Core.Instance.RenderManager2D.BuildTextureFromRaw(asset);
            textureSprite.Texture = runtimeTexture;
            textureSize = new int2(asset.Width, asset.Height);
            hasTexture = true;
            LayoutPreview();
        }

        /// <summary>
        /// Clears the current preview.
        /// </summary>
        public void ClearPreview() {
            hasTexture = false;
            textureSprite.Texture = null;
            textureSprite.Size = new int2(1, 1);
            textureHost.Enabled = false;
        }

        /// <summary>
        /// Handles layout updates when the dockable size changes.
        /// </summary>
        protected override void OnSizeChanged() {
            base.OnSizeChanged();
            if (!isInitialized) {
                return;
            }

            LayoutPreview();
        }

        /// <summary>
        /// Lays out the preview sprite within the panel.
        /// </summary>
        void LayoutPreview() {
            if (!hasTexture) {
                textureHost.Enabled = false;
                return;
            }

            textureHost.Enabled = true;
            int availableWidth = Math.Max(1, Size.X - ContentPadding * 2);
            int availableHeight = Math.Max(1, Size.Y - TitleBarHeight - ContentPadding * 2);
            int sourceWidth = Math.Max(1, textureSize.X);
            int sourceHeight = Math.Max(1, textureSize.Y);

            double widthScale = availableWidth / (double)sourceWidth;
            double heightScale = availableHeight / (double)sourceHeight;
            double scale = Math.Min(widthScale, heightScale);

            int targetWidth = Math.Max(1, (int)Math.Round(sourceWidth * scale));
            int targetHeight = Math.Max(1, (int)Math.Round(sourceHeight * scale));

            int offsetX = ContentPadding + (availableWidth - targetWidth) / 2;
            int offsetY = ContentPadding + (availableHeight - targetHeight) / 2;

            textureHost.Position = new float3(offsetX, offsetY, 0.2f);
            textureSprite.Size = new int2(targetWidth, targetHeight);
        }
    }
}
