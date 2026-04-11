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
        public const int PanelHeight = 152;
        /// <summary>
        /// Padding applied inside the dialog panel.
        /// </summary>
        public const int PanelPadding = 16;
        /// <summary>
        /// Height reserved for the footer button row.
        /// </summary>
        public const int FooterHeight = 28;
        /// <summary>
        /// Radius used for the dialog background.
        /// </summary>
        const float PanelRadius = 6f;
        /// <summary>
        /// Border thickness used for the dialog background.
        /// </summary>
        const float PanelBorderThickness = 2f;
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
        /// Render order used for text labels.
        /// </summary>
        readonly byte TextOrder;
        /// <summary>
        /// Cached panel position relative to the host window.
        /// </summary>
        int2 PanelPosition;
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
                Position = float3.Zero
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

            MessageHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero
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
                Position = float3.Zero
            };
            PanelRoot.AddChild(SaveButtonHost);

            SaveButton = new ButtonComponent("Save", SaveButtonSize, font, HandleSaveClicked, 0f);
            SaveButtonHost.AddComponent(SaveButton);
            SaveButton.SetRenderOrders(TextOrder, TextOrder);

            DontSaveButtonHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero
            };
            PanelRoot.AddChild(DontSaveButtonHost);

            DontSaveButton = new ButtonComponent("Don't Save", DontSaveButtonSize, font, HandleDontSaveClicked, 0f);
            DontSaveButtonHost.AddComponent(DontSaveButton);
            DontSaveButton.SetRenderOrders(TextOrder, TextOrder);

            CancelButtonHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero
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
            Enabled = true;
        }

        /// <summary>
        /// Hides the dialog and clears its input-capture blocker.
        /// </summary>
        public void Hide() {
            EditorInputCaptureService.ClearBlocker(this);
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
            PanelPosition = new int2(
                Math.Max(0, (safeWidth - PanelWidth) / 2),
                Math.Max(0, (safeHeight - PanelHeight) / 2));

            PanelRoot.Position = new float3(PanelPosition.X, PanelPosition.Y, 0.1f);
            EditorInputCaptureService.SetBlocker(this, PanelPosition, new int2(PanelWidth, PanelHeight));

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
            int contentWidth = Math.Max(0, PanelWidth - PanelPadding * 2);
            FontTightMetrics messageMetrics = Font.MeasureTight(MessageText.Text);
            MessageHost.Position = new float3(PanelPadding, PanelPadding, 0.2f);
            MessageText.Size = new int2(contentWidth, (int)Math.Ceiling(Math.Max(messageMetrics.Height, Font.LineHeight)));
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
    }
}
