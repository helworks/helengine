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
        const int PlatformTabSpacing = 0;
        /// <summary>
        /// Inner padding used by the attached processor panel.
        /// </summary>
        const int ProcessorPanelPadding = 12;
        /// <summary>
        /// Vertical overlap used so the attached processor panel touches the tab row seam.
        /// </summary>
        const int ProcessorPanelTopOverlap = 3;
        /// <summary>
        /// Square size of the flip-winding checkbox control.
        /// </summary>
        const int FlipWindingCheckBoxSize = 24;
        /// <summary>
        /// Width allocated to one processor field label inside the panel.
        /// </summary>
        const int ProcessorFieldLabelWidth = 120;
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
        /// Label text used for the texture max-resolution processor setting.
        /// </summary>
        const string TextureMaxResolutionLabel = "Max Resolution";
        /// <summary>
        /// Label text used for the texture color-format processor setting.
        /// </summary>
        const string TextureColorFormatLabel = "Color Format";
        /// <summary>
        /// Label text used for the texture alpha-precision processor setting.
        /// </summary>
        const string TextureAlphaPrecisionLabel = "Alpha";
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
        /// Host entity for the attached processor panel chrome.
        /// </summary>
        readonly EditorEntity ProcessorPanelRoot;
        /// <summary>
        /// Background used to render the attached processor panel chrome.
        /// </summary>
        readonly RoundedRectComponent ProcessorPanelBackground;
        /// <summary>
        /// Shared platform tab strip used to switch the active processor platform.
        /// </summary>
        readonly PlatformTabStripView PlatformTabStrip;
        /// <summary>
        /// Supported platform identifiers shown in the current tab row.
        /// </summary>
        readonly List<string> SupportedPlatformIds;
        /// <summary>
        /// Available texture color-format values shown in the processor combo box.
        /// </summary>
        readonly List<string> TextureColorFormatValues;
        /// <summary>
        /// Available texture alpha-precision values shown in the processor combo box.
        /// </summary>
        readonly List<string> TextureAlphaPrecisionValues;
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
        /// Host entity for the max-resolution label.
        /// </summary>
        readonly EditorEntity TextureMaxResolutionLabelHost;
        /// <summary>
        /// Text component used to render the max-resolution label.
        /// </summary>
        readonly TextComponent TextureMaxResolutionLabelText;
        /// <summary>
        /// Host entity for the max-resolution text box.
        /// </summary>
        readonly EditorEntity TextureMaxResolutionTextBoxHost;
        /// <summary>
        /// Text box used to edit texture max resolution for the selected platform.
        /// </summary>
        readonly TextBoxComponent TextureMaxResolutionTextBox;
        /// <summary>
        /// Host entity for the color-format label.
        /// </summary>
        readonly EditorEntity TextureColorFormatLabelHost;
        /// <summary>
        /// Text component used to render the color-format label.
        /// </summary>
        readonly TextComponent TextureColorFormatLabelText;
        /// <summary>
        /// Host entity for the color-format combo box.
        /// </summary>
        readonly EditorEntity TextureColorFormatComboBoxHost;
        /// <summary>
        /// Combo box used to edit the texture color format for the selected platform.
        /// </summary>
        readonly ComboBoxComponent TextureColorFormatComboBox;
        /// <summary>
        /// Host entity for the alpha-precision label.
        /// </summary>
        readonly EditorEntity TextureAlphaPrecisionLabelHost;
        /// <summary>
        /// Text component used to render the alpha-precision label.
        /// </summary>
        readonly TextComponent TextureAlphaPrecisionLabelText;
        /// <summary>
        /// Host entity for the alpha-precision combo box.
        /// </summary>
        readonly EditorEntity TextureAlphaPrecisionComboBoxHost;
        /// <summary>
        /// Combo box used to edit texture alpha precision for the selected platform.
        /// </summary>
        readonly ComboBoxComponent TextureAlphaPrecisionComboBox;
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
        /// Tracks whether the importer combo box is being synchronized from external state.
        /// </summary>
        bool IsUpdatingImporterSelection;
        /// <summary>
        /// Tracks whether texture controls are being synchronized from pending platform settings.
        /// </summary>
        bool IsUpdatingTextureControls;

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
            TextureColorFormatValues = new List<string>(Enum.GetNames<TextureAssetColorFormat>());
            TextureAlphaPrecisionValues = new List<string>(Enum.GetNames<TextureAssetAlphaPrecision>());
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

            ProcessorPanelRoot = new EditorEntity();
            ProcessorPanelRoot.LayerMask = layerMask;
            ProcessorPanelRoot.InternalEntity = true;
            RootEntity.AddChild(ProcessorPanelRoot);

            ProcessorPanelBackground = new RoundedRectComponent {
                FillColor = ThemeManager.Colors.SurfacePrimary,
                BorderColor = ThemeManager.Colors.AccentTertiary,
                BorderThickness = 2f,
                Radius = 6f,
                Corners = RoundedRectCorners.BottomLeft | RoundedRectCorners.BottomRight,
                RenderOrder2D = RenderOrder2D.PanelSurface,
                Size = new int2(1, 1)
            };
            ProcessorPanelRoot.AddComponent(ProcessorPanelBackground);

            PlatformTabStrip = new PlatformTabStripView(font, layerMask, PlatformTabWidth, ControlHeight, PlatformTabSpacing, ControlHeight);
            RootEntity.AddChild(PlatformTabStrip.Root);

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

            TextureMaxResolutionLabelHost = new EditorEntity();
            TextureMaxResolutionLabelHost.LayerMask = layerMask;
            RootEntity.AddChild(TextureMaxResolutionLabelHost);

            TextureMaxResolutionLabelText = new TextComponent();
            TextureMaxResolutionLabelText.Font = font;
            TextureMaxResolutionLabelText.Text = TextureMaxResolutionLabel;
            TextureMaxResolutionLabelText.Color = ThemeManager.Colors.InputForegroundPrimary;
            TextureMaxResolutionLabelText.RenderOrder2D = TextOrder;
            TextureMaxResolutionLabelHost.AddComponent(TextureMaxResolutionLabelText);

            TextureMaxResolutionTextBoxHost = new EditorEntity();
            TextureMaxResolutionTextBoxHost.LayerMask = layerMask;
            RootEntity.AddChild(TextureMaxResolutionTextBoxHost);

            TextureMaxResolutionTextBox = new TextBoxComponent(new int2(80, ControlHeight), font);
            TextureMaxResolutionTextBox.TextChanged += HandleTextureMaxResolutionTextChanged;
            TextureMaxResolutionTextBoxHost.AddComponent(TextureMaxResolutionTextBox);

            TextureColorFormatLabelHost = new EditorEntity();
            TextureColorFormatLabelHost.LayerMask = layerMask;
            RootEntity.AddChild(TextureColorFormatLabelHost);

            TextureColorFormatLabelText = new TextComponent();
            TextureColorFormatLabelText.Font = font;
            TextureColorFormatLabelText.Text = TextureColorFormatLabel;
            TextureColorFormatLabelText.Color = ThemeManager.Colors.InputForegroundPrimary;
            TextureColorFormatLabelText.RenderOrder2D = TextOrder;
            TextureColorFormatLabelHost.AddComponent(TextureColorFormatLabelText);

            TextureColorFormatComboBoxHost = new EditorEntity();
            TextureColorFormatComboBoxHost.LayerMask = layerMask;
            RootEntity.AddChild(TextureColorFormatComboBoxHost);

            TextureColorFormatComboBox = new ComboBoxComponent(new int2(180, ControlHeight), font, TextureColorFormatValues, 0);
            TextureColorFormatComboBox.SelectionChanged += HandleTextureColorFormatChanged;
            TextureColorFormatComboBoxHost.AddComponent(TextureColorFormatComboBox);

            TextureAlphaPrecisionLabelHost = new EditorEntity();
            TextureAlphaPrecisionLabelHost.LayerMask = layerMask;
            RootEntity.AddChild(TextureAlphaPrecisionLabelHost);

            TextureAlphaPrecisionLabelText = new TextComponent();
            TextureAlphaPrecisionLabelText.Font = font;
            TextureAlphaPrecisionLabelText.Text = TextureAlphaPrecisionLabel;
            TextureAlphaPrecisionLabelText.Color = ThemeManager.Colors.InputForegroundPrimary;
            TextureAlphaPrecisionLabelText.RenderOrder2D = TextOrder;
            TextureAlphaPrecisionLabelHost.AddComponent(TextureAlphaPrecisionLabelText);

            TextureAlphaPrecisionComboBoxHost = new EditorEntity();
            TextureAlphaPrecisionComboBoxHost.LayerMask = layerMask;
            RootEntity.AddChild(TextureAlphaPrecisionComboBoxHost);

            TextureAlphaPrecisionComboBox = new ComboBoxComponent(new int2(180, ControlHeight), font, TextureAlphaPrecisionValues, 0);
            TextureAlphaPrecisionComboBox.SelectionChanged += HandleTextureAlphaPrecisionChanged;
            TextureAlphaPrecisionComboBoxHost.AddComponent(TextureAlphaPrecisionComboBox);

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
        public int PlatformTabCount => PlatformTabStrip.TabCount;

        /// <summary>
        /// Gets the currently selected processor platform identifier.
        /// </summary>
        public string SelectedPlatformId => CurrentPlatformId;

        /// <summary>
        /// Gets the shared platform tab strip used by the processor section.
        /// </summary>
        public PlatformTabStripView PlatformTabStripView => PlatformTabStrip;

        /// <summary>
        /// Gets a value indicating whether model processor controls are visible.
        /// </summary>
        public bool IsModelProcessorVisible => CurrentEntryKind == AssetEntryKind.Model;

        /// <summary>
        /// Gets a value indicating whether texture processor controls are visible.
        /// </summary>
        public bool IsTextureProcessorVisible => CurrentEntryKind == AssetEntryKind.Image || CurrentEntryKind == AssetEntryKind.Font;

        /// <summary>
        /// Gets the current pending flip-winding value for the selected platform.
        /// </summary>
        public bool CurrentFlipWindingValue => GetPendingPlatformSettings(CurrentPlatformId).Model.FlipWinding;

        /// <summary>
        /// Gets the current pending max-resolution value for the selected platform texture settings.
        /// </summary>
        public int CurrentTextureMaxResolutionValue => GetPendingPlatformSettings(CurrentPlatformId).Texture.MaxResolution;

        /// <summary>
        /// Gets the current pending color-format value for the selected platform texture settings.
        /// </summary>
        public TextureAssetColorFormat CurrentTextureColorFormatValue => GetPendingPlatformSettings(CurrentPlatformId).Texture.ColorFormat;

        /// <summary>
        /// Gets the current pending alpha-precision value for the selected platform texture settings.
        /// </summary>
        public TextureAssetAlphaPrecision CurrentTextureAlphaPrecisionValue => GetPendingPlatformSettings(CurrentPlatformId).Texture.AlphaPrecision;

        /// <summary>
        /// Shows the view with the provided importer list, current settings, and supported platforms.
        /// </summary>
        /// <param name="importerIds">Registered importer identifiers.</param>
        /// <param name="importerId">Current importer identifier to edit.</param>
        /// <param name="processorSettings">Current processor settings to edit.</param>
        /// <param name="supportedPlatforms">Project-supported platform identifiers.</param>
        /// <param name="activePlatformId">Currently active platform identifier.</param>
        /// <param name="entryKind">Kind of asset entry being edited.</param>
        public void Show(
            IReadOnlyList<string> importerIds,
            string importerId,
            AssetProcessorSettings processorSettings,
            IReadOnlyList<string> supportedPlatforms,
            string activePlatformId,
            AssetEntryKind entryKind) {
            if (importerIds == null) {
                throw new ArgumentNullException(nameof(importerIds));
            } else if (string.IsNullOrWhiteSpace(importerId)) {
                throw new ArgumentException("Importer id must be provided.", nameof(importerId));
            } else if (processorSettings == null) {
                throw new ArgumentNullException(nameof(processorSettings));
            } else if (supportedPlatforms == null) {
                throw new ArgumentNullException(nameof(supportedPlatforms));
            } else if (supportedPlatforms.Count == 0) {
                throw new ArgumentException("At least one platform must be provided.", nameof(supportedPlatforms));
            } else if (string.IsNullOrWhiteSpace(activePlatformId)) {
                throw new ArgumentException("Active platform id must be provided.", nameof(activePlatformId));
            }

            SetImporterIds(importerIds);
            SetSupportedPlatforms(supportedPlatforms);

            ActiveImporterId = importerId;
            PendingImporterId = importerId;
            ActiveProcessorSettings = CloneProcessorSettings(processorSettings);
            PendingProcessorSettings = CloneProcessorSettings(processorSettings);
            EnsurePlatformSettingsExist(ActiveProcessorSettings);
            EnsurePlatformSettingsExist(PendingProcessorSettings);
            CurrentPlatformId = ResolveSelectedPlatformId(activePlatformId);
            CurrentEntryKind = entryKind;

            int selectedIndex = FindImporterIndex(ActiveImporterId);
            IsUpdatingImporterSelection = true;
            ComboBox.SetItems(ImporterIds, selectedIndex);
            IsUpdatingImporterSelection = false;
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
            int tabStripTop = currentTop;
            PlatformTabStrip.UpdateLayout(0, tabStripTop, width);

            int processorPanelTop = tabStripTop + ControlHeight - ProcessorPanelTopOverlap;
            int processorPanelContentTop = tabStripTop + ControlHeight + ProcessorPanelPadding;
            currentTop = processorPanelContentTop;
            if (IsModelProcessorVisible) {
                int labelOffsetY = (int)Math.Round((ControlHeight - labelHeight) / 2d);
                FlipWindingLabelHost.Position = new float3(ProcessorPanelPadding, currentTop + labelOffsetY, 0.1f);
                FlipWindingLabelText.Size = new int2(Math.Max(1, width - (ProcessorPanelPadding * 2)), labelHeight);

                FlipWindingCheckBoxHost.Position = new float3(Math.Max(ProcessorPanelPadding, width - ProcessorPanelPadding - FlipWindingCheckBoxSize), currentTop, 0.1f);
                currentTop += ControlHeight + RowSpacing;
            }

            if (IsTextureProcessorVisible) {
                int labelOffsetY = (int)Math.Round((ControlHeight - labelHeight) / 2d);
                int controlLeft = ProcessorPanelPadding + ProcessorFieldLabelWidth + ProcessorPanelPadding;
                int controlWidth = Math.Max(1, width - controlLeft - ProcessorPanelPadding);

                TextureMaxResolutionLabelHost.Position = new float3(ProcessorPanelPadding, currentTop + labelOffsetY, 0.1f);
                TextureMaxResolutionLabelText.Size = new int2(ProcessorFieldLabelWidth, labelHeight);
                TextureMaxResolutionTextBoxHost.Position = new float3(controlLeft, currentTop, 0.1f);
                TextureMaxResolutionTextBox.Size = new int2(controlWidth, ControlHeight);
                currentTop += ControlHeight + RowSpacing;

                TextureColorFormatLabelHost.Position = new float3(ProcessorPanelPadding, currentTop + labelOffsetY, 0.1f);
                TextureColorFormatLabelText.Size = new int2(ProcessorFieldLabelWidth, labelHeight);
                TextureColorFormatComboBoxHost.Position = new float3(controlLeft, currentTop, 0.1f);
                TextureColorFormatComboBox.Size = new int2(controlWidth, ControlHeight);
                currentTop += ControlHeight + RowSpacing;

                TextureAlphaPrecisionLabelHost.Position = new float3(ProcessorPanelPadding, currentTop + labelOffsetY, 0.1f);
                TextureAlphaPrecisionLabelText.Size = new int2(ProcessorFieldLabelWidth, labelHeight);
                TextureAlphaPrecisionComboBoxHost.Position = new float3(controlLeft, currentTop, 0.1f);
                TextureAlphaPrecisionComboBox.Size = new int2(controlWidth, ControlHeight);
                currentTop += ControlHeight + RowSpacing;
            }

            ApplyHost.Position = new float3(ProcessorPanelPadding, currentTop, 0.1f);

            currentTop += ControlHeight + RowSpacing;
            StatusHost.Position = new float3(ProcessorPanelPadding, currentTop, 0.1f);
            StatusText.Size = new int2(Math.Max(1, width - (ProcessorPanelPadding * 2)), labelHeight);

            int processorPanelHeight = (currentTop + labelHeight + ProcessorPanelPadding) - processorPanelTop;
            ProcessorPanelRoot.Position = new float3(0f, processorPanelTop, 0.05f);
            ProcessorPanelBackground.Size = new int2(width, Math.Max(1, processorPanelHeight));

            LayoutHeightValue = processorPanelTop + processorPanelHeight;
        }

        /// <summary>
        /// Handles combo box selection changes.
        /// </summary>
        /// <param name="index">Selected index.</param>
        /// <param name="value">Selected importer identifier.</param>
        void HandleComboSelectionChanged(int index, string value) {
            if (IsUpdatingImporterSelection) {
                return;
            }

            if (string.IsNullOrWhiteSpace(value)) {
                throw new InvalidOperationException("Importer selection was not provided.");
            }

            PendingImporterId = value;
            UpdateStatusText();
            HandleApplyClicked();
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
        /// Applies one max-resolution textbox value to the currently selected platform texture settings.
        /// </summary>
        /// <param name="component">Text box that raised the change event.</param>
        void HandleTextureMaxResolutionTextChanged(TextBoxComponent component) {
            if (IsUpdatingTextureControls) {
                return;
            }

            if (!int.TryParse(component.Text, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int maxResolution)) {
                return;
            } else if (maxResolution < 0) {
                throw new InvalidOperationException("Texture max resolution must not be negative.");
            }

            AssetPlatformProcessorSettings platformSettings = GetPendingPlatformSettings(CurrentPlatformId);
            platformSettings.Texture.MaxResolution = maxResolution;
            UpdateStatusText();
        }

        /// <summary>
        /// Applies one color-format combo-box value to the currently selected platform texture settings.
        /// </summary>
        /// <param name="selectedIndex">Selected color-format index.</param>
        /// <param name="selectedValue">Selected color-format name.</param>
        void HandleTextureColorFormatChanged(int selectedIndex, string selectedValue) {
            if (IsUpdatingTextureControls) {
                return;
            } else if (string.IsNullOrWhiteSpace(selectedValue)) {
                throw new InvalidOperationException("Texture color format selection was not provided.");
            }

            AssetPlatformProcessorSettings platformSettings = GetPendingPlatformSettings(CurrentPlatformId);
            platformSettings.Texture.ColorFormat = Enum.Parse<TextureAssetColorFormat>(selectedValue, false);
            UpdateStatusText();
        }

        /// <summary>
        /// Applies one alpha-precision combo-box value to the currently selected platform texture settings.
        /// </summary>
        /// <param name="selectedIndex">Selected alpha-precision index.</param>
        /// <param name="selectedValue">Selected alpha-precision name.</param>
        void HandleTextureAlphaPrecisionChanged(int selectedIndex, string selectedValue) {
            if (IsUpdatingTextureControls) {
                return;
            } else if (string.IsNullOrWhiteSpace(selectedValue)) {
                throw new InvalidOperationException("Texture alpha precision selection was not provided.");
            }

            AssetPlatformProcessorSettings platformSettings = GetPendingPlatformSettings(CurrentPlatformId);
            platformSettings.Texture.AlphaPrecision = Enum.Parse<TextureAssetAlphaPrecision>(selectedValue, false);
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
            PlatformTabStrip.Root.Enabled = SupportedPlatformIds.Count > 0;
            ProcessorPanelRoot.Enabled = SupportedPlatformIds.Count > 0;

            bool showModelProcessor = IsModelProcessorVisible;
            bool showTextureProcessor = IsTextureProcessorVisible;
            FlipWindingLabelHost.Enabled = showModelProcessor;
            FlipWindingCheckBoxHost.Enabled = showModelProcessor;
            TextureMaxResolutionLabelHost.Enabled = showTextureProcessor;
            TextureMaxResolutionTextBoxHost.Enabled = showTextureProcessor;
            TextureColorFormatLabelHost.Enabled = showTextureProcessor;
            TextureColorFormatComboBoxHost.Enabled = showTextureProcessor;
            TextureAlphaPrecisionLabelHost.Enabled = showTextureProcessor;
            TextureAlphaPrecisionComboBoxHost.Enabled = showTextureProcessor;

            if (showModelProcessor) {
                FlipWindingCheckBox.IsChecked = GetPendingPlatformSettings(CurrentPlatformId).Model.FlipWinding;
            }

            if (showTextureProcessor) {
                SyncTextureProcessorControlsFromPendingSettings();
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
            PlatformTabStrip.SetPlatforms(SupportedPlatformIds, CurrentPlatformId, HandlePlatformTabClicked);
        }

        /// <summary>
        /// Updates the selected-state visuals for each platform tab.
        /// </summary>
        void UpdatePlatformTabVisualState() {
            PlatformTabStrip.SetSelectedPlatform(CurrentPlatformId);
        }

        /// <summary>
        /// Synchronizes the texture processor controls from the current pending platform settings.
        /// </summary>
        void SyncTextureProcessorControlsFromPendingSettings() {
            TextureAssetProcessorSettings textureSettings = GetPendingPlatformSettings(CurrentPlatformId).Texture;

            IsUpdatingTextureControls = true;
            TextureMaxResolutionTextBox.Text = textureSettings.MaxResolution.ToString(System.Globalization.CultureInfo.InvariantCulture);
            TextureColorFormatComboBox.SetItems(TextureColorFormatValues, GetTextureColorFormatIndex(textureSettings.ColorFormat));
            TextureAlphaPrecisionComboBox.SetItems(TextureAlphaPrecisionValues, GetTextureAlphaPrecisionIndex(textureSettings.AlphaPrecision));
            IsUpdatingTextureControls = false;
        }

        /// <summary>
        /// Resolves the combo-box index for one texture color format.
        /// </summary>
        /// <param name="colorFormat">Texture color format to locate.</param>
        /// <returns>Index of the color format inside the combo-box item list.</returns>
        int GetTextureColorFormatIndex(TextureAssetColorFormat colorFormat) {
            string colorFormatName = colorFormat.ToString();
            for (int i = 0; i < TextureColorFormatValues.Count; i++) {
                if (string.Equals(TextureColorFormatValues[i], colorFormatName, StringComparison.Ordinal)) {
                    return i;
                }
            }

            throw new InvalidOperationException($"Unsupported texture color format '{colorFormat}'.");
        }

        /// <summary>
        /// Resolves the combo-box index for one texture alpha precision.
        /// </summary>
        /// <param name="alphaPrecision">Texture alpha precision to locate.</param>
        /// <returns>Index of the alpha precision inside the combo-box item list.</returns>
        int GetTextureAlphaPrecisionIndex(TextureAssetAlphaPrecision alphaPrecision) {
            string alphaPrecisionName = alphaPrecision.ToString();
            for (int i = 0; i < TextureAlphaPrecisionValues.Count; i++) {
                if (string.Equals(TextureAlphaPrecisionValues[i], alphaPrecisionName, StringComparison.Ordinal)) {
                    return i;
                }
            }

            throw new InvalidOperationException($"Unsupported texture alpha precision '{alphaPrecision}'.");
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
            if (platformSettings == null) {
                return clone;
            }

            clone.Texture = CloneTextureProcessorSettings(platformSettings.Texture);
            clone.Model = CloneModelProcessorSettings(platformSettings.Model);
            return clone;
        }

        /// <summary>
        /// Creates a copy of texture processor settings.
        /// </summary>
        /// <param name="textureSettings">Texture settings to copy.</param>
        /// <returns>Copied texture settings.</returns>
        TextureAssetProcessorSettings CloneTextureProcessorSettings(TextureAssetProcessorSettings textureSettings) {
            TextureAssetProcessorSettings clone = new TextureAssetProcessorSettings();
            if (textureSettings == null) {
                return clone;
            }

            clone.MaxResolution = textureSettings.MaxResolution;
            clone.ColorFormat = textureSettings.ColorFormat;
            clone.AlphaPrecision = textureSettings.AlphaPrecision;
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
                if (leftPlatform.Texture.MaxResolution != rightPlatform.Texture.MaxResolution
                    || leftPlatform.Texture.ColorFormat != rightPlatform.Texture.ColorFormat
                    || leftPlatform.Texture.AlphaPrecision != rightPlatform.Texture.AlphaPrecision) {
                    return false;
                }

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
