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
        readonly FontAsset DialogFontValue;

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
        public ProfilesDialog(FontAsset font) : base("ProfilesDialog", "Profiles", font, PanelWidth, PanelHeight, HeaderHeight) {
            if (font == null) {
                throw new ArgumentNullException(nameof(font));
            }

            DialogFontValue = font;
            SupportedPlatformIds = new List<string>(8);

            PlatformLabelHost = CreateTextHost();
            DialogPanelRoot.AddChild(PlatformLabelHost);
            PlatformLabelText = CreateLabelText("Platform");
            PlatformLabelHost.AddComponent(PlatformLabelText);

            PlatformComboBoxHost = CreateTextHost();
            DialogPanelRoot.AddChild(PlatformComboBoxHost);
            PlatformComboBox = new ComboBoxComponent(new int2(PlatformComboBoxWidth, FieldRowHeight), DialogFontValue, Array.Empty<string>(), -1);
            PlatformComboBox.SelectionChanged += HandlePlatformSelectionChanged;
            PlatformComboBox.SetRenderOrders(DialogPanelOrder, DialogTextOrder, RenderOrder2D.ModalBackground, RenderOrder2D.ModalForeground);
            PlatformComboBoxHost.AddComponent(PlatformComboBox);

            int settingValueWidth = PanelWidth - (PanelPadding * 2) - LabelColumnWidth - 12;
            BuildSettingsSection = new EditorPlatformSettingsSection(
                DialogPanelRoot,
                LayerMask,
                DialogFontValue,
                DialogPanelOrder,
                DialogTextOrder,
                LabelColumnWidth,
                settingValueWidth);
            GraphicsSettingsSection = new EditorPlatformSettingsSection(
                DialogPanelRoot,
                LayerMask,
                DialogFontValue,
                DialogPanelOrder,
                DialogTextOrder,
                LabelColumnWidth,
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
            CancelButton = new ButtonComponent("Cancel", CancelButtonSize, DialogFontValue, HandleCancelClicked, 0f);
            CancelButtonHost.AddComponent(CancelButton);
            CancelButton.SetRenderOrders(DialogTextOrder, DialogTextOrder);

            SaveButtonHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            DialogPanelRoot.AddChild(SaveButtonHost);
            SaveButton = new ButtonComponent("Save", SaveButtonSize, DialogFontValue, HandleSaveClicked, 0f);
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

            PlatformBuildProfileDefinition buildProfile = ResolveBuildProfile(platform);
            PlatformGraphicsProfileDefinition graphicsProfile = ResolveGraphicsProfile(platform, buildProfile);

            platform.Build.SelectedBuildProfileId = buildProfile?.ProfileId ?? platform.Build.SelectedBuildProfileId;
            platform.Graphics.SelectedGraphicsProfileId = graphicsProfile?.ProfileId ?? platform.Graphics.SelectedGraphicsProfileId;

            BuildSettingsSection.Rebuild(
                buildProfile != null ? $"Build Profile: {buildProfile.DisplayName}" : "Build Profiles",
                buildProfile?.Settings,
                platform.Build.SelectedOptionValues);
            GraphicsSettingsSection.Rebuild(
                graphicsProfile != null ? $"Graphics Profile: {graphicsProfile.DisplayName}" : "Graphics Profiles",
                graphicsProfile?.Settings,
                platform.Graphics.SelectedOptionValues);
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
                    platform.Build.SelectedOptionValues ??= [];
                    platform.Graphics.SelectedOptionValues ??= [];
                    platform.Build.SelectedBuildProfileId ??= string.Empty;
                    platform.Graphics.SelectedGraphicsProfileId ??= string.Empty;

                    return platform;
                }
            }

            EditorPlatformProfileSettingsDocument createdPlatform = new EditorPlatformProfileSettingsDocument {
                PlatformId = platformId,
                Build = new EditorBuildProfileSettingsDocument(),
                Graphics = new EditorGraphicsProfileSettingsDocument()
            };
            createdPlatform.Build.SelectedOptionValues ??= [];
            createdPlatform.Graphics.SelectedOptionValues ??= [];
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
            float rowY = HeaderHeight + PanelPadding;
            PlatformLabelHost.Position = new float3(PanelPadding, rowY + 2f, 0.1f);
            PlatformComboBoxHost.Position = new float3(PanelPadding + LabelColumnWidth + 12f, rowY, 0.1f);
        }

        /// <summary>
        /// Positions the builder-defined settings sections beneath the platform selector.
        /// </summary>
        void LayoutSettingsSections() {
            float buildTopY = HeaderHeight + PanelPadding + FieldRowHeight + SectionSpacing;
            BuildSettingsSection.Root.Position = new float3(PanelPadding, buildTopY, 0.1f);
            BuildSettingsSection.Layout();

            float graphicsTopY = buildTopY + BuildSettingsSection.ContentHeight + SectionSpacing;
            GraphicsSettingsSection.Root.Position = new float3(PanelPadding, graphicsTopY, 0.1f);
            GraphicsSettingsSection.Layout();
        }

        /// <summary>
        /// Positions the status text above the footer.
        /// </summary>
        void LayoutStatus() {
            float statusY = HeaderHeight + PanelPadding + FieldRowHeight + SectionSpacing + BuildSettingsSection.ContentHeight + SectionSpacing + GraphicsSettingsSection.ContentHeight + 8f;
            float footerLimitY = PanelHeight - FooterHeight - 38f;
            if (statusY > footerLimitY) {
                statusY = footerLimitY;
            }
            StatusHost.Position = new float3(PanelPadding, statusY, 0.1f);
        }

        /// <summary>
        /// Positions the save and cancel buttons along the footer.
        /// </summary>
        void LayoutButtons() {
            float buttonY = PanelHeight - FooterHeight - 2f;
            CancelButtonHost.Position = new float3(PanelWidth - PanelPadding - CancelButtonSize.X, buttonY, 0.1f);
            SaveButtonHost.Position = new float3(PanelWidth - PanelPadding - CancelButtonSize.X - 10f - SaveButtonSize.X, buttonY, 0.1f);
        }

        /// <summary>
        /// Raises the cancel action when the shared close button is pressed.
        /// </summary>
        protected override void OnCloseRequested() {
            HandleCancelClicked();
        }
    }
}
