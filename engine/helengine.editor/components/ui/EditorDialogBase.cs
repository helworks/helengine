namespace helengine.editor {
    /// <summary>
    /// Provides the shared modal panel, title-bar chrome, close button, and drag behavior used by editor dialogs.
    /// </summary>
    public abstract class EditorDialogBase : EditorEntity {
        /// <summary>
        /// Width used by the shared close button chrome.
        /// </summary>
        public const int CloseButtonWidth = 40;

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
        /// Width reserved on the right side of the host title bar so the window buttons stay interactive.
        /// </summary>
        const int HostTitleBarButtonGapWidth = EditorTitleBar.HeightPixels * 4;

        /// <summary>
        /// Border thickness used for the shared dialog panel chrome.
        /// </summary>
        const float PanelBorderThickness = 2f;

        /// <summary>
        /// Font used by shared dialog title-bar content.
        /// </summary>
        readonly FontAsset Font;

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
        /// Tracks whether the title bar is currently being dragged.
        /// </summary>
        bool IsDragging;

        /// <summary>
        /// Width of the dialog panel owned by the shared shell.
        /// </summary>
        int DialogWidth { get; }

        /// <summary>
        /// Height of the dialog panel owned by the shared shell.
        /// </summary>
        int DialogHeight { get; }

        /// <summary>
        /// Height of the dialog title bar.
        /// </summary>
        int DialogHeaderHeight { get; }

        /// <summary>
        /// Initializes the shared dialog shell for one concrete editor dialog.
        /// </summary>
        /// <param name="dialogName">Entity name used to identify the dialog in the editor tree.</param>
        /// <param name="dialogTitle">Title text shown in the header.</param>
        /// <param name="font">Font used by the dialog chrome.</param>
        /// <param name="dialogWidth">Fixed panel width for the dialog.</param>
        /// <param name="dialogHeight">Fixed panel height for the dialog.</param>
        /// <param name="dialogHeaderHeight">Fixed title-bar height for the dialog.</param>
        protected EditorDialogBase(string dialogName, string dialogTitle, FontAsset font, int dialogWidth, int dialogHeight, int dialogHeaderHeight) {
            if (string.IsNullOrWhiteSpace(dialogName)) {
                throw new ArgumentException("A dialog name is required.", nameof(dialogName));
            }
            if (string.IsNullOrWhiteSpace(dialogTitle)) {
                throw new ArgumentException("A dialog title is required.", nameof(dialogTitle));
            }
            if (font == null) {
                throw new ArgumentNullException(nameof(font));
            }

            Font = font;
            DialogWidth = dialogWidth;
            DialogHeight = dialogHeight;
            DialogHeaderHeight = dialogHeaderHeight;
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
                Size = new int2(DialogWidth, DialogHeaderHeight)
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
                Size = new int2(1, DialogHeaderHeight)
            };
            CloseButtonHost.AddComponent(CloseButtonSeparator);

            CloseButton = new ButtonComponent("X", new int2(CloseButtonWidth, DialogHeaderHeight), font, HandleCloseClicked, 0f);
            CloseButtonHost.AddComponent(CloseButton);
            CloseButton.SetRenderOrders(TextOrder, TextOrder);
            CloseButton.UseHoverOnlyBackground();
            CloseButton.UseSquareCorners();
            CloseButton.SetTextColor(ThemeManager.Colors.AccentQuaternary);
        }

        /// <summary>
        /// Gets the font used by the dialog shell.
        /// </summary>
        protected FontAsset DialogFont => Font;

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
        /// Updates the shared fullscreen backdrop and blocking rectangle for the current host size.
        /// </summary>
        protected void UpdateDialogBackdrop() {
            BackdropRoot.Position = float3.Zero;
            int topWidth = Math.Max(0, HostSize.X - HostTitleBarButtonGapWidth);
            BackdropTopRoot.Position = float3.Zero;
            BackdropTopSurface.Size = new int2(topWidth, EditorTitleBar.HeightPixels);
            BackdropTopInteractable.Size = new int2(topWidth, EditorTitleBar.HeightPixels);
            BackdropBodyRoot.Position = new float3(0f, EditorTitleBar.HeightPixels, 0f);
            int bodyHeight = Math.Max(0, HostSize.Y - EditorTitleBar.HeightPixels);
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
            ClampDialogPosition();
            ApplyDialogPosition();
            UpdateDialogBackdrop();
            UpdateDialogChromeLayout();
            return true;
        }

        /// <summary>
        /// Registers a pointer blocker that covers the modal host area while the dialog is visible.
        /// </summary>
        void UpdateDialogInputBlockers(int topWidth, int bodyHeight) {
            if (topWidth > 0 && EditorTitleBar.HeightPixels > 0) {
                EditorInputCaptureService.SetBlocker(BackdropTopRoot, int2.Zero, new int2(topWidth, EditorTitleBar.HeightPixels));
            } else {
                EditorInputCaptureService.ClearBlocker(BackdropTopRoot);
            }

            if (HostSize.X > 0 && bodyHeight > 0) {
                EditorInputCaptureService.SetBlocker(BackdropBodyRoot, new int2(0, EditorTitleBar.HeightPixels), new int2(HostSize.X, bodyHeight));
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
        /// Updates the shared title-bar geometry for the current panel size.
        /// </summary>
        protected void UpdateDialogChromeLayout() {
            HeaderRoot.Position = new float3(0f, 0f, 0.2f);
            HeaderBackground.Size = new int2(DialogWidth, DialogHeaderHeight);
            HeaderInteractable.Size = new int2(DialogWidth, DialogHeaderHeight);

            int closeButtonX = DialogWidth - CloseButtonWidth;
            CloseButtonHost.Position = new float3(closeButtonX, 0f, 0.2f);
            CloseButtonSeparator.Size = new int2(1, DialogHeaderHeight);

            FontTightMetrics titleMetrics = Font.MeasureTight(TitleText.Text);
            float titleY = GetTextTopOffset(DialogHeaderHeight, titleMetrics);
            TitleHost.Position = new float3(HeaderPadding, titleY, 0.2f);
            int textWidth = Math.Max(1, closeButtonX - HeaderPadding - HeaderButtonSpacing);
            TitleText.Size = new int2(textWidth, Math.Max(1, (int)Math.Ceiling(titleMetrics.Height)));
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
            int closeButtonX = DialogWidth - CloseButtonWidth;
            return pos.X >= closeButtonX &&
                   pos.X <= closeButtonX + CloseButtonWidth &&
                   pos.Y >= 0 &&
                   pos.Y <= DialogHeaderHeight;
        }

        /// <summary>
        /// Raises the concrete dialog close behavior when the shared close button is pressed.
        /// </summary>
        protected abstract void OnCloseRequested();
    }
}
