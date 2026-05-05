namespace helengine.editor {
    /// <summary>
    /// Compact overlay dialog that lets the user choose a source platform before copying build settings.
    /// </summary>
    public sealed class BuildDialogCopySettingsDialog : EditorDialogBase {
        /// <summary>
        /// Fixed panel width used by the dialog.
        /// </summary>
        public const int PanelWidth = 360;
        /// <summary>
        /// Fixed panel height used by the dialog.
        /// </summary>
        public const int PanelHeight = 200;
        /// <summary>
        /// Padding applied inside the dialog panel.
        /// </summary>
        public const int PanelPadding = 16;
        /// <summary>
        /// Vertical spacing between dialog sections.
        /// </summary>
        public const int SectionSpacing = 10;
        /// <summary>
        /// Height reserved for the draggable title bar.
        /// </summary>
        public const int HeaderHeight = 32;
        /// <summary>
        /// Height reserved for the footer action buttons.
        /// </summary>
        public const int FooterHeight = 28;
        /// <summary>
        /// Height reserved for the source-platform combo box.
        /// </summary>
        public const int SourceComboHeight = 24;
        /// <summary>
        /// Horizontal spacing between the confirm and cancel buttons.
        /// </summary>
        public const int ButtonSpacing = 8;
        /// <summary>
        /// Host entity for the source-platform label.
        /// </summary>
        readonly EditorEntity SourceLabelHost;
        /// <summary>
        /// Label text that describes the source-platform combo box.
        /// </summary>
        readonly TextComponent SourceLabelText;
        /// <summary>
        /// Host entity for the source-platform combo box.
        /// </summary>
        readonly EditorEntity SourceComboHost;
        /// <summary>
        /// Combo box used to choose the source platform.
        /// </summary>
        readonly ComboBoxComponent SourceComboBox;
        /// <summary>
        /// Host entity for the copy button.
        /// </summary>
        readonly EditorEntity CopyButtonHost;
        /// <summary>
        /// Button used to confirm the currently selected source platform.
        /// </summary>
        readonly ButtonComponent CopyButton;
        /// <summary>
        /// Host entity for the cancel button.
        /// </summary>
        readonly EditorEntity CancelButtonHost;
        /// <summary>
        /// Button used to close the chooser without applying a copy.
        /// </summary>
        readonly ButtonComponent CancelButton;
        /// <summary>
        /// Host entity for the empty-state message.
        /// </summary>
        readonly EditorEntity EmptyStateHost;
        /// <summary>
        /// Empty-state message shown when no source platform is available.
        /// </summary>
        readonly TextComponent EmptyStateText;
        /// <summary>
        /// Backing list of source platform ids shown in the combo box.
        /// </summary>
        readonly List<string> SourcePlatformIds;
        /// <summary>
        /// Tracks whether the modal finished initialization.
        /// </summary>
        bool IsInitialized;
        /// <summary>
        /// Raised when the user confirms one source platform.
        /// </summary>
        public event Action<string> ConfirmRequested;
        /// <summary>
        /// Raised when the user closes the chooser without confirming a selection.
        /// </summary>
        public event Action CancelRequested;
        /// <summary>
        /// Initializes a new copy-settings chooser modal.
        /// </summary>
        /// <param name="font">Font used for labels and buttons.</param>
        public BuildDialogCopySettingsDialog(FontAsset font) : this(font, EditorUiMetrics.Default) {
        }

        /// <summary>
        /// Initializes a new copy-settings chooser modal using one shared metrics source.
        /// </summary>
        /// <param name="font">Font used for labels and buttons.</param>
        /// <param name="metrics">Scaled editor UI metrics used to size the dialog.</param>
        public BuildDialogCopySettingsDialog(FontAsset font, EditorUiMetrics metrics) : base("BuildDialogCopySettingsDialog", "Copy Settings", font, metrics, PanelWidth, PanelHeight, HeaderHeight) {
            if (font == null) {
                throw new ArgumentNullException(nameof(font));
            }

            DialogIsResizable = false;
            SetDialogMinimumSize(PanelWidth, PanelHeight);

            SourcePlatformIds = new List<string>(8);

            SourceLabelHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            DialogPanelRoot.AddChild(SourceLabelHost);

            SourceLabelText = new TextComponent {
                Font = DialogFont,
                Text = "Copy settings from",
                Color = ThemeManager.Colors.InputForegroundPrimary,
                RenderOrder2D = DialogTextOrder
            };
            SourceLabelHost.AddComponent(SourceLabelText);

            SourceComboHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            DialogPanelRoot.AddChild(SourceComboHost);

            SourceComboBox = new ComboBoxComponent(GetSourceComboBoxSize(), DialogFont, Array.Empty<string>(), -1);
            ConfigureDialogComboBox(SourceComboBox);
            SourceComboHost.AddComponent(SourceComboBox);

            CopyButtonHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            DialogPanelRoot.AddChild(CopyButtonHost);

            CopyButton = new ButtonComponent("Copy", new int2(1, GetFooterHeightPixels()), DialogFont, HandleCopyButtonClicked);
            CopyButton.SetRenderOrders(DialogPanelOrder, DialogTextOrder);
            CopyButtonHost.AddComponent(CopyButton);

            CancelButtonHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            DialogPanelRoot.AddChild(CancelButtonHost);

            CancelButton = new ButtonComponent("Cancel", new int2(1, GetFooterHeightPixels()), DialogFont, HandleCancelClicked);
            CancelButton.SetRenderOrders(DialogPanelOrder, DialogTextOrder);
            CancelButtonHost.AddComponent(CancelButton);

            EmptyStateHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            DialogPanelRoot.AddChild(EmptyStateHost);

            EmptyStateText = new TextComponent {
                Font = DialogFont,
                Text = "No other platforms are available to copy from.",
                Color = ThemeManager.Colors.AccentQuaternary,
                RenderOrder2D = DialogTextOrder
            };
            EmptyStateHost.AddComponent(EmptyStateText);

            Enabled = false;
            IsInitialized = true;
        }
        /// <summary>
        /// Shows the chooser with the supplied source-platform list.
        /// </summary>
        /// <param name="sourcePlatformIds">Platform ids available to copy from.</param>
        public void Show(IReadOnlyList<string> sourcePlatformIds) {
            if (sourcePlatformIds == null) {
                throw new ArgumentNullException(nameof(sourcePlatformIds));
            }

            SourcePlatformIds.Clear();
            for (int index = 0; index < sourcePlatformIds.Count; index++) {
                string sourcePlatformId = sourcePlatformIds[index];
                if (string.IsNullOrWhiteSpace(sourcePlatformId)) {
                    throw new ArgumentException("Source platform ids must not be blank.", nameof(sourcePlatformIds));
                }

                SourcePlatformIds.Add(sourcePlatformId);
            }

            ResetDialogPositioning();
            Enabled = true;
            CenterDialogIfNeeded();
            UpdateDialogChromeLayout();
            SourceComboBox.IsOpen = false;
            RebuildSourceItems();
            LayoutContent();
        }
        /// <summary>
        /// Hides the chooser and clears transient state.
        /// </summary>
        public void Hide() {
            SourceComboBox.IsOpen = false;
            ClearDialogBackdrop();
            ResetDialogPositioning();
            Enabled = false;
        }
        /// <summary>
        /// Updates the chooser layout to match the host window dimensions.
        /// </summary>
        /// <param name="windowWidth">Current host width.</param>
        /// <param name="windowHeight">Current host height.</param>
        public void UpdateLayout(int windowWidth, int windowHeight) {
            if (!IsInitialized) {
                return;
            }

            if (!UpdateDialogFrame(windowWidth, windowHeight)) {
                return;
            }

            LayoutContent();
        }
        /// <summary>
        /// Rebuilds the source-platform combo box and updates the empty-state visibility.
        /// </summary>
        void RebuildSourceItems() {
            int selectedIndex = SourcePlatformIds.Count > 0 ? 0 : -1;
            SourceComboBox.SetItems(SourcePlatformIds, selectedIndex);
            UpdateActionState();
        }
        /// <summary>
        /// Positions and sizes the modal content.
        /// </summary>
        void LayoutContent() {
            int contentWidth = GetContentWidth();
            int labelY = GetLabelTop();
            int comboY = labelY + GetLabelHeight() + GetLabelToComboSpacing();
            int buttonY = DialogHeight - GetPanelPaddingPixels() - GetFooterHeightPixels();
            int buttonWidth = Math.Max(1, (contentWidth - GetButtonSpacingPixels()) / 2);

            SourceLabelHost.Position = new float3(GetPanelPaddingPixels(), labelY, 0.1f);
            SourceLabelText.Size = new int2(contentWidth, GetLabelHeight());

            SourceComboHost.Position = new float3(GetPanelPaddingPixels(), comboY, 0.1f);
            SourceComboBox.Size = GetSourceComboBoxSize();

            EmptyStateHost.Position = new float3(GetPanelPaddingPixels(), comboY + GetEmptyStateTopOffset(), 0.1f);
            EmptyStateText.Size = GetSourceComboBoxSize();

            CopyButtonHost.Position = new float3(GetPanelPaddingPixels(), buttonY, 0.1f);
            CopyButton.SetSize(new int2(buttonWidth, GetFooterHeightPixels()));

            CancelButtonHost.Position = new float3(GetPanelPaddingPixels() + buttonWidth + GetButtonSpacingPixels(), buttonY, 0.1f);
            CancelButton.SetSize(new int2(buttonWidth, GetFooterHeightPixels()));

            UpdateActionState();
        }
        /// <summary>
        /// Updates button and content visibility based on whether any source platform is available.
        /// </summary>
        void UpdateActionState() {
            bool hasSourcePlatforms = SourcePlatformIds.Count > 0;
            SourceComboHost.Enabled = hasSourcePlatforms;
            CopyButtonHost.Enabled = hasSourcePlatforms;
            EmptyStateHost.Enabled = !hasSourcePlatforms;
        }
        /// <summary>
        /// Raises the confirm event for the current selection.
        /// </summary>
        void HandleCopyButtonClicked() {
            if (!SourceComboBox.HasSelection) {
                return;
            }

            ConfirmRequested?.Invoke(SourceComboBox.SelectedItem);
        }
        /// <summary>
        /// Hides the chooser and raises the cancel event.
        /// </summary>
        void HandleCancelClicked() {
            Hide();
            CancelRequested?.Invoke();
        }

        /// <summary>
        /// Raises the cancel path when the shared dialog close button is used.
        /// </summary>
        protected override void OnCloseRequested() {
            HandleCancelClicked();
        }

        /// <summary>
        /// Gets the scaled content width used by the dialog body.
        /// </summary>
        /// <returns>Scaled body content width in pixels.</returns>
        int GetContentWidth() {
            return DialogWidth - (GetPanelPaddingPixels() * 2);
        }

        /// <summary>
        /// Gets the scaled top position of the source label row.
        /// </summary>
        /// <returns>Scaled source-label top position in pixels.</returns>
        int GetLabelTop() {
            return DialogMetrics.ScalePixels(PanelPadding + HeaderHeight + SectionSpacing);
        }

        /// <summary>
        /// Gets the scaled label height used by the dialog.
        /// </summary>
        /// <returns>Scaled label height in pixels.</returns>
        int GetLabelHeight() {
            return DialogMetrics.ScalePixels(18);
        }

        /// <summary>
        /// Gets the scaled spacing between the label and combo box.
        /// </summary>
        /// <returns>Scaled spacing in pixels.</returns>
        int GetLabelToComboSpacing() {
            return DialogMetrics.ScalePixels(6);
        }

        /// <summary>
        /// Gets the scaled empty-state offset beneath the combo row.
        /// </summary>
        /// <returns>Scaled empty-state top offset in pixels.</returns>
        int GetEmptyStateTopOffset() {
            return DialogMetrics.ScalePixels(4);
        }

        /// <summary>
        /// Gets the scaled panel padding used by the dialog body.
        /// </summary>
        /// <returns>Scaled panel padding in pixels.</returns>
        int GetPanelPaddingPixels() {
            return DialogMetrics.ScalePixels(PanelPadding);
        }

        /// <summary>
        /// Gets the scaled footer button height.
        /// </summary>
        /// <returns>Scaled footer button height in pixels.</returns>
        int GetFooterHeightPixels() {
            return DialogMetrics.ScalePixels(FooterHeight);
        }

        /// <summary>
        /// Gets the scaled spacing between footer buttons.
        /// </summary>
        /// <returns>Scaled button spacing in pixels.</returns>
        int GetButtonSpacingPixels() {
            return DialogMetrics.ScalePixels(ButtonSpacing);
        }

        /// <summary>
        /// Gets the scaled combo-box size used by the dialog.
        /// </summary>
        /// <returns>Scaled combo-box size.</returns>
        int2 GetSourceComboBoxSize() {
            return new int2(GetContentWidth(), DialogMetrics.ScalePixels(SourceComboHeight));
        }
    }
}
