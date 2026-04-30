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
        public const int PanelHeight = 560;
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
        /// Top margin applied before the bordered scene-list container.
        /// </summary>
        public const int SceneListTopMargin = 8;
        /// <summary>
        /// Inner padding used inside the bordered scene-list container.
        /// </summary>
        public const int SceneListPadding = 8;
        /// <summary>
        /// Height reserved for each rendered queue item row.
        /// </summary>
        public const int QueueRowHeight = 42;
        /// <summary>
        /// Height reserved for the queue section header bar.
        /// </summary>
        public const int QueueHeaderHeight = 28;
        /// <summary>
        /// Inner padding used inside the bordered queue container.
        /// </summary>
        public const int QueueListPadding = 8;
        /// <summary>
        /// Vertical gap applied between bordered queue cards.
        /// </summary>
        public const int QueueCardSpacing = 8;
        /// <summary>
        /// Inner left padding applied inside each bordered queue card.
        /// </summary>
        public const int QueueCardTextPadding = 8;
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
        /// Root entity used to place bordered queue-item cards under the queue header.
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
        /// Host entities created for the currently rendered queue rows.
        /// </summary>
        readonly List<EditorEntity> QueueItemHosts;
        /// <summary>
        /// Text components used to render queue item summaries.
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
        /// Bordered card backgrounds rendered for the current queue items.
        /// </summary>
        readonly List<RoundedRectComponent> QueueItemCardBackgrounds;
        /// <summary>
        /// Host entity for the output-directory label.
        /// </summary>
        readonly EditorEntity OutputLabelHost;
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
        /// Host entity for the output-directory textbox.
        /// </summary>
        readonly EditorEntity OutputFieldHost;
        /// <summary>
        /// Text box used to edit the active platform's output directory.
        /// </summary>
        readonly TextBoxComponent OutputDirectoryField;
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
            BuildColumnRoot.AddChild(AddToBuildButtonHost);

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
        }

        /// <summary>
        /// Shows the dialog using enabled platforms, project scenes, and the current local build configuration.
        /// </summary>
        /// <param name="supportedPlatformIds">Enabled platforms for the project.</param>
        /// <param name="sceneIds">Project-relative scenes available for the build.</param>
        /// <param name="activePlatformId">Platform tab to activate first.</param>
        /// <param name="buildConfig">Current local build configuration to render.</param>
        public void Show(IReadOnlyList<string> supportedPlatformIds, IReadOnlyList<string> sceneIds, string activePlatformId, EditorBuildConfigDocument buildConfig) {
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
            EnsurePlatformConfigs();
            SetActivePlatform(activePlatformId);
            RebuildPlatformTabs();
            RebuildActivePlatformSceneRows();
            RebuildQueueRows();
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

            AddRequested?.Invoke(new BuildDialogAddRequest(ActivePlatformId, selectedSceneIds, platformConfig.OutputDirectoryPath));
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
            MapLabelTexts.Clear();
            MapCheckBoxes.Clear();

            EditorBuildPlatformConfigDocument platformConfig = FindPlatformConfig(ActivePlatformId);
            List<string> selectedSceneIds = platformConfig.SelectedSceneIds;
            int topOffset = SceneListPadding;
            int checkBoxX = GetBuildColumnWidth() - SceneListPadding - 18;

            for (int index = 0; index < SceneIds.Count; index++) {
                string sceneId = SceneIds[index];
                float rowY = topOffset + (index * SceneRowHeight);

                EditorEntity labelHost = new EditorEntity {
                    LayerMask = LayerMask,
                    Position = new float3(SceneListPadding, rowY, 0.1f),
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
                    Position = new float3(QueueListPadding, index * QueueRowHeight, 0.1f),
                    InternalEntity = true
                };
                QueueItemsRoot.AddChild(queueItemHost);
                QueueItemHosts.Add(queueItemHost);

                RoundedRectComponent queueCardBackground = new RoundedRectComponent {
                    FillColor = ThemeManager.Colors.SurfacePrimary,
                    BorderColor = ThemeManager.Colors.AccentTertiary,
                    BorderThickness = 2f,
                    Radius = 6f,
                    RenderOrder2D = DialogPanelOrder,
                    Size = new int2(GetQueueCardWidth(), GetQueueCardHeight())
                };
                queueItemHost.AddComponent(queueCardBackground);
                QueueItemCardBackgrounds.Add(queueCardBackground);

                EditorEntity removeButtonHost = new EditorEntity {
                    LayerMask = LayerMask,
                    Position = new float3(GetQueueCardWidth() - 36, 6f, 0.1f),
                    InternalEntity = true
                };
                queueItemHost.AddChild(removeButtonHost);
                QueueItemRemoveButtonHosts.Add(removeButtonHost);

                ButtonComponent removeButton = new ButtonComponent("X", new int2(28, 24), DialogFont, () => HandleQueueItemRemoveClicked(queueItem.QueueItemId));
                removeButton.SetRenderOrders(DialogPanelOrder, DialogTextOrder);
                removeButtonHost.AddComponent(removeButton);
                QueueItemRemoveButtons.Add(removeButton);

                TextComponent queueText = new TextComponent {
                    Font = DialogFont,
                    Text = BuildQueueItemText(queueItem),
                    Color = ThemeManager.Colors.InputForegroundPrimary,
                    RenderOrder2D = DialogTextOrder
                };
                EditorEntity queueTextHost = new EditorEntity {
                    LayerMask = LayerMask,
                    Position = new float3(QueueCardTextPadding, 10f, 0.1f),
                    InternalEntity = true
                };
                queueItemHost.AddChild(queueTextHost);
                queueTextHost.AddComponent(queueText);
                QueueItemTexts.Add(queueText);
            }
        }

        /// <summary>
        /// Anchors the copy-source, output-folder, and add-to-build controls to the lower portion of the left column.
        /// </summary>
        void LayoutLowerLeftControls() {
            int addButtonY = PanelHeight - HeaderHeight - PanelPadding - FooterButtonHeight - 8;
            int outputFieldY = addButtonY - 16 - OutputFieldHeight;
            int outputLabelY = outputFieldY - 20;
            int copyComboY = outputLabelY - 16 - OutputFieldHeight;
            int copyLabelY = copyComboY - 20;
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
            string text = queueItem.PlatformId + " | " + queueItem.Status + " | " + queueItem.SelectedSceneIds.Count + " scene(s)";
            if (!string.IsNullOrWhiteSpace(queueItem.StatusMessage)) {
                text += " | " + queueItem.StatusMessage;
            }

            return text;
        }

        /// <summary>
        /// Applies the static layout for the queue button based on the current panel geometry.
        /// </summary>
        void LayoutStaticControls() {
            int buildQueueButtonY = PanelHeight - HeaderHeight - PanelPadding - FooterButtonHeight - 8;
            BuildQueueButtonHost.Position = new float3(0f, buildQueueButtonY, 0.1f);
            QueueListBackground.Size = new int2(QueueColumnWidth, GetQueueSectionHeight());
            QueueHeaderBackground.Size = new int2(QueueColumnWidth, QueueHeaderHeight);
        }

        /// <summary>
        /// Syncs the current active platform checkbox and output-folder edits into the mutable build configuration.
        /// </summary>
        void SyncActivePlatformConfig() {
            if (CurrentBuildConfig == null || string.IsNullOrWhiteSpace(ActivePlatformId)) {
                return;
            }

            EditorBuildPlatformConfigDocument platformConfig = FindPlatformConfig(ActivePlatformId);
            platformConfig.SelectedSceneIds.Clear();
            for (int index = 0; index < MapCheckBoxes.Count; index++) {
                if (MapCheckBoxes[index].IsChecked) {
                    platformConfig.SelectedSceneIds.Add(SceneIds[index]);
                }
            }

            platformConfig.OutputDirectoryPath = OutputDirectoryField.Text ?? string.Empty;
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
        /// Gets the width available for one bordered queue card inside the queue section.
        /// </summary>
        /// <returns>Width available for one queue card.</returns>
        int GetQueueCardWidth() {
            return QueueColumnWidth - (QueueListPadding * 2);
        }

        /// <summary>
        /// Gets the height used by one bordered queue card.
        /// </summary>
        /// <returns>Height used by one queue card.</returns>
        int GetQueueCardHeight() {
            return QueueRowHeight - QueueCardSpacing;
        }

        /// <summary>
        /// Gets the height available for the bordered queue section above the queue action button.
        /// </summary>
        /// <returns>Height available for the queue section chrome and cards.</returns>
        int GetQueueSectionHeight() {
            return PanelHeight - HeaderHeight - PanelPadding - FooterButtonHeight - 20;
        }

        /// <summary>
        /// Computes the width available for the output-folder text box after reserving browse-button space.
        /// </summary>
        /// <returns>Width available for the output-folder text box.</returns>
        int GetOutputFieldWidth() {
            return GetBuildColumnWidth() - BrowseButtonWidth - 8;
        }

        /// <summary>
        /// Raises the cancel event when the shared close button is pressed.
        /// </summary>
        protected override void OnCloseRequested() {
            HandleCancelRequested();
        }
    }
}
