namespace helengine.editor {
    /// <summary>
    /// Floating modal dialog used to prepare local per-platform build selections and queued builds.
    /// </summary>
    public class BuildDialog : EditorEntity {
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
        /// Height reserved for each rendered queue item row.
        /// </summary>
        public const int QueueRowHeight = 42;
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
        /// Corner radius applied to the dialog background.
        /// </summary>
        const float PanelRadius = 6f;
        /// <summary>
        /// Border thickness applied to the dialog background.
        /// </summary>
        const float PanelBorderThickness = 2f;
        /// <summary>
        /// Render order used for panel surfaces.
        /// </summary>
        readonly byte PanelOrder;
        /// <summary>
        /// Render order used for panel foreground text and controls.
        /// </summary>
        readonly byte TextOrder;
        /// <summary>
        /// Font used for labels and controls.
        /// </summary>
        readonly FontAsset Font;
        /// <summary>
        /// Root entity hosting the panel background and child sections.
        /// </summary>
        readonly EditorEntity PanelRoot;
        /// <summary>
        /// Background shape for the modal panel.
        /// </summary>
        readonly RoundedRectComponent PanelBackground;
        /// <summary>
        /// Root entity for the title bar.
        /// </summary>
        readonly EditorEntity HeaderRoot;
        /// <summary>
        /// Title-bar background surface.
        /// </summary>
        readonly SpriteComponent HeaderBackground;
        /// <summary>
        /// Interactable region used to drag the dialog by its title bar.
        /// </summary>
        readonly InteractableComponent HeaderInteractable;
        /// <summary>
        /// Host entity for the dialog title text.
        /// </summary>
        readonly EditorEntity TitleHost;
        /// <summary>
        /// Title text shown in the header.
        /// </summary>
        readonly TextComponent TitleText;
        /// <summary>
        /// Host entity for the close button.
        /// </summary>
        readonly EditorEntity CloseButtonHost;
        /// <summary>
        /// Header close button.
        /// </summary>
        readonly ButtonComponent CloseButton;
        /// <summary>
        /// Root entity for all left-side build-planning controls.
        /// </summary>
        readonly EditorEntity BuildColumnRoot;
        /// <summary>
        /// Root entity for all right-side queue controls.
        /// </summary>
        readonly EditorEntity QueueColumnRoot;
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
        /// Platform id shown by the currently active tab.
        /// </summary>
        string ActivePlatformId;
        /// <summary>
        /// Tracks whether the user is actively dragging the title bar.
        /// </summary>
        bool IsDragging;
        /// <summary>
        /// Cached panel position relative to the host window.
        /// </summary>
        int2 PanelPosition;
        /// <summary>
        /// Cached host size used to center the dialog before the first drag.
        /// </summary>
        int2 HostSize;
        /// <summary>
        /// Tracks whether the dialog has been manually positioned.
        /// </summary>
        bool IsUserPositioned;

        /// <summary>
        /// Raised when the user wants to add one queued build from the active platform tab.
        /// </summary>
        public event Action<BuildDialogAddRequest> AddRequested;
        /// <summary>
        /// Raised when the user wants to start running the queued builds.
        /// </summary>
        public event Action BuildQueueRequested;
        /// <summary>
        /// Raised when the user closes the build dialog without confirming another action.
        /// </summary>
        public event Action CancelRequested;

        /// <summary>
        /// Gets the mutable build configuration currently being edited by the dialog.
        /// </summary>
        public EditorBuildConfigDocument BuildConfig => CurrentBuildConfig;
        /// <summary>
        /// Gets a value indicating whether the dialog is currently visible.
        /// </summary>
        public bool IsVisible => Enabled;

        /// <summary>
        /// Initializes one build dialog with a shared modal shell and build-planning controls.
        /// </summary>
        /// <param name="font">Font used for dialog labels and controls.</param>
        public BuildDialog(FontAsset font) {
            if (font == null) {
                throw new ArgumentNullException(nameof(font));
            }

            Font = font;
            PanelOrder = RenderOrder2D.ModalBackground;
            TextOrder = RenderOrder2D.ModalForeground;
            PlatformTabHosts = new List<EditorEntity>(8);
            PlatformTabs = new List<ButtonComponent>(8);
            MapLabelHosts = new List<EditorEntity>(16);
            MapLabelTexts = new List<TextComponent>(16);
            MapCheckBoxHosts = new List<EditorEntity>(16);
            MapCheckBoxes = new List<CheckBoxComponent>(16);
            QueueItemHosts = new List<EditorEntity>(16);
            QueueItemTexts = new List<TextComponent>(16);
            SceneIds = new List<string>(32);
            SupportedPlatformIds = new List<string>(8);

            LayerMask = 0b1000000000000000;
            InternalEntity = true;
            Name = "BuildDialog";
            Enabled = false;

            PanelRoot = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            AddChild(PanelRoot);

            PanelBackground = new RoundedRectComponent {
                FillColor = ThemeManager.Colors.SurfacePrimary,
                BorderColor = ThemeManager.Colors.AccentTertiary,
                BorderThickness = PanelBorderThickness,
                Radius = PanelRadius,
                RenderOrder2D = PanelOrder,
                Size = new int2(PanelWidth, PanelHeight)
            };
            PanelRoot.AddComponent(PanelBackground);

            HeaderRoot = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            PanelRoot.AddChild(HeaderRoot);

            HeaderBackground = new SpriteComponent {
                Texture = TextureUtils.PixelTexture,
                Color = ThemeManager.Colors.AccentSecondary,
                RenderOrder2D = PanelOrder,
                Size = new int2(PanelWidth, HeaderHeight)
            };
            HeaderRoot.AddComponent(HeaderBackground);

            HeaderInteractable = new InteractableComponent {
                Size = new int2(PanelWidth, HeaderHeight)
            };
            HeaderInteractable.CursorEvent += HandleHeaderCursor;
            HeaderRoot.AddComponent(HeaderInteractable);

            TitleHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = new float3(10f, 8f, 0.1f),
                InternalEntity = true
            };
            HeaderRoot.AddChild(TitleHost);

            TitleText = new TextComponent {
                Font = font,
                Text = "Build",
                Color = ThemeManager.Colors.TextOnAccent,
                RenderOrder2D = TextOrder
            };
            TitleHost.AddComponent(TitleText);

            CloseButtonHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = new float3(PanelWidth - 40, 0f, 0.1f),
                InternalEntity = true
            };
            HeaderRoot.AddChild(CloseButtonHost);

            CloseButton = new ButtonComponent("X", new int2(40, HeaderHeight), font, HandleCancelRequested);
            CloseButton.SetRenderOrders(PanelOrder, TextOrder);
            CloseButton.UseSquareCorners();
            CloseButton.SetTextColor(ThemeManager.Colors.TextOnAccent);
            CloseButtonHost.AddComponent(CloseButton);

            BuildColumnRoot = new EditorEntity {
                LayerMask = LayerMask,
                Position = new float3(PanelPadding, HeaderHeight + PanelPadding, 0.1f),
                InternalEntity = true
            };
            PanelRoot.AddChild(BuildColumnRoot);

            QueueColumnRoot = new EditorEntity {
                LayerMask = LayerMask,
                Position = new float3(PanelWidth - QueueColumnWidth - PanelPadding, HeaderHeight + PanelPadding, 0.1f),
                InternalEntity = true
            };
            PanelRoot.AddChild(QueueColumnRoot);

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
                Font = font,
                Text = "Copy Map List From",
                Color = ThemeManager.Colors.TextPrimary,
                RenderOrder2D = TextOrder
            };
            CopySourceLabelHost.AddComponent(CopySourceLabelText);

            CopySourcePlatformComboBoxHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            BuildColumnRoot.AddChild(CopySourcePlatformComboBoxHost);

            CopySourcePlatformComboBox = new ComboBoxComponent(new int2(200, OutputFieldHeight), font, Array.Empty<string>(), -1);
            CopySourcePlatformComboBox.SetRenderOrders(PanelOrder, TextOrder, RenderOrder2D.ModalBackground, RenderOrder2D.ModalForeground);
            CopySourcePlatformComboBoxHost.AddComponent(CopySourcePlatformComboBox);

            CopyMapListButtonHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            BuildColumnRoot.AddChild(CopyMapListButtonHost);

            CopyMapListButton = new ButtonComponent("Copy", new int2(84, FooterButtonHeight), font, HandleCopyMapListClicked);
            CopyMapListButton.SetRenderOrders(PanelOrder, TextOrder);
            CopyMapListButtonHost.AddComponent(CopyMapListButton);

            OutputLabelText = new TextComponent {
                Font = font,
                Text = "Output Folder",
                Color = ThemeManager.Colors.TextPrimary,
                RenderOrder2D = TextOrder
            };
            OutputLabelHost.AddComponent(OutputLabelText);

            OutputFieldHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            BuildColumnRoot.AddChild(OutputFieldHost);

            OutputDirectoryField = new TextBoxComponent(new int2(GetBuildColumnWidth(), OutputFieldHeight), font, "Select an output folder");
            OutputFieldHost.AddComponent(OutputDirectoryField);

            AddToBuildButtonHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            BuildColumnRoot.AddChild(AddToBuildButtonHost);

            AddToBuildButton = new ButtonComponent("Add to Build", new int2(FooterButtonWidth, FooterButtonHeight), font, HandleAddToBuildClicked);
            AddToBuildButton.SetRenderOrders(PanelOrder, TextOrder);
            AddToBuildButtonHost.AddComponent(AddToBuildButton);

            BuildQueueButtonHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            QueueColumnRoot.AddChild(BuildQueueButtonHost);

            BuildQueueButton = new ButtonComponent("Build Queue", new int2(FooterButtonWidth, FooterButtonHeight), font, HandleBuildQueueRequested);
            BuildQueueButton.SetRenderOrders(PanelOrder, TextOrder);
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

            CopyPlatforms(supportedPlatformIds);
            CopyScenes(sceneIds);
            CurrentBuildConfig = buildConfig;
            EnsurePlatformConfigs();
            SetActivePlatform(activePlatformId);
            RebuildPlatformTabs();
            RebuildActivePlatformSceneRows();
            RebuildQueueRows();
            LayoutStaticControls();
            CenterPanelIfNeeded();
            Enabled = true;
        }

        /// <summary>
        /// Updates the cached host size used to center or preserve the dialog position during editor resizes.
        /// </summary>
        /// <param name="width">Current host width in pixels.</param>
        /// <param name="height">Current host height in pixels.</param>
        public void UpdateLayout(int width, int height) {
            HostSize = new int2(Math.Max(1, width), Math.Max(1, height));
            if (IsUserPositioned) {
                PanelRoot.Position = new float3(PanelPosition.X, PanelPosition.Y, 0f);
                return;
            }

            CenterPanelIfNeeded();
        }

        /// <summary>
        /// Hides the dialog and stops any active title-bar drag.
        /// </summary>
        public void Hide() {
            IsDragging = false;
            Enabled = false;
        }

        /// <summary>
        /// Captures one queued-build request from the current active platform state.
        /// </summary>
        void HandleAddToBuildClicked() {
            SyncActivePlatformConfig();

            EditorBuildPlatformConfigDocument platformConfig = FindPlatformConfig(ActivePlatformId);
            List<string> selectedSceneIds = new List<string>(platformConfig.SelectedSceneIds.Count);
            for (int index = 0; index < platformConfig.SelectedSceneIds.Count; index++) {
                selectedSceneIds.Add(platformConfig.SelectedSceneIds[index]);
            }

            AddRequested?.Invoke(new BuildDialogAddRequest(ActivePlatformId, selectedSceneIds, platformConfig.OutputDirectoryPath));
        }

        /// <summary>
        /// Raises the build-queue request event for the current queue.
        /// </summary>
        void HandleBuildQueueRequested() {
            SyncActivePlatformConfig();
            BuildQueueRequested?.Invoke();
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
        /// Handles title-bar dragging so the dialog can be repositioned.
        /// </summary>
        /// <param name="relPos">Pointer position relative to the title bar.</param>
        /// <param name="delta">Pointer movement delta.</param>
        /// <param name="state">Pointer interaction state.</param>
        void HandleHeaderCursor(int2 relPos, int2 delta, PointerInteraction state) {
            if (state == PointerInteraction.Press) {
                IsDragging = true;
                IsUserPositioned = true;
                return;
            }

            if (state == PointerInteraction.Release || state == PointerInteraction.Leave) {
                IsDragging = false;
                return;
            }

            if (state == PointerInteraction.Hover && IsDragging) {
                PanelPosition = new int2(PanelPosition.X + delta.X, PanelPosition.Y + delta.Y);
                PanelRoot.Position = new float3(PanelPosition.X, PanelPosition.Y, 0f);
            }
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

                ButtonComponent tabButton = new ButtonComponent(platformId, new int2(PlatformTabWidth, PlatformTabHeight), Font, () => HandlePlatformTabClicked(platformId));
                tabButton.SetRenderOrders(PanelOrder, TextOrder);
                if (platformId != ActivePlatformId) {
                    tabButton.UseHoverOnlyBackground();
                    tabButton.SetTextColor(ThemeManager.Colors.TextPrimary);
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
            int topOffset = PlatformTabHeight + 16;
            int checkBoxX = GetBuildColumnWidth() - 22;

            for (int index = 0; index < SceneIds.Count; index++) {
                string sceneId = SceneIds[index];
                float rowY = topOffset + (index * SceneRowHeight);

                EditorEntity labelHost = new EditorEntity {
                    LayerMask = LayerMask,
                    Position = new float3(0f, rowY, 0.1f),
                    InternalEntity = true
                };
                BuildColumnRoot.AddChild(labelHost);
                MapLabelHosts.Add(labelHost);

                TextComponent labelText = new TextComponent {
                    Font = Font,
                    Text = sceneId,
                    Color = ThemeManager.Colors.TextPrimary,
                    RenderOrder2D = TextOrder
                };
                labelHost.AddComponent(labelText);
                MapLabelTexts.Add(labelText);

                EditorEntity checkBoxHost = new EditorEntity {
                    LayerMask = LayerMask,
                    Position = new float3(checkBoxX, rowY - 2, 0.1f),
                    InternalEntity = true
                };
                BuildColumnRoot.AddChild(checkBoxHost);
                MapCheckBoxHosts.Add(checkBoxHost);

                bool isChecked = selectedSceneIds.Contains(sceneId);
                CheckBoxComponent checkBox = new CheckBoxComponent(new int2(18, 18), Font, isChecked);
                checkBox.SetRenderOrders(PanelOrder, TextOrder);
                checkBoxHost.AddComponent(checkBox);
                MapCheckBoxes.Add(checkBox);
            }

            float copyControlsY = topOffset + (SceneIds.Count * SceneRowHeight) + 16f;
            CopySourceLabelHost.Position = new float3(0f, copyControlsY, 0.1f);
            CopySourcePlatformComboBoxHost.Position = new float3(0f, CopySourceLabelHost.Position.Y + 20f, 0.1f);
            CopyMapListButtonHost.Position = new float3(CopySourcePlatformComboBox.Size.X + 8f, CopySourcePlatformComboBoxHost.Position.Y, 0.1f);
            RebuildCopySourcePlatformItems();

            OutputLabelHost.Position = new float3(0f, CopySourcePlatformComboBoxHost.Position.Y + OutputFieldHeight + 16f, 0.1f);
            OutputFieldHost.Position = new float3(0f, OutputLabelHost.Position.Y + 20f, 0.1f);
            OutputDirectoryField.Text = platformConfig.OutputDirectoryPath ?? "";
            AddToBuildButtonHost.Position = new float3(0f, OutputFieldHost.Position.Y + OutputFieldHeight + 16, 0.1f);
        }

        /// <summary>
        /// Rebuilds the queue summary rows shown on the right side of the dialog.
        /// </summary>
        void RebuildQueueRows() {
            ClearEntities(QueueItemHosts);
            QueueItemTexts.Clear();

            for (int index = 0; index < CurrentBuildConfig.QueueItems.Count; index++) {
                EditorBuildQueueItemDocument queueItem = CurrentBuildConfig.QueueItems[index];
                EditorEntity queueItemHost = new EditorEntity {
                    LayerMask = LayerMask,
                    Position = new float3(0f, index * QueueRowHeight, 0.1f),
                    InternalEntity = true
                };
                QueueColumnRoot.AddChild(queueItemHost);
                QueueItemHosts.Add(queueItemHost);

                TextComponent queueText = new TextComponent {
                    Font = Font,
                    Text = BuildQueueItemText(queueItem),
                    Color = ThemeManager.Colors.TextPrimary,
                    RenderOrder2D = TextOrder
                };
                queueItemHost.AddComponent(queueText);
                QueueItemTexts.Add(queueText);
            }
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
            BuildQueueButtonHost.Position = new float3(0f, PanelHeight - HeaderHeight - PanelPadding - FooterButtonHeight - 8, 0.1f);
        }

        /// <summary>
        /// Centers the panel the first time it is shown before any user drag occurs.
        /// </summary>
        void CenterPanelIfNeeded() {
            if (IsUserPositioned) {
                return;
            }

            int width = 1280;
            int height = 720;
            if (HostSize.X > 0 && HostSize.Y > 0) {
                width = HostSize.X;
                height = HostSize.Y;
            }

            PanelPosition = new int2((width - PanelWidth) / 2, (height - PanelHeight) / 2);
            PanelRoot.Position = new float3(PanelPosition.X, PanelPosition.Y, 0f);
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
    }
}
