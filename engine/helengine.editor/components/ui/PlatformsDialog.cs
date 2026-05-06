namespace helengine.editor {
    /// <summary>
    /// Floating modal dialog used to edit project-supported platforms and the explicit active platform.
    /// </summary>
    public sealed class PlatformsDialog : EditorDialogBase {
        /// <summary>
        /// Fixed panel width used by the dialog.
        /// </summary>
        public const int PanelWidth = 420;

        /// <summary>
        /// Fixed panel height used by the dialog.
        /// </summary>
        public const int PanelHeight = 300;

        /// <summary>
        /// Padding applied inside the dialog panel.
        /// </summary>
        public const int PanelPadding = 16;

        /// <summary>
        /// Height reserved for the draggable title bar.
        /// </summary>
        public const int HeaderHeight = 32;

        /// <summary>
        /// Height reserved for the footer buttons.
        /// </summary>
        public const int FooterHeight = 28;

        /// <summary>
        /// Vertical spacing between dialog sections.
        /// </summary>
        public const int SectionSpacing = 10;

        /// <summary>
        /// Height reserved for each platform row.
        /// </summary>
        public const int PlatformRowHeight = 24;

        /// <summary>
        /// Height reserved for the active-platform combo box.
        /// </summary>
        public const int ActivePlatformComboBoxHeight = 24;

        /// <summary>
        /// Width reserved for each platform checkbox.
        /// </summary>
        public const int PlatformCheckBoxSize = 18;

        /// <summary>
        /// Fixed size used for the cancel button.
        /// </summary>
        static readonly int2 CancelButtonBaseSize = new int2(88, 22);

        /// <summary>
        /// Fixed size used for the save button.
        /// </summary>
        static readonly int2 SaveButtonBaseSize = new int2(88, 22);

        /// <summary>
        /// Host entity for the platform-section label.
        /// </summary>
        readonly EditorEntity PlatformsLabelHost;

        /// <summary>
        /// Label text shown above the supported-platform checklist.
        /// </summary>
        readonly TextComponent PlatformsLabelText;

        /// <summary>
        /// Hosts created for each platform label row.
        /// </summary>
        readonly List<EditorEntity> PlatformLabelHosts;

        /// <summary>
        /// Text components used to render the platform labels.
        /// </summary>
        readonly List<TextComponent> PlatformLabelTexts;

        /// <summary>
        /// Hosts created for each platform checkbox row.
        /// </summary>
        readonly List<EditorEntity> PlatformCheckBoxHosts;

        /// <summary>
        /// Checkbox components used to select project-supported platforms.
        /// </summary>
        readonly List<CheckBoxComponent> PlatformCheckBoxes;

        /// <summary>
        /// Host entity for the active-platform label.
        /// </summary>
        readonly EditorEntity ActivePlatformLabelHost;

        /// <summary>
        /// Label text shown above the active-platform combo box.
        /// </summary>
        readonly TextComponent ActivePlatformLabelText;

        /// <summary>
        /// Host entity for the active-platform combo box.
        /// </summary>
        readonly EditorEntity ActivePlatformComboBoxHost;

        /// <summary>
        /// Combo box used to pick the explicit active platform from the enabled platform set.
        /// </summary>
        readonly ComboBoxComponent ActivePlatformComboBox;

        /// <summary>
        /// Host entity for validation or helper text.
        /// </summary>
        readonly EditorEntity StatusHost;

        /// <summary>
        /// Validation or helper text shown above the footer.
        /// </summary>
        readonly TextComponent StatusText;

        /// <summary>
        /// Host entity for the cancel button.
        /// </summary>
        readonly EditorEntity CancelButtonHost;

        /// <summary>
        /// Cancel button component.
        /// </summary>
        readonly ButtonComponent CancelButton;

        /// <summary>
        /// Host entity for the save button.
        /// </summary>
        readonly EditorEntity SaveButtonHost;

        /// <summary>
        /// Save button component.
        /// </summary>
        readonly ButtonComponent SaveButton;

        /// <summary>
        /// Available platform identifiers shown by the dialog in row order.
        /// </summary>
        readonly List<string> AvailablePlatformIds;

        /// <summary>
        /// Enabled platform identifiers currently offered by the active-platform combo box.
        /// </summary>
        readonly List<string> EnabledPlatformIds;

        /// <summary>
        /// Tracks whether the dialog finished initialization.
        /// </summary>
        bool IsInitialized;

        /// <summary>
        /// Raised when the user confirms the selected project platforms.
        /// </summary>
        public event Action<PlatformsSelection> ConfirmRequested;

        /// <summary>
        /// Raised when the user cancels the project-platform workflow.
        /// </summary>
        public event Action CancelRequested;

        /// <summary>
        /// Initializes one project-platforms dialog.
        /// </summary>
        /// <param name="font">Font used for labels and buttons.</param>
        public PlatformsDialog(FontAsset font) : this(font, EditorUiMetrics.Default) {
        }

        /// <summary>
        /// Initializes one project-platforms dialog using one shared metrics source.
        /// </summary>
        /// <param name="font">Font used for labels and buttons.</param>
        /// <param name="metrics">Scaled editor UI metrics used to size the dialog.</param>
        public PlatformsDialog(FontAsset font, EditorUiMetrics metrics)
            : base("PlatformsDialog", "Platforms", font, metrics, PanelWidth, PanelHeight, HeaderHeight) {
            if (font == null) {
                throw new ArgumentNullException(nameof(font));
            }

            DialogIsResizable = false;
            SetDialogMinimumSize(PanelWidth, PanelHeight);

            PlatformLabelHosts = new List<EditorEntity>(8);
            PlatformLabelTexts = new List<TextComponent>(8);
            PlatformCheckBoxHosts = new List<EditorEntity>(8);
            PlatformCheckBoxes = new List<CheckBoxComponent>(8);
            AvailablePlatformIds = new List<string>(8);
            EnabledPlatformIds = new List<string>(8);

            PlatformsLabelHost = CreateInternalHost();
            DialogPanelRoot.AddChild(PlatformsLabelHost);
            PlatformsLabelText = CreateLabelText("Enabled platforms");
            PlatformsLabelHost.AddComponent(PlatformsLabelText);

            ActivePlatformLabelHost = CreateInternalHost();
            DialogPanelRoot.AddChild(ActivePlatformLabelHost);
            ActivePlatformLabelText = CreateLabelText("Active platform");
            ActivePlatformLabelHost.AddComponent(ActivePlatformLabelText);

            ActivePlatformComboBoxHost = CreateInternalHost();
            DialogPanelRoot.AddChild(ActivePlatformComboBoxHost);
            ActivePlatformComboBox = new ComboBoxComponent(GetActivePlatformComboBoxSize(), DialogFont, Array.Empty<string>(), -1);
            ConfigureDialogComboBox(ActivePlatformComboBox);
            ActivePlatformComboBoxHost.AddComponent(ActivePlatformComboBox);

            StatusHost = CreateInternalHost();
            DialogPanelRoot.AddChild(StatusHost);
            StatusText = new TextComponent {
                Font = DialogFont,
                Text = string.Empty,
                Color = ThemeManager.Colors.StateWarning,
                Size = new int2(1, Math.Max(1, (int)Math.Ceiling(Math.Max(DialogFont.LineHeight, 1f)))),
                RenderOrder2D = DialogTextOrder
            };
            StatusHost.AddComponent(StatusText);

            CancelButtonHost = CreateInternalHost();
            DialogPanelRoot.AddChild(CancelButtonHost);
            CancelButton = new ButtonComponent("Cancel", GetFooterButtonSize(CancelButtonBaseSize), DialogFont, HandleCancelClicked, 0f);
            CancelButton.SetRenderOrders(DialogTextOrder, DialogTextOrder);
            CancelButtonHost.AddComponent(CancelButton);

            SaveButtonHost = CreateInternalHost();
            DialogPanelRoot.AddChild(SaveButtonHost);
            SaveButton = new ButtonComponent("Save", GetFooterButtonSize(SaveButtonBaseSize), DialogFont, HandleSaveClicked, 0f);
            SaveButton.SetRenderOrders(DialogTextOrder, DialogTextOrder);
            SaveButtonHost.AddComponent(SaveButton);

            Enabled = false;
            IsInitialized = true;
        }

        /// <summary>
        /// Shows the dialog for the supplied available platforms, enabled project platforms, and explicit active platform.
        /// </summary>
        /// <param name="availablePlatformIds">Available platform identifiers that can be enabled for the current project.</param>
        /// <param name="supportedPlatformIds">Currently enabled project platform identifiers.</param>
        /// <param name="activePlatformId">Current explicit active platform identifier.</param>
        public void Show(IReadOnlyList<string> availablePlatformIds, IReadOnlyList<string> supportedPlatformIds, string activePlatformId) {
            if (availablePlatformIds == null) {
                throw new ArgumentNullException(nameof(availablePlatformIds));
            }
            if (supportedPlatformIds == null) {
                throw new ArgumentNullException(nameof(supportedPlatformIds));
            }
            if (string.IsNullOrWhiteSpace(activePlatformId)) {
                throw new ArgumentException("Active platform id must be provided.", nameof(activePlatformId));
            }

            ResetDialogPositioning();
            Enabled = true;
            StatusText.Text = string.Empty;

            RebuildPlatformRows(availablePlatformIds, supportedPlatformIds);
            RebuildActivePlatformItems(activePlatformId);
            ShowDialogImmediately();
        }

        /// <summary>
        /// Hides the dialog and clears its transient state.
        /// </summary>
        public void Hide() {
            ClearDialogBackdrop();
            ResetDialogPositioning();
            Enabled = false;
            StatusText.Text = string.Empty;
            ActivePlatformComboBox.IsOpen = false;
            ActivePlatformComboBox.SetItems(Array.Empty<string>(), -1);
            ClearPlatformRows();
            AvailablePlatformIds.Clear();
            EnabledPlatformIds.Clear();
        }

        /// <summary>
        /// Updates dialog sizing and layout to fit the current host window dimensions.
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
        /// Validates and raises the confirmed platform selection.
        /// </summary>
        void HandleSaveClicked() {
            List<string> selectedPlatformIds = CollectSelectedPlatformIds();
            if (selectedPlatformIds.Count == 0) {
                StatusText.Text = "Select at least one platform.";
                return;
            }
            if (!ActivePlatformComboBox.HasSelection || !ContainsPlatform(selectedPlatformIds, ActivePlatformComboBox.SelectedItem)) {
                StatusText.Text = "Select an active platform from the enabled platforms.";
                return;
            }

            StatusText.Text = string.Empty;
            if (ConfirmRequested != null) {
                ConfirmRequested(new PlatformsSelection(selectedPlatformIds, ActivePlatformComboBox.SelectedItem));
            }
        }

        /// <summary>
        /// Rebuilds the platform rows for the supplied available and supported platform identifiers.
        /// </summary>
        /// <param name="availablePlatformIds">Available platform identifiers shown in row order.</param>
        /// <param name="supportedPlatformIds">Enabled project platform identifiers.</param>
        void RebuildPlatformRows(IReadOnlyList<string> availablePlatformIds, IReadOnlyList<string> supportedPlatformIds) {
            ClearPlatformRows();
            AvailablePlatformIds.Clear();

            for (int index = 0; index < availablePlatformIds.Count; index++) {
                string platformId = availablePlatformIds[index];
                if (string.IsNullOrWhiteSpace(platformId)) {
                    throw new ArgumentException("Available platform ids must not be blank.", nameof(availablePlatformIds));
                }

                string normalizedPlatformId = platformId.Trim();
                AvailablePlatformIds.Add(normalizedPlatformId);
                CreatePlatformRow(normalizedPlatformId, ContainsPlatform(supportedPlatformIds, normalizedPlatformId));
            }
        }

        /// <summary>
        /// Creates one visual row for the supplied platform identifier.
        /// </summary>
        /// <param name="platformId">Platform identifier rendered by the new row.</param>
        /// <param name="isChecked">True when the row starts enabled.</param>
        void CreatePlatformRow(string platformId, bool isChecked) {
            EditorEntity checkBoxHost = CreateInternalHost();
            DialogPanelRoot.AddChild(checkBoxHost);
            PlatformCheckBoxHosts.Add(checkBoxHost);

            CheckBoxComponent checkBox = new CheckBoxComponent(GetPlatformCheckBoxSize(), DialogFont, isChecked);
            checkBox.CheckedChanged += HandlePlatformCheckBoxChanged;
            checkBoxHost.AddComponent(checkBox);
            checkBox.SetRenderOrders(DialogTextOrder, DialogTextOrder);
            PlatformCheckBoxes.Add(checkBox);

            EditorEntity labelHost = CreateInternalHost();
            DialogPanelRoot.AddChild(labelHost);
            PlatformLabelHosts.Add(labelHost);

            TextComponent labelText = CreateLabelText(platformId);
            labelHost.AddComponent(labelText);
            PlatformLabelTexts.Add(labelText);
        }

        /// <summary>
        /// Rebuilds the active-platform combo box from the currently enabled platform rows.
        /// </summary>
        /// <param name="preferredPlatformId">Preferred active platform identifier to preserve when it remains enabled.</param>
        void RebuildActivePlatformItems(string preferredPlatformId) {
            EnabledPlatformIds.Clear();

            for (int index = 0; index < PlatformCheckBoxes.Count; index++) {
                if (PlatformCheckBoxes[index].IsChecked) {
                    EnabledPlatformIds.Add(AvailablePlatformIds[index]);
                }
            }

            int selectedIndex = ResolveSelectedActivePlatformIndex(preferredPlatformId);
            ActivePlatformComboBox.SetItems(EnabledPlatformIds, selectedIndex);
            ActivePlatformComboBox.IsOpen = false;
        }

        /// <summary>
        /// Resolves the selected index for the active-platform combo box.
        /// </summary>
        /// <param name="preferredPlatformId">Preferred active platform identifier to preserve.</param>
        /// <returns>Selected combo-box index, or -1 when explicit replacement is required.</returns>
        int ResolveSelectedActivePlatformIndex(string preferredPlatformId) {
            if (string.IsNullOrWhiteSpace(preferredPlatformId)) {
                return -1;
            }

            for (int index = 0; index < EnabledPlatformIds.Count; index++) {
                if (string.Equals(EnabledPlatformIds[index], preferredPlatformId, StringComparison.OrdinalIgnoreCase)) {
                    return index;
                }
            }

            return -1;
        }

        /// <summary>
        /// Handles checkbox changes by rebuilding the active-platform combo box without implicit fallback.
        /// </summary>
        /// <param name="checkBox">Checkbox that changed.</param>
        /// <param name="isChecked">New checkbox value.</param>
        void HandlePlatformCheckBoxChanged(CheckBoxComponent checkBox, bool isChecked) {
            string preferredPlatformId = ActivePlatformComboBox.HasSelection ? ActivePlatformComboBox.SelectedItem : string.Empty;
            RebuildActivePlatformItems(preferredPlatformId);
        }

        /// <summary>
        /// Collects the currently enabled platform identifiers in stable row order.
        /// </summary>
        /// <returns>Enabled platform identifiers in row order.</returns>
        List<string> CollectSelectedPlatformIds() {
            List<string> selectedPlatformIds = new List<string>(PlatformCheckBoxes.Count);

            for (int index = 0; index < PlatformCheckBoxes.Count; index++) {
                if (PlatformCheckBoxes[index].IsChecked) {
                    selectedPlatformIds.Add(AvailablePlatformIds[index]);
                }
            }

            return selectedPlatformIds;
        }

        /// <summary>
        /// Removes all dynamic platform rows from the dialog.
        /// </summary>
        void ClearPlatformRows() {
            for (int index = 0; index < PlatformCheckBoxes.Count; index++) {
                PlatformCheckBoxes[index].CheckedChanged -= HandlePlatformCheckBoxChanged;
            }

            for (int index = 0; index < PlatformLabelHosts.Count; index++) {
                DialogPanelRoot.RemoveChild(PlatformLabelHosts[index]);
            }
            for (int index = 0; index < PlatformCheckBoxHosts.Count; index++) {
                DialogPanelRoot.RemoveChild(PlatformCheckBoxHosts[index]);
            }

            PlatformLabelHosts.Clear();
            PlatformLabelTexts.Clear();
            PlatformCheckBoxHosts.Clear();
            PlatformCheckBoxes.Clear();
        }

        /// <summary>
        /// Positions and sizes the dialog content.
        /// </summary>
        void LayoutContent() {
            int contentLeft = GetPanelPaddingPixels();
            int contentWidth = GetContentWidth();
            int platformsLabelTop = GetPlatformsLabelTop();
            int firstPlatformRowTop = platformsLabelTop + GetLabelHeight() + GetSectionSpacingPixels();

            PlatformsLabelHost.Position = new float3(contentLeft, platformsLabelTop, 0.1f);
            PlatformsLabelText.Size = new int2(contentWidth, GetLabelHeight());

            for (int index = 0; index < PlatformCheckBoxHosts.Count; index++) {
                int rowTop = firstPlatformRowTop + (index * GetPlatformRowHeightPixels());
                PlatformCheckBoxHosts[index].Position = new float3(contentLeft, rowTop, 0.1f);
                PlatformLabelHosts[index].Position = new float3(contentLeft + GetPlatformCheckBoxColumnWidthPixels(), rowTop, 0.1f);
                PlatformLabelTexts[index].Size = new int2(contentWidth - GetPlatformCheckBoxColumnWidthPixels(), GetPlatformRowHeightPixels());
            }

            int activeLabelTop = firstPlatformRowTop + (PlatformCheckBoxHosts.Count * GetPlatformRowHeightPixels()) + GetSectionSpacingPixels();
            ActivePlatformLabelHost.Position = new float3(contentLeft, activeLabelTop, 0.1f);
            ActivePlatformLabelText.Size = new int2(contentWidth, GetLabelHeight());

            int activeComboTop = activeLabelTop + GetLabelHeight() + GetComboBoxSpacingPixels();
            ActivePlatformComboBoxHost.Position = new float3(contentLeft, activeComboTop, 0.1f);
            ActivePlatformComboBox.Size = GetActivePlatformComboBoxSize();

            int footerTop = DialogHeight - GetPanelPaddingPixels() - GetFooterHeightPixels();
            int buttonTop = footerTop + Math.Max(0, (GetFooterHeightPixels() - GetFooterButtonSize(CancelButtonBaseSize).Y) / 2);
            int saveButtonX = DialogWidth - GetPanelPaddingPixels() - GetFooterButtonSize(SaveButtonBaseSize).X;
            int cancelButtonX = saveButtonX - GetSectionSpacingPixels() - GetFooterButtonSize(CancelButtonBaseSize).X;

            StatusHost.Position = new float3(contentLeft, footerTop - GetStatusHeightPixels() - GetSectionSpacingPixels(), 0.1f);
            StatusText.Size = new int2(contentWidth, GetStatusHeightPixels());

            CancelButtonHost.Position = new float3(cancelButtonX, buttonTop, 0.1f);
            CancelButton.SetSize(GetFooterButtonSize(CancelButtonBaseSize));

            SaveButtonHost.Position = new float3(saveButtonX, buttonTop, 0.1f);
            SaveButton.SetSize(GetFooterButtonSize(SaveButtonBaseSize));
        }

        /// <summary>
        /// Returns true when the supplied platform identifier exists in the provided list.
        /// </summary>
        /// <param name="platformIds">Platform identifiers to search.</param>
        /// <param name="platformId">Platform identifier to locate.</param>
        /// <returns>True when the identifier exists in the provided list; otherwise false.</returns>
        bool ContainsPlatform(IReadOnlyList<string> platformIds, string platformId) {
            if (platformIds == null || string.IsNullOrWhiteSpace(platformId)) {
                return false;
            }

            for (int index = 0; index < platformIds.Count; index++) {
                if (string.Equals(platformIds[index], platformId, StringComparison.OrdinalIgnoreCase)) {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Creates one internal host entity for dialog-owned content.
        /// </summary>
        /// <returns>Dialog-owned internal host entity.</returns>
        EditorEntity CreateInternalHost() {
            return new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
        }

        /// <summary>
        /// Creates one standard dialog label text component.
        /// </summary>
        /// <param name="text">Initial label text.</param>
        /// <returns>Configured label text component.</returns>
        TextComponent CreateLabelText(string text) {
            return new TextComponent {
                Font = DialogFont,
                Text = text,
                Color = ThemeManager.Colors.InputForegroundPrimary,
                RenderOrder2D = DialogTextOrder
            };
        }

        /// <summary>
        /// Gets the scaled body content width used by the dialog.
        /// </summary>
        /// <returns>Scaled body content width in pixels.</returns>
        int GetContentWidth() {
            return DialogWidth - (GetPanelPaddingPixels() * 2);
        }

        /// <summary>
        /// Gets the scaled top position of the platforms-section label.
        /// </summary>
        /// <returns>Scaled top position for the platforms-section label.</returns>
        int GetPlatformsLabelTop() {
            return DialogMetrics.ScalePixels(PanelPadding + HeaderHeight + SectionSpacing);
        }

        /// <summary>
        /// Gets the scaled padding applied inside the dialog panel.
        /// </summary>
        /// <returns>Scaled panel padding in pixels.</returns>
        int GetPanelPaddingPixels() {
            return DialogMetrics.ScalePixels(PanelPadding);
        }

        /// <summary>
        /// Gets the scaled footer height used by the dialog.
        /// </summary>
        /// <returns>Scaled footer height in pixels.</returns>
        int GetFooterHeightPixels() {
            return DialogMetrics.ScalePixels(FooterHeight);
        }

        /// <summary>
        /// Gets the scaled section spacing used by the dialog.
        /// </summary>
        /// <returns>Scaled section spacing in pixels.</returns>
        int GetSectionSpacingPixels() {
            return DialogMetrics.ScalePixels(SectionSpacing);
        }

        /// <summary>
        /// Gets the scaled height used for each platform row.
        /// </summary>
        /// <returns>Scaled platform row height in pixels.</returns>
        int GetPlatformRowHeightPixels() {
            return DialogMetrics.ScalePixels(PlatformRowHeight);
        }

        /// <summary>
        /// Gets the scaled size used by each platform checkbox.
        /// </summary>
        /// <returns>Scaled checkbox size in pixels.</returns>
        int2 GetPlatformCheckBoxSize() {
            int size = DialogMetrics.ScalePixels(PlatformCheckBoxSize);
            return new int2(size, size);
        }

        /// <summary>
        /// Gets the scaled width reserved for the platform checkbox column.
        /// </summary>
        /// <returns>Scaled checkbox column width in pixels.</returns>
        int GetPlatformCheckBoxColumnWidthPixels() {
            return DialogMetrics.ScalePixels(PlatformCheckBoxSize + 12);
        }

        /// <summary>
        /// Gets the scaled combo-box spacing beneath its label.
        /// </summary>
        /// <returns>Scaled spacing in pixels.</returns>
        int GetComboBoxSpacingPixels() {
            return DialogMetrics.ScalePixels(6);
        }

        /// <summary>
        /// Gets the scaled label height used by the dialog.
        /// </summary>
        /// <returns>Scaled label height in pixels.</returns>
        int GetLabelHeight() {
            return DialogMetrics.ScalePixels(18);
        }

        /// <summary>
        /// Gets the scaled status-text height used above the footer.
        /// </summary>
        /// <returns>Scaled status-text height in pixels.</returns>
        int GetStatusHeightPixels() {
            return DialogMetrics.ScalePixels(18);
        }

        /// <summary>
        /// Gets the scaled size of the active-platform combo box.
        /// </summary>
        /// <returns>Scaled active-platform combo-box size.</returns>
        int2 GetActivePlatformComboBoxSize() {
            return new int2(GetContentWidth(), DialogMetrics.ScalePixels(ActivePlatformComboBoxHeight));
        }

        /// <summary>
        /// Gets the scaled size of one footer button.
        /// </summary>
        /// <param name="baseSize">Unscaled footer button size.</param>
        /// <returns>Scaled footer button size.</returns>
        int2 GetFooterButtonSize(int2 baseSize) {
            return new int2(DialogMetrics.ScalePixels(baseSize.X), DialogMetrics.ScalePixels(baseSize.Y));
        }

        /// <summary>
        /// Repositions the dialog-owned content whenever the shared modal shell position or size changes.
        /// </summary>
        protected override void HandleDialogLayoutChanged() {
            LayoutContent();
        }

        /// <summary>
        /// Raises the cancel path when the shared dialog close button is used.
        /// </summary>
        protected override void OnCloseRequested() {
            HandleCancelClicked();
        }
    }
}
