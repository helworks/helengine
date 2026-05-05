using helengine.baseplatform.Definitions;

namespace helengine.editor {
    /// <summary>
    /// Floating modal dialog used to edit per-platform build and graphics profiles.
    /// </summary>
    public class ProfilesDialog : EditorDialogBase {
        /// <summary>
        /// Fixed panel width used by the dialog.
        /// </summary>
        public const int PanelWidth = 560;

        /// <summary>
        /// Fixed panel height used by the dialog.
        /// </summary>
        public const int PanelHeight = 372;

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
        /// Vertical spacing between groups of controls.
        /// </summary>
        public const int SectionSpacing = 10;

        /// <summary>
        /// Height reserved for each form row.
        /// </summary>
        public const int FieldRowHeight = 24;

        /// <summary>
        /// Height reserved for section titles.
        /// </summary>
        public const int SectionTitleHeight = 18;

        /// <summary>
        /// Width reserved for field labels.
        /// </summary>
        public const int LabelColumnWidth = 180;

        /// <summary>
        /// Width reserved for numeric text fields.
        /// </summary>
        public const int NumericFieldWidth = 96;

        /// <summary>
        /// Width reserved for the platform selector combo box.
        /// </summary>
        public const int PlatformComboBoxWidth = 220;

        /// <summary>
        /// Fixed size used for the save button.
        /// </summary>
        static readonly int2 SaveButtonSize = new int2(88, 22);

        /// <summary>
        /// Fixed size used for the cancel button.
        /// </summary>
        static readonly int2 CancelButtonSize = new int2(88, 22);

        /// <summary>
        /// Font used for all dialog labels and controls.
        /// </summary>
        FontAsset DialogFontValue;

        /// <summary>
        /// Host entity for the platform selector label.
        /// </summary>
        readonly EditorEntity PlatformLabelHost;

        /// <summary>
        /// Label describing the platform selector.
        /// </summary>
        readonly TextComponent PlatformLabelText;

        /// <summary>
        /// Host entity for the platform selector control.
        /// </summary>
        readonly EditorEntity PlatformComboBoxHost;

        /// <summary>
        /// Combo box used to switch between per-platform profile records.
        /// </summary>
        readonly ComboBoxComponent PlatformComboBox;

        /// <summary>
        /// Builder-defined build settings rendered for the active platform.
        /// </summary>
        readonly EditorPlatformSettingsSection BuildSettingsSection;

        /// <summary>
        /// Builder-defined graphics settings rendered for the active platform.
        /// </summary>
        readonly EditorPlatformSettingsSection GraphicsSettingsSection;

        /// <summary>
        /// Builder-defined codegen settings rendered for the active platform.
        /// </summary>
        readonly EditorPlatformSettingsSection CodegenSettingsSection;

        /// <summary>
        /// Host entity for the status text.
        /// </summary>
        readonly EditorEntity StatusHost;

        /// <summary>
        /// Status text shown above the footer buttons.
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
        /// Supported platform ids currently shown in the combo box.
        /// </summary>
        readonly List<string> SupportedPlatformIds;

        /// <summary>
        /// Mutable profile document currently being edited.
        /// </summary>
        EditorProfileSettingsDocument CurrentDocument;

        /// <summary>
        /// Builder-provided metadata for the currently active platform.
        /// </summary>
        EditorPlatformBuildSelectionModel ActivePlatformSelectionModel;

        /// <summary>
        /// Currently selected platform id being edited.
        /// </summary>
        string CurrentPlatformId;

        /// <summary>
        /// Tracks whether the platform selection is being updated by code instead of by the user.
        /// </summary>
        bool IsInitializingSelection;

        /// <summary>
        /// Tracks whether the dialog has completed initialization.
        /// </summary>
        bool IsInitialized;

        /// <summary>
        /// Raised when the user confirms the edited profiles.
        /// </summary>
        public event Action<ProfilesDialogSelection> ConfirmRequested;

        /// <summary>
        /// Raised when the user cancels the profiles workflow.
        /// </summary>
        public event Action CancelRequested;

        /// <summary>
        /// Initializes one profiles dialog.
        /// </summary>
        /// <param name="font">Font used for labels and buttons.</param>
        public ProfilesDialog(FontAsset font) : this(font, EditorUiMetrics.Default) {
        }

        /// <summary>
        /// Initializes one profiles dialog using one shared metrics source.
        /// </summary>
        /// <param name="font">Font used for labels and buttons.</param>
        /// <param name="metrics">Scaled editor UI metrics used to size the dialog.</param>
        public ProfilesDialog(FontAsset font, EditorUiMetrics metrics) : base("ProfilesDialog", "Profiles", font, metrics, PanelWidth, PanelHeight, HeaderHeight) {
            if (font == null) {
                throw new ArgumentNullException(nameof(font));
            }

            DialogFontValue = font;
            SetDialogMinimumSize(PanelWidth, PanelHeight);
            SupportedPlatformIds = new List<string>(8);

            PlatformLabelHost = CreateTextHost();
            DialogPanelRoot.AddChild(PlatformLabelHost);
            PlatformLabelText = CreateLabelText("Platform");
            PlatformLabelHost.AddComponent(PlatformLabelText);

            PlatformComboBoxHost = CreateTextHost();
            DialogPanelRoot.AddChild(PlatformComboBoxHost);
            PlatformComboBox = new ComboBoxComponent(GetPlatformComboBoxSize(), DialogFontValue, Array.Empty<string>(), -1);
            PlatformComboBox.SelectionChanged += HandlePlatformSelectionChanged;
            ConfigureDialogComboBox(PlatformComboBox);
            PlatformComboBoxHost.AddComponent(PlatformComboBox);

            int settingValueWidth = GetSettingValueWidth();
            BuildSettingsSection = new EditorPlatformSettingsSection(
                DialogPanelRoot,
                LayerMask,
                DialogFontValue,
                DialogPanelOrder,
                DialogTextOrder,
                GetLabelColumnWidth(),
                settingValueWidth);
            GraphicsSettingsSection = new EditorPlatformSettingsSection(
                DialogPanelRoot,
                LayerMask,
                DialogFontValue,
                DialogPanelOrder,
                DialogTextOrder,
                GetLabelColumnWidth(),
                settingValueWidth);
            CodegenSettingsSection = new EditorPlatformSettingsSection(
                DialogPanelRoot,
                LayerMask,
                DialogFontValue,
                DialogPanelOrder,
                DialogTextOrder,
                GetLabelColumnWidth(),
                settingValueWidth);

            StatusHost = CreateTextHost();
            DialogPanelRoot.AddChild(StatusHost);
            StatusText = new TextComponent {
                Font = DialogFontValue,
                Text = string.Empty,
                Color = ThemeManager.Colors.StateWarning,
                Size = new int2(1, Math.Max(1, (int)Math.Ceiling(Math.Max(DialogFontValue.LineHeight, 1f)))),
                RenderOrder2D = DialogTextOrder
            };
            StatusHost.AddComponent(StatusText);

            CancelButtonHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            DialogPanelRoot.AddChild(CancelButtonHost);
            CancelButton = new ButtonComponent("Cancel", GetFooterButtonSize(), DialogFontValue, HandleCancelClicked, 0f);
            CancelButtonHost.AddComponent(CancelButton);
            CancelButton.SetRenderOrders(DialogTextOrder, DialogTextOrder);

            SaveButtonHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            DialogPanelRoot.AddChild(SaveButtonHost);
            SaveButton = new ButtonComponent("Save", GetFooterButtonSize(), DialogFontValue, HandleSaveClicked, 0f);
            SaveButtonHost.AddComponent(SaveButton);
            SaveButton.SetRenderOrders(DialogTextOrder, DialogTextOrder);

            Enabled = false;
            IsInitialized = true;
        }

        /// <summary>
        /// Shows the dialog for the provided profile document and platform set.
        /// </summary>
        /// <param name="document">Mutable profile settings document for the current project.</param>
        /// <param name="supportedPlatforms">Supported platform identifiers declared by the project.</param>
        /// <param name="activePlatformId">Platform currently being edited.</param>
        public void Show(EditorProfileSettingsDocument document, IReadOnlyList<string> supportedPlatforms, string activePlatformId) {
            Show(document, supportedPlatforms, activePlatformId, null);
        }

        /// <summary>
        /// Shows the dialog for the provided profile document and platform set.
        /// </summary>
        /// <param name="document">Mutable profile settings document for the current project.</param>
        /// <param name="supportedPlatforms">Supported platform identifiers declared by the project.</param>
        /// <param name="activePlatformId">Platform currently being edited.</param>
        /// <param name="selectionModel">Builder-provided metadata for the active platform.</param>
        public void Show(EditorProfileSettingsDocument document, IReadOnlyList<string> supportedPlatforms, string activePlatformId, EditorPlatformBuildSelectionModel selectionModel) {
            if (document == null) {
                throw new ArgumentNullException(nameof(document));
            }
            if (supportedPlatforms == null) {
                throw new ArgumentNullException(nameof(supportedPlatforms));
            }
            if (string.IsNullOrWhiteSpace(activePlatformId)) {
                throw new ArgumentException("Active platform id must be provided.", nameof(activePlatformId));
            }

            int activeIndex = ResolvePlatformIndex(supportedPlatforms, activePlatformId);
            CurrentDocument = document;
            CurrentPlatformId = supportedPlatforms[activeIndex];
            ActivePlatformSelectionModel = selectionModel;

            SupportedPlatformIds.Clear();
            for (int i = 0; i < supportedPlatforms.Count; i++) {
                SupportedPlatformIds.Add(supportedPlatforms[i]);
            }

            ResetDialogPositioning();
            Enabled = true;
            StatusText.Text = string.Empty;

            IsInitializingSelection = true;
            PlatformComboBox.SetItems(SupportedPlatformIds, activeIndex);
            IsInitializingSelection = false;
            LoadSelectedPlatformIntoFields(CurrentPlatformId);
        }

        /// <summary>
        /// Hides the dialog and clears the transient selection state.
        /// </summary>
        public void Hide() {
            ClearDialogBackdrop();
            ResetDialogPositioning();
            Enabled = false;
            StatusText.Text = string.Empty;
            CurrentDocument = null;
            CurrentPlatformId = string.Empty;
            SupportedPlatformIds.Clear();
        }

        /// <summary>
        /// Updates dialog layout to fit the current host window size.
        /// </summary>
        /// <param name="windowWidth">Current host window width.</param>
        /// <param name="windowHeight">Current host window height.</param>
        public void UpdateLayout(int windowWidth, int windowHeight) {
            if (!IsInitialized) {
                return;
            }
            if (!UpdateDialogFrame(windowWidth, windowHeight)) {
                return;
            }

            LayoutPlatformSelector();
            LayoutSettingsSections();
            LayoutStatus();
            LayoutButtons();
        }

        /// <summary>
        /// Handles platform selection changes from the combo box.
        /// </summary>
        /// <param name="index">Selected combo-box row index.</param>
        /// <param name="platformId">Selected platform identifier.</param>
        void HandlePlatformSelectionChanged(int index, string platformId) {
            if (IsInitializingSelection || CurrentDocument == null) {
                return;
            }

            if (!TryStoreCurrentPlatformFields(out string errorMessage)) {
                StatusText.Text = errorMessage;
                IsInitializingSelection = true;
                PlatformComboBox.SelectedIndex = ResolvePlatformIndex(SupportedPlatformIds, CurrentPlatformId);
                IsInitializingSelection = false;
                return;
            }

            CurrentPlatformId = platformId;
            StatusText.Text = string.Empty;
            LoadSelectedPlatformIntoFields(platformId);
        }

        /// <summary>
        /// Handles save button clicks by validating the current fields and raising the confirmed selection.
        /// </summary>
        void HandleSaveClicked() {
            if (!TryStoreCurrentPlatformFields(out string errorMessage)) {
                StatusText.Text = errorMessage;
                return;
            }

            StatusText.Text = string.Empty;
            if (ConfirmRequested != null) {
                ConfirmRequested(new ProfilesDialogSelection(CurrentPlatformId, CurrentDocument));
            }
        }

        /// <summary>
        /// Handles cancel button clicks by raising the cancel action.
        /// </summary>
        void HandleCancelClicked() {
            if (CancelRequested != null) {
                CancelRequested();
            }
        }

        /// <summary>
        /// Loads one platform profile into the visible fields.
        /// </summary>
        /// <param name="platformId">Platform identifier to load.</param>
        void LoadSelectedPlatformIntoFields(string platformId) {
            EditorPlatformProfileSettingsDocument platform = GetPlatformDocument(platformId);
            platform.Build.SelectedOptionValues ??= [];
            platform.Graphics.SelectedOptionValues ??= [];
            platform.Codegen.SelectedOptionValues ??= [];

            PlatformBuildProfileDefinition buildProfile = ResolveBuildProfile(platform);
            PlatformGraphicsProfileDefinition graphicsProfile = ResolveGraphicsProfile(platform, buildProfile);
            PlatformCodegenProfileDefinition codegenProfile = ResolveCodegenProfile(platform, buildProfile);

            platform.Build.SelectedBuildProfileId = buildProfile?.ProfileId ?? platform.Build.SelectedBuildProfileId;
            platform.Graphics.SelectedGraphicsProfileId = graphicsProfile?.ProfileId ?? platform.Graphics.SelectedGraphicsProfileId;
            platform.Codegen.SelectedCodegenProfileId = codegenProfile?.ProfileId ?? platform.Codegen.SelectedCodegenProfileId;

            BuildSettingsSection.Rebuild(
                buildProfile != null ? $"Build Profile: {buildProfile.DisplayName}" : "Build Profiles",
                buildProfile?.Settings,
                platform.Build.SelectedOptionValues);
            GraphicsSettingsSection.Rebuild(
                graphicsProfile != null ? $"Graphics Profile: {graphicsProfile.DisplayName}" : "Graphics Profiles",
                graphicsProfile?.Settings,
                platform.Graphics.SelectedOptionValues);
            CodegenSettingsSection.Rebuild(
                codegenProfile != null ? $"Codegen Profile: {codegenProfile.DisplayName}" : "Codegen Profiles",
                codegenProfile?.Settings,
                platform.Codegen.SelectedOptionValues);
            LayoutSettingsSections();
        }

        /// <summary>
        /// Stores the current field values into the active platform profile.
        /// </summary>
        /// <param name="errorMessage">Validation error message when parsing fails.</param>
        /// <returns>True when the current fields were parsed and stored successfully.</returns>
        bool TryStoreCurrentPlatformFields(out string errorMessage) {
            errorMessage = string.Empty;
            if (CurrentDocument == null) {
                errorMessage = "No profile document is loaded.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(CurrentPlatformId)) {
                errorMessage = "No active platform is selected.";
                return false;
            }

            EditorPlatformProfileSettingsDocument platform = GetPlatformDocument(CurrentPlatformId);
            PlatformBuildProfileDefinition buildProfile = ResolveBuildProfile(platform);
            PlatformGraphicsProfileDefinition graphicsProfile = ResolveGraphicsProfile(platform, buildProfile);

            platform.Build.SelectedBuildProfileId = buildProfile?.ProfileId ?? platform.Build.SelectedBuildProfileId;
            platform.Graphics.SelectedGraphicsProfileId = graphicsProfile?.ProfileId ?? platform.Graphics.SelectedGraphicsProfileId;

            if (!BuildSettingsSection.TryValidate(out errorMessage)) {
                return false;
            }

            if (!GraphicsSettingsSection.TryValidate(out errorMessage)) {
                return false;
            }

            if (!CodegenSettingsSection.TryValidate(out errorMessage)) {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Returns one platform profile record, creating it when the document is missing the record.
        /// </summary>
        /// <param name="platformId">Platform identifier to locate.</param>
        /// <returns>Matching platform profile document.</returns>
        EditorPlatformProfileSettingsDocument GetPlatformDocument(string platformId) {
            if (CurrentDocument == null) {
                throw new InvalidOperationException("The profiles dialog does not have a loaded document.");
            }

            for (int i = 0; i < CurrentDocument.Platforms.Count; i++) {
                EditorPlatformProfileSettingsDocument platform = CurrentDocument.Platforms[i];
                if (platform != null && string.Equals(platform.PlatformId, platformId, StringComparison.OrdinalIgnoreCase)) {
                    if (platform.Build == null) {
                        platform.Build = new EditorBuildProfileSettingsDocument();
                    }
                    if (platform.Graphics == null) {
                        platform.Graphics = new EditorGraphicsProfileSettingsDocument();
                    }
                    if (platform.Codegen == null) {
                        platform.Codegen = new EditorCodegenProfileSettingsDocument();
                    }
                    platform.Build.SelectedOptionValues ??= [];
                    platform.Graphics.SelectedOptionValues ??= [];
                    platform.Codegen.SelectedOptionValues ??= [];
                    platform.Build.SelectedBuildProfileId ??= string.Empty;
                    platform.Graphics.SelectedGraphicsProfileId ??= string.Empty;
                    platform.Codegen.SelectedCodegenProfileId ??= string.Empty;

                    return platform;
                }
            }

            EditorPlatformProfileSettingsDocument createdPlatform = new EditorPlatformProfileSettingsDocument {
                PlatformId = platformId,
                Build = new EditorBuildProfileSettingsDocument(),
                Graphics = new EditorGraphicsProfileSettingsDocument(),
                Codegen = new EditorCodegenProfileSettingsDocument()
            };
            createdPlatform.Build.SelectedOptionValues ??= [];
            createdPlatform.Graphics.SelectedOptionValues ??= [];
            createdPlatform.Codegen.SelectedOptionValues ??= [];
            CurrentDocument.Platforms.Add(createdPlatform);
            return createdPlatform;
        }

        /// <summary>
        /// Resolves the current builder-provided build profile for one platform record.
        /// </summary>
        /// <param name="platform">Persisted platform profile record.</param>
        /// <returns>Resolved build profile metadata, or null when no builder metadata is available.</returns>
        PlatformBuildProfileDefinition ResolveBuildProfile(EditorPlatformProfileSettingsDocument platform) {
            if (ActivePlatformSelectionModel == null || platform == null) {
                return null;
            }

            PlatformBuildProfileDefinition buildProfile = ActivePlatformSelectionModel.ResolveBuildProfile(platform.Build?.SelectedBuildProfileId);
            return buildProfile;
        }

        /// <summary>
        /// Resolves the current builder-provided graphics profile for one platform record.
        /// </summary>
        /// <param name="platform">Persisted platform profile record.</param>
        /// <param name="buildProfile">Currently selected build profile metadata.</param>
        /// <returns>Resolved graphics profile metadata, or null when no builder metadata is available.</returns>
        PlatformGraphicsProfileDefinition ResolveGraphicsProfile(EditorPlatformProfileSettingsDocument platform, PlatformBuildProfileDefinition buildProfile) {
            if (ActivePlatformSelectionModel == null || platform == null) {
                return null;
            }

            string graphicsProfileId = platform.Graphics?.SelectedGraphicsProfileId;
            if (string.IsNullOrWhiteSpace(graphicsProfileId) && buildProfile != null) {
                graphicsProfileId = buildProfile.GraphicsProfileId;
            }

            return ActivePlatformSelectionModel.ResolveGraphicsProfile(graphicsProfileId);
        }

        /// <summary>
        /// Resolves the current builder-provided codegen profile for one platform record.
        /// </summary>
        /// <param name="platform">Persisted platform profile record.</param>
        /// <param name="buildProfile">Currently selected build profile metadata.</param>
        /// <returns>Resolved codegen profile metadata, or null when no builder metadata is available.</returns>
        PlatformCodegenProfileDefinition ResolveCodegenProfile(EditorPlatformProfileSettingsDocument platform, PlatformBuildProfileDefinition buildProfile) {
            if (ActivePlatformSelectionModel == null || platform == null) {
                return null;
            }

            string codegenProfileId = platform.Codegen?.SelectedCodegenProfileId;
            if (string.IsNullOrWhiteSpace(codegenProfileId) && buildProfile != null) {
                codegenProfileId = buildProfile.CodegenProfileId;
            }

            return ActivePlatformSelectionModel.ResolveCodegenProfile(codegenProfileId);
        }

        /// <summary>
        /// Returns the selected index for one platform identifier in the supported list.
        /// </summary>
        /// <param name="platforms">Supported platform identifiers.</param>
        /// <param name="platformId">Platform identifier to locate.</param>
        /// <returns>Zero-based platform index.</returns>
        int ResolvePlatformIndex(IReadOnlyList<string> platforms, string platformId) {
            for (int i = 0; i < platforms.Count; i++) {
                if (string.Equals(platforms[i], platformId, StringComparison.OrdinalIgnoreCase)) {
                    return i;
                }
            }

            throw new InvalidOperationException($"Platform '{platformId}' is not available in the dialog.");
        }

        /// <summary>
        /// Creates one host entity for a left-aligned dialog row.
        /// </summary>
        /// <returns>New dialog host entity.</returns>
        EditorEntity CreateTextHost() {
            return new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
        }

        /// <summary>
        /// Creates one standard label text component for the dialog.
        /// </summary>
        /// <param name="text">Label text to render.</param>
        /// <returns>Configured label text component.</returns>
        TextComponent CreateLabelText(string text) {
            return new TextComponent {
                Font = DialogFontValue,
                Text = text,
                Color = ThemeManager.Colors.InputForegroundPrimary,
                Size = new int2(1, Math.Max(1, (int)Math.Ceiling(Math.Max(DialogFontValue.LineHeight, 1f)))),
                RenderOrder2D = DialogTextOrder
            };
        }

        /// <summary>
        /// Positions the platform selector row.
        /// </summary>
        void LayoutPlatformSelector() {
            float rowY = GetPlatformSelectorTop();
            PlatformLabelHost.Position = new float3(GetPanelPaddingPixels(), rowY + GetPlatformLabelOffsetY(), 0.1f);
            PlatformComboBoxHost.Position = new float3(GetPanelPaddingPixels() + GetLabelColumnWidth() + GetLabelColumnSpacingPixels(), rowY, 0.1f);
        }

        /// <summary>
        /// Positions the builder-defined settings sections beneath the platform selector.
        /// </summary>
        void LayoutSettingsSections() {
            float buildTopY = GetPlatformSelectorTop() + GetFieldRowHeightPixels() + GetSectionSpacingPixels();
            BuildSettingsSection.Root.Position = new float3(GetPanelPaddingPixels(), buildTopY, 0.1f);
            BuildSettingsSection.Layout();

            float graphicsTopY = buildTopY + BuildSettingsSection.ContentHeight + GetSectionSpacingPixels();
            GraphicsSettingsSection.Root.Position = new float3(GetPanelPaddingPixels(), graphicsTopY, 0.1f);
            GraphicsSettingsSection.Layout();

            float codegenTopY = graphicsTopY + GraphicsSettingsSection.ContentHeight + GetSectionSpacingPixels();
            CodegenSettingsSection.Root.Position = new float3(GetPanelPaddingPixels(), codegenTopY, 0.1f);
            CodegenSettingsSection.Layout();
        }

        /// <summary>
        /// Positions the status text above the footer.
        /// </summary>
        void LayoutStatus() {
            float statusY = GetPlatformSelectorTop() + GetFieldRowHeightPixels() + GetSectionSpacingPixels() + BuildSettingsSection.ContentHeight + GetSectionSpacingPixels() + GraphicsSettingsSection.ContentHeight + GetSectionSpacingPixels() + CodegenSettingsSection.ContentHeight + GetStatusTopPadding();
            float footerLimitY = DialogHeight - GetFooterHeightPixels() - GetFooterStatusBottomGap();
            if (statusY > footerLimitY) {
                statusY = footerLimitY;
            }
            StatusHost.Position = new float3(GetPanelPaddingPixels(), statusY, 0.1f);
        }

        /// <summary>
        /// Positions the save and cancel buttons along the footer.
        /// </summary>
        void LayoutButtons() {
            int2 footerButtonSize = GetFooterButtonSize();
            float buttonY = DialogHeight - GetFooterHeightPixels() - GetFooterButtonTopOffset();
            float cancelX = DialogWidth - GetPanelPaddingPixels() - footerButtonSize.X;
            float saveX = cancelX - GetFooterButtonSpacingPixels() - footerButtonSize.X;

            CancelButtonHost.Position = new float3(cancelX, buttonY, 0.1f);
            SaveButtonHost.Position = new float3(saveX, buttonY, 0.1f);
        }

        /// <summary>
        /// Raises the cancel action when the shared close button is pressed.
        /// </summary>
        protected override void OnCloseRequested() {
            HandleCancelClicked();
        }

        /// <summary>
        /// Gets the scaled platform-selector top position.
        /// </summary>
        /// <returns>Scaled selector top position in pixels.</returns>
        float GetPlatformSelectorTop() {
            return DialogMetrics.ScalePixels(HeaderHeight + PanelPadding);
        }

        /// <summary>
        /// Gets the scaled panel padding used by the dialog.
        /// </summary>
        /// <returns>Scaled panel padding in pixels.</returns>
        int GetPanelPaddingPixels() {
            return DialogMetrics.ScalePixels(PanelPadding);
        }

        /// <summary>
        /// Gets the scaled label column width used by settings rows.
        /// </summary>
        /// <returns>Scaled label column width in pixels.</returns>
        int GetLabelColumnWidth() {
            return DialogMetrics.ScalePixels(LabelColumnWidth);
        }

        /// <summary>
        /// Gets the scaled width available to the setting value column.
        /// </summary>
        /// <returns>Scaled setting-value width in pixels.</returns>
        int GetSettingValueWidth() {
            return DialogWidth - (GetPanelPaddingPixels() * 2) - GetLabelColumnWidth() - GetLabelColumnSpacingPixels();
        }

        /// <summary>
        /// Gets the scaled spacing between the label and selector columns.
        /// </summary>
        /// <returns>Scaled label-column spacing in pixels.</returns>
        int GetLabelColumnSpacingPixels() {
            return DialogMetrics.ScalePixels(12);
        }

        /// <summary>
        /// Gets the scaled field row height used by the platform selector.
        /// </summary>
        /// <returns>Scaled field row height in pixels.</returns>
        int GetFieldRowHeightPixels() {
            return DialogMetrics.ScalePixels(FieldRowHeight);
        }

        /// <summary>
        /// Gets the scaled spacing between dialog sections.
        /// </summary>
        /// <returns>Scaled section spacing in pixels.</returns>
        int GetSectionSpacingPixels() {
            return DialogMetrics.ScalePixels(SectionSpacing);
        }

        /// <summary>
        /// Gets the scaled vertical offset applied to the platform label.
        /// </summary>
        /// <returns>Scaled label offset in pixels.</returns>
        int GetPlatformLabelOffsetY() {
            return DialogMetrics.ScalePixels(2);
        }

        /// <summary>
        /// Gets the scaled footer button size.
        /// </summary>
        /// <returns>Scaled footer button size.</returns>
        int2 GetFooterButtonSize() {
            return new int2(DialogMetrics.ScalePixels(SaveButtonSize.X), DialogMetrics.ScalePixels(SaveButtonSize.Y));
        }

        /// <summary>
        /// Gets the scaled footer button spacing.
        /// </summary>
        /// <returns>Scaled footer button spacing in pixels.</returns>
        int GetFooterButtonSpacingPixels() {
            return DialogMetrics.ScalePixels(10);
        }

        /// <summary>
        /// Gets the scaled footer band height.
        /// </summary>
        /// <returns>Scaled footer band height in pixels.</returns>
        int GetFooterHeightPixels() {
            return DialogMetrics.ScalePixels(FooterHeight);
        }

        /// <summary>
        /// Gets the scaled top offset used to vertically center footer buttons.
        /// </summary>
        /// <returns>Scaled footer button top offset in pixels.</returns>
        int GetFooterButtonTopOffset() {
            return DialogMetrics.ScalePixels(2);
        }

        /// <summary>
        /// Gets the scaled top padding applied before the status row.
        /// </summary>
        /// <returns>Scaled status-row top padding in pixels.</returns>
        int GetStatusTopPadding() {
            return DialogMetrics.ScalePixels(8);
        }

        /// <summary>
        /// Gets the scaled gap preserved between the status row and footer.
        /// </summary>
        /// <returns>Scaled status/footer gap in pixels.</returns>
        int GetFooterStatusBottomGap() {
            return DialogMetrics.ScalePixels(38);
        }

        /// <summary>
        /// Gets the scaled platform-selector combo-box size.
        /// </summary>
        /// <returns>Scaled combo-box size.</returns>
        int2 GetPlatformComboBoxSize() {
            return new int2(DialogMetrics.ScalePixels(PlatformComboBoxWidth), GetFieldRowHeightPixels());
        }
    }
}
