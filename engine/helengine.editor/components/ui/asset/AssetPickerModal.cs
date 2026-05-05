namespace helengine.editor {
    /// <summary>
    /// Floating picker window that allows users to select an asset from the project.
    /// </summary>
    public class AssetPickerModal : EditorEntity {
        /// <summary>
        /// Default minimum width for the picker panel.
        /// </summary>
        public const int MinPanelWidth = 360;
        /// <summary>
        /// Default minimum height for the picker panel.
        /// </summary>
        public const int MinPanelHeight = 260;
        /// <summary>
        /// Default maximum width for the picker panel.
        /// </summary>
        public const int MaxPanelWidth = 920;
        /// <summary>
        /// Default maximum height for the picker panel.
        /// </summary>
        public const int MaxPanelHeight = 720;
        /// <summary>
        /// Padding applied inside the panel.
        /// </summary>
        public const int PanelPadding = 16;
        /// <summary>
        /// Spacing between header and content sections.
        /// </summary>
        public const int SectionSpacing = 10;
        /// <summary>
        /// Height of the header bar used for dragging the picker.
        /// </summary>
        public const int HeaderHeight = 32;
        /// <summary>
        /// Height of each row in the asset list.
        /// </summary>
        public const int RowHeight = AssetBrowserView.RowHeight;
        /// <summary>
        /// Height of the toolbar area above the list.
        /// </summary>
        public const int ToolbarHeight = AssetBrowserView.ToolbarHeight;
        /// <summary>
        /// Fixed size for the close button.
        /// </summary>
        static readonly int2 CloseButtonSize = new int2(72, 22);
        /// <summary>
        /// Default radius for the modal panel background.
        /// </summary>
        const float PanelRadius = 6f;
        /// <summary>
        /// Default border thickness for the modal panel background.
        /// </summary>
        const float PanelBorderThickness = 2f;
        /// <summary>
        /// Render order used by the fullscreen modal backdrop behind the panel.
        /// </summary>
        const byte BackdropOrder = RenderOrder2D.ModalBackground - 1;
        /// <summary>
        /// Padding inside the header for text and buttons.
        /// </summary>
        const int HeaderPadding = 8;
        /// <summary>
        /// Horizontal spacing between the header text and the close button.
        /// </summary>
        const int HeaderButtonSpacing = 8;
        /// <summary>
        /// Width reserved on the right side of the host title bar for the minimize, maximize, and close button cluster.
        /// </summary>
        const int HostTitleBarButtonGapWidth = EditorDialogBase.CloseButtonWidth * 3;

        /// <summary>
        /// Font used for header and list labels.
        /// </summary>
        FontAsset Font;
        /// <summary>
        /// Shared scaled metrics used to size the picker shell and spacing.
        /// </summary>
        EditorUiMetrics Metrics;
        /// <summary>
        /// Root entity hosting the fullscreen modal backdrop.
        /// </summary>
        readonly EditorEntity BackdropRoot;
        /// <summary>
        /// Root entity hosting the title-bar backdrop strip.
        /// </summary>
        readonly EditorEntity BackdropTopRoot;
        /// <summary>
        /// Dimming surface rendered across the title-bar area while leaving the window buttons free.
        /// </summary>
        readonly SpriteComponent BackdropTopSurface;
        /// <summary>
        /// Interactable that absorbs pointer input over the title-bar backdrop strip.
        /// </summary>
        readonly InteractableComponent BackdropTopInteractable;
        /// <summary>
        /// Root entity hosting the editor-content backdrop block.
        /// </summary>
        readonly EditorEntity BackdropBodyRoot;
        /// <summary>
        /// Fullscreen dimming surface rendered behind the picker panel.
        /// </summary>
        readonly SpriteComponent BackdropBodySurface;
        /// <summary>
        /// Fullscreen interactable that absorbs pointer input outside the panel.
        /// </summary>
        readonly InteractableComponent BackdropBodyInteractable;
        /// <summary>
        /// Root entity for the picker panel.
        /// </summary>
        readonly EditorEntity PanelRoot;
        /// <summary>
        /// Panel background shape.
        /// </summary>
        readonly RoundedRectComponent PanelBackground;
        /// <summary>
        /// Root entity for the draggable header area.
        /// </summary>
        readonly EditorEntity HeaderRoot;
        /// <summary>
        /// Background sprite for the header area.
        /// </summary>
        readonly SpriteComponent HeaderBackground;
        /// <summary>
        /// Interactable region used to drag the picker window.
        /// </summary>
        readonly InteractableComponent HeaderInteractable;
        /// <summary>
        /// Header text host entity.
        /// </summary>
        readonly EditorEntity HeaderHost;
        /// <summary>
        /// Header text label.
        /// </summary>
        readonly TextComponent HeaderText;
        /// <summary>
        /// Host entity for the close button.
        /// </summary>
        readonly EditorEntity CloseButtonHost;
        /// <summary>
        /// Close button component.
        /// </summary>
        readonly ButtonComponent CloseButton;
        /// <summary>
        /// Shared asset browser view hosting the toolbar and list.
        /// </summary>
        readonly AssetBrowserView BrowserView;
        /// <summary>
        /// Render order used for panel backgrounds.
        /// </summary>
        readonly byte PanelOrder;
        /// <summary>
        /// Render order used for text labels.
        /// </summary>
        readonly byte TextOrder;
        /// <summary>
        /// Cached panel size for layout.
        /// </summary>
        int2 PanelSize;
        /// <summary>
        /// Cached panel position relative to the host.
        /// </summary>
        int2 PanelPosition;
        /// <summary>
        /// Cached host size for clamping.
        /// </summary>
        int2 HostSize;
        /// <summary>
        /// True when the user has positioned the picker manually.
        /// </summary>
        bool IsUserPositioned;
        /// <summary>
        /// True when the header is actively being dragged.
        /// </summary>
        bool IsDragging;
        /// <summary>
        /// Callback invoked when the user picks an asset.
        /// </summary>
        Action<AssetBrowserEntry> PickedCallback;
        /// <summary>
        /// Tracks whether the modal has completed initialization.
        /// </summary>
        bool IsInitialized;

        /// <summary>
        /// Initializes a new asset picker modal for the provided project path.
        /// </summary>
        /// <param name="font">Font used for labels.</param>
        /// <param name="projectPath">Path to the project root.</param>
        public AssetPickerModal(FontAsset font, string projectPath)
            : this(font, EditorUiMetrics.Default, projectPath) {
        }

        /// <summary>
        /// Initializes a new asset picker modal for the provided project path using one shared metrics source.
        /// </summary>
        /// <param name="font">Font used for labels.</param>
        /// <param name="metrics">Scaled editor UI metrics used to size the picker.</param>
        /// <param name="projectPath">Path to the project root.</param>
        public AssetPickerModal(FontAsset font, EditorUiMetrics metrics, string projectPath) {
            if (font == null) {
                throw new ArgumentNullException(nameof(font));
            }
            if (metrics == null) {
                throw new ArgumentNullException(nameof(metrics));
            }
            if (string.IsNullOrWhiteSpace(projectPath)) {
                throw new ArgumentException("Project path must be provided.", nameof(projectPath));
            }

            Font = font;
            Metrics = metrics;
            PanelSize = new int2(GetMinimumPanelWidthPixels(), GetMinimumPanelHeightPixels());

            LayerMask = 0b1000000000000000;
            InternalEntity = true;
            Name = "AssetPickerModal";

            PanelOrder = RenderOrder2D.ModalBackground;
            byte toolbarOrder = RenderOrder2D.ModalBackground;
            byte rowBackgroundOrder = RenderOrder2D.ModalBackground;
            byte iconBackgroundOrder = RenderOrder2D.ModalBackground;
            TextOrder = RenderOrder2D.ModalForeground;

            BackdropRoot = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            AddChild(BackdropRoot);

            BackdropTopRoot = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            BackdropRoot.AddChild(BackdropTopRoot);

            BackdropTopSurface = new SpriteComponent {
                Texture = TextureUtils.PixelTexture,
                Color = new byte4(0, 0, 0, 144),
                RenderOrder2D = BackdropOrder,
                Size = new int2(0, 0)
            };
            BackdropTopRoot.AddComponent(BackdropTopSurface);

            BackdropTopInteractable = new InteractableComponent {
                Size = new int2(0, 0)
            };
            BackdropTopRoot.AddComponent(BackdropTopInteractable);

            BackdropBodyRoot = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            BackdropRoot.AddChild(BackdropBodyRoot);

            BackdropBodySurface = new SpriteComponent {
                Texture = TextureUtils.PixelTexture,
                Color = new byte4(0, 0, 0, 144),
                RenderOrder2D = BackdropOrder,
                Size = new int2(0, 0)
            };
            BackdropBodyRoot.AddComponent(BackdropBodySurface);

            BackdropBodyInteractable = new InteractableComponent {
                Size = new int2(0, 0)
            };
            BackdropBodyRoot.AddComponent(BackdropBodyInteractable);

            PanelRoot = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero
            };
            AddChild(PanelRoot);

            PanelBackground = new RoundedRectComponent {
                FillColor = ThemeManager.Colors.SurfacePrimary,
                BorderColor = ThemeManager.Colors.AccentTertiary,
                BorderThickness = PanelBorderThickness,
                Radius = PanelRadius,
                RenderOrder2D = PanelOrder,
                Size = new int2(0, 0)
            };
            PanelRoot.AddComponent(PanelBackground);

            HeaderRoot = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero
            };
            PanelRoot.AddChild(HeaderRoot);

            HeaderBackground = new SpriteComponent {
                Texture = TextureUtils.PixelTexture,
                Color = ThemeManager.Colors.SurfacePrimary,
                RenderOrder2D = PanelOrder,
                Size = new int2(0, 0)
            };
            HeaderRoot.AddComponent(HeaderBackground);

            HeaderInteractable = new InteractableComponent {
                Size = new int2(0, 0)
            };
            HeaderInteractable.CursorEvent += HandleHeaderCursor;
            HeaderRoot.AddComponent(HeaderInteractable);

            HeaderHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero
            };
            HeaderRoot.AddChild(HeaderHost);

            double lineHeight = Math.Max((double)font.LineHeight, 1.0);
            HeaderText = new TextComponent {
                Font = font,
                Text = "Select Asset",
                Color = ThemeManager.Colors.InputForegroundPrimary,
                Size = new int2(1, (int)Math.Ceiling(lineHeight)),
                RenderOrder2D = TextOrder
            };
            HeaderHost.AddComponent(HeaderText);

            CloseButtonHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero
            };
            HeaderRoot.AddChild(CloseButtonHost);

            CloseButton = new ButtonComponent("X", GetCloseButtonSize(), font, Hide, 0f);
            CloseButtonHost.AddComponent(CloseButton);
            CloseButton.SetRenderOrders(TextOrder, TextOrder);

            BrowserView = new AssetBrowserView(
                Font,
                projectPath,
                LayerMask,
                toolbarOrder,
                rowBackgroundOrder,
                iconBackgroundOrder,
                TextOrder);
            BrowserView.SetToolbarButtonRenderOrders(TextOrder, TextOrder);
            BrowserView.AssetActivated += HandleAssetActivated;
            PanelRoot.AddChild(BrowserView.Entity);

            Enabled = false;
            IsInitialized = true;
        }

        /// <summary>
        /// Gets a value indicating whether the modal is currently visible.
        /// </summary>
        public bool IsVisible => Enabled;

        /// <summary>
        /// Shows the modal and registers the callback to receive the picked asset.
        /// </summary>
        /// <param name="onPicked">Callback invoked when an asset is selected.</param>
        public void Show(Action<AssetBrowserEntry> onPicked) {
            if (onPicked == null) {
                throw new ArgumentNullException(nameof(onPicked));
            }

            IsUserPositioned = false;
            IsDragging = false;
            PickedCallback = onPicked;
            BrowserView.ClearExtensionFilter();
            Enabled = true;
            BrowserView.RefreshEntries();
        }

        /// <summary>
        /// Shows the modal with an extension filter and registers the callback to receive the picked asset.
        /// </summary>
        /// <param name="onPicked">Callback invoked when an asset is selected.</param>
        /// <param name="extensionFilter">Extension filter for assets.</param>
        public void Show(Action<AssetBrowserEntry> onPicked, string extensionFilter) {
            if (onPicked == null) {
                throw new ArgumentNullException(nameof(onPicked));
            }

            IsUserPositioned = false;
            IsDragging = false;
            PickedCallback = onPicked;
            BrowserView.SetExtensionFilter(extensionFilter);
            Enabled = true;
            BrowserView.RefreshEntries();
        }

        /// <summary>
        /// Hides the modal and clears any pending pick callback.
        /// </summary>
        public void Hide() {
            IsUserPositioned = false;
            IsDragging = false;
            PickedCallback = null;
            BrowserView.ClearExtensionFilter();
            HideBackdrop();
            Enabled = false;
        }

        /// <summary>
        /// Updates modal sizing and layout to fit the provided window dimensions.
        /// </summary>
        /// <param name="windowWidth">Current window width.</param>
        /// <param name="windowHeight">Current window height.</param>
        public void UpdateLayout(int windowWidth, int windowHeight) {
            if (!IsInitialized) {
                return;
            }

            if (!Enabled) {
                HideBackdrop();
                return;
            }

            int width = Math.Max(1, windowWidth);
            int height = Math.Max(1, windowHeight);
            HostSize = new int2(width, height);

            int maxWidth = Math.Max(GetMinimumPanelWidthPixels(), width - GetPanelPaddingPixels() * 2);
            int maxHeight = Math.Max(GetMinimumPanelHeightPixels(), height - GetPanelPaddingPixels() * 2);
            int panelWidth = Math.Min(GetMaximumPanelWidthPixels(), maxWidth);
            int panelHeight = Math.Min(GetMaximumPanelHeightPixels(), maxHeight);
            panelWidth = Math.Min(panelWidth, width);
            panelHeight = Math.Min(panelHeight, height);

            PanelSize = new int2(panelWidth, panelHeight);
            PanelBackground.Size = PanelSize;
            UpdateBackdrop();
            if (!IsUserPositioned) {
                int panelX = Math.Max(0, (width - panelWidth) / 2);
                int panelY = Math.Max(0, (height - panelHeight) / 2);
                PanelPosition = new int2(panelX, panelY);
            }
            ClampPanelPosition();
            ApplyPanelPosition();

            LayoutHeader(panelWidth);
            LayoutBrowserView(panelWidth, panelHeight);
        }

        /// <summary>
        /// Hides the backdrop geometry when the modal is not visible.
        /// </summary>
        void HideBackdrop() {
            BackdropTopSurface.Size = new int2(0, 0);
            BackdropTopInteractable.Size = new int2(0, 0);
            BackdropBodySurface.Size = new int2(0, 0);
            BackdropBodyInteractable.Size = new int2(0, 0);
        }

        /// <summary>
        /// Updates the backdrop geometry so the host title-bar buttons remain clickable.
        /// </summary>
        void UpdateBackdrop() {
            int topWidth = Math.Max(0, HostSize.X - GetHostTitleBarButtonGapWidthPixels());
            BackdropTopRoot.Position = float3.Zero;
            BackdropTopSurface.Size = new int2(topWidth, Metrics.HostTitleBarHeight);
            BackdropTopInteractable.Size = new int2(topWidth, Metrics.HostTitleBarHeight);
            BackdropBodyRoot.Position = new float3(0f, Metrics.HostTitleBarHeight, 0f);
            int bodyHeight = Math.Max(0, HostSize.Y - Metrics.HostTitleBarHeight);
            BackdropBodySurface.Size = new int2(HostSize.X, bodyHeight);
            BackdropBodyInteractable.Size = new int2(HostSize.X, bodyHeight);
        }

        /// <summary>
        /// Updates header placement within the panel.
        /// </summary>
        /// <param name="panelWidth">Panel width for sizing.</param>
        void LayoutHeader(int panelWidth) {
            int headerWidth = Math.Max(0, panelWidth - GetPanelPaddingPixels() * 2);
            HeaderRoot.Position = new float3(GetPanelPaddingPixels(), GetPanelPaddingPixels(), 0.2f);
            HeaderBackground.Size = new int2(headerWidth, GetHeaderHeightPixels());
            HeaderInteractable.Size = new int2(headerWidth, GetHeaderHeightPixels());

            int closeButtonY = (int)Math.Round((GetHeaderHeightPixels() - GetCloseButtonSize().Y) * 0.5);
            int closeButtonX = Math.Max(GetHeaderButtonSpacingPixels(), headerWidth - GetCloseButtonSize().X - GetHeaderButtonSpacingPixels());
            CloseButtonHost.Position = new float3(closeButtonX, closeButtonY, 0.2f);

            var headerMetrics = Font.MeasureTight(HeaderText.Text);
            float headerTextY = GetTextTopOffset(GetHeaderHeightPixels(), headerMetrics);
            HeaderHost.Position = new float3(GetHeaderPaddingPixels(), headerTextY, 0.2f);
            int textWidth = Math.Max(0, closeButtonX - GetHeaderPaddingPixels() - GetHeaderButtonSpacingPixels());
            HeaderText.Size = new int2(textWidth, (int)Math.Ceiling(headerMetrics.Height));
        }

        /// <summary>
        /// Updates the asset browser view placement within the panel.
        /// </summary>
        /// <param name="panelWidth">Panel width for sizing.</param>
        /// <param name="panelHeight">Panel height for sizing.</param>
        void LayoutBrowserView(int panelWidth, int panelHeight) {
            int contentWidth = Math.Max(0, panelWidth - GetPanelPaddingPixels() * 2);
            int contentHeight = Math.Max(0, panelHeight - GetPanelPaddingPixels() - GetHeaderHeightPixels() - GetSectionSpacingPixels() - GetPanelPaddingPixels());

            BrowserView.Entity.Position = new float3(GetPanelPaddingPixels(), GetPanelPaddingPixels() + GetHeaderHeightPixels() + GetSectionSpacingPixels(), 0.2f);
            BrowserView.UpdateLayout(contentWidth, contentHeight);
        }

        /// <summary>
        /// Applies the current panel position to the panel root entity.
        /// </summary>
        void ApplyPanelPosition() {
            PanelRoot.Position = new float3(PanelPosition.X, PanelPosition.Y, 0.1f);
        }

        /// <summary>
        /// Clamps the panel position so it remains within the host bounds.
        /// </summary>
        void ClampPanelPosition() {
            int maxX = Math.Max(0, HostSize.X - PanelSize.X);
            int maxY = Math.Max(0, HostSize.Y - PanelSize.Y);

            int clampedX = PanelPosition.X;
            if (clampedX < 0) {
                clampedX = 0;
            } else if (clampedX > maxX) {
                clampedX = maxX;
            }

            int clampedY = PanelPosition.Y;
            if (clampedY < 0) {
                clampedY = 0;
            } else if (clampedY > maxY) {
                clampedY = maxY;
            }

            PanelPosition = new int2(clampedX, clampedY);
        }

        /// <summary>
        /// Handles pointer interactions on the header to allow dragging the picker window.
        /// </summary>
        /// <param name="pos">Pointer position relative to the header.</param>
        /// <param name="delta">Pointer movement delta.</param>
        /// <param name="state">Pointer interaction state.</param>
        void HandleHeaderCursor(int2 pos, int2 delta, PointerInteraction state) {
            switch (state) {
                case PointerInteraction.Press:
                    if (IsPointerOverCloseButton(pos)) {
                        return;
                    }
                    IsDragging = true;
                    IsUserPositioned = true;
                    break;
                case PointerInteraction.Hover:
                    if (IsDragging) {
                        PanelPosition = new int2(PanelPosition.X + delta.X, PanelPosition.Y + delta.Y);
                        ClampPanelPosition();
                        ApplyPanelPosition();
                    }
                    break;
                case PointerInteraction.Release:
                case PointerInteraction.Leave:
                    IsDragging = false;
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// Determines whether the pointer is inside the close button region.
        /// </summary>
        /// <param name="pos">Pointer position relative to the header.</param>
        /// <returns>True if the pointer is over the close button area.</returns>
        bool IsPointerOverCloseButton(int2 pos) {
            int headerWidth = Math.Max(0, PanelSize.X - GetPanelPaddingPixels() * 2);
            int closeButtonX = Math.Max(GetHeaderButtonSpacingPixels(), headerWidth - GetCloseButtonSize().X - GetHeaderButtonSpacingPixels());
            int closeButtonY = (int)Math.Round((GetHeaderHeightPixels() - GetCloseButtonSize().Y) * 0.5);
            return pos.X >= closeButtonX &&
                   pos.X <= closeButtonX + GetCloseButtonSize().X &&
                   pos.Y >= closeButtonY &&
                   pos.Y <= closeButtonY + GetCloseButtonSize().Y;
        }

        /// <summary>
        /// Computes the vertical offset needed to center text using tight metrics.
        /// </summary>
        /// <param name="containerHeight">Height of the container in pixels.</param>
        /// <param name="metrics">Tight font metrics for the text.</param>
        /// <returns>Top offset to position the line.</returns>
        float GetTextTopOffset(float containerHeight, FontTightMetrics metrics) {
            return (float)Math.Round(containerHeight * 0.5 - metrics.Height * 0.5 - metrics.MinTop);
        }

        /// <summary>
        /// Handles activation events from the shared asset browser view.
        /// </summary>
        /// <param name="entry">Activated asset entry.</param>
        void HandleAssetActivated(AssetBrowserEntry entry) {
            if (entry == null) {
                throw new ArgumentNullException(nameof(entry));
            }

            NotifyAssetPicked(entry);
        }

        /// <summary>
        /// Notifies listeners that a file entry was picked and hides the modal.
        /// </summary>
        /// <param name="entry">Picked asset entry.</param>
        void NotifyAssetPicked(AssetBrowserEntry entry) {
            if (entry == null) {
                throw new ArgumentNullException(nameof(entry));
            }

            Action<AssetBrowserEntry> callback = PickedCallback;
            PickedCallback = null;
            Hide();
            if (callback != null) {
                callback(entry);
            }
        }

        /// <summary>
        /// Gets the scaled minimum panel width.
        /// </summary>
        /// <returns>Scaled minimum panel width in pixels.</returns>
        int GetMinimumPanelWidthPixels() {
            return Metrics.ScalePixels(MinPanelWidth);
        }

        /// <summary>
        /// Gets the scaled minimum panel height.
        /// </summary>
        /// <returns>Scaled minimum panel height in pixels.</returns>
        int GetMinimumPanelHeightPixels() {
            return Metrics.ScalePixels(MinPanelHeight);
        }

        /// <summary>
        /// Gets the scaled maximum panel width.
        /// </summary>
        /// <returns>Scaled maximum panel width in pixels.</returns>
        int GetMaximumPanelWidthPixels() {
            return Metrics.ScalePixels(MaxPanelWidth);
        }

        /// <summary>
        /// Gets the scaled maximum panel height.
        /// </summary>
        /// <returns>Scaled maximum panel height in pixels.</returns>
        int GetMaximumPanelHeightPixels() {
            return Metrics.ScalePixels(MaxPanelHeight);
        }

        /// <summary>
        /// Gets the scaled panel padding.
        /// </summary>
        /// <returns>Scaled panel padding in pixels.</returns>
        int GetPanelPaddingPixels() {
            return Metrics.ScalePixels(PanelPadding);
        }

        /// <summary>
        /// Gets the scaled section spacing between the header and browser content.
        /// </summary>
        /// <returns>Scaled section spacing in pixels.</returns>
        int GetSectionSpacingPixels() {
            return Metrics.ScalePixels(SectionSpacing);
        }

        /// <summary>
        /// Gets the scaled header height.
        /// </summary>
        /// <returns>Scaled header height in pixels.</returns>
        int GetHeaderHeightPixels() {
            return Metrics.ScalePixels(HeaderHeight);
        }

        /// <summary>
        /// Gets the scaled header padding used before the title text begins.
        /// </summary>
        /// <returns>Scaled header padding in pixels.</returns>
        int GetHeaderPaddingPixels() {
            return Metrics.ScalePixels(HeaderPadding);
        }

        /// <summary>
        /// Gets the scaled spacing preserved between the title text and close button.
        /// </summary>
        /// <returns>Scaled header button spacing in pixels.</returns>
        int GetHeaderButtonSpacingPixels() {
            return Metrics.ScalePixels(HeaderButtonSpacing);
        }

        /// <summary>
        /// Gets the scaled reserved width for the host window control cluster.
        /// </summary>
        /// <returns>Scaled reserved host title-bar button width in pixels.</returns>
        int GetHostTitleBarButtonGapWidthPixels() {
            return Metrics.ScalePixels(HostTitleBarButtonGapWidth);
        }

        /// <summary>
        /// Gets the scaled close-button size.
        /// </summary>
        /// <returns>Scaled close-button size.</returns>
        int2 GetCloseButtonSize() {
            return new int2(
                Metrics.ScalePixels(CloseButtonSize.X),
                Metrics.ScalePixels(CloseButtonSize.Y));
        }
    }
}
