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
        readonly AnchorComponent MessageAnchor;

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
        readonly AnchorComponent FooterAnchor;

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
        public UnsavedChangesDialog(FontAsset font) : base("UnsavedChangesDialog", "Unsaved Changes", font, PanelWidth, PanelHeight, HeaderHeight) {
            DialogMinimumSize = new int2(PanelWidth, PanelHeight);

            MessageHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            DialogPanelRoot.AddChild(MessageHost);
            MessageAnchor = new AnchorComponent();
            MessageHost.AddComponent(MessageAnchor);
            MessageAnchor.SetAnchorDistances(left: PanelPadding, top: PanelPadding + HeaderHeight + SectionSpacing);

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
                Size = new int2(SaveButtonSize.X + DontSaveButtonSize.X + CancelButtonSize.X + FooterButtonSpacing * 2, FooterHeight)
            };
            FooterHost.AddComponent(FooterBoundsSurface);
            FooterAnchor = new AnchorComponent();
            FooterHost.AddComponent(FooterAnchor);
            FooterAnchor.SetAnchorDistances(right: PanelPadding, bottom: PanelPadding);

            SaveButtonHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            FooterHost.AddChild(SaveButtonHost);

            SaveButton = new ButtonComponent("Save", SaveButtonSize, DialogFont, HandleSaveClicked, 0f);
            SaveButtonHost.AddComponent(SaveButton);
            SaveButton.SetRenderOrders(DialogTextOrder, DialogTextOrder);

            DontSaveButtonHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            FooterHost.AddChild(DontSaveButtonHost);

            DontSaveButton = new ButtonComponent("Don't Save", DontSaveButtonSize, DialogFont, HandleDontSaveClicked, 0f);
            DontSaveButtonHost.AddComponent(DontSaveButton);
            DontSaveButton.SetRenderOrders(DialogTextOrder, DialogTextOrder);

            CancelButtonHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            FooterHost.AddChild(CancelButtonHost);

            CancelButton = new ButtonComponent("Cancel", CancelButtonSize, DialogFont, HandleCancelClicked, 0f);
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
        /// Updates the message text placement inside the dialog panel.
        /// </summary>
        void LayoutMessage() {
            int contentWidth = Math.Max(1, DialogWidth - PanelPadding * 2);
            FontTightMetrics messageMetrics = DialogFont.MeasureTight(MessageText.Text);
            MessageText.Size = new int2(contentWidth, Math.Max(1, (int)Math.Ceiling(Math.Max(messageMetrics.Height, DialogFont.LineHeight))));
        }

        /// <summary>
        /// Updates footer button placement within the dialog panel.
        /// </summary>
        void LayoutButtons() {
            int footerWidth = SaveButtonSize.X + DontSaveButtonSize.X + CancelButtonSize.X + FooterButtonSpacing * 2;
            int footerTop = Math.Max(0, DialogHeight - PanelPadding - FooterHeight);
            int saveButtonY = Math.Max(0, (FooterHeight - SaveButtonSize.Y) / 2);
            int dontSaveButtonY = Math.Max(0, (FooterHeight - DontSaveButtonSize.Y) / 2);
            int cancelButtonY = Math.Max(0, (FooterHeight - CancelButtonSize.Y) / 2);
            int cancelButtonX = footerWidth - CancelButtonSize.X;
            int dontSaveButtonX = cancelButtonX - FooterButtonSpacing - DontSaveButtonSize.X;
            int saveButtonX = dontSaveButtonX - FooterButtonSpacing - SaveButtonSize.X;

            FooterBoundsSurface.Size = new int2(footerWidth, FooterHeight);
            FooterHost.LocalPosition = new float3(DialogWidth - PanelPadding - footerWidth, footerTop, 0.2f);
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
    }
}
