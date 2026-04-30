using helengine.platforms;

namespace helengine.editor {
    /// <summary>
    /// Floating modal dialog used to change the supported build platforms for the current project.
    /// </summary>
    public class BuildSettingsDialog : EditorDialogBase {
        /// <summary>
        /// Fixed panel width used by the dialog.
        /// </summary>
        public const int PanelWidth = 420;

        /// <summary>
        /// Fixed panel height used by the dialog.
        /// </summary>
        public const int PanelHeight = 236;

        /// <summary>
        /// Padding applied inside the dialog panel.
        /// </summary>
        public const int PanelPadding = 16;

        /// <summary>
        /// Vertical spacing between dialog sections.
        /// </summary>
        public const int SectionSpacing = 10;

        /// <summary>
        /// Height reserved for each platform row.
        /// </summary>
        public const int PlatformRowHeight = 24;

        /// <summary>
        /// Height reserved for the footer buttons.
        /// </summary>
        public const int FooterHeight = 28;

        /// <summary>
        /// Height reserved for the draggable title bar.
        /// </summary>
        public const int HeaderHeight = 32;

        /// <summary>
        /// Fixed size used for the cancel button.
        /// </summary>
        static readonly int2 CancelButtonSize = new int2(88, 22);

        /// <summary>
        /// Fixed size used for the save button.
        /// </summary>
        static readonly int2 SaveButtonSize = new int2(88, 22);

        /// <summary>
        /// Fixed size used for each platform checkbox.
        /// </summary>
        static readonly int2 CheckBoxSize = new int2(18, 18);

        /// <summary>
        /// Host entity for validation or empty-state text.
        /// </summary>
        readonly EditorEntity StatusHost;

        /// <summary>
        /// Validation or empty-state text shown above the footer.
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
        /// Hosts created for each platform label row.
        /// </summary>
        readonly List<EditorEntity> PlatformLabelHosts;

        /// <summary>
        /// Text components used to render the platform names.
        /// </summary>
        readonly List<TextComponent> PlatformLabelTexts;

        /// <summary>
        /// Hosts created for each platform checkbox row.
        /// </summary>
        readonly List<EditorEntity> PlatformCheckBoxHosts;

        /// <summary>
        /// Checkbox components used to select supported platforms.
        /// </summary>
        readonly List<CheckBoxComponent> PlatformCheckBoxes;

        /// <summary>
        /// Platform descriptors currently shown by the dialog.
        /// </summary>
        readonly List<AvailablePlatformDescriptor> AvailablePlatforms;

        /// <summary>
        /// Tracks whether the dialog has completed initialization.
        /// </summary>
        bool IsInitialized;

        /// <summary>
        /// Raised when the user confirms one supported-platform selection.
        /// </summary>
        public event Action<BuildSettingsSelection> ConfirmRequested;

        /// <summary>
        /// Raised when the user cancels the build-settings workflow.
        /// </summary>
        public event Action CancelRequested;

        /// <summary>
        /// Initializes a new build-settings dialog.
        /// </summary>
        /// <param name="font">Font used for labels and buttons.</param>
        public BuildSettingsDialog(FontAsset font) : base("BuildSettingsDialog", "Build Platforms", font, PanelWidth, PanelHeight, HeaderHeight) {
            PlatformLabelHosts = new List<EditorEntity>(8);
            PlatformLabelTexts = new List<TextComponent>(8);
            PlatformCheckBoxHosts = new List<EditorEntity>(8);
            PlatformCheckBoxes = new List<CheckBoxComponent>(8);
            AvailablePlatforms = new List<AvailablePlatformDescriptor>(8);

            StatusHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            DialogPanelRoot.AddChild(StatusHost);

            StatusText = new TextComponent {
                Font = DialogFont,
                Text = string.Empty,
                Color = ThemeManager.Colors.StateWarning,
                Size = new int2(1, Math.Max(1, (int)Math.Ceiling(Math.Max(DialogFont.LineHeight, 1f)))),
                RenderOrder2D = DialogTextOrder
            };
            StatusHost.AddComponent(StatusText);

            CancelButtonHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            DialogPanelRoot.AddChild(CancelButtonHost);

            CancelButton = new ButtonComponent("Cancel", CancelButtonSize, DialogFont, HandleCancelClicked, 0f);
            CancelButtonHost.AddComponent(CancelButton);
            CancelButton.SetRenderOrders(DialogTextOrder, DialogTextOrder);

            SaveButtonHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            DialogPanelRoot.AddChild(SaveButtonHost);

            SaveButton = new ButtonComponent("Save", SaveButtonSize, DialogFont, HandleSaveClicked, 0f);
            SaveButtonHost.AddComponent(SaveButton);
            SaveButton.SetRenderOrders(DialogTextOrder, DialogTextOrder);

            Enabled = false;
            IsInitialized = true;
        }

        /// <summary>
        /// Shows the dialog for the provided available and currently supported platforms.
        /// </summary>
        /// <param name="availablePlatforms">Selectable platforms discovered for the current engine environment.</param>
        /// <param name="supportedPlatforms">Platforms currently written into the project file.</param>
        public void Show(IReadOnlyList<AvailablePlatformDescriptor> availablePlatforms, IReadOnlyList<string> supportedPlatforms) {
            if (availablePlatforms == null) {
                throw new ArgumentNullException(nameof(availablePlatforms));
            }
            if (supportedPlatforms == null) {
                throw new ArgumentNullException(nameof(supportedPlatforms));
            }

            ResetDialogPositioning();
            Enabled = true;
            StatusText.Text = string.Empty;

            RebuildPlatformRows(availablePlatforms, supportedPlatforms);
            ApplyEmptyPlatformMessage();
        }

        /// <summary>
        /// Hides the dialog and clears its input-capture blocker.
        /// </summary>
        public void Hide() {
            ClearDialogBackdrop();
            ResetDialogPositioning();
            Enabled = false;
            StatusText.Text = string.Empty;
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
            LayoutPlatformRows();
            LayoutStatus();
            LayoutButtons();
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

            StatusText.Text = string.Empty;
            if (ConfirmRequested != null) {
                ConfirmRequested(new BuildSettingsSelection(selectedPlatformIds));
            }
        }

        /// <summary>
        /// Rebuilds the platform rows for the current available-platform set.
        /// </summary>
        /// <param name="availablePlatforms">Selectable available platforms.</param>
        /// <param name="supportedPlatforms">Currently supported project platforms.</param>
        void RebuildPlatformRows(IReadOnlyList<AvailablePlatformDescriptor> availablePlatforms, IReadOnlyList<string> supportedPlatforms) {
            ClearPlatformRows();
            AvailablePlatforms.Clear();

            for (int platformIndex = 0; platformIndex < availablePlatforms.Count; platformIndex++) {
                AvailablePlatformDescriptor platform = availablePlatforms[platformIndex];
                AvailablePlatforms.Add(platform);
                CreatePlatformRow(platform, IsSupportedPlatform(platform.Id, supportedPlatforms));
            }
        }

        /// <summary>
        /// Applies the empty-state message when no selectable platforms are available.
        /// </summary>
        void ApplyEmptyPlatformMessage() {
            if (AvailablePlatforms.Count == 0) {
                StatusText.Text = "No installed platforms are available for this engine.";
                return;
            }

            StatusText.Text = string.Empty;
        }

        /// <summary>
        /// Removes all existing platform row entities and clears the backing lists.
        /// </summary>
        void ClearPlatformRows() {
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
        /// Creates one platform label row and matching checkbox.
        /// </summary>
        /// <param name="platform">Platform descriptor to render.</param>
        /// <param name="isChecked">True when the platform should start selected.</param>
        void CreatePlatformRow(AvailablePlatformDescriptor platform, bool isChecked) {
            EditorEntity labelHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            DialogPanelRoot.AddChild(labelHost);
            PlatformLabelHosts.Add(labelHost);

            TextComponent labelText = new TextComponent {
                Font = DialogFont,
                Text = platform.DisplayName,
                Color = ThemeManager.Colors.InputForegroundPrimary,
                Size = new int2(1, Math.Max(1, (int)Math.Ceiling(Math.Max(DialogFont.LineHeight, 1f)))),
                RenderOrder2D = DialogTextOrder
            };
            labelHost.AddComponent(labelText);
            PlatformLabelTexts.Add(labelText);

            EditorEntity checkBoxHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            DialogPanelRoot.AddChild(checkBoxHost);
            PlatformCheckBoxHosts.Add(checkBoxHost);

            CheckBoxComponent checkBox = new CheckBoxComponent(CheckBoxSize, DialogFont, isChecked);
            checkBoxHost.AddComponent(checkBox);
            checkBox.SetRenderOrders(DialogTextOrder, DialogTextOrder);
            PlatformCheckBoxes.Add(checkBox);
        }

        /// <summary>
        /// Determines whether one platform id exists in the currently supported list.
        /// </summary>
        /// <param name="platformId">Platform id to locate.</param>
        /// <param name="supportedPlatforms">Current supported-platform ids.</param>
        /// <returns>True when the platform id is supported.</returns>
        bool IsSupportedPlatform(string platformId, IReadOnlyList<string> supportedPlatforms) {
            for (int index = 0; index < supportedPlatforms.Count; index++) {
                if (string.Equals(supportedPlatforms[index], platformId, StringComparison.Ordinal)) {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Collects the selected platform ids using the visible row order.
        /// </summary>
        /// <returns>Selected platform ids in row order.</returns>
        List<string> CollectSelectedPlatformIds() {
            List<string> selectedPlatformIds = new List<string>(PlatformCheckBoxes.Count);

            for (int index = 0; index < PlatformCheckBoxes.Count; index++) {
                if (PlatformCheckBoxes[index].IsChecked) {
                    selectedPlatformIds.Add(AvailablePlatforms[index].Id);
                }
            }

            return selectedPlatformIds;
        }

        /// <summary>
        /// Positions each visible platform row.
        /// </summary>
        void LayoutPlatformRows() {
            int rowsTop = PanelPadding + HeaderHeight + SectionSpacing;
            int checkBoxX = PanelWidth - PanelPadding - CheckBoxSize.X;
            int labelYAdjust = Math.Max(0, (PlatformRowHeight - GetDialogLineHeight()) / 2);
            int checkBoxYAdjust = Math.Max(0, (PlatformRowHeight - CheckBoxSize.Y) / 2);

            for (int index = 0; index < PlatformLabelHosts.Count; index++) {
                int rowTop = rowsTop + (PlatformRowHeight * index);
                PlatformLabelHosts[index].Position = new float3(PanelPadding, rowTop + labelYAdjust, 0f);
                PlatformCheckBoxHosts[index].Position = new float3(checkBoxX, rowTop + checkBoxYAdjust, 0f);
            }
        }

        /// <summary>
        /// Positions the validation and empty-state text.
        /// </summary>
        void LayoutStatus() {
            int rowsTop = PanelPadding + HeaderHeight + SectionSpacing;
            int rowsHeight = PlatformRowHeight * Math.Max(1, AvailablePlatforms.Count);
            int statusTop = rowsTop + rowsHeight + SectionSpacing;
            StatusHost.Position = new float3(PanelPadding, statusTop, 0f);
        }

        /// <summary>
        /// Positions the footer buttons.
        /// </summary>
        void LayoutButtons() {
            int footerTop = PanelHeight - PanelPadding - FooterHeight;
            int cancelX = PanelWidth - PanelPadding - SaveButtonSize.X - SectionSpacing - CancelButtonSize.X;
            int saveX = PanelWidth - PanelPadding - SaveButtonSize.X;
            int buttonY = footerTop + Math.Max(0, (FooterHeight - SaveButtonSize.Y) / 2);

            CancelButtonHost.Position = new float3(cancelX, buttonY, 0f);
            SaveButtonHost.Position = new float3(saveX, buttonY, 0f);
        }

        /// <summary>
        /// Raises the cancel action when the shared close button is pressed.
        /// </summary>
        protected override void OnCloseRequested() {
            HandleCancelClicked();
        }
    }
}
