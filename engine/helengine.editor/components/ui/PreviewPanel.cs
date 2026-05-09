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
        /// Vertical spacing reserved between the preview image and the resolution caption.
        /// </summary>
        const int ResolutionLabelGap = 6;
        /// <summary>
        /// Minimum zoom multiplier allowed for texture previews.
        /// </summary>
        const double MinimumZoomScale = 0.25d;
        /// <summary>
        /// Maximum zoom multiplier allowed for texture previews.
        /// </summary>
        const double MaximumZoomScale = 16.0d;
        /// <summary>
        /// Multiplier applied for each scroll-wheel notch while zooming a texture preview.
        /// </summary>
        const double ZoomStepFactor = 1.1d;

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
        /// Entity that positions the resolution caption beneath texture previews.
        /// </summary>
        readonly EditorEntity resolutionLabelHost;
        /// <summary>
        /// Text component that renders the texture resolution caption.
        /// </summary>
        readonly TextComponent resolutionLabelText;
        /// <summary>
        /// Currently active preview source.
        /// </summary>
        IPreviewSource ActivePreviewSourceValue;
        /// <summary>
        /// Tracks whether the panel finished initialization.
        /// </summary>
        bool isInitialized;
        /// <summary>
        /// Current zoom factor applied to texture previews, relative to the fitted size.
        /// </summary>
        double TextureZoomScale;
        /// <summary>
        /// Additional translation applied to texture previews after cursor-centered zooming.
        /// </summary>
        float2 TexturePanOffset;
        /// <summary>
        /// Tracks whether a middle-mouse drag is currently active on the texture preview.
        /// </summary>
        bool IsMiddleMouseDragging;
        /// <summary>
        /// Tracks whether a left-mouse drag is currently active on an interactive non-texture preview.
        /// </summary>
        bool IsLeftMouseDragging;

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

            resolutionLabelHost = new EditorEntity();
            resolutionLabelHost.LayerMask = LayerMask;
            resolutionLabelHost.Enabled = false;
            contentRoot.AddChild(resolutionLabelHost);

            resolutionLabelText = new TextComponent();
            resolutionLabelText.Font = TitleFont;
            resolutionLabelText.Text = string.Empty;
            resolutionLabelText.Color = ThemeManager.Colors.InputForegroundPrimary;
            resolutionLabelText.RenderOrder2D = spriteOrder;
            resolutionLabelHost.AddComponent(resolutionLabelText);

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

            ResetTexturePreviewLayout();
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
            ResetTexturePreviewLayout();
            ClearPreviewVisuals();
        }

        /// <summary>
        /// Updates the active preview source for the current frame.
        /// </summary>
        internal void UpdatePreviewSource() {
            if (ActivePreviewSourceValue == null) {
                return;
            }

            if (IsTexturePreviewSource()) {
                HandlePreviewWheelInput();
                HandlePreviewPanInput();
            } else if (ActivePreviewSourceValue is IPreviewInteractionSource interactionSource) {
                HandlePreviewInteractionInput(interactionSource);
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
            resolutionLabelText.Font = TitleFont;
        }

        /// <summary>
        /// Lays out the preview sprite within the panel.
        /// </summary>
        void LayoutPreview() {
            if (ActivePreviewSourceValue == null || ActivePreviewSourceValue.Texture == null) {
                textureHost.Enabled = false;
                resolutionLabelHost.Enabled = false;
                return;
            }

            RuntimeTexture texture = ActivePreviewSourceValue.Texture;
            if (IsTexturePreviewSource()) {
                LayoutTexturePreview(texture);
                return;
            }

            LayoutGenericPreview(texture);
        }

        /// <summary>
        /// Lays out a texture preview using the current zoom factor and caption state.
        /// </summary>
        /// <param name="texture">Texture currently exposed by the active preview source.</param>
        void LayoutTexturePreview(RuntimeTexture texture) {
            textureHost.Enabled = true;
            resolutionLabelHost.Enabled = true;

            int2 contentSize = GetContentSize();
            string labelText = BuildResolutionLabelText(texture);
            int2 labelSize = GetResolutionLabelSize(labelText);
            resolutionLabelText.Text = labelText;
            resolutionLabelText.Size = labelSize;

            int2 textureViewportSize = GetTextureViewportSize(contentSize, labelSize);
            int2 targetSize = GetTextureDisplaySize(texture, textureViewportSize, TextureZoomScale);
            float2 centeredPosition = GetCenteredTexturePosition(textureViewportSize, targetSize);

            textureHost.Position = new float3(
                centeredPosition.X + TexturePanOffset.X,
                centeredPosition.Y + TexturePanOffset.Y,
                0.2f);
            textureSprite.Size = targetSize;
            resolutionLabelHost.Position = new float3(
                GetContentPaddingPixels() + Math.Max(0, (textureViewportSize.X - labelSize.X) / 2),
                GetContentPaddingPixels() + textureViewportSize.Y + ResolutionLabelGap,
                0.2f);
        }

        /// <summary>
        /// Lays out a non-texture preview without the resolution caption or zoom offset.
        /// </summary>
        /// <param name="texture">Texture currently exposed by the active preview source.</param>
        void LayoutGenericPreview(RuntimeTexture texture) {
            textureHost.Enabled = true;
            resolutionLabelHost.Enabled = false;
            resolutionLabelText.Text = string.Empty;
            resolutionLabelText.Size = new int2(1, 1);

            int2 contentSize = GetContentSize();
            int2 targetSize = GetTextureDisplaySize(texture, contentSize, 1d);
            float2 centeredPosition = GetCenteredTexturePosition(contentSize, targetSize);

            textureHost.Position = new float3(centeredPosition.X, centeredPosition.Y, 0.2f);
            textureSprite.Size = targetSize;
        }

        /// <summary>
        /// Clears the displayed texture and disables the preview host.
        /// </summary>
        void ClearPreviewVisuals() {
            textureSprite.Texture = null;
            textureSprite.Size = new int2(1, 1);
            textureHost.Enabled = false;
            resolutionLabelHost.Enabled = false;
            resolutionLabelText.Text = string.Empty;
            resolutionLabelText.Size = new int2(1, 1);
        }

        /// <summary>
        /// Resets the interaction state used by the currently active preview source.
        /// </summary>
        void ResetTexturePreviewLayout() {
            TextureZoomScale = 1d;
            TexturePanOffset = new float2(0f, 0f);
            IsMiddleMouseDragging = false;
            IsLeftMouseDragging = false;
        }

        /// <summary>
        /// Handles wheel zoom input for the active texture preview.
        /// </summary>
        void HandlePreviewWheelInput() {
            if (!IsTexturePreviewSource() || ActivePreviewSourceValue.Texture == null) {
                return;
            }

            InputSystem input = Core.Instance.Input;
            int wheelDelta = input.GetMouseScrollWheelDelta();
            if (wheelDelta == 0) {
                return;
            }

            int2 pointer = input.GetMousePosition();
            if (EditorInputCaptureService.IsPointerBlocked(pointer, owner => !ReferenceEquals(owner, this))) {
                return;
            }

            if (!IsPointerInsideContent(pointer)) {
                return;
            }

            int2 contentSize = GetContentSize();
            string labelText = BuildResolutionLabelText(ActivePreviewSourceValue.Texture);
            int2 labelSize = GetResolutionLabelSize(labelText);
            int2 textureViewportSize = GetTextureViewportSize(contentSize, labelSize);
            int2 currentSize = textureSprite.Size;
            if (currentSize.X <= 0 || currentSize.Y <= 0) {
                return;
            }

            double zoomNotches = wheelDelta / 120.0d;
            double nextZoomScale = TextureZoomScale * Math.Pow(ZoomStepFactor, zoomNotches);
            nextZoomScale = Math.Max(MinimumZoomScale, Math.Min(MaximumZoomScale, nextZoomScale));
            if (Math.Abs(nextZoomScale - TextureZoomScale) < 0.000001d) {
                return;
            }

            float3 contentOrigin = contentRoot.Position;
            float pointerLocalX = pointer.X - contentOrigin.X;
            float pointerLocalY = pointer.Y - contentOrigin.Y;
            float currentLeft = textureHost.LocalPosition.X;
            float currentTop = textureHost.LocalPosition.Y;
            double anchorX = (pointerLocalX - currentLeft) / (double)currentSize.X;
            double anchorY = (pointerLocalY - currentTop) / (double)currentSize.Y;

            TextureZoomScale = nextZoomScale;

            int2 nextSize = GetTextureDisplaySize(ActivePreviewSourceValue.Texture, textureViewportSize, TextureZoomScale);
            float2 centeredPosition = GetCenteredTexturePosition(textureViewportSize, nextSize);
            float desiredLeft = pointerLocalX - (float)(anchorX * nextSize.X);
            float desiredTop = pointerLocalY - (float)(anchorY * nextSize.Y);
            TexturePanOffset = new float2(desiredLeft - centeredPosition.X, desiredTop - centeredPosition.Y);
        }

        /// <summary>
        /// Handles middle-mouse drag input for texture previews.
        /// </summary>
        void HandlePreviewPanInput() {
            if (!IsTexturePreviewSource() || ActivePreviewSourceValue.Texture == null) {
                IsMiddleMouseDragging = false;
                return;
            }

            InputSystem input = Core.Instance.Input;
            int2 pointer = input.GetMousePosition();
            if (EditorInputCaptureService.IsPointerBlocked(pointer, owner => !ReferenceEquals(owner, this))) {
                IsMiddleMouseDragging = false;
                return;
            }

            if (input.WasMouseMiddleButtonPressed()) {
                IsMiddleMouseDragging = IsPointerInsideContent(pointer);
            }

            if (!IsMiddleMouseDragging) {
                if (input.GetMouseMiddleButtonState() == ButtonState.Released) {
                    IsMiddleMouseDragging = false;
                }

                return;
            }

            if (input.GetMouseMiddleButtonState() == ButtonState.Released) {
                IsMiddleMouseDragging = false;
                return;
            }

            int2 delta = input.GetMouseDelta();
            if (delta.X == 0 && delta.Y == 0) {
                return;
            }

            TexturePanOffset = new float2(
                TexturePanOffset.X + delta.X,
                TexturePanOffset.Y + delta.Y);
        }

        /// <summary>
        /// Handles wheel and left-drag input for interactive preview sources.
        /// </summary>
        /// <param name="interactionSource">Active preview source that accepts pointer interaction.</param>
        void HandlePreviewInteractionInput(IPreviewInteractionSource interactionSource) {
            if (interactionSource == null) {
                throw new ArgumentNullException(nameof(interactionSource));
            }

            InputSystem input = Core.Instance.Input;
            int2 pointer = input.GetMousePosition();
            if (EditorInputCaptureService.IsPointerBlocked(pointer, owner => !ReferenceEquals(owner, this))) {
                IsLeftMouseDragging = false;
                return;
            }

            if (!IsPointerInsideContent(pointer)) {
                IsLeftMouseDragging = false;
                return;
            }

            int wheelDelta = input.GetMouseScrollWheelDelta();
            if (wheelDelta != 0) {
                interactionSource.HandleMouseWheel(wheelDelta);
            }

            if (input.WasMouseLeftButtonPressed() || input.WasMouseMiddleButtonPressed()) {
                IsLeftMouseDragging = true;
            }

            if (!IsLeftMouseDragging) {
                if (input.GetMouseLeftButtonState() == ButtonState.Released &&
                    input.GetMouseMiddleButtonState() == ButtonState.Released) {
                    IsLeftMouseDragging = false;
                }

                return;
            }

            if (input.GetMouseLeftButtonState() == ButtonState.Released &&
                input.GetMouseMiddleButtonState() == ButtonState.Released) {
                IsLeftMouseDragging = false;
                return;
            }

            int2 delta = input.GetMouseDelta();
            if (delta.X == 0 && delta.Y == 0) {
                return;
            }

            interactionSource.HandleMouseDrag(delta);
        }

        /// <summary>
        /// Returns true when the active preview source exposes a texture preview.
        /// </summary>
        /// <returns>True when the panel is currently showing a texture preview.</returns>
        bool IsTexturePreviewSource() {
            return ActivePreviewSourceValue is TexturePreviewSource;
        }

        /// <summary>
        /// Builds the caption text used beneath a texture preview.
        /// </summary>
        /// <param name="texture">Texture to describe.</param>
        /// <returns>Human-readable resolution string.</returns>
        string BuildResolutionLabelText(RuntimeTexture texture) {
            if (texture == null) {
                throw new ArgumentNullException(nameof(texture));
            }

            return texture.Width + " x " + texture.Height;
        }

        /// <summary>
        /// Measures the caption text used beneath a texture preview.
        /// </summary>
        /// <param name="labelText">Caption text to measure.</param>
        /// <returns>Measured label size in pixels.</returns>
        int2 GetResolutionLabelSize(string labelText) {
            if (TitleFont == null) {
                return new int2(1, 1);
            }

            float2 measured = TitleFont.MeasureString(labelText);
            int width = Math.Max(1, (int)Math.Ceiling(measured.X));
            int height = Math.Max(1, (int)Math.Ceiling(TitleFont.LineHeight));
            return new int2(width, height);
        }

        /// <summary>
        /// Computes the usable preview viewport after subtracting the caption space when needed.
        /// </summary>
        /// <param name="contentSize">Panel content size available before caption layout.</param>
        /// <param name="labelSize">Measured caption size.</param>
        /// <returns>Usable image viewport size in pixels.</returns>
        int2 GetTextureViewportSize(int2 contentSize, int2 labelSize) {
            int viewportHeight = contentSize.Y;
            if (labelSize.Y > 0) {
                viewportHeight = Math.Max(1, viewportHeight - labelSize.Y - ResolutionLabelGap);
            }

            return new int2(Math.Max(1, contentSize.X), Math.Max(1, viewportHeight));
        }

        /// <summary>
        /// Computes the displayed size of one texture for the supplied viewport and zoom scale.
        /// </summary>
        /// <param name="texture">Texture being laid out.</param>
        /// <param name="viewportSize">Available viewport size in pixels.</param>
        /// <param name="zoomScale">Additional zoom multiplier to apply.</param>
        /// <returns>Scaled texture size in pixels.</returns>
        int2 GetTextureDisplaySize(RuntimeTexture texture, int2 viewportSize, double zoomScale) {
            if (texture == null) {
                throw new ArgumentNullException(nameof(texture));
            }

            int sourceWidth = Math.Max(1, texture.Width);
            int sourceHeight = Math.Max(1, texture.Height);
            double widthScale = viewportSize.X / (double)sourceWidth;
            double heightScale = viewportSize.Y / (double)sourceHeight;
            double scale = Math.Min(widthScale, heightScale) * zoomScale;

            int targetWidth = Math.Max(1, (int)Math.Round(sourceWidth * scale));
            int targetHeight = Math.Max(1, (int)Math.Round(sourceHeight * scale));
            return new int2(targetWidth, targetHeight);
        }

        /// <summary>
        /// Computes the top-left position used to center one texture inside a viewport.
        /// </summary>
        /// <param name="viewportSize">Available viewport size in pixels.</param>
        /// <param name="displaySize">Current texture size in pixels.</param>
        /// <returns>Centered top-left position relative to the preview content root.</returns>
        float2 GetCenteredTexturePosition(int2 viewportSize, int2 displaySize) {
            float left = GetContentPaddingPixels() + (viewportSize.X - displaySize.X) * 0.5f;
            float top = GetContentPaddingPixels() + (viewportSize.Y - displaySize.Y) * 0.5f;
            return new float2(left, top);
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

        /// <summary>
        /// Returns true when the pointer lies inside the preview content area below the title bar.
        /// </summary>
        /// <param name="pointer">Pointer position in screen coordinates.</param>
        /// <returns>True when the pointer is inside the preview body.</returns>
        bool IsPointerInsideContent(int2 pointer) {
            int panelLeft = (int)Math.Round(Position.X);
            int panelTop = (int)Math.Round(Position.Y) + TitleBarHeightPixels;
            int panelWidth = Size.X;
            int panelHeight = Size.Y;

            return pointer.X >= panelLeft &&
                   pointer.X < panelLeft + panelWidth &&
                   pointer.Y >= panelTop &&
                   pointer.Y < panelTop + panelHeight;
        }
    }
}
