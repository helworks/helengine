using helengine.baseplatform.Definitions;

namespace helengine.editor {
    /// <summary>
    /// Floating modal dialog used to prepare local per-platform build selections and queued builds.
    /// </summary>
    public class BuildDialog : EditorDialogBase {
        /// <summary>
        /// Fixed panel width used by the dialog.
        /// </summary>
        public const int PanelWidth = 920;
        /// <summary>
        /// Fixed panel height used by the dialog.
        /// </summary>
        public const int PanelHeight = 720;
        /// <summary>
        /// Height used by the existing build-planning controls so their positions stay unchanged when the dialog grows.
        /// </summary>
        public const int LegacyContentHeight = 560;
        /// <summary>
        /// Height reserved for the draggable title bar.
        /// </summary>
        public const int HeaderHeight = 32;
        /// <summary>
        /// Padding applied inside the dialog beneath the title bar.
        /// </summary>
        public const int PanelPadding = 16;
        /// <summary>
        /// Width reserved for the queue column on the right side of the dialog.
        /// </summary>
        public const int QueueColumnWidth = 300;
        /// <summary>
        /// Width reserved for each platform tab button.
        /// </summary>
        public const int PlatformTabWidth = 120;
        /// <summary>
        /// Height reserved for each platform tab button.
        /// </summary>
        public const int PlatformTabHeight = 24;
        /// <summary>
        /// Height reserved for each scene selection row.
        /// </summary>
        public const int SceneRowHeight = 24;
        /// <summary>
        /// Width reserved for the scene ordering textbox in each scene row.
        /// </summary>
        public const int SceneOrderFieldWidth = 44;
        /// <summary>
        /// Height reserved for the scene ordering textbox in each scene row.
        /// </summary>
        public const int SceneOrderFieldHeight = 18;
        /// <summary>
        /// Top margin applied before the bordered scene-list container.
        /// </summary>
        public const int SceneListTopMargin = 8;
        /// <summary>
        /// Inner padding used inside the bordered scene-list container.
        /// </summary>
        public const int SceneListPadding = 8;
        /// <summary>
        /// Height reserved for each queued build row.
        /// </summary>
        public const int QueueRowHeight = 56;
        /// <summary>
        /// Width reserved for the queue-row remove button.
        /// </summary>
        public const int QueueCardRemoveButtonWidth = 28;
        /// <summary>
        /// Height reserved for the queue section header bar.
        /// </summary>
        public const int QueueHeaderHeight = 28;
        /// <summary>
        /// Inner padding used inside the bordered queue container.
        /// </summary>
        public const int QueueListPadding = 8;
        /// <summary>
        /// Inner left padding applied inside each queued build row.
        /// </summary>
        public const int QueueCardTextPadding = 8;
        /// <summary>
        /// Horizontal gap reserved between clipped status text and the remove button.
        /// </summary>
        public const int QueueCardTextButtonGap = 12;
        /// <summary>
        /// Height reserved for the output directory text field.
        /// </summary>
        public const int OutputFieldHeight = 28;
        /// <summary>
        /// Height reserved for footer buttons.
        /// </summary>
        public const int FooterButtonHeight = 28;
        /// <summary>
        /// Width reserved for footer action buttons.
        /// </summary>
        public const int FooterButtonWidth = 124;
        /// <summary>
        /// Width reserved for the output-folder browse button.
        /// </summary>
        public const int BrowseButtonWidth = 84;
        /// <summary>
        /// Fixed per-frame time step used by transient scene-list feedback effects.
        /// </summary>
        public const float SceneListEffectFrameDeltaSeconds = 1f / 60f;
        /// <summary>
        /// Total duration used by the invalid scene-list shake effect.
        /// </summary>
        public const float SceneListShakeDurationSeconds = 0.3f;
        /// <summary>
        /// Peak horizontal amplitude used by the invalid scene-list shake effect.
        /// </summary>
        public const float SceneListShakeAmplitudePixels = 10f;
        /// <summary>
        /// Oscillation frequency used by the invalid scene-list shake effect.
        /// </summary>
        public const float SceneListShakeFrequencyHz = 16f;
        /// <summary>
        /// Height reserved for the dedicated build-log section beneath the existing controls.
        /// </summary>
        public const int BuildLogsSectionHeight = 160;
        /// <summary>
        /// Internal padding used inside the build-log section.
        /// </summary>
        const int BuildLogsPadding = 8;
        /// <summary>
        /// Height reserved for the build-log title row.
        /// </summary>
        const int BuildLogsTitleHeight = 18;
        /// <summary>
        /// Height reserved for the build-log progress bar.
        /// </summary>
        const int BuildLogsProgressBarHeight = 12;
        /// <summary>
        /// Height reserved for each visible build-log line.
        /// </summary>
        const int BuildLogLineHeight = 18;
        /// <summary>
        /// Maximum number of build-log lines shown at once.
        /// </summary>
        const int BuildLogVisibleLineCount = 5;
        /// <summary>
        /// Root entity for all left-side build-planning controls.
        /// </summary>
        readonly EditorEntity BuildColumnRoot;
        /// <summary>
        /// Root entity for the bordered scene-list container.
        /// </summary>
        readonly EditorEntity SceneListRoot;
        /// <summary>
        /// Bordered background surface rendered behind the scene list.
        /// </summary>
        readonly RoundedRectComponent SceneListBackground;
        /// <summary>
        /// Root entity for all right-side queue controls.
        /// </summary>
        readonly EditorEntity QueueColumnRoot;
        /// <summary>
        /// Root entity for the bordered queue section.
        /// </summary>
        readonly EditorEntity QueueSectionRoot;
        /// <summary>
        /// Bordered background surface rendered behind the queue section.
        /// </summary>
        readonly RoundedRectComponent QueueListBackground;
        /// <summary>
        /// Background surface used for the queue section title bar.
        /// </summary>
        readonly RoundedRectComponent QueueHeaderBackground;
        /// <summary>
        /// Host entity for the queue section title text.
        /// </summary>
        readonly EditorEntity QueueHeaderTextHost;
        /// <summary>
        /// Text component used to render the queue section title.
        /// </summary>
        readonly TextComponent QueueHeaderText;
        /// <summary>
        /// Root entity used to place the queued-build rows under the queue header.
        /// </summary>
        readonly EditorEntity QueueItemsRoot;
        /// <summary>
        /// Host entities created for the currently rendered platform tabs.
        /// </summary>
        readonly List<EditorEntity> PlatformTabHosts;
        /// <summary>
        /// Platform tab buttons rendered for each enabled platform.
        /// </summary>
        readonly List<ButtonComponent> PlatformTabs;
        /// <summary>
        /// Host entities created for the currently rendered map labels.
        /// </summary>
        readonly List<EditorEntity> MapLabelHosts;
        /// <summary>
        /// Text components used to render the current platform's map list.
        /// </summary>
        readonly List<TextComponent> MapLabelTexts;
        /// <summary>
        /// Host entities created for the current platform's map checkboxes.
        /// </summary>
        readonly List<EditorEntity> MapCheckBoxHosts;
        /// <summary>
        /// Checkbox components used to select maps for the active platform.
        /// </summary>
        readonly List<CheckBoxComponent> MapCheckBoxes;
        /// <summary>
        /// Host entities created for the current platform's scene-order text boxes.
        /// </summary>
        readonly List<EditorEntity> MapOrderHosts;
        /// <summary>
        /// Text boxes used to edit per-scene ordering numbers.
        /// </summary>
        readonly List<TextBoxComponent> MapOrderFields;
        /// <summary>
        /// Host entities created for the currently rendered queue rows.
        /// </summary>
        readonly List<EditorEntity> QueueItemHosts;
        /// <summary>
        /// Text components used to render queue item summaries and clipped status lines.
        /// </summary>
        readonly List<TextComponent> QueueItemTexts;
        /// <summary>
        /// Host entities created for the current queue-item remove buttons.
        /// </summary>
        readonly List<EditorEntity> QueueItemRemoveButtonHosts;
        /// <summary>
        /// Remove buttons rendered for the current queue items.
        /// </summary>
        readonly List<ButtonComponent> QueueItemRemoveButtons;
        /// <summary>
        /// Row backgrounds rendered behind the current queue items.
        /// </summary>
        readonly List<RoundedRectComponent> QueueItemCardBackgrounds;
        /// <summary>
        /// Host entity for the output-directory label.
        /// </summary>
        readonly EditorEntity OutputLabelHost;
        /// <summary>
        /// Host entity for the debug-build label.
        /// </summary>
        readonly EditorEntity DebugBuildLabelHost;
        /// <summary>
        /// Host entity for the copy-source label.
        /// </summary>
        readonly EditorEntity CopySourceLabelHost;
        /// <summary>
        /// Host entity for the copy-source combo box.
        /// </summary>
        readonly EditorEntity CopySourcePlatformComboBoxHost;
        /// <summary>
        /// Host entity for the copy-map-list button.
        /// </summary>
        readonly EditorEntity CopyMapListButtonHost;
        /// <summary>
        /// Label text describing the source-platform combo box.
        /// </summary>
        readonly TextComponent CopySourceLabelText;
        /// <summary>
        /// Combo box used to pick the source platform for map-list copying.
        /// </summary>
        readonly ComboBoxComponent CopySourcePlatformComboBox;
        /// <summary>
        /// Button used to copy one platform's selected map list into the active platform.
        /// </summary>
        readonly ButtonComponent CopyMapListButton;
        /// <summary>
        /// Output-directory label text.
        /// </summary>
        readonly TextComponent OutputLabelText;
        /// <summary>
        /// Debug-build label text.
        /// </summary>
        readonly TextComponent DebugBuildLabelText;
        /// <summary>
        /// Host entity for the output-directory textbox.
        /// </summary>
        readonly EditorEntity OutputFieldHost;
        /// <summary>
        /// Text box used to edit the active platform's output directory.
        /// </summary>
        readonly TextBoxComponent OutputDirectoryField;
        /// <summary>
        /// Host entity for the debug-build checkbox.
        /// </summary>
        readonly EditorEntity DebugBuildCheckBoxHost;
        /// <summary>
        /// Checkbox used to choose whether the active platform defaults to a debug native build.
        /// </summary>
        readonly CheckBoxComponent DebugBuildCheckBox;
        /// <summary>
        /// Host entity for the output-folder browse button.
        /// </summary>
        readonly EditorEntity BrowseOutputFolderButtonHost;
        /// <summary>
        /// Button used to open a host-provided folder picker for the active platform output directory.
        /// </summary>
        readonly ButtonComponent BrowseOutputFolderButton;
        /// <summary>
        /// Host entity for the Add to Build button.
        /// </summary>
        readonly EditorEntity AddToBuildButtonHost;
        /// <summary>
        /// Button used to add one queued build from the active platform tab.
        /// </summary>
        readonly ButtonComponent AddToBuildButton;
        /// <summary>
        /// Host entity for the Build Queue button.
        /// </summary>
        readonly EditorEntity BuildQueueButtonHost;
        /// <summary>
        /// Button used to run all pending queued builds.
        /// </summary>
        readonly ButtonComponent BuildQueueButton;
        /// <summary>
        /// Root entity for the dedicated build-log section at the bottom of the dialog.
        /// </summary>
        readonly EditorEntity BuildLogsRoot;
        /// <summary>
        /// Bordered background surface rendered behind the build-log section.
        /// </summary>
        readonly RoundedRectComponent BuildLogsBackground;
        /// <summary>
        /// Host entity for the build-log section title.
        /// </summary>
        readonly EditorEntity BuildLogsTitleHost;
        /// <summary>
        /// Text component used to render the build-log section title.
        /// </summary>
        readonly TextComponent BuildLogsTitleText;
        /// <summary>
        /// Host entity for the build progress bar track.
        /// </summary>
        readonly EditorEntity BuildLogsProgressTrackHost;
        /// <summary>
        /// Background used as the progress bar track.
        /// </summary>
        readonly RoundedRectComponent BuildLogsProgressTrack;
        /// <summary>
        /// Host entity for the filled progress bar segment.
        /// </summary>
        readonly EditorEntity BuildLogsProgressFillHost;
        /// <summary>
        /// Filled progress bar segment indicating queue completion.
        /// </summary>
        readonly RoundedRectComponent BuildLogsProgressFill;
        /// <summary>
        /// Host entity for the build-log text block.
        /// </summary>
        readonly EditorEntity BuildLogsTextHost;
        /// <summary>
        /// Text component used to render the build log lines.
        /// </summary>
        readonly TextComponent BuildLogsText;
        /// <summary>
        /// Project-relative scene ids shown by the active tab.
        /// </summary>
        readonly List<string> SceneIds;
        /// <summary>
        /// Enabled platform ids currently shown by the dialog.
        /// </summary>
        readonly List<string> SupportedPlatformIds;
        /// <summary>
        /// Current mutable build configuration driving the dialog.
        /// </summary>
        EditorBuildConfigDocument CurrentBuildConfig;
        /// <summary>
        /// Builder-provided metadata for the currently active platform.
        /// </summary>
        EditorPlatformBuildSelectionModel ActivePlatformSelectionModel;
        /// <summary>
        /// Tracks whether the scene-list container is currently showing an invalid-selection border.
        /// </summary>
        bool IsSceneListInvalid;
        /// <summary>
        /// Tracks whether the scene-list container is currently playing its invalid-selection shake.
        /// </summary>
        bool IsSceneListShakeActive;
        /// <summary>
        /// Elapsed shake time for the current invalid scene-list feedback pass.
        /// </summary>
        float SceneListShakeElapsedSeconds;
        /// <summary>
        /// Horizontal offset currently applied to the scene-list container during invalid-selection feedback.
        /// </summary>
        float SceneListShakeOffsetX;
        /// <summary>
        /// Platform id shown by the currently active tab.
        /// </summary>
        string ActivePlatformId;
        /// <summary>
        /// Raised when the user wants to add one queued build from the active platform tab.
        /// </summary>
        public event Action<BuildDialogAddRequest> AddRequested;
        /// <summary>
        /// Raised when the user wants to browse for an output folder for the active platform.
        /// </summary>
        public event Action BrowseOutputFolderRequested;
        /// <summary>
        /// Raised when the user wants to start running the queued builds.
        /// </summary>
        public event Action BuildQueueRequested;
        /// <summary>
        /// Raised when the user wants to remove one queued build item from the current queue.
        /// </summary>
        public event Action<string> RemoveQueueItemRequested;
        /// <summary>
        /// Raised when the user closes the build dialog without confirming another action.
        /// </summary>
        public event Action CancelRequested;

        /// <summary>
        /// Gets the mutable build configuration currently being edited by the dialog.
        /// </summary>
        public EditorBuildConfigDocument BuildConfig => CurrentBuildConfig;
        /// <summary>
        /// Initializes one build dialog with a shared modal shell and build-planning controls.
        /// </summary>
        /// <param name="font">Font used for dialog labels and controls.</param>
        public BuildDialog(FontAsset font) : base("BuildDialog", "Build", font, PanelWidth, PanelHeight, HeaderHeight) {
            PlatformTabHosts = new List<EditorEntity>(8);
            PlatformTabs = new List<ButtonComponent>(8);
            MapLabelHosts = new List<EditorEntity>(16);
            MapLabelTexts = new List<TextComponent>(16);
            MapCheckBoxHosts = new List<EditorEntity>(16);
            MapCheckBoxes = new List<CheckBoxComponent>(16);
            MapOrderHosts = new List<EditorEntity>(16);
            MapOrderFields = new List<TextBoxComponent>(16);
            QueueItemHosts = new List<EditorEntity>(16);
            QueueItemTexts = new List<TextComponent>(16);
            QueueItemRemoveButtonHosts = new List<EditorEntity>(16);
            QueueItemRemoveButtons = new List<ButtonComponent>(16);
            QueueItemCardBackgrounds = new List<RoundedRectComponent>(16);
            SceneIds = new List<string>(32);
            SupportedPlatformIds = new List<string>(8);

            BuildColumnRoot = new EditorEntity {
                LayerMask = LayerMask,
                Position = new float3(PanelPadding, HeaderHeight + PanelPadding, 0.1f),
                InternalEntity = true
            };
            DialogPanelRoot.AddChild(BuildColumnRoot);

            SceneListRoot = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            BuildColumnRoot.AddChild(SceneListRoot);
            SceneListRoot.AddComponent(new BuildDialogFeedbackUpdateComponent(this));

            SceneListBackground = new RoundedRectComponent {
                FillColor = ThemeManager.Colors.SurfacePrimary,
                BorderColor = ThemeManager.Colors.AccentTertiary,
                BorderThickness = 2f,
                Radius = 6f,
                RenderOrder2D = DialogPanelOrder,
                Size = new int2(GetBuildColumnWidth(), 1)
            };
            SceneListRoot.AddComponent(SceneListBackground);

            QueueColumnRoot = new EditorEntity {
                LayerMask = LayerMask,
                Position = new float3(PanelWidth - QueueColumnWidth - PanelPadding, HeaderHeight + PanelPadding, 0.1f),
                InternalEntity = true
            };
            DialogPanelRoot.AddChild(QueueColumnRoot);

            QueueSectionRoot = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            QueueColumnRoot.AddChild(QueueSectionRoot);

            QueueListBackground = new RoundedRectComponent {
                FillColor = ThemeManager.Colors.SurfacePrimary,
                BorderColor = ThemeManager.Colors.AccentTertiary,
                BorderThickness = 2f,
                Radius = 6f,
                RenderOrder2D = DialogPanelOrder,
                Size = new int2(QueueColumnWidth, 1)
            };
            QueueSectionRoot.AddComponent(QueueListBackground);

            QueueHeaderBackground = new RoundedRectComponent {
                FillColor = ThemeManager.Colors.AccentSecondary,
                BorderColor = ThemeManager.Colors.AccentSecondary,
                BorderThickness = 0f,
                Radius = 6f,
                RenderOrder2D = DialogPanelOrder,
                Size = new int2(QueueColumnWidth, QueueHeaderHeight)
            };
            QueueSectionRoot.AddComponent(QueueHeaderBackground);

            QueueHeaderTextHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = new float3(QueueListPadding, 6f, 0.1f),
                InternalEntity = true
            };
            QueueSectionRoot.AddChild(QueueHeaderTextHost);

            QueueHeaderText = new TextComponent {
                Font = DialogFont,
                Text = "Queue",
                Color = ThemeManager.Colors.InputForegroundPrimary,
                RenderOrder2D = DialogTextOrder
            };
            QueueHeaderTextHost.AddComponent(QueueHeaderText);

            QueueItemsRoot = new EditorEntity {
                LayerMask = LayerMask,
                Position = new float3(0f, QueueHeaderHeight + QueueListPadding, 0.1f),
                InternalEntity = true
            };
            QueueSectionRoot.AddChild(QueueItemsRoot);

            OutputLabelHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            BuildColumnRoot.AddChild(OutputLabelHost);

            CopySourceLabelHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            BuildColumnRoot.AddChild(CopySourceLabelHost);

            CopySourceLabelText = new TextComponent {
                Font = DialogFont,
                Text = "Copy Map List From",
                Color = ThemeManager.Colors.InputForegroundPrimary,
                RenderOrder2D = DialogTextOrder
            };
            CopySourceLabelHost.AddComponent(CopySourceLabelText);

            CopySourcePlatformComboBoxHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            BuildColumnRoot.AddChild(CopySourcePlatformComboBoxHost);

            CopySourcePlatformComboBox = new ComboBoxComponent(new int2(200, OutputFieldHeight), DialogFont, Array.Empty<string>(), -1);
            CopySourcePlatformComboBox.SetRenderOrders(DialogPanelOrder, DialogTextOrder, RenderOrder2D.ModalBackground, RenderOrder2D.ModalForeground);
            CopySourcePlatformComboBoxHost.AddComponent(CopySourcePlatformComboBox);

            CopyMapListButtonHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            BuildColumnRoot.AddChild(CopyMapListButtonHost);

            CopyMapListButton = new ButtonComponent("Copy", new int2(84, FooterButtonHeight), DialogFont, HandleCopyMapListClicked);
            CopyMapListButton.SetRenderOrders(DialogPanelOrder, DialogTextOrder);
            CopyMapListButtonHost.AddComponent(CopyMapListButton);

            OutputLabelText = new TextComponent {
                Font = DialogFont,
                Text = "Output Folder",
                Color = ThemeManager.Colors.InputForegroundPrimary,
                RenderOrder2D = DialogTextOrder
            };
            OutputLabelHost.AddComponent(OutputLabelText);

            DebugBuildLabelHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            BuildColumnRoot.AddChild(DebugBuildLabelHost);

            DebugBuildLabelText = new TextComponent {
                Font = DialogFont,
                Text = "Debug build",
                Color = ThemeManager.Colors.InputForegroundPrimary,
                RenderOrder2D = DialogTextOrder
            };
            DebugBuildLabelHost.AddComponent(DebugBuildLabelText);

            OutputFieldHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            BuildColumnRoot.AddChild(OutputFieldHost);

            OutputDirectoryField = new TextBoxComponent(new int2(GetOutputFieldWidth(), OutputFieldHeight), DialogFont, "Select an output folder");
            OutputDirectoryField.SetRenderOrders(DialogPanelOrder, DialogTextOrder);
            OutputDirectoryField.TextChanged += HandleOutputDirectoryFieldTextChanged;
            OutputFieldHost.AddComponent(OutputDirectoryField);

            DebugBuildCheckBoxHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            BuildColumnRoot.AddChild(DebugBuildCheckBoxHost);

            DebugBuildCheckBox = new CheckBoxComponent(new int2(18, 18), DialogFont, false);
            DebugBuildCheckBox.SetRenderOrders(DialogPanelOrder, DialogTextOrder);
            DebugBuildCheckBoxHost.AddComponent(DebugBuildCheckBox);

            BrowseOutputFolderButtonHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            BuildColumnRoot.AddChild(BrowseOutputFolderButtonHost);

            BrowseOutputFolderButton = new ButtonComponent("Browse", new int2(BrowseButtonWidth, FooterButtonHeight), DialogFont, HandleBrowseOutputFolderClicked);
            BrowseOutputFolderButton.SetRenderOrders(DialogPanelOrder, DialogTextOrder);
            BrowseOutputFolderButtonHost.AddComponent(BrowseOutputFolderButton);

            AddToBuildButtonHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            QueueColumnRoot.AddChild(AddToBuildButtonHost);

            AddToBuildButton = new ButtonComponent("Add to Build", new int2(FooterButtonWidth, FooterButtonHeight), DialogFont, HandleAddToBuildClicked);
            AddToBuildButton.SetRenderOrders(DialogPanelOrder, DialogTextOrder);
            AddToBuildButtonHost.AddComponent(AddToBuildButton);

            BuildQueueButtonHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            QueueColumnRoot.AddChild(BuildQueueButtonHost);

            BuildQueueButton = new ButtonComponent("Build Queue", new int2(FooterButtonWidth, FooterButtonHeight), DialogFont, HandleBuildQueueRequested);
            BuildQueueButton.SetRenderOrders(DialogPanelOrder, DialogTextOrder);
            BuildQueueButtonHost.AddComponent(BuildQueueButton);

            BuildLogsRoot = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            DialogPanelRoot.AddChild(BuildLogsRoot);

            BuildLogsBackground = new RoundedRectComponent {
                FillColor = ThemeManager.Colors.SurfacePrimary,
                BorderColor = ThemeManager.Colors.AccentTertiary,
                BorderThickness = 2f,
                Radius = 6f,
                RenderOrder2D = DialogPanelOrder,
                Size = new int2(1, 1)
            };
            BuildLogsRoot.AddComponent(BuildLogsBackground);

            BuildLogsTitleHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            BuildLogsRoot.AddChild(BuildLogsTitleHost);

            BuildLogsTitleText = new TextComponent {
                Font = DialogFont,
                Text = "Build Logs",
                Color = ThemeManager.Colors.InputForegroundPrimary,
                RenderOrder2D = DialogTextOrder
            };
            BuildLogsTitleHost.AddComponent(BuildLogsTitleText);

            BuildLogsProgressTrackHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            BuildLogsRoot.AddChild(BuildLogsProgressTrackHost);

            BuildLogsProgressTrack = new RoundedRectComponent {
                FillColor = ThemeManager.Colors.SurfaceInput,
                BorderColor = ThemeManager.Colors.AccentTertiary,
                BorderThickness = 1f,
                Radius = 4f,
                RenderOrder2D = DialogPanelOrder,
                Size = new int2(1, BuildLogsProgressBarHeight)
            };
            BuildLogsProgressTrackHost.AddComponent(BuildLogsProgressTrack);

            BuildLogsProgressFillHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            BuildLogsProgressTrackHost.AddChild(BuildLogsProgressFillHost);

            BuildLogsProgressFill = new RoundedRectComponent {
                FillColor = ThemeManager.Colors.AccentSecondary,
                BorderColor = ThemeManager.Colors.AccentSecondary,
                BorderThickness = 0f,
                Radius = 4f,
                RenderOrder2D = DialogPanelOrder,
                Size = new int2(1, BuildLogsProgressBarHeight - 2)
            };
            BuildLogsProgressFillHost.AddComponent(BuildLogsProgressFill);

            BuildLogsTextHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            BuildLogsRoot.AddChild(BuildLogsTextHost);

            BuildLogsText = new TextComponent {
                Font = DialogFont,
                Text = string.Empty,
                Color = ThemeManager.Colors.InputForegroundPrimary,
                RenderOrder2D = DialogTextOrder,
                Size = new int2(1, 1)
            };
            BuildLogsTextHost.AddComponent(BuildLogsText);
        }

        /// <summary>
        /// Shows the dialog using enabled platforms, project scenes, and the current local build configuration.
        /// </summary>
        /// <param name="supportedPlatformIds">Enabled platforms for the project.</param>
        /// <param name="sceneIds">Project-relative scenes available for the build.</param>
        /// <param name="activePlatformId">Platform tab to activate first.</param>
        /// <param name="buildConfig">Current local build configuration to render.</param>
        public void Show(
            IReadOnlyList<string> supportedPlatformIds,
            IReadOnlyList<string> sceneIds,
            string activePlatformId,
            EditorBuildConfigDocument buildConfig) {
            Show(supportedPlatformIds, sceneIds, activePlatformId, buildConfig, null);
        }

        /// <summary>
        /// Shows the dialog for the provided available and currently supported platforms.
        /// </summary>
        /// <param name="availablePlatforms">Selectable platforms discovered for the current engine environment.</param>
        /// <param name="supportedPlatforms">Platforms currently written into the project file.</param>
        /// <param name="activePlatformId">Platform tab to activate first.</param>
        /// <param name="buildConfig">Current local build configuration to render.</param>
        /// <param name="selectionModel">Builder-provided metadata for the active platform.</param>
        public void Show(
            IReadOnlyList<string> supportedPlatformIds,
            IReadOnlyList<string> sceneIds,
            string activePlatformId,
            EditorBuildConfigDocument buildConfig,
            EditorPlatformBuildSelectionModel selectionModel) {
            if (supportedPlatformIds == null) {
                throw new ArgumentNullException(nameof(supportedPlatformIds));
            }

            if (sceneIds == null) {
                throw new ArgumentNullException(nameof(sceneIds));
            }

            if (buildConfig == null) {
                throw new ArgumentNullException(nameof(buildConfig));
            }

            ResetDialogPositioning();
            CopyPlatforms(supportedPlatformIds);
            CopyScenes(sceneIds);
            CurrentBuildConfig = buildConfig;
            ActivePlatformSelectionModel = selectionModel;
            EnsurePlatformConfigs();
            SetActivePlatform(activePlatformId);
            RebuildPlatformTabs();
            RebuildActivePlatformSceneRows();
            RebuildQueueRows();
            RebuildBuildLogs();
            LayoutStaticControls();
            UpdateDialogChromeLayout();
            CenterDialogIfNeeded();
            Enabled = true;
        }

        /// <summary>
        /// Updates the cached host size used to center or preserve the dialog position during editor resizes.
        /// </summary>
        /// <param name="width">Current host width in pixels.</param>
        /// <param name="height">Current host height in pixels.</param>
        public void UpdateLayout(int width, int height) {
            if (!UpdateDialogFrame(width, height)) {
                return;
            }

            LayoutLowerLeftControls();
            LayoutStaticControls();
        }

        /// <summary>
        /// Hides the dialog and stops any active title-bar drag.
        /// </summary>
        public void Hide() {
            ClearDialogBackdrop();
            ResetDialogPositioning();
            Enabled = false;
        }

        /// <summary>
        /// Captures one queued-build request from the current active platform state.
        /// </summary>
        void HandleAddToBuildClicked() {
            SyncActivePlatformConfig();

            EditorBuildPlatformConfigDocument platformConfig = FindPlatformConfig(ActivePlatformId);
            bool hasInvalidOutputDirectory = string.IsNullOrWhiteSpace(platformConfig.OutputDirectoryPath);
            if (hasInvalidOutputDirectory) {
                OutputDirectoryField.SetInvalidState(true);
                OutputDirectoryField.TriggerInvalidShake();
            }

            List<string> selectedSceneIds = new List<string>(platformConfig.SelectedSceneIds.Count);
            for (int index = 0; index < platformConfig.SelectedSceneIds.Count; index++) {
                selectedSceneIds.Add(platformConfig.SelectedSceneIds[index]);
            }

            bool hasNoSelectedScenes = selectedSceneIds.Count == 0;
            if (hasNoSelectedScenes) {
                SetSceneListInvalidState(true);
                TriggerSceneListInvalidShake();
            }

            if (hasInvalidOutputDirectory || hasNoSelectedScenes) {
                return;
            }

            List<string> orderedSceneIds = BuildOrderedSceneIds(platformConfig, selectedSceneIds);
            EnsurePlatformSelectionDefaults(platformConfig);
            AddRequested?.Invoke(new BuildDialogAddRequest(
                ActivePlatformId,
                orderedSceneIds,
                platformConfig.OutputDirectoryPath,
                platformConfig.DebugBuild,
                platformConfig.SelectedBuildProfileId,
                platformConfig.SelectedGraphicsProfileId,
                platformConfig.SelectedBuildOptionValues,
                platformConfig.SelectedGraphicsOptionValues));
        }

        /// <summary>
        /// Clears the output-folder invalid state as soon as the current text becomes non-blank.
        /// </summary>
        /// <param name="textBox">Output-folder text box that changed.</param>
        void HandleOutputDirectoryFieldTextChanged(TextBoxComponent textBox) {
            if (textBox == null) {
                throw new ArgumentNullException(nameof(textBox));
            }

            if (!string.IsNullOrWhiteSpace(textBox.Text)) {
                textBox.SetInvalidState(false);
            }
        }

        /// <summary>
        /// Clears the scene-list invalid state as soon as at least one scene becomes selected again.
        /// Empty scene selections are still validated only when Add to Build is clicked.
        /// </summary>
        /// <param name="checkBox">Checkbox whose selection changed.</param>
        /// <param name="isChecked">True when the checkbox is now selected.</param>
        void HandleSceneSelectionChanged(CheckBoxComponent checkBox, bool isChecked) {
            if (checkBox == null) {
                throw new ArgumentNullException(nameof(checkBox));
            }

            if (isChecked) {
                SetSceneListInvalidState(false);
            }
        }

        /// <summary>
        /// Raises the queue-item remove request event for the supplied queued build id.
        /// </summary>
        /// <param name="queueItemId">Persisted queue item id that should be removed.</param>
        void HandleQueueItemRemoveClicked(string queueItemId) {
            if (string.IsNullOrWhiteSpace(queueItemId)) {
                throw new ArgumentException("Queue item id is required.", nameof(queueItemId));
            }

            RemoveQueueItemRequested?.Invoke(queueItemId);
        }

        /// <summary>
        /// Raises the build-queue request event for the current queue.
        /// </summary>
        void HandleBuildQueueRequested() {
            SyncActivePlatformConfig();
            BuildQueueRequested?.Invoke();
        }

        /// <summary>
        /// Raises the browse request so the editor host can choose one output folder for the active platform.
        /// </summary>
        void HandleBrowseOutputFolderClicked() {
            SyncActivePlatformConfig();
            BrowseOutputFolderRequested?.Invoke();
        }

        /// <summary>
        /// Copies the selected map list from the chosen source platform into the current active platform.
        /// </summary>
        void HandleCopyMapListClicked() {
            if (!CopySourcePlatformComboBox.HasSelection) {
                return;
            }

            SyncActivePlatformConfig();

            string sourcePlatformId = CopySourcePlatformComboBox.SelectedItem;
            EditorBuildPlatformConfigDocument sourcePlatformConfig = FindPlatformConfig(sourcePlatformId);
            EditorBuildPlatformConfigDocument activePlatformConfig = FindPlatformConfig(ActivePlatformId);

            activePlatformConfig.SelectedSceneIds.Clear();
            for (int index = 0; index < sourcePlatformConfig.SelectedSceneIds.Count; index++) {
                activePlatformConfig.SelectedSceneIds.Add(sourcePlatformConfig.SelectedSceneIds[index]);
            }

            CopySceneOrders(sourcePlatformConfig, activePlatformConfig);

            RebuildActivePlatformSceneRows();
        }

        /// <summary>
        /// Raises the cancel event and hides the dialog.
        /// </summary>
        void HandleCancelRequested() {
            Hide();
            CancelRequested?.Invoke();
        }

        /// <summary>
        /// Copies the enabled platform ids into the dialog state.
        /// </summary>
        /// <param name="supportedPlatformIds">Platforms enabled for the project.</param>
        void CopyPlatforms(IReadOnlyList<string> supportedPlatformIds) {
            SupportedPlatformIds.Clear();
            for (int index = 0; index < supportedPlatformIds.Count; index++) {
                SupportedPlatformIds.Add(supportedPlatformIds[index]);
            }
        }

        /// <summary>
        /// Copies the available project scenes into the dialog state.
        /// </summary>
        /// <param name="sceneIds">Project-relative scene ids shown by the active tab.</param>
        void CopyScenes(IReadOnlyList<string> sceneIds) {
            SceneIds.Clear();
            for (int index = 0; index < sceneIds.Count; index++) {
                SceneIds.Add(sceneIds[index]);
            }
        }

        /// <summary>
        /// Copies one platform's scene-order entries into another platform configuration.
        /// </summary>
        /// <param name="sourcePlatformConfig">Platform configuration supplying the saved order values.</param>
        /// <param name="destinationPlatformConfig">Platform configuration receiving the copied order values.</param>
        void CopySceneOrders(EditorBuildPlatformConfigDocument sourcePlatformConfig, EditorBuildPlatformConfigDocument destinationPlatformConfig) {
            if (sourcePlatformConfig == null) {
                throw new ArgumentNullException(nameof(sourcePlatformConfig));
            }

            if (destinationPlatformConfig == null) {
                throw new ArgumentNullException(nameof(destinationPlatformConfig));
            }

            destinationPlatformConfig.SceneOrders.Clear();
            for (int index = 0; index < sourcePlatformConfig.SceneOrders.Count; index++) {
                EditorBuildSceneOrderDocument sourceSceneOrder = sourcePlatformConfig.SceneOrders[index];
                destinationPlatformConfig.SceneOrders.Add(new EditorBuildSceneOrderDocument {
                    SceneId = sourceSceneOrder.SceneId,
                    OrderNumber = sourceSceneOrder.OrderNumber
                });
            }
        }

        /// <summary>
        /// Ensures the build configuration contains one local platform entry for each enabled platform.
        /// </summary>
        void EnsurePlatformConfigs() {
            if (CurrentBuildConfig.Platforms == null) {
                CurrentBuildConfig.Platforms = new List<EditorBuildPlatformConfigDocument>(SupportedPlatformIds.Count);
            }

            for (int platformIndex = 0; platformIndex < SupportedPlatformIds.Count; platformIndex++) {
                string platformId = SupportedPlatformIds[platformIndex];
                EditorBuildPlatformConfigDocument platformConfig = FindPlatformConfig(platformId);
                if (platformConfig == null) {
                    platformConfig = new EditorBuildPlatformConfigDocument {
                        PlatformId = platformId
                    };
                    CurrentBuildConfig.Platforms.Add(platformConfig);
                }

                if (platformConfig.SelectedSceneIds == null) {
                    platformConfig.SelectedSceneIds = new List<string>();
                }

                if (platformConfig.SceneOrders == null) {
                    platformConfig.SceneOrders = new List<EditorBuildSceneOrderDocument>();
                }
            }

            if (CurrentBuildConfig.QueueItems == null) {
                CurrentBuildConfig.QueueItems = new List<EditorBuildQueueItemDocument>();
            }
        }

        /// <summary>
        /// Sets the active platform id, falling back to the first enabled platform when needed.
        /// </summary>
        /// <param name="activePlatformId">Requested initial active platform id.</param>
        void SetActivePlatform(string activePlatformId) {
            if (!string.IsNullOrWhiteSpace(activePlatformId) && SupportedPlatformIds.Contains(activePlatformId)) {
                ActivePlatformId = activePlatformId;
                return;
            }

            if (SupportedPlatformIds.Count == 0) {
                ActivePlatformId = "";
                return;
            }

            ActivePlatformId = SupportedPlatformIds[0];
        }

        /// <summary>
        /// Rebuilds the platform tab buttons using the current enabled platform list.
        /// </summary>
        void RebuildPlatformTabs() {
            ClearEntities(PlatformTabHosts);
            PlatformTabs.Clear();

            for (int index = 0; index < SupportedPlatformIds.Count; index++) {
                string platformId = SupportedPlatformIds[index];
                EditorEntity tabHost = new EditorEntity {
                    LayerMask = LayerMask,
                    Position = new float3(index * (PlatformTabWidth + 8), 0f, 0.1f),
                    InternalEntity = true
                };
                BuildColumnRoot.AddChild(tabHost);
                PlatformTabHosts.Add(tabHost);

                ButtonComponent tabButton = new ButtonComponent(platformId, new int2(PlatformTabWidth, PlatformTabHeight), DialogFont, () => HandlePlatformTabClicked(platformId));
                tabButton.SetRenderOrders(DialogPanelOrder, DialogTextOrder);
                if (platformId != ActivePlatformId) {
                    tabButton.UseHoverOnlyBackground();
                    tabButton.SetTextColor(ThemeManager.Colors.InputForegroundPrimary);
                }

                tabHost.AddComponent(tabButton);
                PlatformTabs.Add(tabButton);
            }
        }

        /// <summary>
        /// Switches the active tab to the supplied platform id and rerenders the active platform content.
        /// </summary>
        /// <param name="platformId">Platform id that should become active.</param>
        void HandlePlatformTabClicked(string platformId) {
            if (ActivePlatformId == platformId) {
                return;
            }

            SyncActivePlatformConfig();
            ActivePlatformId = platformId;
            RebuildPlatformTabs();
            RebuildActivePlatformSceneRows();
        }

        /// <summary>
        /// Rebuilds the scene checklist for the current active platform.
        /// </summary>
        void RebuildActivePlatformSceneRows() {
            ClearEntities(MapLabelHosts);
            ClearEntities(MapCheckBoxHosts);
            ClearEntities(MapOrderHosts);
            MapLabelTexts.Clear();
            MapCheckBoxes.Clear();
            MapOrderFields.Clear();

            EditorBuildPlatformConfigDocument platformConfig = FindPlatformConfig(ActivePlatformId);
            EnsureSceneOrderEntries(platformConfig);
            List<string> selectedSceneIds = platformConfig.SelectedSceneIds;
            List<string> orderedSceneIds = BuildDisplayedSceneIds(platformConfig);
            int topOffset = SceneListPadding;
            int orderFieldX = SceneListPadding;
            int sceneLabelX = orderFieldX + SceneOrderFieldWidth + 8;
            int checkBoxX = GetBuildColumnWidth() - SceneListPadding - 18;

            for (int index = 0; index < orderedSceneIds.Count; index++) {
                string sceneId = orderedSceneIds[index];
                float rowY = topOffset + (index * SceneRowHeight);

                EditorEntity orderHost = new EditorEntity {
                    LayerMask = LayerMask,
                    Position = new float3(orderFieldX, rowY - 2, 0.1f),
                    InternalEntity = true
                };
                SceneListRoot.AddChild(orderHost);
                MapOrderHosts.Add(orderHost);

                TextBoxComponent orderField = new TextBoxComponent(new int2(SceneOrderFieldWidth, SceneOrderFieldHeight), DialogFont, string.Empty);
                orderField.SetRenderOrders(DialogPanelOrder, DialogTextOrder);
                orderField.TextChanged += currentOrderField => HandleSceneOrderFieldChanged(sceneId, currentOrderField);
                orderField.Submitted += currentOrderField => HandleSceneOrderFieldSubmitted(sceneId, currentOrderField);
                orderField.Text = GetSceneOrderNumber(platformConfig, sceneId).ToString();
                orderHost.AddComponent(orderField);
                MapOrderFields.Add(orderField);

                EditorEntity labelHost = new EditorEntity {
                    LayerMask = LayerMask,
                    Position = new float3(sceneLabelX, rowY, 0.1f),
                    InternalEntity = true
                };
                SceneListRoot.AddChild(labelHost);
                MapLabelHosts.Add(labelHost);

                TextComponent labelText = new TextComponent {
                    Font = DialogFont,
                    Text = sceneId,
                    Color = ThemeManager.Colors.InputForegroundPrimary,
                    RenderOrder2D = DialogTextOrder
                };
                labelHost.AddComponent(labelText);
                MapLabelTexts.Add(labelText);

                EditorEntity checkBoxHost = new EditorEntity {
                    LayerMask = LayerMask,
                    Position = new float3(checkBoxX, rowY - 2, 0.1f),
                    InternalEntity = true
                };
                SceneListRoot.AddChild(checkBoxHost);
                MapCheckBoxHosts.Add(checkBoxHost);

                bool isChecked = selectedSceneIds.Contains(sceneId);
                CheckBoxComponent checkBox = new CheckBoxComponent(new int2(18, 18), DialogFont, isChecked);
                checkBox.SetRenderOrders(DialogPanelOrder, DialogTextOrder);
                checkBox.CheckedChanged += HandleSceneSelectionChanged;
                checkBoxHost.AddComponent(checkBox);
                MapCheckBoxes.Add(checkBox);
            }

            LayoutLowerLeftControls();
            RebuildCopySourcePlatformItems();
            OutputDirectoryField.Text = platformConfig.OutputDirectoryPath ?? "";
            OutputDirectoryField.SetInvalidState(false);
            DebugBuildCheckBox.IsChecked = platformConfig.DebugBuild;
            SetSceneListInvalidState(false);
        }

        /// <summary>
        /// Replaces the active platform output-directory text with a host-selected folder path.
        /// </summary>
        /// <param name="outputDirectoryPath">Folder path chosen by the host picker.</param>
        public void SetOutputDirectoryPath(string outputDirectoryPath) {
            if (string.IsNullOrWhiteSpace(outputDirectoryPath)) {
                return;
            }

            OutputDirectoryField.Text = outputDirectoryPath;

            if (CurrentBuildConfig == null || string.IsNullOrWhiteSpace(ActivePlatformId)) {
                return;
            }

            EditorBuildPlatformConfigDocument platformConfig = FindPlatformConfig(ActivePlatformId);
            platformConfig.OutputDirectoryPath = outputDirectoryPath;
        }

        /// <summary>
        /// Rebuilds the queue summary rows shown on the right side of the dialog.
        /// </summary>
        void RebuildQueueRows() {
            ClearEntities(QueueItemHosts);
            QueueItemTexts.Clear();
            ClearEntities(QueueItemRemoveButtonHosts);
            QueueItemRemoveButtons.Clear();
            QueueItemCardBackgrounds.Clear();

            for (int index = 0; index < CurrentBuildConfig.QueueItems.Count; index++) {
                EditorBuildQueueItemDocument queueItem = CurrentBuildConfig.QueueItems[index];
                EditorEntity queueItemHost = new EditorEntity {
                    LayerMask = LayerMask,
                    Position = new float3(2f, index * QueueRowHeight, 0.1f),
                    InternalEntity = true
                };
                QueueItemsRoot.AddChild(queueItemHost);
                QueueItemHosts.Add(queueItemHost);

                RoundedRectComponent queueCardBackground = new RoundedRectComponent {
                    FillColor = ThemeManager.Colors.SurfacePrimary,
                    BorderColor = ThemeManager.Colors.SurfacePrimary,
                    BorderThickness = 0f,
                    Radius = 0f,
                    RenderOrder2D = DialogPanelOrder,
                    Size = new int2(GetQueueCardWidth(), GetQueueCardHeight())
                };
                queueItemHost.AddComponent(queueCardBackground);
                QueueItemCardBackgrounds.Add(queueCardBackground);

                EditorEntity queueSeparatorHost = new EditorEntity {
                    LayerMask = LayerMask,
                    Position = new float3(0f, QueueRowHeight - 1, 0.2f),
                    InternalEntity = true
                };
                queueItemHost.AddChild(queueSeparatorHost);

                SpriteComponent queueSeparator = new SpriteComponent {
                    Texture = TextureUtils.PixelTexture,
                    Color = ThemeManager.Colors.AccentTertiary,
                    RenderOrder2D = DialogPanelOrder,
                    Size = new int2(GetQueueCardWidth(), 1)
                };
                queueSeparatorHost.AddComponent(queueSeparator);

                EditorEntity removeButtonHost = new EditorEntity {
                    LayerMask = LayerMask,
                    Position = new float3(GetQueueCardWidth() - QueueCardRemoveButtonWidth - QueueCardTextPadding, 8f, 0.2f),
                    InternalEntity = true
                };
                queueItemHost.AddChild(removeButtonHost);
                QueueItemRemoveButtonHosts.Add(removeButtonHost);

                ButtonComponent removeButton = new ButtonComponent("X", new int2(QueueCardRemoveButtonWidth, 24), DialogFont, () => HandleQueueItemRemoveClicked(queueItem.QueueItemId));
                removeButton.SetRenderOrders(DialogPanelOrder, DialogTextOrder);
                removeButtonHost.AddComponent(removeButton);
                QueueItemRemoveButtons.Add(removeButton);

                int queueTextWidth = GetQueueCardTextWidth();
                int queueTextHeight = Math.Max(GetDialogLineHeight() * 2, GetQueueCardHeight() - 12);
                TextComponent queueText = new TextComponent {
                    Font = DialogFont,
                    Text = BuildQueueItemText(queueItem),
                    Color = ThemeManager.Colors.InputForegroundPrimary,
                    RenderOrder2D = DialogTextOrder,
                    Size = new int2(queueTextWidth, queueTextHeight)
                };
                EditorEntity queueTextHost = new EditorEntity {
                    LayerMask = LayerMask,
                    Position = new float3(QueueCardTextPadding, 8f, 0.2f),
                    InternalEntity = true
                };
                queueItemHost.AddChild(queueTextHost);
                queueTextHost.AddComponent(queueText);
                QueueItemTexts.Add(queueText);
            }
        }

        /// <summary>
        /// Rebuilds the bottom build-log section using the current persisted queue state.
        /// </summary>
        void RebuildBuildLogs() {
            BuildLogsText.Text = BuildBuildLogText();
        }

        /// <summary>
        /// Anchors the copy-source, output-folder, and add-to-build controls to the lower portion of the left column.
        /// </summary>
        void LayoutLowerLeftControls() {
            int outputFieldY = LegacyContentHeight - HeaderHeight - PanelPadding - FooterButtonHeight - 8 - 16 - OutputFieldHeight;
            int addButtonY = outputFieldY + OutputFieldHeight + 16 + 18 + 16;
            int outputLabelY = outputFieldY - 20;
            int copyComboY = outputLabelY - 16 - OutputFieldHeight;
            int copyLabelY = copyComboY - 20;
            int debugBuildY = outputFieldY + OutputFieldHeight + 16;
            int sceneListTop = PlatformTabHeight + SceneListTopMargin;
            int sceneListHeight = Math.Max(1, copyLabelY - 12 - sceneListTop);

            SceneListRoot.Position = new float3(SceneListShakeOffsetX, sceneListTop, 0.1f);
            SceneListBackground.Size = new int2(GetBuildColumnWidth(), sceneListHeight);
            CopySourceLabelHost.Position = new float3(0f, copyLabelY, 0.1f);
            CopySourcePlatformComboBoxHost.Position = new float3(0f, copyComboY, 0.1f);
            CopyMapListButtonHost.Position = new float3(CopySourcePlatformComboBox.Size.X + 8f, copyComboY, 0.1f);
            OutputLabelHost.Position = new float3(0f, outputLabelY, 0.1f);
            OutputFieldHost.Position = new float3(OutputDirectoryField.CurrentShakeOffsetX, outputFieldY, 0.1f);
            BrowseOutputFolderButtonHost.Position = new float3(GetOutputFieldWidth() + 8f, outputFieldY, 0.1f);
            DebugBuildLabelHost.Position = new float3(24f, debugBuildY, 0.1f);
            DebugBuildCheckBoxHost.Position = new float3(0f, debugBuildY - 2, 0.1f);
            AddToBuildButtonHost.Position = new float3(0f, addButtonY, 0.1f);
        }

        /// <summary>
        /// Advances transient scene-list feedback animations such as the invalid-selection shake.
        /// </summary>
        internal void UpdateFeedbackAnimation() {
            if (!IsSceneListShakeActive) {
                return;
            }

            SceneListShakeElapsedSeconds += SceneListEffectFrameDeltaSeconds;
            if (SceneListShakeElapsedSeconds >= SceneListShakeDurationSeconds) {
                SceneListShakeOffsetX = 0f;
                IsSceneListShakeActive = false;
                LayoutLowerLeftControls();
                return;
            }

            double progress = SceneListShakeElapsedSeconds / SceneListShakeDurationSeconds;
            double amplitude = SceneListShakeAmplitudePixels * (1d - progress);
            double angle = SceneListShakeElapsedSeconds * SceneListShakeFrequencyHz * Math.PI * 2d;
            double offset = Math.Sin(angle) * amplitude;
            SceneListShakeOffsetX = (float)offset;
            LayoutLowerLeftControls();
        }

        /// <summary>
        /// Builds one queue-row summary string for the supplied persisted queue item.
        /// </summary>
        /// <param name="queueItem">Persisted queue item to summarize.</param>
        /// <returns>Queue summary text shown in the queue column.</returns>
        string BuildQueueItemText(EditorBuildQueueItemDocument queueItem) {
            string summaryText = queueItem.PlatformId + " | " + queueItem.Status + " | " + queueItem.SelectedSceneIds.Count + " scene(s)";
            if (!string.IsNullOrWhiteSpace(queueItem.SelectedBuildProfileId)) {
                summaryText += " | build " + queueItem.SelectedBuildProfileId;
            }
            if (!string.IsNullOrWhiteSpace(queueItem.SelectedGraphicsProfileId)) {
                summaryText += " | gfx " + queueItem.SelectedGraphicsProfileId;
            }

            string statusMessage = BuildQueueItemStatusMessage(queueItem.StatusMessage);
            return summaryText + "\n" + statusMessage;
        }

        /// <summary>
        /// Applies the static layout for the queue button based on the current panel geometry.
        /// </summary>
        void LayoutStaticControls() {
            int buildQueueButtonY = LegacyContentHeight - HeaderHeight - PanelPadding - FooterButtonHeight - 8;
            AddToBuildButtonHost.Position = new float3(0f, buildQueueButtonY, 0.1f);
            BuildQueueButtonHost.Position = new float3(FooterButtonWidth + 8f, buildQueueButtonY, 0.1f);
            QueueListBackground.Size = new int2(QueueColumnWidth, GetQueueSectionHeight());
            QueueHeaderBackground.Size = new int2(QueueColumnWidth, QueueHeaderHeight);
            LayoutBuildLogsSection();
        }

        /// <summary>
        /// Positions and sizes the build-log section beneath the existing controls.
        /// </summary>
        void LayoutBuildLogsSection() {
            int buildLogsTopY = LegacyContentHeight;
            int buildLogsWidth = PanelWidth - (PanelPadding * 2);
            int buildLogsInnerWidth = buildLogsWidth - (BuildLogsPadding * 2);
            int progressTrackY = BuildLogsPadding + BuildLogsTitleHeight + 6;
            int progressTrackWidth = buildLogsInnerWidth;
            int progressFillWidth = GetBuildLogProgressFillWidth(Math.Max(0, progressTrackWidth - 2));
            int logTextY = progressTrackY + BuildLogsProgressBarHeight + 10;
            int logTextHeight = Math.Max(1, BuildLogsSectionHeight - logTextY - BuildLogsPadding);

            BuildLogsRoot.Position = new float3(PanelPadding, buildLogsTopY, 0.1f);
            BuildLogsBackground.Size = new int2(buildLogsWidth, BuildLogsSectionHeight);

            BuildLogsTitleHost.Position = new float3(BuildLogsPadding, BuildLogsPadding, 0.1f);
            BuildLogsTitleText.Size = new int2(buildLogsInnerWidth, BuildLogsTitleHeight);

            BuildLogsProgressTrackHost.Position = new float3(BuildLogsPadding, progressTrackY, 0.1f);
            BuildLogsProgressTrack.Size = new int2(progressTrackWidth, BuildLogsProgressBarHeight);
            BuildLogsProgressFillHost.Position = new float3(1f, 1f, 0.1f);
            BuildLogsProgressFill.Size = new int2(progressFillWidth, BuildLogsProgressBarHeight - 2);

            BuildLogsTextHost.Position = new float3(BuildLogsPadding, logTextY, 0.1f);
            BuildLogsText.Size = new int2(buildLogsInnerWidth, Math.Max(BuildLogLineHeight, logTextHeight));
        }

        /// <summary>
        /// Syncs the current active platform checkbox and output-folder edits into the mutable build configuration.
        /// </summary>
        void SyncActivePlatformConfig() {
            if (CurrentBuildConfig == null || string.IsNullOrWhiteSpace(ActivePlatformId)) {
                return;
            }

            EditorBuildPlatformConfigDocument platformConfig = FindPlatformConfig(ActivePlatformId);
            EnsureSceneOrderEntries(platformConfig);
            platformConfig.SelectedSceneIds.Clear();
            for (int index = 0; index < MapCheckBoxes.Count; index++) {
                if (MapCheckBoxes[index].IsChecked) {
                    platformConfig.SelectedSceneIds.Add(SceneIds[index]);
                }
            }

            platformConfig.OutputDirectoryPath = OutputDirectoryField.Text ?? string.Empty;
            platformConfig.DebugBuild = DebugBuildCheckBox.IsChecked;
            EnsurePlatformSelectionDefaults(platformConfig);
        }

        /// <summary>
        /// Ensures the active platform has a selected build profile and graphics profile snapshot.
        /// </summary>
        /// <param name="platformConfig">Active platform configuration to normalize.</param>
        void EnsurePlatformSelectionDefaults(EditorBuildPlatformConfigDocument platformConfig) {
            if (platformConfig == null || ActivePlatformSelectionModel == null) {
                return;
            }

            PlatformBuildProfileDefinition buildProfile = ResolveBuildProfile(platformConfig);
            if (buildProfile != null) {
                platformConfig.SelectedBuildProfileId = buildProfile.ProfileId;
                if (string.IsNullOrWhiteSpace(platformConfig.SelectedGraphicsProfileId)) {
                    platformConfig.SelectedGraphicsProfileId = buildProfile.GraphicsProfileId;
                }
                EnsureSettingDefaults(platformConfig.SelectedBuildOptionValues, buildProfile.Settings);
            }

            PlatformGraphicsProfileDefinition graphicsProfile = ResolveGraphicsProfile(platformConfig, buildProfile);
            if (graphicsProfile != null) {
                platformConfig.SelectedGraphicsProfileId = graphicsProfile.ProfileId;
                EnsureSettingDefaults(platformConfig.SelectedGraphicsOptionValues, graphicsProfile.Settings);
            }

            platformConfig.SelectedBuildOptionValues ??= new Dictionary<string, string>();
            platformConfig.SelectedGraphicsOptionValues ??= new Dictionary<string, string>();
        }

        /// <summary>
        /// Resolves the selected build profile metadata for one platform configuration.
        /// </summary>
        /// <param name="platformConfig">Platform configuration to inspect.</param>
        /// <returns>Resolved build profile metadata, or null when unavailable.</returns>
        PlatformBuildProfileDefinition ResolveBuildProfile(EditorBuildPlatformConfigDocument platformConfig) {
            if (platformConfig == null || ActivePlatformSelectionModel == null) {
                return null;
            }

            return ActivePlatformSelectionModel.ResolveBuildProfile(platformConfig.SelectedBuildProfileId);
        }

        /// <summary>
        /// Resolves the selected graphics profile metadata for one platform configuration.
        /// </summary>
        /// <param name="platformConfig">Platform configuration to inspect.</param>
        /// <param name="buildProfile">Resolved build profile metadata.</param>
        /// <returns>Resolved graphics profile metadata, or null when unavailable.</returns>
        PlatformGraphicsProfileDefinition ResolveGraphicsProfile(EditorBuildPlatformConfigDocument platformConfig, PlatformBuildProfileDefinition buildProfile) {
            if (platformConfig == null || ActivePlatformSelectionModel == null) {
                return null;
            }

            string graphicsProfileId = platformConfig.SelectedGraphicsProfileId;
            if (string.IsNullOrWhiteSpace(graphicsProfileId) && buildProfile != null) {
                graphicsProfileId = buildProfile.GraphicsProfileId;
            }

            return ActivePlatformSelectionModel.ResolveGraphicsProfile(graphicsProfileId);
        }

        /// <summary>
        /// Seeds missing option values from the supplied setting collection.
        /// </summary>
        /// <param name="values">Persisted option values.</param>
        /// <param name="settings">Builder-provided setting definitions.</param>
        static void EnsureSettingDefaults(Dictionary<string, string> values, PlatformSettingDefinition[] settings) {
            if (values == null || settings == null) {
                return;
            }

            for (int index = 0; index < settings.Length; index++) {
                PlatformSettingDefinition setting = settings[index];
                if (setting == null || string.IsNullOrWhiteSpace(setting.SettingId)) {
                    continue;
                }

                if (!values.TryGetValue(setting.SettingId, out string existingValue) || string.IsNullOrWhiteSpace(existingValue)) {
                    values[setting.SettingId] = setting.DefaultValue;
                }
            }
        }

        /// <summary>
        /// Keeps the persisted scene-order entries aligned with the current project scene list.
        /// </summary>
        /// <param name="platformConfig">Platform configuration that stores the saved ordering values.</param>
        void EnsureSceneOrderEntries(EditorBuildPlatformConfigDocument platformConfig) {
            if (platformConfig.SceneOrders == null) {
                platformConfig.SceneOrders = new List<EditorBuildSceneOrderDocument>();
            }

            for (int index = platformConfig.SceneOrders.Count - 1; index >= 0; index--) {
                EditorBuildSceneOrderDocument sceneOrder = platformConfig.SceneOrders[index];
                if (!SceneIds.Contains(sceneOrder.SceneId)) {
                    platformConfig.SceneOrders.RemoveAt(index);
                }
            }

            for (int index = 0; index < SceneIds.Count; index++) {
                string sceneId = SceneIds[index];
                if (FindSceneOrder(platformConfig, sceneId) != null) {
                    continue;
                }

                platformConfig.SceneOrders.Add(new EditorBuildSceneOrderDocument {
                    SceneId = sceneId,
                    OrderNumber = GetNextSceneOrderNumber(platformConfig)
                });
            }
        }

        /// <summary>
        /// Applies one scene-order edit to the persisted active-platform configuration.
        /// </summary>
        /// <param name="sceneId">Project-relative scene identifier whose order should be updated.</param>
        /// <param name="textBox">Textbox currently editing the order value.</param>
        void HandleSceneOrderFieldChanged(string sceneId, TextBoxComponent textBox) {
            if (string.IsNullOrWhiteSpace(sceneId)) {
                throw new ArgumentException("Scene id is required.", nameof(sceneId));
            }

            if (textBox == null) {
                throw new ArgumentNullException(nameof(textBox));
            }

            EditorBuildPlatformConfigDocument platformConfig = FindPlatformConfig(ActivePlatformId);
            EditorBuildSceneOrderDocument sceneOrder = FindSceneOrder(platformConfig, sceneId);
            if (sceneOrder == null) {
                return;
            }

            if (!int.TryParse(textBox.Text, out int orderNumber) || orderNumber <= 0) {
                textBox.SetInvalidState(true);
                return;
            }

            sceneOrder.OrderNumber = orderNumber;
            textBox.SetInvalidState(false);
        }

        /// <summary>
        /// Commits one scene-order edit and rebuilds the active platform rows after Enter is pressed.
        /// </summary>
        /// <param name="sceneId">Project-relative scene identifier whose order was submitted.</param>
        /// <param name="textBox">Textbox that submitted the current order value.</param>
        void HandleSceneOrderFieldSubmitted(string sceneId, TextBoxComponent textBox) {
            if (string.IsNullOrWhiteSpace(sceneId)) {
                throw new ArgumentException("Scene id is required.", nameof(sceneId));
            }

            if (textBox == null) {
                throw new ArgumentNullException(nameof(textBox));
            }

            SyncActivePlatformConfig();
            EditorBuildPlatformConfigDocument platformConfig = FindPlatformConfig(ActivePlatformId);
            EditorBuildSceneOrderDocument sceneOrder = FindSceneOrder(platformConfig, sceneId);
            if (sceneOrder == null) {
                return;
            }

            if (!int.TryParse(textBox.Text, out int orderNumber) || orderNumber <= 0) {
                textBox.SetInvalidState(true);
                return;
            }

            sceneOrder.OrderNumber = orderNumber;
            textBox.SetInvalidState(false);
            RebuildActivePlatformSceneRows();
        }

        /// <summary>
        /// Finds one persisted scene-order entry for the requested scene identifier.
        /// </summary>
        /// <param name="platformConfig">Platform configuration containing the saved ordering values.</param>
        /// <param name="sceneId">Project-relative scene identifier to find.</param>
        /// <returns>Matching scene-order entry, or null when none exists.</returns>
        EditorBuildSceneOrderDocument FindSceneOrder(EditorBuildPlatformConfigDocument platformConfig, string sceneId) {
            for (int index = 0; index < platformConfig.SceneOrders.Count; index++) {
                EditorBuildSceneOrderDocument sceneOrder = platformConfig.SceneOrders[index];
                if (sceneOrder.SceneId == sceneId) {
                    return sceneOrder;
                }
            }

            return null;
        }

        /// <summary>
        /// Returns the next available ordering number for a new scene entry.
        /// </summary>
        /// <param name="platformConfig">Platform configuration that stores the saved ordering values.</param>
        /// <returns>1-based ordering number that comes after all currently saved order values.</returns>
        int GetNextSceneOrderNumber(EditorBuildPlatformConfigDocument platformConfig) {
            int nextOrderNumber = 1;
            for (int index = 0; index < platformConfig.SceneOrders.Count; index++) {
                int candidateOrderNumber = platformConfig.SceneOrders[index].OrderNumber;
                if (candidateOrderNumber >= nextOrderNumber) {
                    nextOrderNumber = candidateOrderNumber + 1;
                }
            }

            return nextOrderNumber;
        }

        /// <summary>
        /// Reads the persisted ordering number for one scene, falling back to the catalog order when needed.
        /// </summary>
        /// <param name="platformConfig">Platform configuration containing the saved ordering values.</param>
        /// <param name="sceneId">Project-relative scene identifier whose order should be resolved.</param>
        /// <returns>1-based ordering number for the requested scene.</returns>
        int GetSceneOrderNumber(EditorBuildPlatformConfigDocument platformConfig, string sceneId) {
            EditorBuildSceneOrderDocument sceneOrder = FindSceneOrder(platformConfig, sceneId);
            if (sceneOrder != null && sceneOrder.OrderNumber > 0) {
                return sceneOrder.OrderNumber;
            }

            int sceneIndex = SceneIds.IndexOf(sceneId);
            if (sceneIndex >= 0) {
                return sceneIndex + 1;
            }

            return int.MaxValue;
        }

        /// <summary>
        /// Sorts one selected-scene list by the currently persisted per-scene ordering values.
        /// </summary>
        /// <param name="platformConfig">Platform configuration that stores the saved ordering values.</param>
        /// <param name="selectedSceneIds">Selected scenes to sort for the queued build request.</param>
        /// <returns>New scene-id list ordered by the saved per-scene order numbers.</returns>
        List<string> BuildOrderedSceneIds(EditorBuildPlatformConfigDocument platformConfig, IReadOnlyList<string> selectedSceneIds) {
            List<string> orderedSceneIds = new List<string>(selectedSceneIds.Count);
            for (int index = 0; index < selectedSceneIds.Count; index++) {
                orderedSceneIds.Add(selectedSceneIds[index]);
            }

            orderedSceneIds.Sort((leftSceneId, rightSceneId) => {
                int leftOrderNumber = GetSceneOrderNumber(platformConfig, leftSceneId);
                int rightOrderNumber = GetSceneOrderNumber(platformConfig, rightSceneId);
                int orderComparison = leftOrderNumber.CompareTo(rightOrderNumber);
                if (orderComparison != 0) {
                    return orderComparison;
                }

                int leftSceneIndex = SceneIds.IndexOf(leftSceneId);
                int rightSceneIndex = SceneIds.IndexOf(rightSceneId);
                return leftSceneIndex.CompareTo(rightSceneIndex);
            });

            return orderedSceneIds;
        }

        /// <summary>
        /// Builds the current scene-list row order from the saved per-scene ordering values.
        /// </summary>
        /// <param name="platformConfig">Platform configuration that stores the saved ordering values.</param>
        /// <returns>Scene ids sorted for display in the build dialog.</returns>
        List<string> BuildDisplayedSceneIds(EditorBuildPlatformConfigDocument platformConfig) {
            List<string> orderedSceneIds = new List<string>(SceneIds.Count);
            for (int index = 0; index < SceneIds.Count; index++) {
                orderedSceneIds.Add(SceneIds[index]);
            }

            orderedSceneIds.Sort((leftSceneId, rightSceneId) => {
                int leftOrderNumber = GetSceneOrderNumber(platformConfig, leftSceneId);
                int rightOrderNumber = GetSceneOrderNumber(platformConfig, rightSceneId);
                int orderComparison = leftOrderNumber.CompareTo(rightOrderNumber);
                if (orderComparison != 0) {
                    return orderComparison;
                }

                int leftSceneIndex = SceneIds.IndexOf(leftSceneId);
                int rightSceneIndex = SceneIds.IndexOf(rightSceneId);
                return leftSceneIndex.CompareTo(rightSceneIndex);
            });

            return orderedSceneIds;
        }

        /// <summary>
        /// Applies or clears the invalid-selection border state on the scene-list container.
        /// </summary>
        /// <param name="isInvalid">True when the scene-list container should use the invalid border color.</param>
        void SetSceneListInvalidState(bool isInvalid) {
            IsSceneListInvalid = isInvalid;
            SceneListBackground.BorderColor = isInvalid
                ? ThemeManager.Colors.StateDanger
                : ThemeManager.Colors.AccentTertiary;
        }

        /// <summary>
        /// Starts a short horizontal shake on the scene-list container to highlight an invalid empty selection.
        /// </summary>
        void TriggerSceneListInvalidShake() {
            SceneListShakeOffsetX = 0f;
            SceneListShakeElapsedSeconds = 0f;
            IsSceneListShakeActive = true;
        }

        /// <summary>
        /// Returns true when at least one scene checkbox is currently selected in the active platform view.
        /// </summary>
        /// <returns>True when the active platform has one selected scene; otherwise false.</returns>
        bool HasAnySelectedScene() {
            for (int index = 0; index < MapCheckBoxes.Count; index++) {
                if (MapCheckBoxes[index].IsChecked) {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Rebuilds the source-platform combo-box items for the active platform.
        /// </summary>
        void RebuildCopySourcePlatformItems() {
            List<string> copySourcePlatformIds = new List<string>(SupportedPlatformIds.Count);
            for (int index = 0; index < SupportedPlatformIds.Count; index++) {
                string platformId = SupportedPlatformIds[index];
                if (platformId != ActivePlatformId) {
                    copySourcePlatformIds.Add(platformId);
                }
            }

            int selectedIndex = -1;
            if (copySourcePlatformIds.Count > 0) {
                selectedIndex = 0;
            }

            CopySourcePlatformComboBox.SetItems(copySourcePlatformIds, selectedIndex);
        }

        /// <summary>
        /// Finds one platform-specific build configuration entry by platform id.
        /// </summary>
        /// <param name="platformId">Platform id to find.</param>
        /// <returns>Matching platform build configuration entry.</returns>
        EditorBuildPlatformConfigDocument FindPlatformConfig(string platformId) {
            for (int index = 0; index < CurrentBuildConfig.Platforms.Count; index++) {
                EditorBuildPlatformConfigDocument platformConfig = CurrentBuildConfig.Platforms[index];
                if (platformConfig.PlatformId == platformId) {
                    return platformConfig;
                }
            }

            throw new InvalidOperationException("Missing build configuration for platform '" + platformId + "'.");
        }

        /// <summary>
        /// Removes each previously rendered child entity in the supplied host list.
        /// </summary>
        /// <param name="hosts">Host entities to remove from the dialog tree.</param>
        void ClearEntities(List<EditorEntity> hosts) {
            for (int index = 0; index < hosts.Count; index++) {
                EditorEntity host = hosts[index];
                if (host.Parent != null) {
                    host.Enabled = false;
                    host.Parent.RemoveChild(host);
                }
            }

            hosts.Clear();
        }

        /// <summary>
        /// Computes the available width for the left build-planning column.
        /// </summary>
        /// <returns>Width available for build-planning controls.</returns>
        int GetBuildColumnWidth() {
            return PanelWidth - QueueColumnWidth - (PanelPadding * 3);
        }

        /// <summary>
        /// Gets the width available for one queued build row inside the queue section.
        /// </summary>
        /// <returns>Width available for one queue row.</returns>
        int GetQueueCardWidth() {
            return QueueColumnWidth - 4;
        }

        /// <summary>
        /// Gets the height used by one queued build row.
        /// </summary>
        /// <returns>Height used by one queue row.</returns>
        int GetQueueCardHeight() {
            return QueueRowHeight;
        }

        /// <summary>
        /// Gets the usable width for clipped queue-row status text.
        /// </summary>
        /// <returns>Width available for the status line.</returns>
        int GetQueueCardTextWidth() {
            return Math.Max(1, GetQueueCardWidth() - (QueueCardTextPadding * 2) - QueueCardRemoveButtonWidth - QueueCardTextButtonGap);
        }

        /// <summary>
        /// Clips the queue-row status message to fit inside the available row width.
        /// </summary>
        /// <param name="statusMessage">Status message to clip.</param>
        /// <returns>Clipped status message or an empty string when no status message is provided.</returns>
        string BuildQueueItemStatusMessage(string statusMessage) {
            if (string.IsNullOrWhiteSpace(statusMessage)) {
                return string.Empty;
            }

            string sanitizedMessage = statusMessage.Replace('\r', ' ').Replace('\n', ' ');
            return ClipTextToWidth(sanitizedMessage, GetQueueCardTextWidth());
        }

        /// <summary>
        /// Clips a single-line string to the available width using the dialog font metrics.
        /// </summary>
        /// <param name="text">Text to clip.</param>
        /// <param name="maxWidth">Maximum allowed width in pixels.</param>
        /// <returns>Original or clipped text depending on the measured width.</returns>
        string ClipTextToWidth(string text, int maxWidth) {
            if (string.IsNullOrEmpty(text) || maxWidth <= 0) {
                return string.Empty;
            }

            if (DialogFont.MeasureTight(text).Width <= maxWidth) {
                return text;
            }

            string ellipsis = "...";
            if (DialogFont.MeasureTight(ellipsis).Width > maxWidth) {
                return string.Empty;
            }

            int lowerBound = 0;
            int upperBound = text.Length;
            while (lowerBound < upperBound) {
                int candidateLength = (lowerBound + upperBound + 1) / 2;
                string candidateText = text.Substring(0, candidateLength) + ellipsis;

                if (DialogFont.MeasureTight(candidateText).Width <= maxWidth) {
                    lowerBound = candidateLength;
                } else {
                    upperBound = candidateLength - 1;
                }
            }

            if (lowerBound <= 0) {
                return ellipsis;
            }

            return text.Substring(0, lowerBound) + ellipsis;
        }

        /// <summary>
        /// Gets the height available for the bordered queue section above the queue action button.
        /// </summary>
        /// <returns>Height available for the queue section chrome and cards.</returns>
        int GetQueueSectionHeight() {
            return LegacyContentHeight - HeaderHeight - PanelPadding - FooterButtonHeight - 20;
        }

        /// <summary>
        /// Computes the width available for the output-folder text box after reserving browse-button space.
        /// </summary>
        /// <returns>Width available for the output-folder text box.</returns>
        int GetOutputFieldWidth() {
            return GetBuildColumnWidth() - BrowseButtonWidth - 8;
        }

        /// <summary>
        /// Builds the multiline log text shown in the dedicated build-log section.
        /// </summary>
        /// <returns>Queued-build status summary text.</returns>
        string BuildBuildLogText() {
            if (CurrentBuildConfig == null || CurrentBuildConfig.QueueItems == null || CurrentBuildConfig.QueueItems.Count == 0) {
                return string.Concat(
                    "Progress: 0%",
                    "\nNo queued builds yet.");
            }

            double progressFraction = GetBuildProgressFraction();
            int completedCount = GetBuildCompletedCount();
            int totalCount = CurrentBuildConfig.QueueItems.Count;
            List<string> lines = new List<string>(BuildLogVisibleLineCount);
            lines.Add(string.Concat(
                "Progress: ",
                Math.Round(progressFraction * 100d).ToString(System.Globalization.CultureInfo.InvariantCulture),
                "% (",
                completedCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "/",
                totalCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
                " complete)"));

            int maxQueueLines = BuildLogVisibleLineCount - 2;
            int queueLineCount = Math.Min(CurrentBuildConfig.QueueItems.Count, maxQueueLines);
            for (int index = 0; index < queueLineCount; index++) {
                lines.Add(BuildQueueLogLine(CurrentBuildConfig.QueueItems[index]));
            }

            if (CurrentBuildConfig.QueueItems.Count > maxQueueLines) {
                int remainingCount = CurrentBuildConfig.QueueItems.Count - maxQueueLines;
                lines.Add(string.Concat(
                    "... and ",
                    remainingCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    " more item(s)"));
            }

            return string.Join("\n", lines);
        }

        /// <summary>
        /// Formats one persisted queue item into a log line.
        /// </summary>
        /// <param name="queueItem">Queued build item to format.</param>
        /// <returns>Human-readable log line.</returns>
        string BuildQueueLogLine(EditorBuildQueueItemDocument queueItem) {
            if (queueItem == null) {
                throw new ArgumentNullException(nameof(queueItem));
            }

            string line = queueItem.PlatformId + " | " + queueItem.Status;
            if (!string.IsNullOrWhiteSpace(queueItem.StatusMessage)) {
                line += " | " + queueItem.StatusMessage;
            }

            return line;
        }

        /// <summary>
        /// Counts how many queue items have completed or failed.
        /// </summary>
        /// <returns>Number of queue items that are no longer pending.</returns>
        int GetBuildCompletedCount() {
            if (CurrentBuildConfig == null || CurrentBuildConfig.QueueItems == null) {
                return 0;
            }

            int completedCount = 0;
            for (int index = 0; index < CurrentBuildConfig.QueueItems.Count; index++) {
                EditorBuildQueueItemStatus status = CurrentBuildConfig.QueueItems[index].Status;
                if (status == EditorBuildQueueItemStatus.Done || status == EditorBuildQueueItemStatus.Failed || status == EditorBuildQueueItemStatus.Running) {
                    completedCount++;
                }
            }

            return completedCount;
        }

        /// <summary>
        /// Computes the queue completion fraction used by the progress bar.
        /// </summary>
        /// <returns>Progress fraction from 0 to 1.</returns>
        double GetBuildProgressFraction() {
            if (CurrentBuildConfig == null || CurrentBuildConfig.QueueItems == null || CurrentBuildConfig.QueueItems.Count == 0) {
                return 0d;
            }

            return (double)GetBuildCompletedCount() / CurrentBuildConfig.QueueItems.Count;
        }

        /// <summary>
        /// Calculates the filled width for the build-log progress bar.
        /// </summary>
        /// <param name="trackWidth">Full progress bar track width.</param>
        /// <returns>Filled progress bar width in pixels.</returns>
        int GetBuildLogProgressFillWidth(int trackWidth) {
            if (trackWidth <= 0) {
                return 0;
            }

            double fraction = GetBuildProgressFraction();
            if (fraction <= 0d) {
                return 0;
            }

            int fillWidth = (int)Math.Round(trackWidth * fraction, MidpointRounding.AwayFromZero);
            if (fillWidth < 1) {
                fillWidth = 1;
            }

            if (fillWidth > trackWidth) {
                fillWidth = trackWidth;
            }

            return fillWidth;
        }

        /// <summary>
        /// Raises the cancel event when the shared close button is pressed.
        /// </summary>
        protected override void OnCloseRequested() {
            HandleCancelRequested();
        }
    }
}
