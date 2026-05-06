namespace helengine.editor {
    /// <summary>
    /// Provides the shared modal panel, title-bar chrome, close button, and drag behavior used by editor dialogs.
    /// </summary>
    public abstract class EditorDialogBase : EditorEntity, IAnchorBoundsProvider {
        /// <summary>
        /// Width used by the shared close button chrome.
        /// </summary>
        public const int CloseButtonWidth = 40;

        /// <summary>
        /// Square size used for the shared corner resize grips.
        /// </summary>
        public const int ResizeGripSize = 16;

        /// <summary>
        /// Default host width used before the first real layout pass.
        /// </summary>
        const int DefaultFallbackHostWidth = 1280;

        /// <summary>
        /// Default host height used before the first real layout pass.
        /// </summary>
        const int DefaultFallbackHostHeight = 720;

        /// <summary>
        /// Padding used inside the title bar before the title text begins.
        /// </summary>
        const int HeaderPadding = 8;

        /// <summary>
        /// Spacing preserved between the title text and the close button chrome.
        /// </summary>
        const int HeaderButtonSpacing = 8;

        /// <summary>
        /// Radius used for shared dialog panel chrome.
        /// </summary>
        const float PanelRadius = 6f;

        /// <summary>
        /// Render order used by the fullscreen modal backdrop behind the dialog panel.
        /// </summary>
        const byte BackdropOrder = RenderOrder2D.ModalBackground - 1;
        /// <summary>
        /// Width reserved on the right side of the host title bar for the minimize, maximize, and close button cluster.
        /// </summary>
        const int HostTitleBarButtonGapWidth = CloseButtonWidth * 3;

        /// <summary>
        /// Border thickness used for the shared dialog panel chrome.
        /// </summary>
        const float PanelBorderThickness = 2f;

        /// <summary>
        /// Identifies which corner a resize grip controls.
        /// </summary>
        enum ResizeGripKind {
            /// <summary>
            /// Resizes the dialog from the top-left corner.
            /// </summary>
            TopLeft,

            /// <summary>
            /// Resizes the dialog from the bottom-left corner.
            /// </summary>
            BottomLeft,

            /// <summary>
            /// Resizes the dialog from the bottom-right corner.
            /// </summary>
            BottomRight
        }

        /// <summary>
        /// Font used by shared dialog title-bar content.
        /// </summary>
        FontAsset Font;
        /// <summary>
        /// Shared scaled metrics used to size the dialog shell.
        /// </summary>
        EditorUiMetrics Metrics;
        /// <summary>
        /// Unscaled base width used to rebuild the dialog shell when metrics change.
        /// </summary>
        readonly int BaseDialogWidth;
        /// <summary>
        /// Unscaled base height used to rebuild the dialog shell when metrics change.
        /// </summary>
        readonly int BaseDialogHeight;
        /// <summary>
        /// Unscaled base header height used to rebuild the dialog shell when metrics change.
        /// </summary>
        readonly int BaseDialogHeaderHeight;

        /// <summary>
        /// Render order used for dialog panel surfaces.
        /// </summary>
        readonly byte PanelOrder;

        /// <summary>
        /// Render order used for dialog foreground text and controls.
        /// </summary>
        readonly byte TextOrder;

        /// <summary>
        /// Root entity that owns the fullscreen modal backdrop behind the panel.
        /// </summary>
        readonly EditorEntity BackdropRoot;

        /// <summary>
        /// Root entity that hosts the title-bar backdrop strip.
        /// </summary>
        readonly EditorEntity BackdropTopRoot;

        /// <summary>
        /// Backdrop surface rendered across the title-bar area while leaving the window button gap clear.
        /// </summary>
        readonly SpriteComponent BackdropTopSurface;

        /// <summary>
        /// Interactable that absorbs pointer input over the title-bar backdrop strip.
        /// </summary>
        readonly InteractableComponent BackdropTopInteractable;

        /// <summary>
        /// Root entity that hosts the editor-content backdrop block.
        /// </summary>
        readonly EditorEntity BackdropBodyRoot;

        /// <summary>
        /// Backdrop surface rendered behind the dialog panel to dim lower UI.
        /// </summary>
        readonly SpriteComponent BackdropBodySurface;

        /// <summary>
        /// Fullscreen interactable that absorbs pointer input outside the dialog panel.
        /// </summary>
        readonly InteractableComponent BackdropBodyInteractable;

        /// <summary>
        /// Root entity that owns the panel background and all dialog content.
        /// </summary>
        readonly EditorEntity PanelRoot;

        /// <summary>
        /// Tint applied to the shared fullscreen modal backdrop.
        /// </summary>
        static readonly byte4 BackdropColor = new byte4(0, 0, 0, 144);

        /// <summary>
        /// Rounded panel background rendered behind the dialog content.
        /// </summary>
        readonly RoundedRectComponent PanelBackground;

        /// <summary>
        /// Root entity for the draggable title bar.
        /// </summary>
        readonly EditorEntity HeaderRoot;

        /// <summary>
        /// Background surface rendered behind the title bar.
        /// </summary>
        readonly SpriteComponent HeaderBackground;

        /// <summary>
        /// Interactable region used to drag the dialog by its title bar.
        /// </summary>
        readonly InteractableComponent HeaderInteractable;

        /// <summary>
        /// Host entity for the title text.
        /// </summary>
        readonly EditorEntity TitleHost;

        /// <summary>
        /// Title text shown in the dialog header.
        /// </summary>
        readonly TextComponent TitleText;

        /// <summary>
        /// Host entity for the close button chrome.
        /// </summary>
        readonly EditorEntity CloseButtonHost;

        /// <summary>
        /// Separator line rendered on the left edge of the close button chrome.
        /// </summary>
        readonly SpriteComponent CloseButtonSeparator;

        /// <summary>
        /// Close button used to dismiss the dialog.
        /// </summary>
        readonly ButtonComponent CloseButton;

        /// <summary>
        /// Root entity for the top-left resize grip.
        /// </summary>
        readonly EditorEntity ResizeTopLeftHost;

        /// <summary>
        /// Drawable used to make the top-left grip participate in hit testing.
        /// </summary>
        readonly SpriteComponent ResizeTopLeftSurface;

        /// <summary>
        /// Interactable used for the top-left resize grip.
        /// </summary>
        readonly InteractableComponent ResizeTopLeftInteractable;

        /// <summary>
        /// Root entity for the bottom-left resize grip.
        /// </summary>
        readonly EditorEntity ResizeBottomLeftHost;

        /// <summary>
        /// Drawable used to make the bottom-left grip participate in hit testing.
        /// </summary>
        readonly SpriteComponent ResizeBottomLeftSurface;

        /// <summary>
        /// Interactable used for the bottom-left resize grip.
        /// </summary>
        readonly InteractableComponent ResizeBottomLeftInteractable;

        /// <summary>
        /// Root entity for the bottom-right resize grip.
        /// </summary>
        readonly EditorEntity ResizeBottomRightHost;

        /// <summary>
        /// Drawable used to make the bottom-right grip participate in hit testing.
        /// </summary>
        readonly SpriteComponent ResizeBottomRightSurface;

        /// <summary>
        /// Interactable used for the bottom-right resize grip.
        /// </summary>
        readonly InteractableComponent ResizeBottomRightInteractable;

        /// <summary>
        /// Cached host size used to center and clamp dialog movement.
        /// </summary>
        int2 HostSize;

        /// <summary>
        /// Cached panel position relative to the host window.
        /// </summary>
        int2 PanelPosition;

        /// <summary>
        /// Tracks whether the dialog has been manually repositioned by the user.
        /// </summary>
        bool IsUserPositioned;

        /// <summary>
        /// Tracks whether a resize grip is currently being dragged.
        /// </summary>
        bool IsResizing;

        /// <summary>
        /// Tracks whether the title bar is currently being dragged.
        /// </summary>
        bool IsDragging;

        /// <summary>
        /// Width of the dialog panel owned by the shared shell.
        /// </summary>
        protected int DialogWidth { get; private set; }

        /// <summary>
        /// Height of the dialog panel owned by the shared shell.
        /// </summary>
        protected int DialogHeight { get; private set; }

        /// <summary>
        /// Height of the dialog title bar.
        /// </summary>
        int DialogHeaderHeight { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the dialog exposes resize grips.
        /// </summary>
        protected bool DialogIsResizable { get; set; }

        /// <summary>
        /// Gets or sets the minimum size allowed while the dialog is resized.
        /// </summary>
        protected int2 DialogMinimumSize { get; set; }

        /// <summary>
        /// Gets the local bounds used by anchored children that live under the dialog shell.
        /// </summary>
        public int2 AnchorBounds => new int2(DialogWidth, DialogHeight);

        /// <summary>
        /// Raised when the dialog size changes and anchored children should refresh.
        /// </summary>
        public event Action AnchorBoundsChanged;

        /// <summary>
        /// Initializes the shared dialog shell for one concrete editor dialog.
        /// </summary>
        /// <param name="dialogName">Entity name used to identify the dialog in the editor tree.</param>
        /// <param name="dialogTitle">Title text shown in the header.</param>
        /// <param name="font">Font used by the dialog chrome.</param>
        /// <param name="dialogWidth">Fixed panel width for the dialog.</param>
        /// <param name="dialogHeight">Fixed panel height for the dialog.</param>
        /// <param name="dialogHeaderHeight">Fixed title-bar height for the dialog.</param>
        protected EditorDialogBase(string dialogName, string dialogTitle, FontAsset font, int dialogWidth, int dialogHeight, int dialogHeaderHeight)
            : this(dialogName, dialogTitle, font, EditorUiMetrics.Default, dialogWidth, dialogHeight, dialogHeaderHeight) {
        }

        /// <summary>
        /// Initializes the shared dialog shell for one concrete editor dialog using one shared metrics source.
        /// </summary>
        /// <param name="dialogName">Entity name used to identify the dialog in the editor tree.</param>
        /// <param name="dialogTitle">Title text shown in the header.</param>
        /// <param name="font">Font used by the dialog chrome.</param>
        /// <param name="metrics">Scaled editor UI metrics used to size the dialog shell.</param>
        /// <param name="dialogWidth">Unscaled panel width for the dialog.</param>
        /// <param name="dialogHeight">Unscaled panel height for the dialog.</param>
        /// <param name="dialogHeaderHeight">Unscaled title-bar height for the dialog.</param>
        protected EditorDialogBase(string dialogName, string dialogTitle, FontAsset font, EditorUiMetrics metrics, int dialogWidth, int dialogHeight, int dialogHeaderHeight) {
            if (string.IsNullOrWhiteSpace(dialogName)) {
                throw new ArgumentException("A dialog name is required.", nameof(dialogName));
            }
            if (string.IsNullOrWhiteSpace(dialogTitle)) {
                throw new ArgumentException("A dialog title is required.", nameof(dialogTitle));
            }
            if (font == null) {
                throw new ArgumentNullException(nameof(font));
            }
            if (metrics == null) {
                throw new ArgumentNullException(nameof(metrics));
            }

            Font = font;
            Metrics = metrics;
            BaseDialogWidth = dialogWidth;
            BaseDialogHeight = dialogHeight;
            BaseDialogHeaderHeight = dialogHeaderHeight;
            DialogWidth = Metrics.ScalePixels(dialogWidth);
            DialogHeight = Metrics.ScalePixels(dialogHeight);
            DialogHeaderHeight = Metrics.ScalePixels(dialogHeaderHeight);
            DialogIsResizable = true;
            DialogMinimumSize = new int2(Metrics.ScalePixels(240), Metrics.ScalePixels(160));
            PanelOrder = RenderOrder2D.ModalBackground;
            TextOrder = RenderOrder2D.ModalForeground;

            LayerMask = 0b1000000000000000;
            InternalEntity = true;
            Name = dialogName;
            Enabled = false;

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
                Color = BackdropColor,
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
                Color = BackdropColor,
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
                Position = float3.Zero,
                InternalEntity = true
            };
            AddChild(PanelRoot);

            PanelBackground = new RoundedRectComponent {
                FillColor = ThemeManager.Colors.SurfacePrimary,
                BorderColor = ThemeManager.Colors.AccentTertiary,
                BorderThickness = PanelBorderThickness,
                Radius = PanelRadius,
                RenderOrder2D = PanelOrder,
                Size = new int2(DialogWidth, DialogHeight)
            };
            PanelRoot.AddComponent(PanelBackground);

            HeaderRoot = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            PanelRoot.AddChild(HeaderRoot);

            HeaderBackground = new SpriteComponent {
                Texture = TextureUtils.PixelTexture,
                Color = ThemeManager.Colors.AccentSecondary,
                RenderOrder2D = PanelOrder,
                Size = new int2(DialogWidth, DialogHeaderHeight)
            };
            HeaderRoot.AddComponent(HeaderBackground);

            HeaderInteractable = new InteractableComponent {
                Size = new int2(GetHeaderDragWidthPixels(), DialogHeaderHeight)
            };
            HeaderInteractable.CursorEvent += HandleHeaderCursor;
            HeaderRoot.AddComponent(HeaderInteractable);

            TitleHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            HeaderRoot.AddChild(TitleHost);

            TitleText = new TextComponent {
                Font = font,
                Text = dialogTitle,
                Color = ThemeManager.Colors.InputForegroundPrimary,
                Size = new int2(1, Math.Max(1, (int)Math.Ceiling(Math.Max(font.LineHeight, 1f)))),
                RenderOrder2D = TextOrder
            };
            TitleHost.AddComponent(TitleText);

            CloseButtonHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            HeaderRoot.AddChild(CloseButtonHost);

            CloseButtonSeparator = new SpriteComponent {
                Texture = TextureUtils.PixelTexture,
                Color = ThemeManager.Colors.AccentQuaternary,
                RenderOrder2D = TextOrder,
                Size = new int2(GetDialogSeparatorWidth(), DialogHeaderHeight)
            };
            CloseButtonHost.AddComponent(CloseButtonSeparator);

            CloseButton = new ButtonComponent("X", new int2(GetCloseButtonWidthPixels(), DialogHeaderHeight), font, HandleCloseClicked, 0f);
            CloseButtonHost.AddComponent(CloseButton);
            CloseButton.SetRenderOrders(TextOrder, TextOrder);
            CloseButton.UseHoverOnlyBackground();
            CloseButton.UseSquareCorners();
            CloseButton.SetTextColor(ThemeManager.Colors.AccentQuaternary);

            ResizeTopLeftHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero,
                InternalEntity = true,
                Name = "ResizeTopLeftGrip"
            };
            PanelRoot.AddChild(ResizeTopLeftHost);

            ResizeTopLeftSurface = new SpriteComponent {
                Texture = TextureUtils.PixelTexture,
                Color = new byte4(0, 0, 0, 0),
                RenderOrder2D = RenderOrder2D.ModalInput,
                Size = new int2(GetResizeGripSizePixels(), GetResizeGripSizePixels())
            };
            ResizeTopLeftHost.AddComponent(ResizeTopLeftSurface);

            ResizeTopLeftInteractable = new InteractableComponent {
                HoverCursor = PointerCursorKind.ResizeNorthWestSouthEast,
                Size = new int2(GetResizeGripSizePixels(), GetResizeGripSizePixels())
            };
            ResizeTopLeftInteractable.CursorEvent += HandleTopLeftResizeCursor;
            ResizeTopLeftHost.AddComponent(ResizeTopLeftInteractable);

            ResizeBottomLeftHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero,
                InternalEntity = true,
                Name = "ResizeBottomLeftGrip"
            };
            PanelRoot.AddChild(ResizeBottomLeftHost);

            ResizeBottomLeftSurface = new SpriteComponent {
                Texture = TextureUtils.PixelTexture,
                Color = new byte4(0, 0, 0, 0),
                RenderOrder2D = RenderOrder2D.ModalInput,
                Size = new int2(GetResizeGripSizePixels(), GetResizeGripSizePixels())
            };
            ResizeBottomLeftHost.AddComponent(ResizeBottomLeftSurface);

            ResizeBottomLeftInteractable = new InteractableComponent {
                HoverCursor = PointerCursorKind.ResizeNorthEastSouthWest,
                Size = new int2(GetResizeGripSizePixels(), GetResizeGripSizePixels())
            };
            ResizeBottomLeftInteractable.CursorEvent += HandleBottomLeftResizeCursor;
            ResizeBottomLeftHost.AddComponent(ResizeBottomLeftInteractable);

            ResizeBottomRightHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero,
                InternalEntity = true,
                Name = "ResizeBottomRightGrip"
            };
            PanelRoot.AddChild(ResizeBottomRightHost);

            ResizeBottomRightSurface = new SpriteComponent {
                Texture = TextureUtils.PixelTexture,
                Color = new byte4(0, 0, 0, 0),
                RenderOrder2D = RenderOrder2D.ModalInput,
                Size = new int2(GetResizeGripSizePixels(), GetResizeGripSizePixels())
            };
            ResizeBottomRightHost.AddComponent(ResizeBottomRightSurface);

            ResizeBottomRightInteractable = new InteractableComponent {
                HoverCursor = PointerCursorKind.ResizeNorthWestSouthEast,
                Size = new int2(GetResizeGripSizePixels(), GetResizeGripSizePixels())
            };
            ResizeBottomRightInteractable.CursorEvent += HandleBottomRightResizeCursor;
            ResizeBottomRightHost.AddComponent(ResizeBottomRightInteractable);
        }

        /// <summary>
        /// Updates the shared dialog shell dimensions before the next layout pass.
        /// </summary>
        /// <param name="dialogWidth">New panel width in pixels.</param>
        /// <param name="dialogHeight">New panel height in pixels.</param>
        protected void SetDialogSize(int dialogWidth, int dialogHeight) {
            if (dialogWidth <= 0) {
                throw new ArgumentOutOfRangeException(nameof(dialogWidth));
            }

            if (dialogHeight <= 0) {
                throw new ArgumentOutOfRangeException(nameof(dialogHeight));
            }

            int newWidth = Math.Max(Math.Max(1, DialogMinimumSize.X), dialogWidth);
            int newHeight = Math.Max(Math.Max(1, DialogMinimumSize.Y), dialogHeight);
            bool sizeChanged = DialogWidth != newWidth || DialogHeight != newHeight;
            DialogWidth = newWidth;
            DialogHeight = newHeight;

            if (sizeChanged) {
                NotifyAnchorBoundsChanged();
            }
        }

        /// <summary>
        /// Gets the font used by the dialog shell.
        /// </summary>
        protected FontAsset DialogFont => Font;

        /// <summary>
        /// Gets the shared scaled metrics used by the dialog shell.
        /// </summary>
        protected EditorUiMetrics DialogMetrics => Metrics;

        /// <summary>
        /// Gets the render order used for panel surfaces.
        /// </summary>
        protected byte DialogPanelOrder => PanelOrder;

        /// <summary>
        /// Gets the render order used for dialog foreground content.
        /// </summary>
        protected byte DialogTextOrder => TextOrder;

        /// <summary>
        /// Gets the root entity that owns the dialog panel content.
        /// </summary>
        protected EditorEntity DialogPanelRoot => PanelRoot;

        /// <summary>
        /// Gets the rounded panel background rendered behind the dialog content.
        /// </summary>
        protected RoundedRectComponent DialogPanelBackground => PanelBackground;

        /// <summary>
        /// Applies the shared modal render-order configuration used by combo boxes hosted inside the dialog.
        /// </summary>
        /// <param name="comboBox">Combo box whose control and drop-down visuals should be layered for modal presentation.</param>
        protected void ConfigureDialogComboBox(ComboBoxComponent comboBox) {
            if (comboBox == null) {
                throw new ArgumentNullException(nameof(comboBox));
            }

            comboBox.UseModalPresentation();
        }

        /// <summary>
        /// Reapplies shared dialog shell font and metrics after a live UI scale change.
        /// </summary>
        /// <param name="font">Updated font used by the dialog shell.</param>
        /// <param name="metrics">Updated scaled metrics used by the dialog shell.</param>
        protected void ApplyDialogMetrics(FontAsset font, EditorUiMetrics metrics) {
            if (font == null) {
                throw new ArgumentNullException(nameof(font));
            }
            if (metrics == null) {
                throw new ArgumentNullException(nameof(metrics));
            }

            Font = font;
            Metrics = metrics;
            DialogWidth = Metrics.ScalePixels(BaseDialogWidth);
            DialogHeight = Metrics.ScalePixels(BaseDialogHeight);
            DialogHeaderHeight = Metrics.ScalePixels(BaseDialogHeaderHeight);
            TitleText.Font = font;
            TitleText.Size = new int2(TitleText.Size.X, Math.Max(1, (int)Math.Ceiling(Math.Max(font.LineHeight, 1f))));
            SetDialogMinimumSize(240, 160);
            UpdateDialogChromeLayout();
        }

        /// <summary>
        /// Gets the mutable host size used to center and clamp the dialog.
        /// </summary>
        protected int2 DialogHostSize {
            get => HostSize;
            set => HostSize = value;
        }

        /// <summary>
        /// Gets or sets the cached panel position relative to the host window.
        /// </summary>
        protected int2 DialogPanelPosition {
            get => PanelPosition;
            set => PanelPosition = value;
        }

        /// <summary>
        /// Gets or sets a value indicating whether the dialog has been manually positioned.
        /// </summary>
        protected bool DialogIsUserPositioned {
            get => IsUserPositioned;
            set => IsUserPositioned = value;
        }

        /// <summary>
        /// Gets a value indicating whether the dialog is currently visible.
        /// </summary>
        public bool IsVisible => Enabled;

        /// <summary>
        /// Resets transient drag and positioning state before one dialog show or hide transition.
        /// </summary>
        protected void ResetDialogPositioning() {
            IsDragging = false;
            IsResizing = false;
            IsUserPositioned = false;
        }

        /// <summary>
        /// Updates the cached host size using safe minimum dimensions.
        /// </summary>
        /// <param name="width">Current host width.</param>
        /// <param name="height">Current host height.</param>
        protected void UpdateHostSize(int width, int height) {
            HostSize = new int2(Math.Max(1, width), Math.Max(1, height));
        }

        /// <summary>
        /// Raises the anchor bounds changed event for children that are pinned to the dialog shell.
        /// </summary>
        void NotifyAnchorBoundsChanged() {
            if (AnchorBoundsChanged != null) {
                AnchorBoundsChanged();
            }
        }

        /// <summary>
        /// Updates the shared fullscreen backdrop and blocking rectangle for the current host size.
        /// </summary>
        protected void UpdateDialogBackdrop() {
            BackdropRoot.Position = float3.Zero;
            int topWidth = Math.Max(0, HostSize.X - GetHostTitleBarButtonGapWidth());
            BackdropTopRoot.Position = float3.Zero;
            BackdropTopSurface.Size = new int2(topWidth, Metrics.HostTitleBarHeight);
            BackdropTopInteractable.Size = new int2(topWidth, Metrics.HostTitleBarHeight);
            BackdropBodyRoot.Position = new float3(0f, Metrics.HostTitleBarHeight, 0f);
            int bodyHeight = Math.Max(0, HostSize.Y - Metrics.HostTitleBarHeight);
            BackdropBodySurface.Size = new int2(HostSize.X, bodyHeight);
            BackdropBodyInteractable.Size = new int2(HostSize.X, bodyHeight);
            UpdateDialogInputBlockers(topWidth, bodyHeight);
        }

        /// <summary>
        /// Clears the shared fullscreen backdrop blocker when the dialog is hidden.
        /// </summary>
        protected void ClearDialogBackdrop() {
            BackdropTopSurface.Size = new int2(0, 0);
            BackdropTopInteractable.Size = new int2(0, 0);
            BackdropBodySurface.Size = new int2(0, 0);
            BackdropBodyInteractable.Size = new int2(0, 0);
            EditorInputCaptureService.ClearBlocker(BackdropTopRoot);
            EditorInputCaptureService.ClearBlocker(BackdropBodyRoot);
        }

        /// <summary>
        /// Updates the shared dialog shell layout and fullscreen modal backdrop.
        /// </summary>
        /// <param name="windowWidth">Current host window width.</param>
        /// <param name="windowHeight">Current host window height.</param>
        /// <returns>True when the dialog remains visible and should continue laying out content.</returns>
        protected bool UpdateDialogFrame(int windowWidth, int windowHeight) {
            if (!Enabled) {
                ClearDialogBackdrop();
                return false;
            }

            UpdateHostSize(windowWidth, windowHeight);
            CenterDialogIfNeeded();
            ApplyVisibleDialogState();
            return true;
        }

        /// <summary>
        /// Registers a pointer blocker that covers the modal host area while the dialog is visible.
        /// </summary>
        void UpdateDialogInputBlockers(int topWidth, int bodyHeight) {
            if (topWidth > 0 && Metrics.HostTitleBarHeight > 0) {
                EditorInputCaptureService.SetBlocker(BackdropTopRoot, int2.Zero, new int2(topWidth, Metrics.HostTitleBarHeight));
            } else {
                EditorInputCaptureService.ClearBlocker(BackdropTopRoot);
            }

            if (HostSize.X > 0 && bodyHeight > 0) {
                EditorInputCaptureService.SetBlocker(BackdropBodyRoot, new int2(0, Metrics.HostTitleBarHeight), new int2(HostSize.X, bodyHeight));
            } else {
                EditorInputCaptureService.ClearBlocker(BackdropBodyRoot);
            }
        }

        /// <summary>
        /// Centers the dialog inside the current host when the user has not manually moved it.
        /// </summary>
        protected void CenterDialogIfNeeded() {
            if (IsUserPositioned) {
                return;
            }

            int width = HostSize.X > 0 ? HostSize.X : DefaultFallbackHostWidth;
            int height = HostSize.Y > 0 ? HostSize.Y : DefaultFallbackHostHeight;
            PanelPosition = new int2(
                Math.Max(0, (width - DialogWidth) / 2),
                Math.Max(0, (height - DialogHeight) / 2));
            ApplyDialogPosition();
        }

        /// <summary>
        /// Applies the cached panel position to the dialog root entity.
        /// </summary>
        protected void ApplyDialogPosition() {
            PanelRoot.Position = new float3(PanelPosition.X, PanelPosition.Y, 0.1f);
        }

        /// <summary>
        /// Clamps the dialog position to the visible host area.
        /// </summary>
        protected void ClampDialogPosition() {
            int maxX = Math.Max(0, HostSize.X - DialogWidth);
            int maxY = Math.Max(0, HostSize.Y - DialogHeight);

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
        /// Applies the current positioned dialog shell and modal backdrop state.
        /// </summary>
        protected void ApplyVisibleDialogState() {
            ClampDialogPosition();
            ApplyDialogPosition();
            UpdateDialogBackdrop();
            UpdateDialogChromeLayout();
            HandleDialogLayoutChanged();
        }

        /// <summary>
        /// Allows derived dialogs to react when the shell position or size has been applied for the current frame.
        /// </summary>
        protected virtual void HandleDialogLayoutChanged() {
        }

        /// <summary>
        /// Updates the shared title-bar geometry for the current panel size.
        /// </summary>
        protected void UpdateDialogChromeLayout() {
            PanelBackground.Size = new int2(DialogWidth, DialogHeight);
            HeaderRoot.Position = new float3(0f, 0f, 0.2f);
            HeaderBackground.Size = new int2(DialogWidth, DialogHeaderHeight);
            HeaderInteractable.Size = new int2(GetHeaderDragWidthPixels(), DialogHeaderHeight);

            int closeButtonX = DialogWidth - GetCloseButtonWidthPixels();
            CloseButtonHost.Position = new float3(closeButtonX, 0f, 0.2f);
            CloseButtonSeparator.Size = new int2(GetDialogSeparatorWidth(), DialogHeaderHeight);

            FontTightMetrics titleMetrics = Font.MeasureTight(TitleText.Text);
            float titleY = GetTextTopOffset(DialogHeaderHeight, titleMetrics);
            TitleHost.Position = new float3(GetHeaderPaddingPixels(), titleY, 0.2f);
            int textWidth = Math.Max(1, closeButtonX - GetHeaderPaddingPixels() - GetHeaderButtonSpacingPixels());
            TitleText.Size = new int2(textWidth, Math.Max(1, (int)Math.Ceiling(titleMetrics.Height)));
            UpdateResizeGripLayout();
        }

        /// <summary>
        /// Updates the corner resize grips to match the current dialog dimensions.
        /// </summary>
        void UpdateResizeGripLayout() {
            if (!DialogIsResizable) {
                ResizeTopLeftSurface.Size = new int2(0, 0);
                ResizeTopLeftInteractable.Size = new int2(0, 0);
                ResizeBottomLeftSurface.Size = new int2(0, 0);
                ResizeBottomLeftInteractable.Size = new int2(0, 0);
                ResizeBottomRightSurface.Size = new int2(0, 0);
                ResizeBottomRightInteractable.Size = new int2(0, 0);
                return;
            }

            int gripSize = GetResizeGripSizePixels();
            int gripOffsetX = Math.Max(0, DialogWidth - gripSize);
            int gripOffsetY = Math.Max(0, DialogHeight - gripSize);

            ResizeTopLeftHost.Position = new float3(0f, 0f, 0.3f);
            ResizeTopLeftSurface.Size = new int2(gripSize, gripSize);
            ResizeTopLeftInteractable.Size = new int2(gripSize, gripSize);

            ResizeBottomLeftHost.Position = new float3(0f, gripOffsetY, 0.3f);
            ResizeBottomLeftSurface.Size = new int2(gripSize, gripSize);
            ResizeBottomLeftInteractable.Size = new int2(gripSize, gripSize);

            ResizeBottomRightHost.Position = new float3(gripOffsetX, gripOffsetY, 0.3f);
            ResizeBottomRightSurface.Size = new int2(gripSize, gripSize);
            ResizeBottomRightInteractable.Size = new int2(gripSize, gripSize);
        }

        /// <summary>
        /// Handles pointer events for the top-left resize grip.
        /// </summary>
        /// <param name="pos">Pointer position relative to the grip.</param>
        /// <param name="delta">Pointer movement delta.</param>
        /// <param name="state">Pointer interaction state.</param>
        void HandleTopLeftResizeCursor(int2 pos, int2 delta, PointerInteraction state) {
            HandleResizeGripCursor(ResizeGripKind.TopLeft, delta, state);
        }

        /// <summary>
        /// Handles pointer events for the bottom-left resize grip.
        /// </summary>
        /// <param name="pos">Pointer position relative to the grip.</param>
        /// <param name="delta">Pointer movement delta.</param>
        /// <param name="state">Pointer interaction state.</param>
        void HandleBottomLeftResizeCursor(int2 pos, int2 delta, PointerInteraction state) {
            HandleResizeGripCursor(ResizeGripKind.BottomLeft, delta, state);
        }

        /// <summary>
        /// Handles pointer events for the bottom-right resize grip.
        /// </summary>
        /// <param name="pos">Pointer position relative to the grip.</param>
        /// <param name="delta">Pointer movement delta.</param>
        /// <param name="state">Pointer interaction state.</param>
        void HandleBottomRightResizeCursor(int2 pos, int2 delta, PointerInteraction state) {
            HandleResizeGripCursor(ResizeGripKind.BottomRight, delta, state);
        }

        /// <summary>
        /// Applies a drag delta from one resize grip to the dialog size and position.
        /// </summary>
        /// <param name="gripKind">Corner grip being dragged.</param>
        /// <param name="delta">Pointer movement delta supplied by the input system.</param>
        void HandleResizeGripCursor(ResizeGripKind gripKind, int2 delta, PointerInteraction state) {
            if (!DialogIsResizable) {
                return;
            }

            if (state == PointerInteraction.Press) {
                IsResizing = true;
                IsUserPositioned = true;
                ApplyResizeDelta(gripKind, delta);
                return;
            }

            if (state == PointerInteraction.Hover) {
                if (IsResizing) {
                    ApplyResizeDelta(gripKind, delta);
                }

                return;
            }

            if (state == PointerInteraction.Release || state == PointerInteraction.Leave) {
                IsResizing = false;
            }
        }

        /// <summary>
        /// Updates the dialog shell using one resize delta from a corner grip.
        /// </summary>
        /// <param name="gripKind">Corner grip being dragged.</param>
        /// <param name="delta">Pointer movement delta supplied by the input system.</param>
        void ApplyResizeDelta(ResizeGripKind gripKind, int2 delta) {
            int minimumWidth = Math.Max(1, DialogMinimumSize.X);
            int minimumHeight = Math.Max(1, DialogMinimumSize.Y);
            int newX = PanelPosition.X;
            int newY = PanelPosition.Y;
            int newWidth = DialogWidth;
            int newHeight = DialogHeight;

            switch (gripKind) {
                case ResizeGripKind.TopLeft: {
                    int maxWidthShrink = Math.Max(0, DialogWidth - minimumWidth);
                    int widthDelta = Math.Min(delta.X, maxWidthShrink);
                    newX += widthDelta;
                    newWidth -= widthDelta;

                    int maxHeightShrink = Math.Max(0, DialogHeight - minimumHeight);
                    int heightDelta = Math.Min(delta.Y, maxHeightShrink);
                    newY += heightDelta;
                    newHeight -= heightDelta;
                    break;
                }
                case ResizeGripKind.BottomLeft: {
                    int maxWidthShrink = Math.Max(0, DialogWidth - minimumWidth);
                    int widthDelta = Math.Min(delta.X, maxWidthShrink);
                    newX += widthDelta;
                    newWidth -= widthDelta;
                    int maxHeight = Math.Max(minimumHeight, HostSize.Y - PanelPosition.Y);
                    newHeight = Math.Max(minimumHeight, Math.Min(DialogHeight + delta.Y, maxHeight));
                    break;
                }
                case ResizeGripKind.BottomRight: {
                    int maxWidth = Math.Max(minimumWidth, HostSize.X - PanelPosition.X);
                    int maxHeight = Math.Max(minimumHeight, HostSize.Y - PanelPosition.Y);
                    newWidth = Math.Max(minimumWidth, Math.Min(DialogWidth + delta.X, maxWidth));
                    newHeight = Math.Max(minimumHeight, Math.Min(DialogHeight + delta.Y, maxHeight));
                    break;
                }
            }

            DialogWidth = newWidth;
            DialogHeight = newHeight;
            PanelPosition = new int2(newX, newY);
            ClampDialogPosition();
            ApplyDialogPosition();
            UpdateDialogChromeLayout();
            HandleDialogLayoutChanged();
        }

        /// <summary>
        /// Returns the rounded line height used by the dialog font.
        /// </summary>
        /// <returns>Rounded positive line height for layout calculations.</returns>
        protected int GetDialogLineHeight() {
            return Math.Max(1, (int)Math.Ceiling(Math.Max(Font.LineHeight, 1f)));
        }

        /// <summary>
        /// Computes the top offset needed to vertically center text using tight font metrics.
        /// </summary>
        /// <param name="containerHeight">Height of the container that owns the text.</param>
        /// <param name="metrics">Tight font metrics for the current text.</param>
        /// <returns>Top offset that vertically centers the text.</returns>
        protected float GetTextTopOffset(float containerHeight, FontTightMetrics metrics) {
            return (float)Math.Round(containerHeight * 0.5 - metrics.Height * 0.5 - metrics.MinTop);
        }

        /// <summary>
        /// Handles title-bar pointer interactions so the dialog can be dragged without starting from the close button.
        /// </summary>
        /// <param name="pos">Pointer position relative to the title bar.</param>
        /// <param name="delta">Pointer movement delta.</param>
        /// <param name="state">Current pointer interaction state.</param>
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
                        ClampDialogPosition();
                        ApplyDialogPosition();
                        HandleDialogLayoutChanged();
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
        /// Raises the concrete dialog close action from the shared close button.
        /// </summary>
        void HandleCloseClicked() {
            OnCloseRequested();
        }

        /// <summary>
        /// Determines whether the pointer overlaps the close button region of the title bar.
        /// </summary>
        /// <param name="pos">Pointer position relative to the title bar.</param>
        /// <returns>True when the pointer overlaps the close button chrome.</returns>
        bool IsPointerOverCloseButton(int2 pos) {
            int closeButtonX = DialogWidth - GetCloseButtonWidthPixels();
            return pos.X >= closeButtonX &&
                   pos.X <= closeButtonX + GetCloseButtonWidthPixels() &&
                   pos.Y >= 0 &&
                   pos.Y <= DialogHeaderHeight;
        }

        /// <summary>
        /// Sets the dialog minimum size using unscaled base dimensions.
        /// </summary>
        /// <param name="baseWidth">Unscaled minimum width.</param>
        /// <param name="baseHeight">Unscaled minimum height.</param>
        protected void SetDialogMinimumSize(int baseWidth, int baseHeight) {
            DialogMinimumSize = new int2(Metrics.ScalePixels(baseWidth), Metrics.ScalePixels(baseHeight));
        }

        /// <summary>
        /// Gets the scaled width used by the shared close button chrome.
        /// </summary>
        /// <returns>Scaled close-button width in pixels.</returns>
        int GetCloseButtonWidthPixels() {
            return Metrics.ScalePixels(CloseButtonWidth);
        }

        /// <summary>
        /// Gets the scaled width reserved for dragging the header without overlapping the close button.
        /// </summary>
        /// <returns>Scaled drag-region width in pixels.</returns>
        int GetHeaderDragWidthPixels() {
            return Math.Max(0, DialogWidth - GetCloseButtonWidthPixels());
        }

        /// <summary>
        /// Gets the scaled square size used by the resize grips.
        /// </summary>
        /// <returns>Scaled resize-grip size in pixels.</returns>
        int GetResizeGripSizePixels() {
            return Metrics.ScalePixels(ResizeGripSize);
        }

        /// <summary>
        /// Gets the scaled separator width used inside the dialog header.
        /// </summary>
        /// <returns>Scaled separator width in pixels.</returns>
        int GetDialogSeparatorWidth() {
            return Metrics.ScalePixels(1);
        }

        /// <summary>
        /// Gets the scaled header padding used before the title text begins.
        /// </summary>
        /// <returns>Scaled header padding in pixels.</returns>
        int GetHeaderPaddingPixels() {
            return Metrics.ScalePixels(HeaderPadding);
        }

        /// <summary>
        /// Gets the scaled spacing preserved between the title text and the close button chrome.
        /// </summary>
        /// <returns>Scaled header button spacing in pixels.</returns>
        int GetHeaderButtonSpacingPixels() {
            return Metrics.ScalePixels(HeaderButtonSpacing);
        }

        /// <summary>
        /// Gets the scaled reserved width for the host window control cluster.
        /// </summary>
        /// <returns>Scaled reserved host title-bar button width in pixels.</returns>
        int GetHostTitleBarButtonGapWidth() {
            return Metrics.ScalePixels(HostTitleBarButtonGapWidth);
        }

        /// <summary>
        /// Raises the concrete dialog close behavior when the shared close button is pressed.
        /// </summary>
        protected abstract void OnCloseRequested();
    }
}
