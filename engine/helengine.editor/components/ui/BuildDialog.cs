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
        public const int QueueRowHeight = 80;
        /// <summary>
        /// Width reserved for the queue-row remove button.
        /// </summary>
        public const int QueueCardRemoveButtonWidth = 32;
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
        /// Root entity that owns the virtualized visible scene rows inside the bordered scene list.
        /// </summary>
        readonly EditorEntity SceneListItemsRoot;
        /// <summary>
        /// Scroll controller used to page through the active platform's visible scene rows.
        /// </summary>
        readonly ScrollComponent SceneListScrollComponent;
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
        /// Scroll controller used to page through the queued build rows.
        /// </summary>
        readonly ScrollComponent QueueScrollComponent;
        /// <summary>
        /// Host entities created for the currently rendered platform tabs.
        /// </summary>
        readonly List<EditorEntity> PlatformTabHosts;
        /// <summary>
        /// Platform tab buttons rendered for each enabled platform.
        /// </summary>
        readonly List<TabComponent> PlatformTabs;
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
        /// Scene ids for the currently rendered visible rows, kept in the same order as the checkbox and label lists.
        /// </summary>
        readonly List<string> DisplayedSceneIds;
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
        /// Reusable queue-row bundles virtualized against the current scroll offset.
        /// </summary>
        readonly List<BuildDialogQueueRow> QueueRows;
        /// <summary>
        /// Reusable scene-row bundles virtualized against the current scene-list scroll offset.
        /// </summary>
        readonly List<BuildDialogSceneRow> SceneRows;
        /// <summary>
        /// Host entity for the output-directory label.
        /// </summary>
        readonly EditorEntity OutputLabelHost;
        /// <summary>
        /// Host entity for the debug-build label.
        /// </summary>
        readonly EditorEntity DebugBuildLabelHost;
        /// <summary>
        /// Host entity for the copy-settings button.
        /// </summary>
        readonly EditorEntity CopySettingsButtonHost;
        /// <summary>
        /// Button used to open the copy-settings chooser modal.
        /// </summary>
        readonly ButtonComponent CopySettingsButton;
        /// <summary>
        /// Output-directory label text.
        /// </summary>
        readonly TextComponent OutputLabelText;
        /// <summary>
        /// Host entity for the code-module label.
        /// </summary>
        readonly EditorEntity CodeModuleLabelHost;
        /// <summary>
        /// Code-module label text.
        /// </summary>
        readonly TextComponent CodeModuleLabelText;
        /// <summary>
        /// Debug-build label text.
        /// </summary>
        readonly TextComponent DebugBuildLabelText;
        /// <summary>
        /// Host entity for the output-directory textbox.
        /// </summary>
        readonly EditorEntity OutputFieldHost;
        /// <summary>
        /// Host entity for the code-module textbox.
        /// </summary>
        readonly EditorEntity CodeModuleFieldHost;
        /// <summary>
        /// Text box used to edit the active platform's output directory.
        /// </summary>
        readonly TextBoxComponent OutputDirectoryField;
        /// <summary>
        /// Text box used to edit the active platform's selected code-module ids.
        /// </summary>
        readonly TextBoxComponent CodeModuleField;
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
        /// Scroll controller used to page through the build-log lines.
        /// </summary>
        readonly ScrollComponent BuildLogsScrollComponent;
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
        /// Tracks whether pooled scene rows are currently being rebound from persisted configuration state.
        /// </summary>
        bool IsBindingSceneRows;
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
        /// Raised when the user wants to open the copy-settings chooser modal.
        /// </summary>
        public event Action CopySettingsRequested;
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
        public BuildDialog(FontAsset font) : this(font, EditorUiMetrics.Default) {
        }

        /// <summary>
        /// Initializes one build dialog with one shared metrics source.
        /// </summary>
        /// <param name="font">Font used for dialog labels and controls.</param>
        /// <param name="metrics">Scaled editor UI metrics used to size the dialog.</param>
        public BuildDialog(FontAsset font, EditorUiMetrics metrics) : base("BuildDialog", "Build", font, metrics, PanelWidth, PanelHeight, HeaderHeight) {
            SetDialogMinimumSize(PanelWidth, PanelHeight);
            PlatformTabHosts = new List<EditorEntity>(8);
            PlatformTabs = new List<TabComponent>(8);
            MapLabelHosts = new List<EditorEntity>(16);
            MapLabelTexts = new List<TextComponent>(16);
            MapCheckBoxHosts = new List<EditorEntity>(16);
            MapCheckBoxes = new List<CheckBoxComponent>(16);
            MapOrderHosts = new List<EditorEntity>(16);
            MapOrderFields = new List<TextBoxComponent>(16);
            DisplayedSceneIds = new List<string>(16);
            QueueItemHosts = new List<EditorEntity>(16);
            QueueItemTexts = new List<TextComponent>(16);
            QueueItemRemoveButtonHosts = new List<EditorEntity>(16);
            QueueItemRemoveButtons = new List<ButtonComponent>(16);
            QueueItemCardBackgrounds = new List<RoundedRectComponent>(16);
            QueueRows = new List<BuildDialogQueueRow>(16);
            SceneRows = new List<BuildDialogSceneRow>(16);
            SceneIds = new List<string>(32);
            SupportedPlatformIds = new List<string>(8);

            BuildColumnRoot = new EditorEntity {
                LayerMask = LayerMask,
                Position = new float3(GetPanelPaddingPixels(), GetDialogContentTop(), 0.1f),
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

            SceneListItemsRoot = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            SceneListRoot.AddChild(SceneListItemsRoot);

            SceneListScrollComponent = new ScrollComponent();
            SceneListScrollComponent.UpdateOrder = Core.Instance.ObjectManager.GetUpdateOrderForLayer(1);
            SceneListScrollComponent.ScrollOffsetChanged += HandleSceneListScrollOffsetChanged;
            SceneListItemsRoot.AddComponent(SceneListScrollComponent);

            QueueColumnRoot = new EditorEntity {
                LayerMask = LayerMask,
                Position = new float3(GetQueueColumnLeft(), GetDialogContentTop(), 0.1f),
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
                Radius = 0f,
                RenderOrder2D = DialogPanelOrder,
                Size = new int2(GetQueueColumnWidthPixels(), 1)
            };
            QueueSectionRoot.AddComponent(QueueListBackground);

            QueueHeaderBackground = new RoundedRectComponent {
                FillColor = ThemeManager.Colors.AccentSecondary,
                BorderColor = ThemeManager.Colors.AccentSecondary,
                BorderThickness = 0f,
                Radius = 6f,
                RenderOrder2D = DialogPanelOrder,
                Size = new int2(GetQueueColumnWidthPixels(), GetQueueHeaderHeightPixels())
            };
            QueueSectionRoot.AddComponent(QueueHeaderBackground);

            QueueHeaderTextHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = new float3(GetQueueListPaddingPixels(), DialogMetrics.ScalePixels(6), 0.1f),
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
                Position = new float3(0f, GetQueueHeaderHeightPixels() + GetQueueListPaddingPixels(), 0.1f),
                InternalEntity = true
            };
            QueueSectionRoot.AddChild(QueueItemsRoot);

            QueueScrollComponent = new ScrollComponent();
            QueueScrollComponent.UpdateOrder = Core.Instance.ObjectManager.GetUpdateOrderForLayer(1);
            QueueScrollComponent.ScrollOffsetChanged += HandleQueueScrollOffsetChanged;
            QueueItemsRoot.AddComponent(QueueScrollComponent);

            OutputLabelHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            BuildColumnRoot.AddChild(OutputLabelHost);

            CodeModuleLabelHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            BuildColumnRoot.AddChild(CodeModuleLabelHost);

            CopySettingsButtonHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            BuildColumnRoot.AddChild(CopySettingsButtonHost);

            CopySettingsButton = new ButtonComponent("Copy settings from...", new int2(GetBuildColumnWidth(), GetFooterButtonHeightPixels()), DialogFont, HandleCopySettingsButtonClicked);
            CopySettingsButton.SetRenderOrders(DialogPanelOrder, DialogTextOrder);
            CopySettingsButtonHost.AddComponent(CopySettingsButton);

            OutputLabelText = new TextComponent {
                Font = DialogFont,
                Text = "Output Folder",
                Color = ThemeManager.Colors.InputForegroundPrimary,
                RenderOrder2D = DialogTextOrder
            };
            OutputLabelHost.AddComponent(OutputLabelText);

            CodeModuleLabelText = new TextComponent {
                Font = DialogFont,
                Text = "Code Modules",
                Color = ThemeManager.Colors.InputForegroundPrimary,
                RenderOrder2D = DialogTextOrder
            };
            CodeModuleLabelHost.AddComponent(CodeModuleLabelText);

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

            CodeModuleFieldHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            BuildColumnRoot.AddChild(CodeModuleFieldHost);

            OutputDirectoryField = new TextBoxComponent(new int2(GetOutputFieldWidth(), GetOutputFieldHeightPixels()), DialogFont, "Select an output folder");
            OutputDirectoryField.SetRenderOrders(DialogPanelOrder, DialogTextOrder);
            OutputDirectoryField.TextChanged += HandleOutputDirectoryFieldTextChanged;
            OutputFieldHost.AddComponent(OutputDirectoryField);

            CodeModuleField = new TextBoxComponent(new int2(GetOutputFieldWidth(), GetOutputFieldHeightPixels()), DialogFont, "Comma-separated code modules");
            CodeModuleField.SetRenderOrders(DialogPanelOrder, DialogTextOrder);
            CodeModuleField.TextChanged += HandleCodeModuleFieldTextChanged;
            CodeModuleFieldHost.AddComponent(CodeModuleField);

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

            BrowseOutputFolderButton = new ButtonComponent("Browse", new int2(GetBrowseButtonWidthPixels(), GetFooterButtonHeightPixels()), DialogFont, HandleBrowseOutputFolderClicked);
            BrowseOutputFolderButton.SetRenderOrders(DialogPanelOrder, DialogTextOrder);
            BrowseOutputFolderButtonHost.AddComponent(BrowseOutputFolderButton);

            AddToBuildButtonHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            QueueColumnRoot.AddChild(AddToBuildButtonHost);

            AddToBuildButton = new ButtonComponent("Add to Build", new int2(GetFooterButtonWidthPixels(), GetFooterButtonHeightPixels()), DialogFont, HandleAddToBuildClicked);
            AddToBuildButton.SetRenderOrders(DialogPanelOrder, DialogTextOrder);
            AddToBuildButtonHost.AddComponent(AddToBuildButton);

            BuildQueueButtonHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            QueueColumnRoot.AddChild(BuildQueueButtonHost);

            BuildQueueButton = new ButtonComponent("Build Queue", new int2(GetFooterButtonWidthPixels(), GetFooterButtonHeightPixels()), DialogFont, HandleBuildQueueRequested);
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
                Radius = 0f,
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
                Size = new int2(1, GetBuildLogsProgressBarHeightPixels())
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
                SelectionEnabled = true,
                WrapText = true,
                RenderOrder2D = DialogTextOrder,
                Size = new int2(1, 1)
            };
            BuildLogsTextHost.AddComponent(BuildLogsText);

            BuildLogsScrollComponent = new ScrollComponent();
            BuildLogsScrollComponent.UpdateOrder = Core.Instance.ObjectManager.GetUpdateOrderForLayer(1);
            BuildLogsScrollComponent.ScrollOffsetChanged += HandleBuildLogsScrollOffsetChanged;
            BuildLogsTextHost.AddComponent(BuildLogsScrollComponent);
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
            Enabled = true;
            BindDialogState(supportedPlatformIds, sceneIds, activePlatformId, buildConfig, selectionModel, true);
            CenterDialogIfNeeded();
            ApplyVisibleDialogState();
        }

        /// <summary>
        /// Refreshes the visible dialog state after queue mutations without resetting a manual position.
        /// </summary>
        /// <param name="supportedPlatformIds">Visible platform ids rendered as tabs.</param>
        /// <param name="sceneIds">Project-relative scene ids available to the active platform.</param>
        /// <param name="activePlatformId">Platform id that should stay active after the refresh.</param>
        /// <param name="buildConfig">Mutable build config currently being edited.</param>
        /// <param name="selectionModel">Builder-provided metadata for the active platform.</param>
        public void Refresh(
            IReadOnlyList<string> supportedPlatformIds,
            IReadOnlyList<string> sceneIds,
            string activePlatformId,
            EditorBuildConfigDocument buildConfig,
            EditorPlatformBuildSelectionModel selectionModel) {
            if (!Enabled) {
                Show(supportedPlatformIds, sceneIds, activePlatformId, buildConfig, selectionModel);
                return;
            }

            BindDialogState(supportedPlatformIds, sceneIds, activePlatformId, buildConfig, selectionModel, true);
            CenterDialogIfNeeded();
            ApplyVisibleDialogState();
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
            SceneListScrollComponent.ResetScrollOffset();
            QueueScrollComponent.ResetScrollOffset();
            BuildLogsScrollComponent.ResetScrollOffset();
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
                platformConfig.SelectedCodegenProfileId,
                platformConfig.SelectedStorageProfileId,
                platformConfig.SelectedMediaProfileId,
                platformConfig.SelectedBuildOptionValues,
                platformConfig.SelectedGraphicsOptionValues,
                platformConfig.SelectedCodegenOptionValues,
                platformConfig.SelectedCodeModuleIds));
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
        /// Clears the code-module field invalid state as soon as the current text becomes non-blank.
        /// </summary>
        /// <param name="textBox">Code-module textbox that changed.</param>
        void HandleCodeModuleFieldTextChanged(TextBoxComponent textBox) {
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

            for (int index = 0; index < SceneRows.Count; index++) {
                BuildDialogSceneRow row = SceneRows[index];
                if (row.CheckBox == checkBox) {
                    ApplySceneSelectionChanged(row.SceneId, checkBox, isChecked);
                    return;
                }
            }

            throw new InvalidOperationException("Scene selection checkbox is not bound to a visible scene row.");
        }

        /// <summary>
        /// Clears the scene-list invalid state as soon as at least one scene becomes selected again.
        /// Empty scene selections are still validated only when Add to Build is clicked.
        /// </summary>
        /// <param name="sceneId">Scene identifier currently bound to the checkbox.</param>
        /// <param name="checkBox">Checkbox whose selection changed.</param>
        /// <param name="isChecked">True when the checkbox is now selected.</param>
        void ApplySceneSelectionChanged(string sceneId, CheckBoxComponent checkBox, bool isChecked) {
            if (IsBindingSceneRows) {
                return;
            }

            if (string.IsNullOrWhiteSpace(sceneId)) {
                throw new ArgumentException("Scene id is required.", nameof(sceneId));
            }

            if (checkBox == null) {
                throw new ArgumentNullException(nameof(checkBox));
            }

            EditorBuildPlatformConfigDocument platformConfig = FindPlatformConfig(ActivePlatformId);
            if (isChecked) {
                if (!platformConfig.SelectedSceneIds.Contains(sceneId)) {
                    platformConfig.SelectedSceneIds.Add(sceneId);
                }

                SetSceneListInvalidState(false);
                return;
            }

            platformConfig.SelectedSceneIds.Remove(sceneId);
        }

        /// <summary>
        /// Rebinds the dialog state and rebuilds the visible controls.
        /// </summary>
        /// <param name="supportedPlatformIds">Visible platform ids rendered as tabs.</param>
        /// <param name="sceneIds">Project-relative scene ids available to the active platform.</param>
        /// <param name="activePlatformId">Platform id that should stay active after the refresh.</param>
        /// <param name="buildConfig">Mutable build config currently being edited.</param>
        /// <param name="selectionModel">Builder-provided metadata for the active platform.</param>
        /// <param name="resetScrollOffsets">True when the caller wants to reset scroll state.</param>
        void BindDialogState(
            IReadOnlyList<string> supportedPlatformIds,
            IReadOnlyList<string> sceneIds,
            string activePlatformId,
            EditorBuildConfigDocument buildConfig,
            EditorPlatformBuildSelectionModel selectionModel,
            bool resetScrollOffsets) {
            if (supportedPlatformIds == null) {
                throw new ArgumentNullException(nameof(supportedPlatformIds));
            }

            if (sceneIds == null) {
                throw new ArgumentNullException(nameof(sceneIds));
            }

            if (buildConfig == null) {
                throw new ArgumentNullException(nameof(buildConfig));
            }

            CopyPlatforms(supportedPlatformIds);
            CopyScenes(sceneIds);
            CurrentBuildConfig = buildConfig;
            ActivePlatformSelectionModel = selectionModel;
            if (resetScrollOffsets) {
                SceneListScrollComponent.ResetScrollOffset();
                QueueScrollComponent.ResetScrollOffset();
                BuildLogsScrollComponent.ResetScrollOffset();
            }

            EnsurePlatformConfigs();
            SetActivePlatform(activePlatformId);
            RebuildPlatformTabs();
            RebuildActivePlatformSceneRows();
            RebuildQueueRows();
            RebuildBuildLogs();
            LayoutStaticControls();
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
        /// Refreshes the visible queue rows when the queue scroll offset changes.
        /// </summary>
        /// <param name="scrollComponent">Queue scroll controller that triggered the update.</param>
        /// <param name="scrollOffset">Current queue scroll offset.</param>
        void HandleQueueScrollOffsetChanged(ScrollComponent scrollComponent, int scrollOffset) {
            if (scrollComponent == null) {
                throw new ArgumentNullException(nameof(scrollComponent));
            }

            UpdateQueueRowsLayout();
        }

        /// <summary>
        /// Refreshes the visible scene rows when the scene-list scroll offset changes.
        /// </summary>
        /// <param name="scrollComponent">Scene-list scroll controller that triggered the update.</param>
        /// <param name="scrollOffset">Current scene-list scroll offset.</param>
        void HandleSceneListScrollOffsetChanged(ScrollComponent scrollComponent, int scrollOffset) {
            if (scrollComponent == null) {
                throw new ArgumentNullException(nameof(scrollComponent));
            }

            UpdateSceneListRowsLayout();
        }

        /// <summary>
        /// Refreshes the build-log text when the log scroll offset changes.
        /// </summary>
        /// <param name="scrollComponent">Build-log scroll controller that triggered the update.</param>
        /// <param name="scrollOffset">Current build-log scroll offset.</param>
        void HandleBuildLogsScrollOffsetChanged(ScrollComponent scrollComponent, int scrollOffset) {
            if (scrollComponent == null) {
                throw new ArgumentNullException(nameof(scrollComponent));
            }

            UpdateBuildLogsText(null);
        }

        /// <summary>
        /// Raises the request to open the copy-settings chooser modal.
        /// </summary>
        void HandleCopySettingsButtonClicked() {
            CopySettingsRequested?.Invoke();
        }

        /// <summary>
        /// Copies the active build-tab scene selection from one source platform into the current platform.
        /// </summary>
        /// <param name="sourcePlatformId">Source platform whose scene list should be copied.</param>
        public void CopyMapListFrom(string sourcePlatformId) {
            if (string.IsNullOrWhiteSpace(sourcePlatformId)) {
                throw new ArgumentException("Source platform id is required.", nameof(sourcePlatformId));
            }

            SyncActivePlatformConfig();

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
                    Position = new float3(index * PlatformTabWidth, 0f, 0.1f),
                    InternalEntity = true
                };
                BuildColumnRoot.AddChild(tabHost);
                PlatformTabHosts.Add(tabHost);

                TabComponent tabButton = new TabComponent(platformId, new int2(PlatformTabWidth, PlatformTabHeight), DialogFont, () => HandlePlatformTabClicked(platformId));
                tabButton.SetRenderOrders(DialogPanelOrder, DialogTextOrder);
                tabButton.SetSelected(platformId == ActivePlatformId);

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
            SceneListScrollComponent.ResetScrollOffset();
            RebuildPlatformTabs();
            RebuildActivePlatformSceneRows();
        }

        /// <summary>
        /// Rebuilds the scene checklist for the current active platform.
        /// </summary>
        void RebuildActivePlatformSceneRows() {
            DisplayedSceneIds.Clear();

            EditorBuildPlatformConfigDocument platformConfig = FindPlatformConfig(ActivePlatformId);
            EnsureSceneOrderEntries(platformConfig);
            List<string> orderedSceneIds = BuildDisplayedSceneIds(platformConfig);
            for (int index = 0; index < orderedSceneIds.Count; index++) {
                DisplayedSceneIds.Add(orderedSceneIds[index]);
            }

            SceneListScrollComponent.ItemCount = DisplayedSceneIds.Count;
            LayoutLowerLeftControls();
            OutputDirectoryField.Text = platformConfig.OutputDirectoryPath ?? "";
            OutputDirectoryField.SetInvalidState(false);
            CodeModuleField.Text = string.Join(", ", platformConfig.SelectedCodeModuleIds ?? []);
            CodeModuleField.SetInvalidState(false);
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
            int queueItemCount = CurrentBuildConfig == null || CurrentBuildConfig.QueueItems == null ? 0 : CurrentBuildConfig.QueueItems.Count;
            QueueScrollComponent.ItemCount = queueItemCount;
            QueueScrollComponent.VisibleItemCount = GetQueueVisibleRowCount();
            QueueScrollComponent.Size = new int2(GetQueueRowsViewportWidth(), GetQueueRowsViewportHeight());
            QueueScrollComponent.ClampScrollOffset();
            EnsureQueueRowCount(QueueScrollComponent.VisibleItemCount);
            UpdateQueueRowsLayout();
        }

        /// <summary>
        /// Rebuilds the bottom build-log section using the current persisted queue state.
        /// </summary>
        void RebuildBuildLogs() {
            List<string> buildLogLines = BuildBuildLogLines();
            BuildLogsScrollComponent.ItemCount = buildLogLines.Count;
            BuildLogsScrollComponent.VisibleItemCount = GetBuildLogVisibleLineCount();
            BuildLogsScrollComponent.Size = new int2(GetBuildLogsTextViewportWidth(), GetBuildLogsTextViewportHeight());
            BuildLogsScrollComponent.ClampScrollOffset();
            UpdateBuildLogsText(buildLogLines);
        }

        /// <summary>
        /// Refreshes the visible scene rows after the scroll offset or active platform scene order changes.
        /// </summary>
        void UpdateSceneListRowsLayout() {
            int visibleRowCount = SceneListScrollComponent.VisibleItemCount;
            if (visibleRowCount < 1) {
                visibleRowCount = GetSceneListVisibleRowCount();
            }

            SceneListScrollComponent.VisibleItemCount = visibleRowCount;
            SceneListScrollComponent.Size = new int2(GetSceneListViewportWidth(), GetSceneListViewportHeight());
            EnsureSceneRowCount(visibleRowCount);

            MapLabelHosts.Clear();
            MapLabelTexts.Clear();
            MapCheckBoxHosts.Clear();
            MapCheckBoxes.Clear();
            MapOrderHosts.Clear();
            MapOrderFields.Clear();

            if (CurrentBuildConfig == null || string.IsNullOrWhiteSpace(ActivePlatformId)) {
                for (int rowIndex = 0; rowIndex < SceneRows.Count; rowIndex++) {
                    DisableSceneRow(SceneRows[rowIndex]);
                }

                return;
            }

            EditorBuildPlatformConfigDocument platformConfig = FindPlatformConfig(ActivePlatformId);
            int scrollOffset = SceneListScrollComponent.ScrollOffset;
            IsBindingSceneRows = true;

            try {
                for (int rowIndex = 0; rowIndex < SceneRows.Count; rowIndex++) {
                    BuildDialogSceneRow row = SceneRows[rowIndex];
                    if (rowIndex >= visibleRowCount) {
                        DisableSceneRow(row);
                        continue;
                    }

                    int sceneIndex = scrollOffset + rowIndex;
                    if (sceneIndex < 0 || sceneIndex >= DisplayedSceneIds.Count) {
                        DisableSceneRow(row);
                        continue;
                    }

                    string sceneId = DisplayedSceneIds[sceneIndex];
                    row.SceneId = sceneId;
                    row.Root.Enabled = true;
                    row.Root.Position = new float3(0f, GetSceneListPaddingPixels() + (rowIndex * GetSceneRowHeightPixels()), 0.1f);
                    row.OrderHost.Position = new float3(GetSceneListPaddingPixels(), -DialogMetrics.ScalePixels(2), 0.1f);
                    row.LabelHost.Position = new float3(GetSceneLabelX(), 0f, 0.1f);
                    row.CheckBoxHost.Position = new float3(GetSceneCheckBoxX(), -DialogMetrics.ScalePixels(2), 0.1f);
                    row.OrderField.Text = GetSceneOrderNumber(platformConfig, sceneId).ToString();
                    row.LabelText.Text = sceneId;
                    row.CheckBox.IsChecked = platformConfig.SelectedSceneIds.Contains(sceneId);

                    MapOrderHosts.Add(row.OrderHost);
                    MapOrderFields.Add(row.OrderField);
                    MapLabelHosts.Add(row.LabelHost);
                    MapLabelTexts.Add(row.LabelText);
                    MapCheckBoxHosts.Add(row.CheckBoxHost);
                    MapCheckBoxes.Add(row.CheckBox);
                }
            } finally {
                IsBindingSceneRows = false;
            }
        }

        /// <summary>
        /// Refreshes the visible queue rows after the scroll offset or active queue contents change.
        /// </summary>
        void UpdateQueueRowsLayout() {
            int queueItemCount = CurrentBuildConfig == null || CurrentBuildConfig.QueueItems == null ? 0 : CurrentBuildConfig.QueueItems.Count;
            int visibleRowCount = QueueScrollComponent.VisibleItemCount;
            if (visibleRowCount < 1) {
                visibleRowCount = GetQueueVisibleRowCount();
            }

            QueueScrollComponent.VisibleItemCount = visibleRowCount;
            QueueScrollComponent.Size = new int2(GetQueueRowsViewportWidth(), GetQueueRowsViewportHeight());
            EnsureQueueRowCount(visibleRowCount);

            QueueItemHosts.Clear();
            QueueItemTexts.Clear();
            QueueItemRemoveButtonHosts.Clear();
            QueueItemRemoveButtons.Clear();
            QueueItemCardBackgrounds.Clear();

            int scrollOffset = QueueScrollComponent.ScrollOffset;
            for (int rowIndex = 0; rowIndex < QueueRows.Count; rowIndex++) {
                BuildDialogQueueRow row = QueueRows[rowIndex];
                int queueIndex = scrollOffset + rowIndex;
                if (queueIndex < 0 || queueIndex >= queueItemCount) {
                    DisableQueueRow(row);
                    continue;
                }

                EditorBuildQueueItemDocument queueItem = CurrentBuildConfig.QueueItems[queueIndex];
                row.QueueItemId = queueItem.QueueItemId;
                row.Root.Enabled = true;
                row.Root.Position = new float3(2f, rowIndex * GetQueueCardHeight(), 0.1f);
                row.Background.Size = new int2(GetQueueCardWidth(), GetQueueCardHeight());
                row.SeparatorHost.Position = new float3(0f, GetQueueCardHeight() - DialogMetrics.ScalePixels(1), 0.2f);
                row.Separator.Size = new int2(GetQueueCardWidth(), DialogMetrics.ScalePixels(1));
                row.RemoveButtonHost.Position = new float3(
                    GetQueueCardWidth() - GetQueueCardRemoveButtonWidthPixels() - GetQueueCardTextPaddingPixels(),
                    GetQueueCardTextPaddingPixels(),
                    0.2f);
                row.TextHost.Position = new float3(
                    GetQueueCardTextPaddingPixels(),
                    GetQueueCardTextPaddingPixels(),
                    0.2f);
                row.Text.Size = new int2(
                    GetQueueCardTextWidth(),
                    Math.Max(1, GetQueueCardHeight() - (GetQueueCardTextPaddingPixels() * 2)));
                row.Text.Text = BuildQueueItemText(queueItem);

                QueueItemHosts.Add(row.Root);
                QueueItemTexts.Add(row.Text);
                QueueItemRemoveButtonHosts.Add(row.RemoveButtonHost);
                QueueItemRemoveButtons.Add(row.RemoveButton);
                QueueItemCardBackgrounds.Add(row.Background);
            }
        }

        /// <summary>
        /// Refreshes the build-log text after the scroll offset or queue contents change.
        /// </summary>
        /// <param name="buildLogLines">Optional prebuilt set of build-log lines to render.</param>
        void UpdateBuildLogsText(List<string> buildLogLines) {
            if (buildLogLines == null) {
                buildLogLines = BuildBuildLogLines();
            }

            int visibleLineCount = BuildLogsScrollComponent.VisibleItemCount;
            if (visibleLineCount < 1) {
                visibleLineCount = GetBuildLogVisibleLineCount();
            }

            BuildLogsScrollComponent.VisibleItemCount = visibleLineCount;
            BuildLogsScrollComponent.Size = new int2(GetBuildLogsTextViewportWidth(), GetBuildLogsTextViewportHeight());
            BuildLogsText.Size = new int2(GetBuildLogsTextViewportWidth(), Math.Max(GetBuildLogLineHeightPixels(), GetBuildLogsTextViewportHeight()));
            BuildLogsText.Text = BuildBuildLogText(buildLogLines, BuildLogsScrollComponent.ScrollOffset, visibleLineCount);
        }

        /// <summary>
        /// Enables enough pooled queue rows for the current viewport.
        /// </summary>
        /// <param name="count">Number of pooled rows required.</param>
        void EnsureQueueRowCount(int count) {
            for (int index = QueueRows.Count; index < count; index++) {
                BuildDialogQueueRow row = CreateQueueRow();
                QueueRows.Add(row);
            }
        }

        /// <summary>
        /// Enables enough pooled scene rows for the current viewport.
        /// </summary>
        /// <param name="count">Number of pooled rows required.</param>
        void EnsureSceneRowCount(int count) {
            for (int index = SceneRows.Count; index < count; index++) {
                BuildDialogSceneRow row = CreateSceneRow();
                SceneRows.Add(row);
            }
        }

        /// <summary>
        /// Creates one reusable queue row and attaches it to the queue list container.
        /// </summary>
        /// <returns>New queue row bundle.</returns>
        BuildDialogQueueRow CreateQueueRow() {
            BuildDialogQueueRow row = new BuildDialogQueueRow(DialogFont, DialogMetrics, LayerMask, DialogPanelOrder, DialogTextOrder);
            row.RemoveRequested += HandleQueueRowRemoveRequested;
            QueueItemsRoot.AddChild(row.Root);
            return row;
        }

        /// <summary>
        /// Creates one reusable scene row and attaches it to the scene-list container.
        /// </summary>
        /// <returns>New scene row bundle.</returns>
        BuildDialogSceneRow CreateSceneRow() {
            BuildDialogSceneRow row = new BuildDialogSceneRow(DialogFont, DialogMetrics, LayerMask, DialogPanelOrder, DialogTextOrder);
            row.OrderField.TextChanged += currentOrderField => HandleSceneOrderFieldChanged(row.SceneId, currentOrderField);
            row.OrderField.Submitted += currentOrderField => HandleSceneOrderFieldSubmitted(row.SceneId, currentOrderField);
            row.CheckBox.CheckedChanged += (checkBox, isChecked) => ApplySceneSelectionChanged(row.SceneId, checkBox, isChecked);
            SceneListItemsRoot.AddChild(row.Root);
            return row;
        }

        /// <summary>
        /// Clears one pooled queue row when it no longer maps to a visible queue item.
        /// </summary>
        /// <param name="row">Row bundle to disable.</param>
        void DisableQueueRow(BuildDialogQueueRow row) {
            if (row == null) {
                throw new ArgumentNullException(nameof(row));
            }

            row.QueueItemId = string.Empty;
            row.Root.Enabled = false;
            row.Text.Text = string.Empty;
            row.Text.Size = new int2(0, 0);
        }

        /// <summary>
        /// Clears one pooled scene row when it no longer maps to a visible scene entry.
        /// </summary>
        /// <param name="row">Row bundle to disable.</param>
        void DisableSceneRow(BuildDialogSceneRow row) {
            if (row == null) {
                throw new ArgumentNullException(nameof(row));
            }

            bool wasBindingSceneRows = IsBindingSceneRows;
            IsBindingSceneRows = true;

            try {
                row.Root.Enabled = false;
                row.OrderField.Text = string.Empty;
                row.OrderField.SetInvalidState(false);
                row.LabelText.Text = string.Empty;
                row.CheckBox.IsChecked = false;
                row.SceneId = string.Empty;
            } finally {
                IsBindingSceneRows = wasBindingSceneRows;
            }
        }

        /// <summary>
        /// Handles the remove request raised by one visible queue row.
        /// </summary>
        /// <param name="row">Queue row that requested removal.</param>
        void HandleQueueRowRemoveRequested(BuildDialogQueueRow row) {
            if (row == null) {
                throw new ArgumentNullException(nameof(row));
            }

            HandleQueueItemRemoveClicked(row.QueueItemId);
        }

        /// <summary>
        /// Anchors the copy-settings button, output-folder, and add-to-build controls to the lower portion of the left column.
        /// </summary>
        void LayoutLowerLeftControls() {
            int outputFieldY = GetLegacyContentHeightPixels() - GetHeaderHeightPixels() - GetPanelPaddingPixels() - GetFooterButtonHeightPixels() - DialogMetrics.ScalePixels(8) - DialogMetrics.ScalePixels(16) - GetOutputFieldHeightPixels();
            int addButtonY = outputFieldY + GetOutputFieldHeightPixels() + DialogMetrics.ScalePixels(16) + DialogMetrics.ScalePixels(18) + DialogMetrics.ScalePixels(16);
            int outputLabelY = outputFieldY - DialogMetrics.ScalePixels(20);
            int codeModuleFieldY = outputLabelY - DialogMetrics.ScalePixels(16) - GetOutputFieldHeightPixels();
            int codeModuleLabelY = codeModuleFieldY - DialogMetrics.ScalePixels(20);
            int copySettingsButtonY = codeModuleLabelY - DialogMetrics.ScalePixels(16) - GetFooterButtonHeightPixels();
            int debugBuildY = outputFieldY + GetOutputFieldHeightPixels() + DialogMetrics.ScalePixels(16);
            int sceneListTop = GetPlatformTabHeightPixels() + GetSceneListTopMarginPixels();
            int sceneListHeight = Math.Max(1, copySettingsButtonY - DialogMetrics.ScalePixels(12) - sceneListTop);

            SceneListRoot.Position = new float3(SceneListShakeOffsetX, sceneListTop, 0.1f);
            SceneListBackground.Size = new int2(GetBuildColumnWidth(), sceneListHeight);
            SceneListItemsRoot.Position = float3.Zero;
            SceneListScrollComponent.VisibleItemCount = GetSceneListVisibleRowCount();
            SceneListScrollComponent.Size = new int2(GetSceneListViewportWidth(), GetSceneListViewportHeight());
            SceneListScrollComponent.ClampScrollOffset();
            UpdateSceneListRowsLayout();
            CopySettingsButtonHost.Position = new float3(0f, copySettingsButtonY, 0.1f);
            CopySettingsButton.SetSize(new int2(GetBuildColumnWidth(), GetFooterButtonHeightPixels()));
            OutputLabelHost.Position = new float3(0f, outputLabelY, 0.1f);
            OutputFieldHost.Position = new float3(OutputDirectoryField.CurrentShakeOffsetX, outputFieldY, 0.1f);
            CodeModuleLabelHost.Position = new float3(0f, codeModuleLabelY, 0.1f);
            CodeModuleFieldHost.Position = new float3(CodeModuleField.CurrentShakeOffsetX, codeModuleFieldY, 0.1f);
            BrowseOutputFolderButtonHost.Position = new float3(GetOutputFieldWidth() + DialogMetrics.ScalePixels(8), outputFieldY, 0.1f);
            DebugBuildLabelHost.Position = new float3(DialogMetrics.ScalePixels(24), debugBuildY, 0.1f);
            DebugBuildCheckBoxHost.Position = new float3(0f, debugBuildY - DialogMetrics.ScalePixels(2), 0.1f);
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
        /// Builds the optional compact capability summary shown on the third queue-card line.
        /// </summary>
        /// <param name="queueItem">Persisted queue item to summarize.</param>
        /// <returns>Clipped third-line summary, or an empty string when no optional values are present.</returns>
        string BuildQueueItemCapabilitySummary(EditorBuildQueueItemDocument queueItem) {
            if (queueItem == null) {
                throw new ArgumentNullException(nameof(queueItem));
            }

            List<string> segments = new List<string>();
            if (!string.IsNullOrWhiteSpace(queueItem.SelectedBuildProfileId)) {
                segments.Add("build " + queueItem.SelectedBuildProfileId);
            }

            if (!string.IsNullOrWhiteSpace(queueItem.SelectedGraphicsProfileId)) {
                segments.Add("gfx " + queueItem.SelectedGraphicsProfileId);
            }

            if (!string.IsNullOrWhiteSpace(queueItem.SelectedCodegenProfileId)) {
                segments.Add("codegen " + queueItem.SelectedCodegenProfileId);
            }

            if (queueItem.SelectedCodeModuleIds != null && queueItem.SelectedCodeModuleIds.Count > 0) {
                segments.Add("modules " + queueItem.SelectedCodeModuleIds.Count);
            }

            if (segments.Count == 0) {
                return string.Empty;
            }

            return ClipTextToWidth(string.Join(" | ", segments), GetQueueCardTextWidth());
        }

        /// <summary>
        /// Builds one queue-row summary string for the supplied persisted queue item.
        /// </summary>
        /// <param name="queueItem">Persisted queue item to summarize.</param>
        /// <returns>Queue summary text shown in the queue column.</returns>
        string BuildQueueItemText(EditorBuildQueueItemDocument queueItem) {
            if (queueItem == null) {
                throw new ArgumentNullException(nameof(queueItem));
            }

            List<string> lines = new List<string>(3) {
                queueItem.PlatformId + " | " + queueItem.Status,
                queueItem.SelectedSceneIds.Count + " scene(s) | " + (queueItem.DebugBuild ? "Debug" : "Release")
            };

            string capabilitySummary = BuildQueueItemCapabilitySummary(queueItem);
            if (!string.IsNullOrWhiteSpace(capabilitySummary)) {
                lines.Add(capabilitySummary);
            }

            return string.Join("\n", lines);
        }

        /// <summary>
        /// Applies the static layout for the queue button based on the current panel geometry.
        /// </summary>
        void LayoutStaticControls() {
            int buildQueueButtonY = GetLegacyContentHeightPixels() - GetHeaderHeightPixels() - GetPanelPaddingPixels() - GetFooterButtonHeightPixels() - DialogMetrics.ScalePixels(8);
            AddToBuildButtonHost.Position = new float3(0f, buildQueueButtonY, 0.1f);
            BuildQueueButtonHost.Position = new float3(GetFooterButtonWidthPixels() + DialogMetrics.ScalePixels(8), buildQueueButtonY, 0.1f);
            QueueListBackground.Size = new int2(GetQueueColumnWidthPixels(), GetQueueSectionHeight());
            QueueHeaderBackground.Size = new int2(GetQueueColumnWidthPixels(), GetQueueHeaderHeightPixels());
            LayoutQueueSection();
            LayoutBuildLogsSection();
        }

        /// <summary>
        /// Positions and sizes the queue viewport and its pooled rows.
        /// </summary>
        void LayoutQueueSection() {
            int queueRowsTopY = GetQueueHeaderHeightPixels() + GetQueueListPaddingPixels();

            QueueItemsRoot.Position = new float3(0f, queueRowsTopY, 0.1f);
            QueueScrollComponent.Size = new int2(GetQueueRowsViewportWidth(), GetQueueRowsViewportHeight());
            QueueScrollComponent.VisibleItemCount = GetQueueVisibleRowCount();
            UpdateQueueRowsLayout();
        }

        /// <summary>
        /// Positions and sizes the build-log section beneath the existing controls.
        /// </summary>
        void LayoutBuildLogsSection() {
            int buildLogsTopY = GetLegacyContentHeightPixels();
            int buildLogsWidth = DialogWidth - (GetPanelPaddingPixels() * 2);
            int buildLogsInnerWidth = buildLogsWidth - (GetBuildLogsPaddingPixels() * 2);
            int progressTrackY = GetBuildLogsPaddingPixels() + GetBuildLogsTitleHeightPixels() + DialogMetrics.ScalePixels(6);
            int progressTrackWidth = buildLogsInnerWidth;
            int progressFillWidth = GetBuildLogProgressFillWidth(Math.Max(0, progressTrackWidth - (DialogMetrics.ScalePixels(1) * 2)));
            int logTextY = progressTrackY + GetBuildLogsProgressBarHeightPixels() + DialogMetrics.ScalePixels(10);
            int logTextHeight = Math.Max(1, GetBuildLogsSectionHeightPixels() - logTextY - GetBuildLogsPaddingPixels());

            BuildLogsRoot.Position = new float3(GetPanelPaddingPixels(), buildLogsTopY, 0.1f);
            BuildLogsBackground.Size = new int2(buildLogsWidth, GetBuildLogsSectionHeightPixels());

            BuildLogsTitleHost.Position = new float3(GetBuildLogsPaddingPixels(), GetBuildLogsPaddingPixels(), 0.1f);
            BuildLogsTitleText.Size = new int2(buildLogsInnerWidth, GetBuildLogsTitleHeightPixels());

            BuildLogsProgressTrackHost.Position = new float3(GetBuildLogsPaddingPixels(), progressTrackY, 0.1f);
            BuildLogsProgressTrack.Size = new int2(progressTrackWidth, GetBuildLogsProgressBarHeightPixels());
            BuildLogsProgressFillHost.Position = new float3(DialogMetrics.ScalePixels(1), DialogMetrics.ScalePixels(1), 0.1f);
            BuildLogsProgressFill.Size = new int2(progressFillWidth, Math.Max(1, GetBuildLogsProgressBarHeightPixels() - (DialogMetrics.ScalePixels(1) * 2)));

            BuildLogsTextHost.Position = new float3(GetBuildLogsPaddingPixels(), logTextY, 0.1f);
            BuildLogsScrollComponent.Size = new int2(GetBuildLogsTextViewportWidth(), logTextHeight);
            BuildLogsScrollComponent.VisibleItemCount = GetBuildLogVisibleLineCount();
            UpdateBuildLogsText(null);
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
            for (int index = 0; index < SceneRows.Count; index++) {
                BuildDialogSceneRow row = SceneRows[index];
                if (string.IsNullOrWhiteSpace(row.SceneId)) {
                    continue;
                }

                platformConfig.SelectedSceneIds.Remove(row.SceneId);
                if (row.Root.Enabled && row.CheckBox.IsChecked && !platformConfig.SelectedSceneIds.Contains(row.SceneId)) {
                    platformConfig.SelectedSceneIds.Add(row.SceneId);
                }
            }

            platformConfig.OutputDirectoryPath = OutputDirectoryField.Text ?? string.Empty;
            platformConfig.SelectedCodeModuleIds = ParseCodeModuleIds(CodeModuleField.Text);
            platformConfig.DebugBuild = DebugBuildCheckBox.IsChecked;
            EnsurePlatformSelectionDefaults(platformConfig);
        }

        /// <summary>
        /// Parses one comma-separated code-module field into an ordered unique list of module ids.
        /// </summary>
        static List<string> ParseCodeModuleIds(string text) {
            if (string.IsNullOrWhiteSpace(text)) {
                return [];
            }

            HashSet<string> seenModuleIds = new(StringComparer.OrdinalIgnoreCase);
            List<string> parsedModuleIds = [];
            string[] tokens = text.Split(new[] { ',', ';', '\n', '\r', '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            for (int index = 0; index < tokens.Length; index++) {
                string moduleId = tokens[index].Trim();
                if (string.IsNullOrWhiteSpace(moduleId)) {
                    continue;
                }
                if (!seenModuleIds.Add(moduleId)) {
                    continue;
                }

                parsedModuleIds.Add(moduleId);
            }

            return parsedModuleIds;
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
                if (string.IsNullOrWhiteSpace(platformConfig.SelectedCodegenProfileId)) {
                    platformConfig.SelectedCodegenProfileId = buildProfile.CodegenProfileId;
                }
                EnsureSettingDefaults(platformConfig.SelectedBuildOptionValues, buildProfile.Settings);
            }

            PlatformGraphicsProfileDefinition graphicsProfile = ResolveGraphicsProfile(platformConfig, buildProfile);
            if (graphicsProfile != null) {
                platformConfig.SelectedGraphicsProfileId = graphicsProfile.ProfileId;
                EnsureSettingDefaults(platformConfig.SelectedGraphicsOptionValues, graphicsProfile.Settings);
            }

            PlatformCodegenProfileDefinition codegenProfile = ResolveCodegenProfile(platformConfig, buildProfile);
            if (codegenProfile != null) {
                platformConfig.SelectedCodegenProfileId = codegenProfile.ProfileId;
                EnsureSettingDefaults(platformConfig.SelectedCodegenOptionValues, codegenProfile.Settings);
            }

            PlatformStorageProfileDefinition storageProfile = ResolveStorageProfile(platformConfig);
            if (storageProfile != null) {
                platformConfig.SelectedStorageProfileId = storageProfile.ProfileId;
            }

            PlatformMediaProfileDefinition mediaProfile = ResolveMediaProfile(platformConfig);
            if (mediaProfile != null) {
                platformConfig.SelectedMediaProfileId = mediaProfile.ProfileId;
            }

            platformConfig.SelectedBuildOptionValues ??= new Dictionary<string, string>();
            platformConfig.SelectedGraphicsOptionValues ??= new Dictionary<string, string>();
            platformConfig.SelectedCodegenOptionValues ??= new Dictionary<string, string>();
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
        /// Resolves the selected codegen profile metadata for one platform configuration.
        /// </summary>
        /// <param name="platformConfig">Platform configuration to inspect.</param>
        /// <param name="buildProfile">Resolved build profile metadata.</param>
        /// <returns>Resolved codegen profile metadata, or null when unavailable.</returns>
        PlatformCodegenProfileDefinition ResolveCodegenProfile(EditorBuildPlatformConfigDocument platformConfig, PlatformBuildProfileDefinition buildProfile) {
            if (platformConfig == null || ActivePlatformSelectionModel == null) {
                return null;
            }

            string codegenProfileId = platformConfig.SelectedCodegenProfileId;
            if (string.IsNullOrWhiteSpace(codegenProfileId) && buildProfile != null) {
                codegenProfileId = buildProfile.CodegenProfileId;
            }

            return ActivePlatformSelectionModel.ResolveCodegenProfile(codegenProfileId);
        }

        /// <summary>
        /// Resolves the selected storage profile metadata for one platform configuration.
        /// </summary>
        /// <param name="platformConfig">Platform configuration to inspect.</param>
        /// <returns>Resolved storage profile metadata, or null when unavailable.</returns>
        PlatformStorageProfileDefinition ResolveStorageProfile(EditorBuildPlatformConfigDocument platformConfig) {
            if (platformConfig == null || ActivePlatformSelectionModel == null) {
                return null;
            }

            return ActivePlatformSelectionModel.ResolveStorageProfile(platformConfig.SelectedStorageProfileId);
        }

        /// <summary>
        /// Resolves the selected media profile metadata for one platform configuration.
        /// </summary>
        /// <param name="platformConfig">Platform configuration to inspect.</param>
        /// <returns>Resolved media profile metadata, or null when unavailable.</returns>
        PlatformMediaProfileDefinition ResolveMediaProfile(EditorBuildPlatformConfigDocument platformConfig) {
            if (platformConfig == null || ActivePlatformSelectionModel == null) {
                return null;
            }

            return ActivePlatformSelectionModel.ResolveMediaProfile(platformConfig.SelectedMediaProfileId);
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
            if (IsBindingSceneRows) {
                return;
            }

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
            if (IsBindingSceneRows) {
                return;
            }

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
            if (CurrentBuildConfig == null || string.IsNullOrWhiteSpace(ActivePlatformId)) {
                return false;
            }

            EditorBuildPlatformConfigDocument platformConfig = FindPlatformConfig(ActivePlatformId);
            return platformConfig.SelectedSceneIds.Count > 0;
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
            return DialogWidth - GetQueueColumnWidthPixels() - (GetPanelPaddingPixels() * 3);
        }

        /// <summary>
        /// Gets the width available for the scene-list scroll viewport.
        /// </summary>
        /// <returns>Width available for visible scene rows.</returns>
        int GetSceneListViewportWidth() {
            return GetBuildColumnWidth();
        }

        /// <summary>
        /// Gets the height available for the scene-list scroll viewport.
        /// </summary>
        /// <returns>Height available for visible scene rows.</returns>
        int GetSceneListViewportHeight() {
            return Math.Max(1, SceneListBackground.Size.Y);
        }

        /// <summary>
        /// Gets the scaled scene-list padding in pixels.
        /// </summary>
        /// <returns>Scaled scene-list padding in pixels.</returns>
        int GetSceneListPaddingPixels() {
            return DialogMetrics.ScalePixels(SceneListPadding);
        }

        /// <summary>
        /// Gets the scaled scene-row height in pixels.
        /// </summary>
        /// <returns>Scaled scene-row height in pixels.</returns>
        int GetSceneRowHeightPixels() {
            return DialogMetrics.ScalePixels(SceneRowHeight);
        }

        /// <summary>
        /// Gets the number of visible scene rows that fit within the current scene-list viewport.
        /// </summary>
        /// <returns>Visible scene-row count.</returns>
        int GetSceneListVisibleRowCount() {
            int rowHeight = Math.Max(1, GetSceneRowHeightPixels());
            int contentHeight = Math.Max(1, GetSceneListViewportHeight() - (GetSceneListPaddingPixels() * 2));
            return Math.Max(1, (contentHeight + rowHeight - 1) / rowHeight);
        }

        /// <summary>
        /// Gets the local x offset used by visible scene labels inside each pooled row.
        /// </summary>
        /// <returns>Local x offset used by scene labels.</returns>
        int GetSceneLabelX() {
            return GetSceneListPaddingPixels() + DialogMetrics.ScalePixels(SceneOrderFieldWidth) + DialogMetrics.ScalePixels(8);
        }

        /// <summary>
        /// Gets the local x offset used by visible scene checkboxes inside each pooled row.
        /// </summary>
        /// <returns>Local x offset used by scene checkboxes.</returns>
        int GetSceneCheckBoxX() {
            return GetBuildColumnWidth() - GetSceneListPaddingPixels() - DialogMetrics.ScalePixels(18);
        }

        /// <summary>
        /// Gets the width available for one queued build row inside the queue section.
        /// </summary>
        /// <returns>Width available for one queue row.</returns>
        int GetQueueCardWidth() {
            return GetQueueColumnWidthPixels() - DialogMetrics.ScalePixels(4);
        }

        /// <summary>
        /// Gets the height used by one queued build row.
        /// </summary>
        /// <returns>Height used by one queue row.</returns>
        int GetQueueCardHeight() {
            return DialogMetrics.ScalePixels(QueueRowHeight);
        }

        /// <summary>
        /// Gets the usable width for clipped queue-row status text.
        /// </summary>
        /// <returns>Width available for the status line.</returns>
        int GetQueueCardTextWidth() {
            return Math.Max(1, GetQueueCardWidth() - (GetQueueCardTextPaddingPixels() * 2) - GetQueueCardRemoveButtonWidthPixels() - GetQueueCardTextButtonGapPixels());
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
            return GetLegacyContentHeightPixels() - GetHeaderHeightPixels() - GetPanelPaddingPixels() - GetFooterButtonHeightPixels() - DialogMetrics.ScalePixels(20);
        }

        /// <summary>
        /// Computes the width available for the output-folder text box after reserving browse-button space.
        /// </summary>
        /// <returns>Width available for the output-folder text box.</returns>
        int GetOutputFieldWidth() {
            return GetBuildColumnWidth() - GetBrowseButtonWidthPixels() - DialogMetrics.ScalePixels(8);
        }

        /// <summary>
        /// Gets the width available for the queued-build rows inside the queue viewport.
        /// </summary>
        /// <returns>Width available for visible queue rows.</returns>
        int GetQueueRowsViewportWidth() {
            return GetQueueCardWidth();
        }

        /// <summary>
        /// Gets the height available for the queued-build rows inside the queue viewport.
        /// </summary>
        /// <returns>Height available for visible queue rows.</returns>
        int GetQueueRowsViewportHeight() {
            return Math.Max(1, GetQueueSectionHeight() - GetQueueHeaderHeightPixels() - GetQueueListPaddingPixels());
        }

        /// <summary>
        /// Gets the number of visible queue rows that fit within the current queue viewport.
        /// </summary>
        /// <returns>Visible queue row count.</returns>
        int GetQueueVisibleRowCount() {
            return Math.Max(1, GetQueueRowsViewportHeight() / Math.Max(1, GetQueueCardHeight()));
        }

        /// <summary>
        /// Gets the width available for the build-log text viewport.
        /// </summary>
        /// <returns>Width available for visible build-log lines.</returns>
        int GetBuildLogsTextViewportWidth() {
            return DialogWidth - (GetPanelPaddingPixels() * 2) - (GetBuildLogsPaddingPixels() * 2);
        }

        /// <summary>
        /// Gets the height available for the build-log text viewport.
        /// </summary>
        /// <returns>Height available for visible build-log lines.</returns>
        int GetBuildLogsTextViewportHeight() {
            int progressTrackY = GetBuildLogsPaddingPixels() + GetBuildLogsTitleHeightPixels() + DialogMetrics.ScalePixels(6);
            int logTextY = progressTrackY + GetBuildLogsProgressBarHeightPixels() + DialogMetrics.ScalePixels(10);
            return Math.Max(1, GetBuildLogsSectionHeightPixels() - logTextY - GetBuildLogsPaddingPixels());
        }

        /// <summary>
        /// Gets the number of visible build-log lines that fit within the current build-log viewport.
        /// </summary>
        /// <returns>Visible build-log line count.</returns>
        int GetBuildLogVisibleLineCount() {
            return Math.Max(1, GetBuildLogsTextViewportHeight() / GetBuildLogLineHeightPixels());
        }

        /// <summary>
        /// Gets the scaled panel padding used by the dialog.
        /// </summary>
        /// <returns>Scaled panel padding in pixels.</returns>
        int GetPanelPaddingPixels() {
            return DialogMetrics.ScalePixels(PanelPadding);
        }

        /// <summary>
        /// Gets the scaled top offset used by the dialog content columns.
        /// </summary>
        /// <returns>Scaled content top offset in pixels.</returns>
        int GetDialogContentTop() {
            return GetHeaderHeightPixels() + GetPanelPaddingPixels();
        }

        /// <summary>
        /// Gets the scaled header height used by the dialog.
        /// </summary>
        /// <returns>Scaled header height in pixels.</returns>
        int GetHeaderHeightPixels() {
            return DialogMetrics.ScalePixels(HeaderHeight);
        }

        /// <summary>
        /// Gets the scaled queue-column width.
        /// </summary>
        /// <returns>Scaled queue-column width in pixels.</returns>
        int GetQueueColumnWidthPixels() {
            return DialogMetrics.ScalePixels(QueueColumnWidth);
        }

        /// <summary>
        /// Gets the scaled queue-column left position.
        /// </summary>
        /// <returns>Scaled queue-column left position in pixels.</returns>
        int GetQueueColumnLeft() {
            return DialogWidth - GetQueueColumnWidthPixels() - GetPanelPaddingPixels();
        }

        /// <summary>
        /// Gets the scaled footer button width.
        /// </summary>
        /// <returns>Scaled footer button width in pixels.</returns>
        int GetFooterButtonWidthPixels() {
            return DialogMetrics.ScalePixels(FooterButtonWidth);
        }

        /// <summary>
        /// Gets the scaled footer button height.
        /// </summary>
        /// <returns>Scaled footer button height in pixels.</returns>
        int GetFooterButtonHeightPixels() {
            return DialogMetrics.ScalePixels(FooterButtonHeight);
        }

        /// <summary>
        /// Gets the scaled output-field height.
        /// </summary>
        /// <returns>Scaled output-field height in pixels.</returns>
        int GetOutputFieldHeightPixels() {
            return DialogMetrics.ScalePixels(OutputFieldHeight);
        }

        /// <summary>
        /// Gets the scaled browse-button width.
        /// </summary>
        /// <returns>Scaled browse-button width in pixels.</returns>
        int GetBrowseButtonWidthPixels() {
            return DialogMetrics.ScalePixels(BrowseButtonWidth);
        }

        /// <summary>
        /// Gets the scaled queue-header height.
        /// </summary>
        /// <returns>Scaled queue-header height in pixels.</returns>
        int GetQueueHeaderHeightPixels() {
            return DialogMetrics.ScalePixels(QueueHeaderHeight);
        }

        /// <summary>
        /// Gets the scaled queue-list padding.
        /// </summary>
        /// <returns>Scaled queue-list padding in pixels.</returns>
        int GetQueueListPaddingPixels() {
            return DialogMetrics.ScalePixels(QueueListPadding);
        }

        /// <summary>
        /// Gets the scaled scene-list top margin.
        /// </summary>
        /// <returns>Scaled scene-list top margin in pixels.</returns>
        int GetSceneListTopMarginPixels() {
            return DialogMetrics.ScalePixels(SceneListTopMargin);
        }

        /// <summary>
        /// Gets the scaled platform-tab height.
        /// </summary>
        /// <returns>Scaled platform-tab height in pixels.</returns>
        int GetPlatformTabHeightPixels() {
            return DialogMetrics.ScalePixels(PlatformTabHeight);
        }

        /// <summary>
        /// Gets the scaled queue-card remove-button width.
        /// </summary>
        /// <returns>Scaled remove-button width in pixels.</returns>
        int GetQueueCardRemoveButtonWidthPixels() {
            return DialogMetrics.ScalePixels(QueueCardRemoveButtonWidth);
        }

        /// <summary>
        /// Gets the scaled queue-card text padding.
        /// </summary>
        /// <returns>Scaled queue-card text padding in pixels.</returns>
        int GetQueueCardTextPaddingPixels() {
            return DialogMetrics.ScalePixels(QueueCardTextPadding);
        }

        /// <summary>
        /// Gets the scaled queue-card text/button gap.
        /// </summary>
        /// <returns>Scaled queue-card text/button gap in pixels.</returns>
        int GetQueueCardTextButtonGapPixels() {
            return DialogMetrics.ScalePixels(QueueCardTextButtonGap);
        }

        /// <summary>
        /// Gets the scaled legacy content height.
        /// </summary>
        /// <returns>Scaled legacy content height in pixels.</returns>
        int GetLegacyContentHeightPixels() {
            return DialogMetrics.ScalePixels(LegacyContentHeight);
        }

        /// <summary>
        /// Gets the scaled build-log section height.
        /// </summary>
        /// <returns>Scaled build-log section height in pixels.</returns>
        int GetBuildLogsSectionHeightPixels() {
            return DialogMetrics.ScalePixels(BuildLogsSectionHeight);
        }

        /// <summary>
        /// Gets the scaled build-log padding.
        /// </summary>
        /// <returns>Scaled build-log padding in pixels.</returns>
        int GetBuildLogsPaddingPixels() {
            return DialogMetrics.ScalePixels(BuildLogsPadding);
        }

        /// <summary>
        /// Gets the scaled build-log title height.
        /// </summary>
        /// <returns>Scaled build-log title height in pixels.</returns>
        int GetBuildLogsTitleHeightPixels() {
            return DialogMetrics.ScalePixels(BuildLogsTitleHeight);
        }

        /// <summary>
        /// Gets the scaled build-log progress-bar height.
        /// </summary>
        /// <returns>Scaled progress-bar height in pixels.</returns>
        int GetBuildLogsProgressBarHeightPixels() {
            return DialogMetrics.ScalePixels(BuildLogsProgressBarHeight);
        }

        /// <summary>
        /// Gets the scaled build-log line height.
        /// </summary>
        /// <returns>Scaled build-log line height in pixels.</returns>
        int GetBuildLogLineHeightPixels() {
            return DialogMetrics.ScalePixels(BuildLogLineHeight);
        }

        /// <summary>
        /// Builds the logical build-log lines shown in the dedicated build-log section.
        /// </summary>
        /// <returns>Complete set of build-log lines before scrolling is applied.</returns>
        List<string> BuildBuildLogLines() {
            List<string> lines = new List<string>();
            if (CurrentBuildConfig == null || CurrentBuildConfig.QueueItems == null || CurrentBuildConfig.QueueItems.Count == 0) {
                lines.Add("Progress: 0%");
                lines.Add("No queued builds yet.");
                return lines;
            }

            double progressFraction = GetBuildProgressFraction();
            int completedCount = GetBuildCompletedCount();
            int totalCount = CurrentBuildConfig.QueueItems.Count;
            lines.Add(string.Concat(
                "Progress: ",
                Math.Round(progressFraction * 100d).ToString(System.Globalization.CultureInfo.InvariantCulture),
                "% (",
                completedCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "/",
                totalCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
                " complete)"));

            for (int index = 0; index < CurrentBuildConfig.QueueItems.Count; index++) {
                lines.Add(BuildQueueLogLine(CurrentBuildConfig.QueueItems[index]));
            }

            return lines;
        }

        /// <summary>
        /// Builds the multiline build-log text for the currently visible scroll window.
        /// </summary>
        /// <param name="lines">Full build-log line set before scrolling is applied.</param>
        /// <param name="scrollOffset">Current scroll offset in line units.</param>
        /// <param name="visibleLineCount">Number of lines visible in the viewport.</param>
        /// <returns>Visible build-log text for the current scroll offset.</returns>
        string BuildBuildLogText(List<string> lines, int scrollOffset, int visibleLineCount) {
            if (lines == null) {
                throw new ArgumentNullException(nameof(lines));
            }

            if (visibleLineCount < 1) {
                visibleLineCount = 1;
            }

            int maxOffset = Math.Max(0, lines.Count - visibleLineCount);
            if (scrollOffset < 0) {
                scrollOffset = 0;
            } else if (scrollOffset > maxOffset) {
                scrollOffset = maxOffset;
            }

            int endIndex = Math.Min(lines.Count, scrollOffset + visibleLineCount);
            List<string> visibleLines = new List<string>(Math.Max(0, endIndex - scrollOffset));
            for (int index = scrollOffset; index < endIndex; index++) {
                visibleLines.Add(lines[index]);
            }

            return string.Join("\n", visibleLines);
        }

        /// <summary>
        /// Builds the multiline log text shown in the dedicated build-log section.
        /// </summary>
        /// <returns>Queued-build status summary text.</returns>
        string BuildBuildLogText() {
            List<string> lines = BuildBuildLogLines();
            return BuildBuildLogText(lines, 0, GetBuildLogVisibleLineCount());
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
