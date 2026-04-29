namespace helengine.editor {
    /// <summary>
    /// Floating confirmation dialog shown before discarding a dirty scene.
    /// </summary>
    public class UnsavedChangesDialog : EditorEntity {
        /// <summary>
        /// Default panel width for the dialog.
        /// </summary>
        public const int PanelWidth = 420;
        /// <summary>
        /// Default panel height for the dialog.
        /// </summary>
        public const int PanelHeight = 184;
        /// <summary>
        /// Padding applied inside the dialog panel.
        /// </summary>
        public const int PanelPadding = 16;
        /// <summary>
        /// Height reserved for the draggable title bar.
        /// </summary>
        public const int HeaderHeight = 32;
        /// <summary>
        /// Height reserved for the footer button row.
        /// </summary>
        public const int FooterHeight = 28;
        /// <summary>
        /// Spacing used between dialog sections.
        /// </summary>
        public const int SectionSpacing = 10;
        /// <summary>
        /// Radius used for the dialog background.
        /// </summary>
        const float PanelRadius = 6f;
        /// <summary>
        /// Border thickness used for the dialog background.
        /// </summary>
        const float PanelBorderThickness = 2f;
        /// <summary>
        /// Padding used inside the title bar for text and buttons.
        /// </summary>
        const int HeaderPadding = 8;
        /// <summary>
        /// Spacing used between the title text and the close button.
        /// </summary>
        const int HeaderButtonSpacing = 8;
        /// <summary>
        /// Fixed size used for the save button.
        /// </summary>
        static readonly int2 SaveButtonSize = new int2(88, 22);
        /// <summary>
        /// Fixed size used for the don't-save button.
        /// </summary>
        static readonly int2 DontSaveButtonSize = new int2(104, 22);
        /// <summary>
        /// Fixed size used for the cancel button.
        /// </summary>
        static readonly int2 CancelButtonSize = new int2(88, 22);
        /// <summary>
        /// Fixed size used for the header close button.
        /// </summary>
        static readonly int2 CloseButtonSize = new int2(40, HeaderHeight);

        /// <summary>
        /// Font used for dialog labels and buttons.
        /// </summary>
        readonly FontAsset Font;
        /// <summary>
        /// Root entity hosting the dialog content.
        /// </summary>
        readonly EditorEntity PanelRoot;
        /// <summary>
        /// Panel background shape.
        /// </summary>
        readonly RoundedRectComponent PanelBackground;
        /// <summary>
        /// Root entity for the draggable title bar.
        /// </summary>
        readonly EditorEntity HeaderRoot;
        /// <summary>
        /// Background sprite rendered behind the title bar.
        /// </summary>
        readonly SpriteComponent HeaderBackground;
        /// <summary>
        /// Interactable region used to drag the dialog from its title bar.
        /// </summary>
        readonly InteractableComponent HeaderInteractable;
        /// <summary>
        /// Host entity for the dialog title text.
        /// </summary>
        readonly EditorEntity TitleHost;
        /// <summary>
        /// Dialog title text shown in the title bar.
        /// </summary>
        readonly TextComponent TitleText;
        /// <summary>
        /// Host entity for the title-bar close button.
        /// </summary>
        readonly EditorEntity CloseButtonHost;
        /// <summary>
        /// Button used to cancel and close the dialog.
        /// </summary>
        readonly ButtonComponent CloseButton;
        /// <summary>
        /// Host entity for the dialog message text.
        /// </summary>
        readonly EditorEntity MessageHost;
        /// <summary>
        /// Message text shown above the footer buttons.
        /// </summary>
        readonly TextComponent MessageText;
        /// <summary>
        /// Host entity for the save button.
        /// </summary>
        readonly EditorEntity SaveButtonHost;
        /// <summary>
        /// Save button component.
        /// </summary>
        readonly ButtonComponent SaveButton;
        /// <summary>
        /// Host entity for the don't-save button.
        /// </summary>
        readonly EditorEntity DontSaveButtonHost;
        /// <summary>
        /// Don't-save button component.
        /// </summary>
        readonly ButtonComponent DontSaveButton;
        /// <summary>
        /// Host entity for the cancel button.
        /// </summary>
        readonly EditorEntity CancelButtonHost;
        /// <summary>
        /// Cancel button component.
        /// </summary>
        readonly ButtonComponent CancelButton;
        /// <summary>
        /// Render order used for panel backgrounds.
        /// </summary>
        readonly byte PanelOrder;
        /// <summary>
        /// Render order used for text labels and controls.
        /// </summary>
        readonly byte TextOrder;
        /// <summary>
        /// Cached host size used to clamp manual dialog movement.
        /// </summary>
        int2 HostSize;
        /// <summary>
        /// Cached panel position relative to the host window.
        /// </summary>
        int2 PanelPosition;
        /// <summary>
        /// Tracks whether the user has manually moved the dialog.
        /// </summary>
        bool IsUserPositioned;
        /// <summary>
        /// Tracks whether the title bar is currently being dragged.
        /// </summary>
        bool IsDragging;
        /// <summary>
        /// Tracks whether the dialog has completed initialization.
        /// </summary>
        bool IsInitialized;

        /// <summary>
        /// Raised when the user chooses to save the current scene.
        /// </summary>
        public event Action SaveRequested;
        /// <summary>
        /// Raised when the user chooses to discard the current scene changes.
        /// </summary>
        public event Action DontSaveRequested;
        /// <summary>
        /// Raised when the user cancels the pending scene transition.
        /// </summary>
        public event Action CancelRequested;

        /// <summary>
        /// Initializes a new unsaved-changes dialog.
        /// </summary>
        /// <param name="font">Font used for labels and buttons.</param>
        public UnsavedChangesDialog(FontAsset font) {
            if (font == null) {
                throw new ArgumentNullException(nameof(font));
            }

            Font = font;
            LayerMask = 0b1000000000000000;
            InternalEntity = true;
            Name = "UnsavedChangesDialog";

            PanelOrder = RenderOrder2D.ModalBackground;
            TextOrder = RenderOrder2D.ModalForeground;

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
                Size = new int2(PanelWidth, PanelHeight)
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
                Size = new int2(0, 0)
            };
            HeaderRoot.AddComponent(HeaderBackground);

            HeaderInteractable = new InteractableComponent {
                Size = new int2(0, 0)
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
                Text = "Unsaved Changes",
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

            CloseButton = new ButtonComponent("X", CloseButtonSize, font, HandleCloseClicked, 0f);
            CloseButtonHost.AddComponent(CloseButton);
            CloseButton.SetRenderOrders(TextOrder, TextOrder);
            CloseButton.UseHoverOnlyBackground();
            CloseButton.UseSquareCorners();
            CloseButton.SetTextColor(ThemeManager.Colors.AccentQuaternary);

            MessageHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            PanelRoot.AddChild(MessageHost);

            MessageText = new TextComponent {
                Font = font,
                Text = "Do you want to save changes to the current map?",
                Color = ThemeManager.Colors.InputForegroundPrimary,
                Size = new int2(1, Math.Max(1, (int)Math.Ceiling(Math.Max(font.LineHeight, 1f)))),
                RenderOrder2D = TextOrder
            };
            MessageHost.AddComponent(MessageText);

            SaveButtonHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            PanelRoot.AddChild(SaveButtonHost);

            SaveButton = new ButtonComponent("Save", SaveButtonSize, font, HandleSaveClicked, 0f);
            SaveButtonHost.AddComponent(SaveButton);
            SaveButton.SetRenderOrders(TextOrder, TextOrder);

            DontSaveButtonHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            PanelRoot.AddChild(DontSaveButtonHost);

            DontSaveButton = new ButtonComponent("Don't Save", DontSaveButtonSize, font, HandleDontSaveClicked, 0f);
            DontSaveButtonHost.AddComponent(DontSaveButton);
            DontSaveButton.SetRenderOrders(TextOrder, TextOrder);

            CancelButtonHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            PanelRoot.AddChild(CancelButtonHost);

            CancelButton = new ButtonComponent("Cancel", CancelButtonSize, font, HandleCancelClicked, 0f);
            CancelButtonHost.AddComponent(CancelButton);
            CancelButton.SetRenderOrders(TextOrder, TextOrder);

            Enabled = false;
            IsInitialized = true;
        }

        /// <summary>
        /// Gets a value indicating whether the dialog is currently visible.
        /// </summary>
        public bool IsVisible => Enabled;

        /// <summary>
        /// Shows the dialog.
        /// </summary>
        public void Show() {
            IsDragging = false;
            IsUserPositioned = false;
            Enabled = true;
        }

        /// <summary>
        /// Hides the dialog and clears its input-capture blocker.
        /// </summary>
        public void Hide() {
            EditorInputCaptureService.ClearBlocker(this);
            IsDragging = false;
            IsUserPositioned = false;
            Enabled = false;
        }

        /// <summary>
        /// Updates dialog sizing and layout to fit the provided window dimensions.
        /// </summary>
        /// <param name="windowWidth">Current window width.</param>
        /// <param name="windowHeight">Current window height.</param>
        public void UpdateLayout(int windowWidth, int windowHeight) {
            if (!IsInitialized) {
                return;
            }
            if (!Enabled) {
                EditorInputCaptureService.ClearBlocker(this);
                return;
            }

            int safeWidth = Math.Max(1, windowWidth);
            int safeHeight = Math.Max(1, windowHeight);
            HostSize = new int2(safeWidth, safeHeight);
            if (!IsUserPositioned) {
                PanelPosition = new int2(
                    Math.Max(0, (safeWidth - PanelWidth) / 2),
                    Math.Max(0, (safeHeight - PanelHeight) / 2));
            }

            ClampPanelPosition();
            ApplyPanelPosition();
            EditorInputCaptureService.SetBlocker(this, PanelPosition, new int2(PanelWidth, PanelHeight));

            LayoutHeader();
            LayoutMessage();
            LayoutButtons();
        }

        /// <summary>
        /// Raises the save action for the current dirty scene.
        /// </summary>
        void HandleSaveClicked() {
            if (SaveRequested != null) {
                SaveRequested();
            }
        }

        /// <summary>
        /// Raises the discard action for the current dirty scene.
        /// </summary>
        void HandleDontSaveClicked() {
            if (DontSaveRequested != null) {
                DontSaveRequested();
            }
        }

        /// <summary>
        /// Raises the cancel action for the pending scene transition.
        /// </summary>
        void HandleCancelClicked() {
            if (CancelRequested != null) {
                CancelRequested();
            }
        }

        /// <summary>
        /// Raises the cancel action from the title-bar close button.
        /// </summary>
        void HandleCloseClicked() {
            HandleCancelClicked();
        }

        /// <summary>
        /// Updates the title-bar placement within the dialog panel.
        /// </summary>
        void LayoutHeader() {
            int headerWidth = PanelWidth;
            HeaderRoot.Position = new float3(0f, 0f, 0.2f);
            HeaderBackground.Size = new int2(headerWidth, HeaderHeight);
            HeaderInteractable.Size = new int2(headerWidth, HeaderHeight);

            int closeButtonX = headerWidth - CloseButtonSize.X;
            CloseButtonHost.Position = new float3(closeButtonX, 0f, 0.2f);

            FontTightMetrics titleMetrics = Font.MeasureTight(TitleText.Text);
            float titleY = GetTextTopOffset(HeaderHeight, titleMetrics);
            TitleHost.Position = new float3(HeaderPadding, titleY, 0.2f);
            int textWidth = Math.Max(1, closeButtonX - HeaderPadding - HeaderButtonSpacing);
            TitleText.Size = new int2(textWidth, Math.Max(1, (int)Math.Ceiling(titleMetrics.Height)));
        }

        /// <summary>
        /// Updates the message text placement inside the dialog panel.
        /// </summary>
        void LayoutMessage() {
            int contentWidth = Math.Max(1, PanelWidth - PanelPadding * 2);
            FontTightMetrics messageMetrics = Font.MeasureTight(MessageText.Text);
            int messageTop = PanelPadding + HeaderHeight + SectionSpacing;
            MessageHost.Position = new float3(PanelPadding, messageTop, 0.2f);
            MessageText.Size = new int2(contentWidth, Math.Max(1, (int)Math.Ceiling(Math.Max(messageMetrics.Height, Font.LineHeight))));
        }

        /// <summary>
        /// Updates footer button placement within the dialog panel.
        /// </summary>
        void LayoutButtons() {
            int footerTop = PanelHeight - PanelPadding - FooterHeight;
            int cancelButtonX = PanelWidth - PanelPadding - CancelButtonSize.X;
            int dontSaveButtonX = cancelButtonX - 8 - DontSaveButtonSize.X;
            int saveButtonX = dontSaveButtonX - 8 - SaveButtonSize.X;
            int buttonY = footerTop + Math.Max(0, (FooterHeight - SaveButtonSize.Y) / 2);

            SaveButtonHost.Position = new float3(saveButtonX, buttonY, 0.2f);
            DontSaveButtonHost.Position = new float3(dontSaveButtonX, buttonY, 0.2f);
            CancelButtonHost.Position = new float3(cancelButtonX, buttonY, 0.2f);
        }

        /// <summary>
        /// Handles pointer interactions on the title bar to allow dragging the dialog window.
        /// </summary>
        /// <param name="pos">Pointer position relative to the title bar.</param>
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
        /// Determines whether the pointer is inside the close-button region.
        /// </summary>
        /// <param name="pos">Pointer position relative to the title bar.</param>
        /// <returns>True when the pointer overlaps the close button.</returns>
        bool IsPointerOverCloseButton(int2 pos) {
            int closeButtonX = PanelWidth - CloseButtonSize.X;
            return pos.X >= closeButtonX &&
                   pos.X <= closeButtonX + CloseButtonSize.X &&
                   pos.Y >= 0 &&
                   pos.Y <= CloseButtonSize.Y;
        }

        /// <summary>
        /// Applies the cached panel position to the dialog root entity.
        /// </summary>
        void ApplyPanelPosition() {
            PanelRoot.Position = new float3(PanelPosition.X, PanelPosition.Y, 0.1f);
        }

        /// <summary>
        /// Clamps the cached panel position to the visible host area.
        /// </summary>
        void ClampPanelPosition() {
            int maxX = Math.Max(0, HostSize.X - PanelWidth);
            int maxY = Math.Max(0, HostSize.Y - PanelHeight);

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
        /// Computes the vertical offset needed to center text using tight font metrics.
        /// </summary>
        /// <param name="containerHeight">Height of the title bar.</param>
        /// <param name="metrics">Measured metrics for the title text.</param>
        /// <returns>Top offset that vertically centers the text.</returns>
        float GetTextTopOffset(float containerHeight, FontTightMetrics metrics) {
            return (float)Math.Round(containerHeight * 0.5 - metrics.Height * 0.5 - metrics.MinTop);
        }
    }
}
