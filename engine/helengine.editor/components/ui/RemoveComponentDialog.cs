namespace helengine.editor {
    /// <summary>
    /// Floating confirmation dialog shown before removing one component from the selected entity.
    /// </summary>
    public class RemoveComponentDialog : EditorDialogBase {
        /// <summary>
        /// Fixed panel width used by the dialog.
        /// </summary>
        public const int PanelWidth = 420;

        /// <summary>
        /// Fixed panel height used by the dialog.
        /// </summary>
        public const int PanelHeight = 172;

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
        /// Vertical spacing preserved between the dialog sections.
        /// </summary>
        public const int SectionSpacing = 10;

        /// <summary>
        /// Fixed size used for the cancel button.
        /// </summary>
        static readonly int2 CancelButtonSize = new int2(88, 22);

        /// <summary>
        /// Fixed size used for the remove button.
        /// </summary>
        static readonly int2 RemoveButtonSize = new int2(88, 22);

        /// <summary>
        /// Host entity for the dialog message text.
        /// </summary>
        readonly EditorEntity MessageHost;

        /// <summary>
        /// Message text shown above the footer buttons.
        /// </summary>
        readonly TextComponent MessageText;

        /// <summary>
        /// Host entity for the cancel button.
        /// </summary>
        readonly EditorEntity CancelButtonHost;

        /// <summary>
        /// Cancel button component.
        /// </summary>
        readonly ButtonComponent CancelButton;

        /// <summary>
        /// Host entity for the remove button.
        /// </summary>
        readonly EditorEntity RemoveButtonHost;

        /// <summary>
        /// Remove button component.
        /// </summary>
        readonly ButtonComponent RemoveButton;

        /// <summary>
        /// Tracks whether the dialog has completed initialization.
        /// </summary>
        bool IsInitialized;

        /// <summary>
        /// Raised when the user confirms the component removal.
        /// </summary>
        public event Action ConfirmRequested;

        /// <summary>
        /// Raised when the user cancels the component removal.
        /// </summary>
        public event Action CancelRequested;

        /// <summary>
        /// Initializes the remove-component confirmation dialog.
        /// </summary>
        /// <param name="font">Font used by the dialog chrome and body text.</param>
        public RemoveComponentDialog(FontAsset font)
            : base("Remove Component Dialog", "Remove Component", font, PanelWidth, PanelHeight, HeaderHeight) {
            MessageHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            DialogPanelRoot.AddChild(MessageHost);

            MessageText = new TextComponent {
                Font = DialogFont,
                Text = string.Empty,
                Color = ThemeManager.Colors.InputForegroundPrimary,
                Size = new int2(1, GetDialogLineHeight()),
                RenderOrder2D = DialogTextOrder
            };
            MessageHost.AddComponent(MessageText);

            CancelButtonHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            DialogPanelRoot.AddChild(CancelButtonHost);

            CancelButton = new ButtonComponent("Cancel", CancelButtonSize, DialogFont, HandleCancelClicked, 0f);
            CancelButtonHost.AddComponent(CancelButton);
            CancelButton.SetRenderOrders(DialogTextOrder, DialogTextOrder);

            RemoveButtonHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            DialogPanelRoot.AddChild(RemoveButtonHost);

            RemoveButton = new ButtonComponent("Remove", RemoveButtonSize, DialogFont, HandleRemoveClicked, 0f);
            RemoveButtonHost.AddComponent(RemoveButton);
            RemoveButton.SetRenderOrders(DialogTextOrder, DialogTextOrder);

            Enabled = false;
            IsInitialized = true;
        }

        /// <summary>
        /// Shows the dialog for the provided entity and component names.
        /// </summary>
        /// <param name="entityName">Display name of the entity that owns the component.</param>
        /// <param name="componentName">Display name of the component that will be removed.</param>
        public void Show(string entityName, string componentName) {
            if (string.IsNullOrWhiteSpace(entityName)) {
                throw new ArgumentException("An entity name is required.", nameof(entityName));
            }
            if (string.IsNullOrWhiteSpace(componentName)) {
                throw new ArgumentException("A component name is required.", nameof(componentName));
            }

            ResetDialogPositioning();
            MessageText.Text = $"Remove {componentName} from {entityName}?";
            Enabled = true;
        }

        /// <summary>
        /// Hides the dialog and clears its input blocker.
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
        /// Raises the confirmed remove action.
        /// </summary>
        void HandleRemoveClicked() {
            if (ConfirmRequested != null) {
                ConfirmRequested();
            }
        }

        /// <summary>
        /// Raises the cancel action for the dialog.
        /// </summary>
        void HandleCancelClicked() {
            if (CancelRequested != null) {
                CancelRequested();
            }
        }

        /// <summary>
        /// Raises the cancel action when the shared close button is pressed.
        /// </summary>
        void HandleCloseClicked() {
            HandleCancelClicked();
        }

        /// <summary>
        /// Updates the message text placement inside the dialog panel.
        /// </summary>
        void LayoutMessage() {
            int contentWidth = Math.Max(1, PanelWidth - PanelPadding * 2);
            FontTightMetrics messageMetrics = DialogFont.MeasureTight(MessageText.Text);
            int messageTop = PanelPadding + HeaderHeight + SectionSpacing;
            MessageHost.Position = new float3(PanelPadding, messageTop, 0.2f);
            MessageText.Size = new int2(contentWidth, Math.Max(1, (int)Math.Ceiling(Math.Max(messageMetrics.Height, DialogFont.LineHeight))));
        }

        /// <summary>
        /// Updates footer button placement within the dialog panel.
        /// </summary>
        void LayoutButtons() {
            int footerTop = PanelHeight - PanelPadding - FooterHeight;
            int cancelButtonX = PanelWidth - PanelPadding - CancelButtonSize.X;
            int removeButtonX = cancelButtonX - 8 - RemoveButtonSize.X;
            int buttonY = footerTop + Math.Max(0, (FooterHeight - CancelButtonSize.Y) / 2);

            CancelButtonHost.Position = new float3(cancelButtonX, buttonY, 0.2f);
            RemoveButtonHost.Position = new float3(removeButtonX, buttonY, 0.2f);
        }

        /// <summary>
        /// Routes the shared dialog close button to the cancel action.
        /// </summary>
        protected override void OnCloseRequested() {
            HandleCloseClicked();
        }
    }
}
