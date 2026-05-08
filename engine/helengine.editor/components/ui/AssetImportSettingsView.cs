namespace helengine.editor {
    /// <summary>
    /// Presents editable importer and processor settings inside the properties panel.
    /// </summary>
    public class AssetImportSettingsView {
        /// <summary>
        /// Vertical spacing between stacked rows.
        /// </summary>
        const int RowSpacing = 6;
        /// <summary>
        /// Height of combo box and button controls.
        /// </summary>
        const int ControlHeight = 24;
        /// <summary>
        /// Fixed width for the apply button.
        /// </summary>
        const int ApplyButtonWidth = 96;
        /// <summary>
        /// Fixed width for platform tab buttons.
        /// </summary>
        const int PlatformTabWidth = 88;
        /// <summary>
        /// Horizontal spacing between platform tab buttons.
        /// </summary>
        const int PlatformTabSpacing = 6;
        /// <summary>
        /// Square size of the flip-winding checkbox control.
        /// </summary>
        const int FlipWindingCheckBoxSize = 24;
        /// <summary>
        /// Label text shown above the importer selection.
        /// </summary>
        const string ImporterLabel = "Importer";
        /// <summary>
        /// Label text shown above processor settings.
        /// </summary>
        const string ProcessorLabel = "Processor";
        /// <summary>
        /// Label text used for the model winding processor setting.
        /// </summary>
        const string FlipWindingLabel = "Flip Winding";
        /// <summary>
        /// Status prefix used for feedback messages.
        /// </summary>
        const string StatusPrefix = "Status:";

        /// <summary>
        /// Font used for text elements.
        /// </summary>
        readonly FontAsset Font;
        /// <summary>
        /// Render order used for label and status text.
        /// </summary>
        readonly byte TextOrder;
        /// <summary>
        /// Root entity that owns view visuals.
        /// </summary>
        readonly EditorEntity RootEntity;
        /// <summary>
        /// Host entity for the importer label text.
        /// </summary>
        readonly EditorEntity ImporterLabelHost;
        /// <summary>
        /// Text component used to render the importer label.
        /// </summary>
        readonly TextComponent ImporterLabelText;
        /// <summary>
        /// Host entity for the combo box control.
        /// </summary>
        readonly EditorEntity ComboHost;
        /// <summary>
        /// Combo box used to pick the importer identifier.
        /// </summary>
        readonly ComboBoxComponent ComboBox;
        /// <summary>
        /// Host entity for the processor section label.
        /// </summary>
        readonly EditorEntity ProcessorLabelHost;
        /// <summary>
        /// Text component used to render the processor section label.
        /// </summary>
        readonly TextComponent ProcessorLabelText;
        /// <summary>
        /// Host entity for the platform tab row.
        /// </summary>
        readonly EditorEntity PlatformTabsHost;
        /// <summary>
        /// Host entities for platform tab buttons.
        /// </summary>
        readonly List<EditorEntity> PlatformTabButtonHosts;
        /// <summary>
        /// Platform tab buttons used to change the active processor platform.
        /// </summary>
        readonly List<TabComponent> PlatformTabButtons;
        /// <summary>
        /// Supported platform identifiers shown in the current tab row.
        /// </summary>
        readonly List<string> SupportedPlatformIds;
        /// <summary>
        /// Host entity for the flip-winding label.
        /// </summary>
        readonly EditorEntity FlipWindingLabelHost;
        /// <summary>
        /// Text component used to render the flip-winding label.
        /// </summary>
        readonly TextComponent FlipWindingLabelText;
        /// <summary>
        /// Host entity for the flip-winding checkbox control.
        /// </summary>
        readonly EditorEntity FlipWindingCheckBoxHost;
        /// <summary>
        /// Checkbox used to edit the flip-winding processor setting on the selected platform.
        /// </summary>
        readonly CheckBoxComponent FlipWindingCheckBox;
        /// <summary>
        /// Host entity for the apply button.
        /// </summary>
        readonly EditorEntity ApplyHost;
        /// <summary>
        /// Button used to apply the pending settings.
        /// </summary>
        readonly ButtonComponent ApplyButton;
        /// <summary>
        /// Host entity for the status text.
        /// </summary>
        readonly EditorEntity StatusHost;
        /// <summary>
        /// Text component used to render status messages.
        /// </summary>
        readonly TextComponent StatusText;
        /// <summary>
        /// Registered importer identifiers shown in the combo box.
        /// </summary>
        readonly List<string> ImporterIds;

        /// <summary>
        /// Current applied importer identifier for the selected asset.
        /// </summary>
        string ActiveImporterId;
        /// <summary>
        /// Pending importer identifier selected in the combo box.
        /// </summary>
        string PendingImporterId;
        /// <summary>
        /// Current applied processor settings for the selected asset.
        /// </summary>
        AssetProcessorSettings ActiveProcessorSettings;
        /// <summary>
        /// Pending processor settings edited in the view.
        /// </summary>
        AssetProcessorSettings PendingProcessorSettings;
        /// <summary>
        /// Currently selected processor platform tab.
        /// </summary>
        string CurrentPlatformId;
        /// <summary>
        /// Currently displayed asset kind.
        /// </summary>
        AssetEntryKind CurrentEntryKind;
        /// <summary>
        /// Cached height of the view in pixels.
        /// </summary>
        int LayoutHeightValue;
        /// <summary>
        /// Tracks whether the view is visible.
        /// </summary>
        bool IsVisibleValue;

        /// <summary>
        /// Raised when the user clicks apply with pending importer or processor changes.
        /// </summary>
        public event Action<AssetImportSettingsApplyRequest> ApplyRequested;

        /// <summary>
        /// Initializes a new view for asset import settings.
        /// </summary>
        /// <param name="font">Font used for text rendering.</param>
        /// <param name="layerMask">Layer mask applied to the view entities.</param>
        public AssetImportSettingsView(FontAsset font, ushort layerMask) {
            if (font == null) {
                throw new ArgumentNullException(nameof(font));
            }

            Font = font;
            ImporterIds = new List<string>(8);
            SupportedPlatformIds = new List<string>(4);
            PlatformTabButtonHosts = new List<EditorEntity>(4);
            PlatformTabButtons = new List<TabComponent>(4);
            TextOrder = RenderOrder2D.PanelForeground;

            RootEntity = new EditorEntity();
            RootEntity.LayerMask = layerMask;
            RootEntity.InternalEntity = true;

            ImporterLabelHost = new EditorEntity();
            ImporterLabelHost.LayerMask = layerMask;
            RootEntity.AddChild(ImporterLabelHost);

            ImporterLabelText = new TextComponent();
            ImporterLabelText.Font = font;
            ImporterLabelText.Text = ImporterLabel;
            ImporterLabelText.Color = ThemeManager.Colors.InputForegroundPrimary;
            ImporterLabelText.RenderOrder2D = TextOrder;
            ImporterLabelHost.AddComponent(ImporterLabelText);

            ComboHost = new EditorEntity();
            ComboHost.LayerMask = layerMask;
            RootEntity.AddChild(ComboHost);

            ComboBox = new ComboBoxComponent(new int2(160, ControlHeight), font, Array.Empty<string>(), -1);
            ComboBox.SelectionChanged += HandleComboSelectionChanged;
            ComboHost.AddComponent(ComboBox);

            ProcessorLabelHost = new EditorEntity();
            ProcessorLabelHost.LayerMask = layerMask;
            RootEntity.AddChild(ProcessorLabelHost);

            ProcessorLabelText = new TextComponent();
            ProcessorLabelText.Font = font;
            ProcessorLabelText.Text = ProcessorLabel;
            ProcessorLabelText.Color = ThemeManager.Colors.InputForegroundPrimary;
            ProcessorLabelText.RenderOrder2D = TextOrder;
            ProcessorLabelHost.AddComponent(ProcessorLabelText);

            PlatformTabsHost = new EditorEntity();
            PlatformTabsHost.LayerMask = layerMask;
            RootEntity.AddChild(PlatformTabsHost);

            FlipWindingLabelHost = new EditorEntity();
            FlipWindingLabelHost.LayerMask = layerMask;
            RootEntity.AddChild(FlipWindingLabelHost);

            FlipWindingLabelText = new TextComponent();
            FlipWindingLabelText.Font = font;
            FlipWindingLabelText.Text = FlipWindingLabel;
            FlipWindingLabelText.Color = ThemeManager.Colors.InputForegroundPrimary;
            FlipWindingLabelText.RenderOrder2D = TextOrder;
            FlipWindingLabelHost.AddComponent(FlipWindingLabelText);

            FlipWindingCheckBoxHost = new EditorEntity();
            FlipWindingCheckBoxHost.LayerMask = layerMask;
            RootEntity.AddChild(FlipWindingCheckBoxHost);

            FlipWindingCheckBox = new CheckBoxComponent(new int2(FlipWindingCheckBoxSize, FlipWindingCheckBoxSize), font);
            FlipWindingCheckBox.CheckedChanged += (component, isChecked) => HandleFlipWindingCheckedChanged(isChecked);
            FlipWindingCheckBoxHost.AddComponent(FlipWindingCheckBox);

            ApplyHost = new EditorEntity();
            ApplyHost.LayerMask = layerMask;
            RootEntity.AddChild(ApplyHost);

            ApplyButton = new ButtonComponent("Apply", new int2(ApplyButtonWidth, ControlHeight), font, HandleApplyClicked);
            ApplyHost.AddComponent(ApplyButton);

            StatusHost = new EditorEntity();
            StatusHost.LayerMask = layerMask;
            RootEntity.AddChild(StatusHost);

            StatusText = new TextComponent();
            StatusText.Font = font;
            StatusText.Text = string.Empty;
            StatusText.Color = ThemeManager.Colors.InputForegroundSecondary;
            StatusText.RenderOrder2D = TextOrder;
            StatusHost.AddComponent(StatusText);

            Hide();
        }

        /// <summary>
        /// Gets the root entity to attach into the properties panel.
        /// </summary>
        public EditorEntity Root => RootEntity;

        /// <summary>
        /// Gets the current height of the view layout.
        /// </summary>
        public int Height => LayoutHeightValue;

        /// <summary>
        /// Gets a value indicating whether the view is visible.
        /// </summary>
        public bool IsVisible => IsVisibleValue;

        /// <summary>
        /// Gets the number of platform tabs shown in the processor section.
        /// </summary>
        public int PlatformTabCount => SupportedPlatformIds.Count;

        /// <summary>
        /// Gets the currently selected processor platform identifier.
        /// </summary>
        public string SelectedPlatformId => CurrentPlatformId;

        /// <summary>
        /// Gets a value indicating whether model processor controls are visible.
        /// </summary>
        public bool IsModelProcessorVisible => CurrentEntryKind == AssetEntryKind.Model;

        /// <summary>
        /// Gets the current pending flip-winding value for the selected platform.
        /// </summary>
        public bool CurrentFlipWindingValue => GetPendingPlatformSettings(CurrentPlatformId).Model.FlipWinding;

        /// <summary>
        /// Shows the view with the provided importer list, current settings, and supported platforms.
        /// </summary>
        /// <param name="importerIds">Registered importer identifiers.</param>
        /// <param name="settings">Current asset settings to edit.</param>
        /// <param name="supportedPlatforms">Project-supported platform identifiers.</param>
        /// <param name="activePlatformId">Currently active platform identifier.</param>
        /// <param name="entryKind">Kind of asset entry being edited.</param>
        public void Show(
            IReadOnlyList<string> importerIds,
            AssetImportSettings settings,
            IReadOnlyList<string> supportedPlatforms,
            string activePlatformId,
            AssetEntryKind entryKind) {
            if (importerIds == null) {
                throw new ArgumentNullException(nameof(importerIds));
            } else if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            } else if (settings.Importer == null) {
                throw new InvalidOperationException("Asset settings must include importer settings.");
            } else if (supportedPlatforms == null) {
                throw new ArgumentNullException(nameof(supportedPlatforms));
            } else if (supportedPlatforms.Count == 0) {
                throw new ArgumentException("At least one platform must be provided.", nameof(supportedPlatforms));
            } else if (string.IsNullOrWhiteSpace(activePlatformId)) {
                throw new ArgumentException("Active platform id must be provided.", nameof(activePlatformId));
            }

            SetImporterIds(importerIds);
            SetSupportedPlatforms(supportedPlatforms);

            ActiveImporterId = settings.Importer.ImporterId;
            PendingImporterId = settings.Importer.ImporterId;
            ActiveProcessorSettings = CloneProcessorSettings(settings.Processor);
            PendingProcessorSettings = CloneProcessorSettings(settings.Processor);
            EnsurePlatformSettingsExist(ActiveProcessorSettings);
            EnsurePlatformSettingsExist(PendingProcessorSettings);
            CurrentPlatformId = ResolveSelectedPlatformId(activePlatformId);
            CurrentEntryKind = entryKind;

            int selectedIndex = FindImporterIndex(ActiveImporterId);
            ComboBox.SetItems(ImporterIds, selectedIndex);
            ComboBox.IsOpen = false;

            RebuildPlatformTabs();

            IsVisibleValue = true;
            RootEntity.Enabled = true;
            UpdateControlState();
            UpdateStatusText();
        }

        /// <summary>
        /// Hides the view and clears its status.
        /// </summary>
        public void Hide() {
            IsVisibleValue = false;
            RootEntity.Enabled = false;
            ComboBox.IsOpen = false;
            StatusText.Text = string.Empty;
            SupportedPlatformIds.Clear();
            CurrentPlatformId = string.Empty;
        }

        /// <summary>
        /// Updates the view layout within the properties panel.
        /// </summary>
        /// <param name="left">Left offset in pixels.</param>
        /// <param name="top">Top offset in pixels.</param>
        /// <param name="width">Available width for controls.</param>
        public void UpdateLayout(int left, int top, int width) {
            if (!IsVisibleValue) {
                return;
            }

            if (width <= 0) {
                throw new ArgumentOutOfRangeException(nameof(width), "Layout width must be positive.");
            }

            RootEntity.Position = new float3(left, top, 0.2f);

            double lineHeight = Math.Max((double)Font.LineHeight, 1.0);
            int labelHeight = (int)Math.Ceiling(lineHeight);
            int currentTop = 0;

            ImporterLabelHost.Position = new float3(0f, currentTop, 0.1f);
            ImporterLabelText.Size = new int2(width, labelHeight);

            currentTop += labelHeight + RowSpacing;
            ComboHost.Position = new float3(0f, currentTop, 0.1f);
            ComboBox.Size = new int2(width, ControlHeight);

            currentTop += ControlHeight + RowSpacing;
            ProcessorLabelHost.Position = new float3(0f, currentTop, 0.1f);
            ProcessorLabelText.Size = new int2(width, labelHeight);

            currentTop += labelHeight + RowSpacing;
            PlatformTabsHost.Position = new float3(0f, currentTop, 0.1f);
            UpdatePlatformTabLayout();

            currentTop += ControlHeight + RowSpacing;
            if (IsModelProcessorVisible) {
                int labelOffsetY = (int)Math.Round((ControlHeight - labelHeight) / 2d);
                FlipWindingLabelHost.Position = new float3(0f, currentTop + labelOffsetY, 0.1f);
                FlipWindingLabelText.Size = new int2(width, labelHeight);

                FlipWindingCheckBoxHost.Position = new float3(Math.Max(0, width - FlipWindingCheckBoxSize), currentTop, 0.1f);
                currentTop += ControlHeight + RowSpacing;
            }

            ApplyHost.Position = new float3(0f, currentTop, 0.1f);

            currentTop += ControlHeight + RowSpacing;
            StatusHost.Position = new float3(0f, currentTop, 0.1f);
            StatusText.Size = new int2(width, labelHeight);

            LayoutHeightValue = currentTop + labelHeight;
        }

        /// <summary>
        /// Handles combo box selection changes.
        /// </summary>
        /// <param name="index">Selected index.</param>
        /// <param name="value">Selected importer identifier.</param>
        void HandleComboSelectionChanged(int index, string value) {
            if (string.IsNullOrWhiteSpace(value)) {
                throw new InvalidOperationException("Importer selection was not provided.");
            }

            PendingImporterId = value;
            UpdateStatusText();
        }

        /// <summary>
        /// Handles clicks on one processor platform tab.
        /// </summary>
        /// <param name="platformId">Platform identifier represented by the clicked tab.</param>
        void HandlePlatformTabClicked(string platformId) {
            if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            } else if (!SupportedPlatformIds.Contains(platformId, StringComparer.OrdinalIgnoreCase)) {
                throw new InvalidOperationException("The requested platform tab is not available.");
            }

            CurrentPlatformId = platformId;
            UpdateControlState();
            UpdateStatusText();
        }

        /// <summary>
        /// Applies one flip-winding checkbox value to the currently selected platform.
        /// </summary>
        /// <param name="isChecked">Checkbox value to apply.</param>
        void HandleFlipWindingCheckedChanged(bool isChecked) {
            AssetPlatformProcessorSettings platformSettings = GetPendingPlatformSettings(CurrentPlatformId);
            platformSettings.Model.FlipWinding = isChecked;
            UpdateControlState();
            UpdateStatusText();
        }

        /// <summary>
        /// Handles apply button clicks by raising the apply event when needed.
        /// </summary>
        void HandleApplyClicked() {
            if (!HasPendingChange()) {
                return;
            }

            if (ApplyRequested != null) {
                AssetImportSettingsApplyRequest request = new AssetImportSettingsApplyRequest(
                    PendingImporterId,
                    CurrentPlatformId,
                    CloneProcessorSettings(PendingProcessorSettings));
                ApplyRequested(request);
            }
        }

        /// <summary>
        /// Updates processor control visibility and state for the selected platform.
        /// </summary>
        void UpdateControlState() {
            ProcessorLabelHost.Enabled = true;
            PlatformTabsHost.Enabled = SupportedPlatformIds.Count > 0;

            bool showModelProcessor = IsModelProcessorVisible;
            FlipWindingLabelHost.Enabled = showModelProcessor;
            FlipWindingCheckBoxHost.Enabled = showModelProcessor;

            if (showModelProcessor) {
                FlipWindingCheckBox.IsChecked = GetPendingPlatformSettings(CurrentPlatformId).Model.FlipWinding;
            }

            UpdatePlatformTabVisualState();
        }

        /// <summary>
        /// Updates the status message based on current selections.
        /// </summary>
        void UpdateStatusText() {
            if (ImporterIds.Count == 0) {
                StatusText.Text = $"{StatusPrefix} No importers registered.";
                return;
            }

            bool activeValid = IsImporterValid(ActiveImporterId);
            bool pendingValid = IsImporterValid(PendingImporterId);

            if (!activeValid) {
                StatusText.Text = $"{StatusPrefix} Current importer is not registered.";
                return;
            }

            if (!pendingValid) {
                StatusText.Text = $"{StatusPrefix} Select an importer.";
                return;
            }

            if (HasPendingChange()) {
                StatusText.Text = $"{StatusPrefix} Pending asset setting changes.";
                return;
            }

            StatusText.Text = string.Empty;
        }

        /// <summary>
        /// Checks whether any importer or processor state differs from the current applied settings.
        /// </summary>
        /// <returns>True when pending changes exist.</returns>
        bool HasPendingChange() {
            if (!string.Equals(ActiveImporterId, PendingImporterId, StringComparison.OrdinalIgnoreCase)) {
                return true;
            }

            return !ProcessorSettingsMatch(ActiveProcessorSettings, PendingProcessorSettings);
        }

        /// <summary>
        /// Determines whether an importer identifier is available in the list.
        /// </summary>
        /// <param name="importerId">Importer identifier to test.</param>
        /// <returns>True when the identifier exists in the list.</returns>
        bool IsImporterValid(string importerId) {
            if (string.IsNullOrWhiteSpace(importerId)) {
                return false;
            }

            return FindImporterIndex(importerId) >= 0;
        }

        /// <summary>
        /// Finds the index for a given importer identifier.
        /// </summary>
        /// <param name="importerId">Importer identifier to locate.</param>
        /// <returns>Index of the importer, or -1 when missing.</returns>
        int FindImporterIndex(string importerId) {
            for (int i = 0; i < ImporterIds.Count; i++) {
                if (string.Equals(ImporterIds[i], importerId, StringComparison.OrdinalIgnoreCase)) {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// Copies the registered importer list into the view state.
        /// </summary>
        /// <param name="importerIds">Importer identifiers to display.</param>
        void SetImporterIds(IReadOnlyList<string> importerIds) {
            ImporterIds.Clear();

            for (int i = 0; i < importerIds.Count; i++) {
                string importerId = importerIds[i];
                if (string.IsNullOrWhiteSpace(importerId)) {
                    throw new ArgumentException("Importer ids must be provided.", nameof(importerIds));
                }

                ImporterIds.Add(importerId);
            }
        }

        /// <summary>
        /// Copies the supported platform identifiers into the view state.
        /// </summary>
        /// <param name="supportedPlatforms">Platforms supported by the current project.</param>
        void SetSupportedPlatforms(IReadOnlyList<string> supportedPlatforms) {
            SupportedPlatformIds.Clear();

            for (int i = 0; i < supportedPlatforms.Count; i++) {
                string platformId = supportedPlatforms[i];
                if (string.IsNullOrWhiteSpace(platformId)) {
                    throw new ArgumentException("Supported platform ids must be provided.", nameof(supportedPlatforms));
                }

                SupportedPlatformIds.Add(platformId);
            }
        }

        /// <summary>
        /// Resolves the platform that should be selected when the view is shown.
        /// </summary>
        /// <param name="activePlatformId">Editor active-platform identifier.</param>
        /// <returns>Selected platform identifier.</returns>
        string ResolveSelectedPlatformId(string activePlatformId) {
            for (int i = 0; i < SupportedPlatformIds.Count; i++) {
                if (string.Equals(SupportedPlatformIds[i], activePlatformId, StringComparison.OrdinalIgnoreCase)) {
                    return SupportedPlatformIds[i];
                }
            }

            return SupportedPlatformIds[0];
        }

        /// <summary>
        /// Rebuilds the platform tab buttons for the current supported platform list.
        /// </summary>
        void RebuildPlatformTabs() {
            ClearPlatformTabs();

            for (int i = 0; i < SupportedPlatformIds.Count; i++) {
                string platformId = SupportedPlatformIds[i];
                EditorEntity tabHost = new EditorEntity();
                tabHost.LayerMask = RootEntity.LayerMask;
                tabHost.InternalEntity = true;
                PlatformTabsHost.AddChild(tabHost);

                TabComponent tabButton = new TabComponent(
                    platformId,
                    new int2(PlatformTabWidth, ControlHeight),
                    Font,
                    () => HandlePlatformTabClicked(platformId));
                tabHost.AddComponent(tabButton);

                PlatformTabButtonHosts.Add(tabHost);
                PlatformTabButtons.Add(tabButton);
            }

            UpdatePlatformTabVisualState();
        }

        /// <summary>
        /// Removes all existing platform tab button entities.
        /// </summary>
        void ClearPlatformTabs() {
            for (int i = PlatformTabButtonHosts.Count - 1; i >= 0; i--) {
                EditorEntity tabHost = PlatformTabButtonHosts[i];
                tabHost.Dispose();
            }

            PlatformTabButtonHosts.Clear();
            PlatformTabButtons.Clear();
        }

        /// <summary>
        /// Updates the tab positions within the platform-tab row.
        /// </summary>
        void UpdatePlatformTabLayout() {
            for (int i = 0; i < PlatformTabButtonHosts.Count; i++) {
                PlatformTabButtonHosts[i].Position = new float3(i * (PlatformTabWidth + PlatformTabSpacing), 0f, 0.1f);
            }
        }

        /// <summary>
        /// Updates the selected-state visuals for each platform tab.
        /// </summary>
        void UpdatePlatformTabVisualState() {
            for (int i = 0; i < PlatformTabButtons.Count; i++) {
                bool isSelected = string.Equals(SupportedPlatformIds[i], CurrentPlatformId, StringComparison.OrdinalIgnoreCase);
                PlatformTabButtons[i].SetSelected(isSelected);
            }
        }

        /// <summary>
        /// Gets or creates the pending platform settings for one platform id.
        /// </summary>
        /// <param name="platformId">Platform identifier to resolve.</param>
        /// <returns>Pending processor settings for the platform.</returns>
        AssetPlatformProcessorSettings GetPendingPlatformSettings(string platformId) {
            if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }

            if (!PendingProcessorSettings.Platforms.TryGetValue(platformId, out AssetPlatformProcessorSettings platformSettings)) {
                platformSettings = new AssetPlatformProcessorSettings();
                PendingProcessorSettings.Platforms[platformId] = platformSettings;
            }

            return platformSettings;
        }

        /// <summary>
        /// Ensures that the provided processor settings contain one entry for every supported platform.
        /// </summary>
        /// <param name="processorSettings">Processor settings to normalize.</param>
        void EnsurePlatformSettingsExist(AssetProcessorSettings processorSettings) {
            if (processorSettings == null) {
                throw new ArgumentNullException(nameof(processorSettings));
            }

            for (int i = 0; i < SupportedPlatformIds.Count; i++) {
                string platformId = SupportedPlatformIds[i];
                if (!processorSettings.Platforms.ContainsKey(platformId)) {
                    processorSettings.Platforms[platformId] = new AssetPlatformProcessorSettings();
                }
            }
        }

        /// <summary>
        /// Creates a deep clone of processor settings for safe pending editing.
        /// </summary>
        /// <param name="processorSettings">Processor settings to copy.</param>
        /// <returns>Cloned processor settings instance.</returns>
        AssetProcessorSettings CloneProcessorSettings(AssetProcessorSettings processorSettings) {
            AssetProcessorSettings clone = new AssetProcessorSettings();
            if (processorSettings == null || processorSettings.Platforms == null) {
                return clone;
            }

            foreach (KeyValuePair<string, AssetPlatformProcessorSettings> pair in processorSettings.Platforms) {
                if (string.IsNullOrWhiteSpace(pair.Key)) {
                    continue;
                }

                clone.Platforms[pair.Key] = ClonePlatformProcessorSettings(pair.Value);
            }

            return clone;
        }

        /// <summary>
        /// Creates a deep clone of one platform's processor settings.
        /// </summary>
        /// <param name="platformSettings">Platform settings to copy.</param>
        /// <returns>Cloned platform settings.</returns>
        AssetPlatformProcessorSettings ClonePlatformProcessorSettings(AssetPlatformProcessorSettings platformSettings) {
            AssetPlatformProcessorSettings clone = new AssetPlatformProcessorSettings();
            if (platformSettings == null || platformSettings.Model == null) {
                return clone;
            }

            clone.Model = CloneModelProcessorSettings(platformSettings.Model);
            return clone;
        }

        /// <summary>
        /// Creates a copy of model processor settings.
        /// </summary>
        /// <param name="modelSettings">Model settings to copy.</param>
        /// <returns>Copied model settings.</returns>
        ModelAssetProcessorSettings CloneModelProcessorSettings(ModelAssetProcessorSettings modelSettings) {
            ModelAssetProcessorSettings clone = new ModelAssetProcessorSettings();
            if (modelSettings == null) {
                return clone;
            }

            clone.FlipWinding = modelSettings.FlipWinding;
            return clone;
        }

        /// <summary>
        /// Compares two processor-settings instances for value equality.
        /// </summary>
        /// <param name="left">First settings instance.</param>
        /// <param name="right">Second settings instance.</param>
        /// <returns>True when all tracked platform settings match.</returns>
        bool ProcessorSettingsMatch(AssetProcessorSettings left, AssetProcessorSettings right) {
            for (int i = 0; i < SupportedPlatformIds.Count; i++) {
                string platformId = SupportedPlatformIds[i];
                AssetPlatformProcessorSettings leftPlatform = ResolvePlatformSettings(left, platformId);
                AssetPlatformProcessorSettings rightPlatform = ResolvePlatformSettings(right, platformId);
                if (leftPlatform.Model.FlipWinding != rightPlatform.Model.FlipWinding) {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Resolves platform settings from one processor-settings object, defaulting when absent.
        /// </summary>
        /// <param name="processorSettings">Processor settings container to inspect.</param>
        /// <param name="platformId">Platform identifier to resolve.</param>
        /// <returns>Existing or default platform settings.</returns>
        AssetPlatformProcessorSettings ResolvePlatformSettings(AssetProcessorSettings processorSettings, string platformId) {
            if (processorSettings == null || processorSettings.Platforms == null) {
                return new AssetPlatformProcessorSettings();
            }

            if (processorSettings.Platforms.TryGetValue(platformId, out AssetPlatformProcessorSettings platformSettings)) {
                return platformSettings ?? new AssetPlatformProcessorSettings();
            }

            return new AssetPlatformProcessorSettings();
        }
    }
}
