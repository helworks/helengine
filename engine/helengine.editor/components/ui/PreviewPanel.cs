namespace helengine.editor {
    /// <summary>
    /// Dockable panel that hosts the active preview source and renders it inside the preview area.
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
        /// Currently active preview source.
        /// </summary>
        IPreviewSource ActivePreviewSourceValue;
        /// <summary>
        /// Tracks whether the panel finished initialization.
        /// </summary>
        bool isInitialized;

        /// <summary>
        /// Initializes a new preview panel with the provided font.
        /// </summary>
        /// <param name="font">Font used for the title bar.</param>
        public PreviewPanel(FontAsset font) : this(font, EditorUiMetrics.Default) {
        }

        /// <summary>
        /// Initializes a new preview panel with the provided font and shared metrics source.
        /// </summary>
        /// <param name="font">Font used for the title bar.</param>
        /// <param name="metrics">Scaled editor UI metrics used to size the dock chrome and padding.</param>
        public PreviewPanel(FontAsset font, EditorUiMetrics metrics) : base(font, metrics) {
            if (font == null) {
                throw new ArgumentNullException(nameof(font));
            }

            Title = "Preview";
            MinSize = new int2(metrics.ScalePixels(220), metrics.ScalePixels(160));

            spriteOrder = RenderOrder2D.PanelForeground;

            contentRoot = new EditorEntity();
            contentRoot.LayerMask = LayerMask;
            contentRoot.Position = new float3(0, TitleBarHeightPixels, 0.05f);
            AddChild(contentRoot);

            textureHost = new EditorEntity();
            textureHost.LayerMask = LayerMask;
            textureHost.Position = new float3(GetContentPaddingPixels(), GetContentPaddingPixels(), 0.2f);
            contentRoot.AddChild(textureHost);

            textureSprite = new SpriteComponent();
            textureSprite.RenderOrder2D = spriteOrder;
            textureSprite.Color = new byte4(255, 255, 255, 255);
            textureSprite.Size = new int2(1, 1);
            textureHost.AddComponent(textureSprite);

            ClearPreview();
            AddComponent(new PreviewPanelUpdater(this));
            isInitialized = true;
        }

        /// <summary>
        /// Gets the current preview source, when one is bound.
        /// </summary>
        public IPreviewSource ActivePreviewSource => ActivePreviewSourceValue;

        /// <summary>
        /// Reapplies scaled dock metrics after one live UI scale change.
        /// </summary>
        /// <param name="font">Updated dock title font.</param>
        /// <param name="metrics">Updated scaled editor UI metrics.</param>
        public override void ApplyUiMetrics(FontAsset font, EditorUiMetrics metrics) {
            base.ApplyUiMetrics(font, metrics);
        }

        /// <summary>
        /// Displays one texture asset through a texture preview source.
        /// </summary>
        /// <param name="asset">Texture asset to preview.</param>
        public void ShowTexture(TextureAsset asset) {
            if (asset == null) {
                throw new ArgumentNullException(nameof(asset));
            }

            RuntimeTexture runtimeTexture = Core.Instance.RenderManager2D.BuildTextureFromRaw(asset);
            SetPreviewSource(new TexturePreviewSource(runtimeTexture));
        }

        /// <summary>
        /// Binds one active preview source to the panel.
        /// </summary>
        /// <param name="previewSource">Preview source to bind, or null to clear the panel.</param>
        public void SetPreviewSource(IPreviewSource previewSource) {
            if (ReferenceEquals(ActivePreviewSourceValue, previewSource)) {
                return;
            }

            if (ActivePreviewSourceValue != null) {
                ActivePreviewSourceValue.Dispose();
            }

            ActivePreviewSourceValue = previewSource;
            if (ActivePreviewSourceValue == null) {
                ClearPreviewVisuals();
                return;
            }

            ActivePreviewSourceValue.Resize(GetContentSize());
            textureSprite.Texture = ActivePreviewSourceValue.Texture;
            LayoutPreview();
        }

        /// <summary>
        /// Clears the current preview.
        /// </summary>
        public void ClearPreview() {
            if (ActivePreviewSourceValue != null) {
                ActivePreviewSourceValue.Dispose();
            }

            ActivePreviewSourceValue = null;
            ClearPreviewVisuals();
        }

        /// <summary>
        /// Updates the active preview source for the current frame.
        /// </summary>
        internal void UpdatePreviewSource() {
            if (ActivePreviewSourceValue == null) {
                return;
            }

            ActivePreviewSourceValue.Update();
            textureSprite.Texture = ActivePreviewSourceValue.Texture;
            LayoutPreview();
        }

        /// <summary>
        /// Handles layout updates when the dockable size changes.
        /// </summary>
        protected override void OnSizeChanged() {
            base.OnSizeChanged();
            if (!isInitialized) {
                return;
            }

            if (ActivePreviewSourceValue != null) {
                ActivePreviewSourceValue.Resize(GetContentSize());
            }

            LayoutPreview();
        }

        /// <summary>
        /// Updates scaled preview content offsets after the shared dock chrome metrics change.
        /// </summary>
        protected override void HandleUiMetricsApplied() {
            MinSize = new int2(UiMetrics.ScalePixels(220), UiMetrics.ScalePixels(160));
            contentRoot.Position = new float3(0f, TitleBarHeightPixels, 0.05f);
            textureHost.Position = new float3(GetContentPaddingPixels(), GetContentPaddingPixels(), 0.2f);
        }

        /// <summary>
        /// Lays out the preview sprite within the panel.
        /// </summary>
        void LayoutPreview() {
            if (ActivePreviewSourceValue == null || ActivePreviewSourceValue.Texture == null) {
                textureHost.Enabled = false;
                return;
            }

            textureHost.Enabled = true;
            int2 contentSize = GetContentSize();
            int availableWidth = contentSize.X;
            int availableHeight = contentSize.Y;
            int sourceWidth = Math.Max(1, ActivePreviewSourceValue.Texture.Width);
            int sourceHeight = Math.Max(1, ActivePreviewSourceValue.Texture.Height);

            double widthScale = availableWidth / (double)sourceWidth;
            double heightScale = availableHeight / (double)sourceHeight;
            double scale = Math.Min(widthScale, heightScale);

            int targetWidth = Math.Max(1, (int)Math.Round(sourceWidth * scale));
            int targetHeight = Math.Max(1, (int)Math.Round(sourceHeight * scale));

            int offsetX = GetContentPaddingPixels() + (availableWidth - targetWidth) / 2;
            int offsetY = GetContentPaddingPixels() + (availableHeight - targetHeight) / 2;

            textureHost.Position = new float3(offsetX, offsetY, 0.2f);
            textureSprite.Size = new int2(targetWidth, targetHeight);
        }

        /// <summary>
        /// Clears the displayed texture and disables the preview host.
        /// </summary>
        void ClearPreviewVisuals() {
            textureSprite.Texture = null;
            textureSprite.Size = new int2(1, 1);
            textureHost.Enabled = false;
        }

        /// <summary>
        /// Gets the usable content size for the current panel dimensions.
        /// </summary>
        /// <returns>Usable preview content size in pixels.</returns>
        int2 GetContentSize() {
            return new int2(
                Math.Max(1, Size.X - GetContentPaddingPixels() * 2),
                Math.Max(1, Size.Y - TitleBarHeightPixels - GetContentPaddingPixels() * 2));
        }

        /// <summary>
        /// Gets the scaled content padding used around the preview texture.
        /// </summary>
        /// <returns>Scaled preview content padding in pixels.</returns>
        int GetContentPaddingPixels() {
            return UiMetrics.ScalePixels(ContentPadding);
        }
    }
}
