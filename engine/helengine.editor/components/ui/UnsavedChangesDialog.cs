namespace helengine.editor {
    /// <summary>
    /// Floating confirmation dialog shown before discarding a dirty scene.
    /// </summary>
    public class UnsavedChangesDialog : EditorDialogBase {
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
        /// Spacing used between footer buttons.
        /// </summary>
        const int FooterButtonSpacing = 8;

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
        /// Host entity for the dialog message text.
        /// </summary>
        readonly EditorEntity MessageHost;

        /// <summary>
        /// Anchor component that keeps the message block pinned to the upper-left content area.
        /// </summary>
        readonly LayoutComponent MessageAnchor;

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
        /// Host entity for the footer button group anchor region.
        /// </summary>
        readonly EditorEntity FooterHost;

        /// <summary>
        /// Anchor component that keeps the footer row pinned to the bottom-right of the dialog.
        /// </summary>
        readonly LayoutComponent FooterAnchor;

        /// <summary>
        /// Invisible sprite that provides the footer group size for anchoring.
        /// </summary>
        readonly SpriteComponent FooterBoundsSurface;

        /// <summary>
        /// Cancel button component.
        /// </summary>
        readonly ButtonComponent CancelButton;

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
        public UnsavedChangesDialog(FontAsset font) : this(font, EditorUiMetrics.Default) {
        }

        /// <summary>
        /// Initializes a new unsaved-changes dialog using one shared metrics source.
        /// </summary>
        /// <param name="font">Font used for labels and buttons.</param>
        /// <param name="metrics">Scaled editor UI metrics used to size the dialog.</param>
        public UnsavedChangesDialog(FontAsset font, EditorUiMetrics metrics) : base("UnsavedChangesDialog", "Unsaved Changes", font, metrics, PanelWidth, PanelHeight, HeaderHeight) {
            SetDialogMinimumSize(PanelWidth, PanelHeight);

            MessageHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            DialogPanelRoot.AddChild(MessageHost);
            MessageAnchor = new LayoutComponent();
            MessageHost.AddComponent(MessageAnchor);
            MessageAnchor.SetAnchorDistances(left: GetPanelPaddingPixels(), top: GetMessageTop());

            MessageText = new TextComponent {
                Font = DialogFont,
                Text = "Do you want to save changes to the current map?",
                Color = ThemeManager.Colors.InputForegroundPrimary,
                Size = new int2(1, Math.Max(1, (int)Math.Ceiling(Math.Max(DialogFont.LineHeight, 1f)))),
                RenderOrder2D = DialogTextOrder
            };
            MessageHost.AddComponent(MessageText);

            SaveButtonHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            FooterHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            DialogPanelRoot.AddChild(FooterHost);
            FooterBoundsSurface = new SpriteComponent {
                Texture = TextureUtils.PixelTexture,
                Color = new byte4(0, 0, 0, 0),
                RenderOrder2D = RenderOrder2D.ModalInput,
                Size = GetFooterBoundsSize()
            };
            FooterHost.AddComponent(FooterBoundsSurface);
            FooterAnchor = new LayoutComponent();
            FooterHost.AddComponent(FooterAnchor);
            FooterAnchor.SetAnchorDistances(right: GetPanelPaddingPixels(), bottom: GetPanelPaddingPixels());

            SaveButtonHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            FooterHost.AddChild(SaveButtonHost);

            SaveButton = new ButtonComponent("Save", GetSaveButtonSize(), DialogFont, HandleSaveClicked, 0f);
            SaveButtonHost.AddComponent(SaveButton);
            SaveButton.SetRenderOrders(DialogTextOrder, DialogTextOrder);

            DontSaveButtonHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            FooterHost.AddChild(DontSaveButtonHost);

            DontSaveButton = new ButtonComponent("Don't Save", GetDontSaveButtonSize(), DialogFont, HandleDontSaveClicked, 0f);
            DontSaveButtonHost.AddComponent(DontSaveButton);
            DontSaveButton.SetRenderOrders(DialogTextOrder, DialogTextOrder);

            CancelButtonHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            FooterHost.AddChild(CancelButtonHost);

            CancelButton = new ButtonComponent("Cancel", GetCancelButtonSize(), DialogFont, HandleCancelClicked, 0f);
            CancelButtonHost.AddComponent(CancelButton);
            CancelButton.SetRenderOrders(DialogTextOrder, DialogTextOrder);

            Enabled = false;
            IsInitialized = true;
        }

        /// <summary>
        /// Shows the dialog.
        /// </summary>
        public void Show() {
            ResetDialogPositioning();
            Enabled = true;
            ShowDialogImmediately();
        }

        /// <summary>
        /// Hides the dialog and clears its input-capture blocker.
        /// </summary>
        public void Hide() {
            ClearDialogBackdrop();
            ResetDialogPositioning();
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
            if (!UpdateDialogFrame(windowWidth, windowHeight)) {
                return;
            }
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
        /// Updates the message text placement inside the dialog panel.
        /// </summary>
        void LayoutMessage() {
            int contentWidth = Math.Max(1, DialogWidth - (GetPanelPaddingPixels() * 2));
            FontTightMetrics messageMetrics = DialogFont.MeasureTight(MessageText.Text);
            MessageText.Size = new int2(contentWidth, Math.Max(1, (int)Math.Ceiling(Math.Max(messageMetrics.Height, DialogFont.LineHeight))));
        }

        /// <summary>
        /// Updates footer button placement within the dialog panel.
        /// </summary>
        void LayoutButtons() {
            int footerWidth = GetFooterBoundsSize().X;
            int footerTop = Math.Max(0, DialogHeight - GetPanelPaddingPixels() - GetFooterHeightPixels());
            int saveButtonY = Math.Max(0, (GetFooterHeightPixels() - GetSaveButtonSize().Y) / 2);
            int dontSaveButtonY = Math.Max(0, (GetFooterHeightPixels() - GetDontSaveButtonSize().Y) / 2);
            int cancelButtonY = Math.Max(0, (GetFooterHeightPixels() - GetCancelButtonSize().Y) / 2);
            int cancelButtonX = footerWidth - GetCancelButtonSize().X;
            int dontSaveButtonX = cancelButtonX - GetFooterButtonSpacingPixels() - GetDontSaveButtonSize().X;
            int saveButtonX = dontSaveButtonX - GetFooterButtonSpacingPixels() - GetSaveButtonSize().X;

            FooterBoundsSurface.Size = GetFooterBoundsSize();
            FooterHost.LocalPosition = new float3(DialogWidth - GetPanelPaddingPixels() - footerWidth, footerTop, 0.2f);
            SaveButtonHost.LocalPosition = new float3(saveButtonX, saveButtonY, 0.2f);
            DontSaveButtonHost.LocalPosition = new float3(dontSaveButtonX, dontSaveButtonY, 0.2f);
            CancelButtonHost.LocalPosition = new float3(cancelButtonX, cancelButtonY, 0.2f);
        }

        /// <summary>
        /// Raises the cancel action when the shared close button is pressed.
        /// </summary>
        protected override void OnCloseRequested() {
            HandleCancelClicked();
        }

        /// <summary>
        /// Repositions the dialog content whenever the shared modal shell position or size changes.
        /// </summary>
        protected override void HandleDialogLayoutChanged() {
            LayoutMessage();
            LayoutButtons();
        }

        /// <summary>
        /// Gets the scaled panel padding used by the dialog.
        /// </summary>
        /// <returns>Scaled panel padding in pixels.</returns>
        int GetPanelPaddingPixels() {
            return DialogMetrics.ScalePixels(PanelPadding);
        }

        /// <summary>
        /// Gets the scaled top position of the message row.
        /// </summary>
        /// <returns>Scaled message-row top position in pixels.</returns>
        int GetMessageTop() {
            return DialogMetrics.ScalePixels(PanelPadding + HeaderHeight + SectionSpacing);
        }

        /// <summary>
        /// Gets the scaled footer button spacing.
        /// </summary>
        /// <returns>Scaled footer button spacing in pixels.</returns>
        int GetFooterButtonSpacingPixels() {
            return DialogMetrics.ScalePixels(FooterButtonSpacing);
        }

        /// <summary>
        /// Gets the scaled footer band height.
        /// </summary>
        /// <returns>Scaled footer height in pixels.</returns>
        int GetFooterHeightPixels() {
            return DialogMetrics.ScalePixels(FooterHeight);
        }

        /// <summary>
        /// Gets the scaled save button size.
        /// </summary>
        /// <returns>Scaled save button size.</returns>
        int2 GetSaveButtonSize() {
            return new int2(DialogMetrics.ScalePixels(SaveButtonSize.X), DialogMetrics.ScalePixels(SaveButtonSize.Y));
        }

        /// <summary>
        /// Gets the scaled don't-save button size.
        /// </summary>
        /// <returns>Scaled don't-save button size.</returns>
        int2 GetDontSaveButtonSize() {
            return new int2(DialogMetrics.ScalePixels(DontSaveButtonSize.X), DialogMetrics.ScalePixels(DontSaveButtonSize.Y));
        }

        /// <summary>
        /// Gets the scaled cancel button size.
        /// </summary>
        /// <returns>Scaled cancel button size.</returns>
        int2 GetCancelButtonSize() {
            return new int2(DialogMetrics.ScalePixels(CancelButtonSize.X), DialogMetrics.ScalePixels(CancelButtonSize.Y));
        }

        /// <summary>
        /// Gets the scaled footer-bounds size used by the anchor host.
        /// </summary>
        /// <returns>Scaled footer bounds size.</returns>
        int2 GetFooterBoundsSize() {
            return new int2(
                GetSaveButtonSize().X + GetDontSaveButtonSize().X + GetCancelButtonSize().X + (GetFooterButtonSpacingPixels() * 2),
                GetFooterHeightPixels());
        }
    }
}
