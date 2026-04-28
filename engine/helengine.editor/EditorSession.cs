using helengine.projectfile;

namespace helengine.editor {
    /// <summary>
    /// Coordinates editor core initialization, docked UI setup, and scene bootstrapping for a host window.
    /// </summary>
    public class EditorSession {
        /// <summary>
        /// Default debounce delay for shader rebuilds.
        /// </summary>
        const int ShaderBuildDelayMilliseconds = 250;
        /// <summary>
        /// Built-in runtime shader file used for transform-gizmo materials.
        /// </summary>
        const string TransformGizmoShaderFileName = "EditorTransformGizmo.hlsl";
        /// <summary>
        /// Built-in runtime shader file used for highlighted transform-gizmo materials.
        /// </summary>
        const string TransformGizmoHighlightShaderFileName = "EditorTransformGizmoHighlight.hlsl";
        /// <summary>
        /// Built-in runtime shader variant.
        /// </summary>
        const string DefaultRuntimeShaderVariant = "default";
        /// <summary>
        /// Draw order used by the main scene camera.
        /// </summary>
        const byte SceneCameraDrawOrder = 0;
        /// <summary>
        /// Draw order used by the gizmo overlay camera.
        /// </summary>
        const byte GizmoCameraDrawOrder = 1;
        /// <summary>
        /// Identifies the pending scene transition that should continue after the unsaved-changes guard resolves.
        /// </summary>
        enum SceneTransitionKind {
            /// <summary>
            /// No transition is pending.
            /// </summary>
            None,

            /// <summary>
            /// The session should reset to a new empty scene.
            /// </summary>
            NewMap,

            /// <summary>
            /// The session should open one scene file chosen by the user.
            /// </summary>
            OpenMap
        }
        /// <summary>
        /// Editor core driving updates and rendering.
        /// </summary>
        readonly EditorCore core;
        /// <summary>
        /// Project path used for asset browsing.
        /// </summary>
        readonly string projectPath;
        /// <summary>
        /// Project file name shown in the host window title.
        /// </summary>
        readonly string ProjectDisplayName;
        /// <summary>
        /// Font used for UI elements and title bars.
        /// </summary>
        readonly FontAsset uiFont;
        /// <summary>
        /// Content manager used to load editor and project asset files.
        /// </summary>
        readonly ContentManager EditorContentManager;
        /// <summary>
        /// Title bar UI for the editor.
        /// </summary>
        readonly EditorTitleBar titleBar;
        /// <summary>
        /// Docking manager coordinating dock layout and interaction.
        /// </summary>
        readonly DockingManager dockingManager;
        /// <summary>
        /// Scene hierarchy dock panel.
        /// </summary>
        readonly SceneHierarchyPanel sceneHierarchyPanel;
        /// <summary>
        /// Asset browser dock panel.
        /// </summary>
        readonly AssetBrowserPanel assetBrowserPanel;
        /// <summary>
        /// Properties dock panel.
        /// </summary>
        readonly PropertiesPanel propertiesPanel;
        /// <summary>
        /// Logger dock panel.
        /// </summary>
        readonly LoggerPanel loggerPanel;
        /// <summary>
        /// Preview dock panel for texture assets.
        /// </summary>
        readonly PreviewPanel previewPanel;
        /// <summary>
        /// Modal used to pick an asset for editor fields.
        /// </summary>
        readonly AssetPickerModal assetPickerModal;
        /// <summary>
        /// Main viewport dock panel.
        /// </summary>
        readonly EditorViewport mainViewport;
        /// <summary>
        /// UI camera entity used for 2D rendering.
        /// </summary>
        readonly EditorEntity uiCameraEntity;
        /// <summary>
        /// Scene camera entity used for 3D rendering.
        /// </summary>
        readonly EditorEntity sceneCameraEntity;
        /// <summary>
        /// UI camera component.
        /// </summary>
        readonly CameraComponent uiCameraComponent;
        /// <summary>
        /// Scene camera component.
        /// </summary>
        readonly CameraComponent sceneCameraComponent;
        /// <summary>
        /// Overlay camera component that renders gizmos on top of scene geometry.
        /// </summary>
        readonly CameraComponent gizmoCameraComponent;
        /// <summary>
        /// Internal entity that routes editor-wide keyboard-focus input every frame.
        /// </summary>
        readonly EditorEntity keyboardFocusEntity;
        /// <summary>
        /// Hidden editor camera entity used for offscreen rendering.
        /// </summary>
        readonly EditorEntity hiddenCameraEntity;
        /// <summary>
        /// Hidden editor camera component used for offscreen rendering.
        /// </summary>
        readonly CameraComponent hiddenCameraComponent;
        /// <summary>
        /// Render target assigned to the hidden editor camera.
        /// </summary>
        readonly RenderTarget hiddenCameraTarget;
        /// <summary>
        /// Shader module manager responsible for hot-reloading shader modules.
        /// </summary>
        readonly ShaderModuleManager shaderModuleManager;
        /// <summary>
        /// Asset import manager responsible for creating import settings and outputs.
        /// </summary>
        readonly AssetImportManager assetImportManager;
        /// <summary>
        /// Resolves and validates scene save destinations for the current project.
        /// </summary>
        readonly SceneSavePathResolver SceneSavePathResolver;
        /// <summary>
        /// Serializes the current editor scene to `.helen` files.
        /// </summary>
        readonly SceneSaveService SceneSaveService;
        /// <summary>
        /// Creates new scene entities for title-bar add commands.
        /// </summary>
        readonly EditorSceneCreationService SceneCreationService;
        /// <summary>
        /// Applies validated scene-hierarchy reparent operations.
        /// </summary>
        readonly EditorEntityReparentService ReparentService;
        /// <summary>
        /// Modal dialog used to choose scene save destinations.
        /// </summary>
        readonly SaveFileDialog saveFileDialog;
        /// <summary>
        /// Modal dialog used to choose scene files to open.
        /// </summary>
        readonly OpenFileDialog openFileDialog;
        /// <summary>
        /// Modal dialog used to choose a new parent for one scene entity.
        /// </summary>
        readonly ReparentEntityDialog reparentEntityDialog;
        /// <summary>
        /// Modal dialog used to confirm whether pending scene transitions should save dirty changes.
        /// </summary>
        readonly UnsavedChangesDialog unsavedChangesDialog;
        /// <summary>
        /// Deserializes `.helen` scene files into editor entities.
        /// </summary>
        readonly SceneFileLoadService SceneFileLoadService;
        /// <summary>
        /// Absolute path to the current scene file, when one has been saved.
        /// </summary>
        string CurrentScenePath;
        /// <summary>
        /// True when the current scene contains unsaved editor changes.
        /// </summary>
        bool IsSceneDirty;
        /// <summary>
        /// Pending scene transition waiting on the unsaved-changes guard or save flow.
        /// </summary>
        SceneTransitionKind PendingSceneTransition;
        /// <summary>
        /// Absolute path that should be opened when the pending transition resumes.
        /// </summary>
        string PendingOpenScenePath;

        /// <summary>
        /// Initializes a new editor session and sets up cameras, docking, and starter content.
        /// </summary>
        /// <param name="core">Editor core instance that owns shared state.</param>
        /// <param name="projectPath">Path to the project root or project file being edited.</param>
        /// <param name="uiFont">Font used for editor UI text.</param>
        /// <param name="snapModifierFont">Font used for the viewport snap modifier labels.</param>
        /// <param name="render3D">3D renderer instance.</param>
        /// <param name="render2D">2D renderer instance.</param>
        /// <param name="input">Input manager instance.</param>
        /// <param name="renderWidth">Initial render width in pixels.</param>
        /// <param name="renderHeight">Initial render height in pixels.</param>
        /// <param name="toolbarIcons">Toolbar icon textures used by the main viewport tool buttons.</param>
        /// <param name="importers">Asset importers to register for import settings.</param>
        public EditorSession(
            EditorCore core,
            string projectPath,
            FontAsset uiFont,
            FontAsset snapModifierFont,
            RenderManager3D render3D,
            RenderManager2D render2D,
            InputManager input,
            int renderWidth,
            int renderHeight,
            EditorViewportToolbarIconSet toolbarIcons,
            IReadOnlyList<IAssetImporterRegistration> importers) {
            this.core = core ?? throw new ArgumentNullException(nameof(core));
            string canonicalProjectFilePath = ResolveCanonicalProjectFilePath(projectPath);
            this.projectPath = ResolveProjectRootPathFromCanonicalProjectFile(canonicalProjectFilePath);
            ProjectDisplayName = ResolveProjectDisplayNameFromCanonicalProjectFile(canonicalProjectFilePath);
            EditorContentManager = this.core.GetContentManager();
            this.uiFont = uiFont ?? throw new ArgumentNullException(nameof(uiFont));
            snapModifierFont = snapModifierFont ?? throw new ArgumentNullException(nameof(snapModifierFont));
            toolbarIcons = toolbarIcons ?? throw new ArgumentNullException(nameof(toolbarIcons));

            EditorKeyboardFocusService.Reset();
            core.Initialize(render3D, render2D, input);
            core.InputManager.SetKeyboardActive(true);

            EditorProjectPaths.Initialize(this.projectPath);

            assetImportManager = InitializeAssetImports(importers);
            GeneratedAssetProviderRegistry.Register(new EngineGeneratedAssetProvider());

            uiCameraEntity = new EditorEntity();
            uiCameraEntity.InternalEntity = true;
            uiCameraEntity.Position = new float3(0, 3, -8);
            uiCameraComponent = new CameraComponent();
            uiCameraComponent.LayerMask = EditorLayerMasks.EditorUi;
            uiCameraComponent.CameraDrawOrder = 255;
            uiCameraComponent.ClearSettings = new CameraClearSettings(false, new float4(0f, 0f, 0f, 0f), false, 1.0f, false, 0);
            uiCameraEntity.AddComponent(uiCameraComponent);

            sceneCameraEntity = new EditorEntity();
            sceneCameraEntity.InternalEntity = true;
            sceneCameraEntity.Position = new float3(0, 3, -8);
            sceneCameraComponent = new CameraComponent();
            sceneCameraComponent.LayerMask = EditorLayerMasks.SceneObjects | EditorLayerMasks.SceneGrid;
            sceneCameraComponent.CameraDrawOrder = SceneCameraDrawOrder;
            sceneCameraComponent.ClearSettings = new CameraClearSettings(true, new float4(0.39215687f, 0.58431375f, 0.92941177f, 1f), true, 1.0f, false, 0);
            sceneCameraEntity.AddComponent(sceneCameraComponent);
            gizmoCameraComponent = new CameraComponent();
            gizmoCameraComponent.LayerMask = EditorLayerMasks.SceneGizmo;
            gizmoCameraComponent.CameraDrawOrder = GizmoCameraDrawOrder;
            gizmoCameraComponent.ClearSettings = new CameraClearSettings(false, new float4(0f, 0f, 0f, 0f), true, 1.0f, false, 0);
            gizmoCameraComponent.Viewport = sceneCameraComponent.Viewport;
            sceneCameraEntity.AddComponent(gizmoCameraComponent);
            sceneCameraEntity.AddComponent(new EditorViewportCameraController(sceneCameraComponent));
            sceneCameraEntity.AddComponent(new TransformTranslationGizmoDragComponent(sceneCameraComponent));
            sceneCameraEntity.AddComponent(new TransformRotationGizmoDragComponent(sceneCameraComponent));
            sceneCameraEntity.AddComponent(new TransformScaleGizmoDragComponent(sceneCameraComponent));
            keyboardFocusEntity = new EditorEntity {
                InternalEntity = true,
                Enabled = true,
                LayerMask = EditorLayerMasks.EditorUi
            };
            var keyboardFocusUpdateComponent = new EditorKeyboardFocusUpdateComponent {
                UpdateOrder = core.ObjectManager.GetUpdateOrderForLayer(1)
            };
            keyboardFocusEntity.AddComponent(keyboardFocusUpdateComponent);

            float3 toOrigin = float3.Normalize(new float3(-sceneCameraEntity.Position.X, -sceneCameraEntity.Position.Y, -sceneCameraEntity.Position.Z));
            double yaw = Math.Atan2(toOrigin.X, -toOrigin.Z);
            double pitch = Math.Asin(toOrigin.Y);
            float4 orientation;
            float4.CreateFromYawPitchRoll((float)yaw, (float)pitch, 0f, out orientation);
            sceneCameraEntity.Orientation = orientation;

            hiddenCameraEntity = new EditorEntity();
            hiddenCameraEntity.InternalEntity = true;
            hiddenCameraEntity.Enabled = false;
            hiddenCameraEntity.Position = sceneCameraEntity.Position;
            hiddenCameraEntity.Orientation = sceneCameraEntity.Orientation;
            hiddenCameraEntity.LayerMask = EditorLayerMasks.SceneObjects;
            hiddenCameraComponent = new CameraComponent();
            hiddenCameraComponent.LayerMask = EditorLayerMasks.SceneObjects;
            hiddenCameraComponent.Viewport = new float4(0, 0, 640, 360);
            hiddenCameraComponent.ClearSettings = new CameraClearSettings(true, new float4(0f, 0f, 0f, 0f), true, 1.0f, false, 0);
            if (render3D is helengine.directx11.DirectX11Renderer3D pickerRenderer) {
                hiddenCameraTarget = render3D.CreateRenderTarget(640, 360);
                hiddenCameraComponent.RenderTarget = hiddenCameraTarget;
                hiddenCameraEntity.AddComponent(hiddenCameraComponent);
                sceneCameraEntity.AddComponent(new EditorViewportPicker(sceneCameraComponent, gizmoCameraComponent, hiddenCameraEntity, hiddenCameraComponent, pickerRenderer));
            } else {
                hiddenCameraTarget = null;
                hiddenCameraComponent.RenderTarget = null;
                Logger.WriteWarning("Scene picking is currently available only on the DirectX11 renderer.");
            }

            titleBar = new EditorTitleBar(uiFont, Math.Max(1, renderWidth), Math.Max(1, renderHeight), BuildWindowTitle());

            dockingManager = new DockingManager();
            sceneHierarchyPanel = new SceneHierarchyPanel(uiFont);
            assetBrowserPanel = new AssetBrowserPanel(uiFont, this.projectPath);
            mainViewport = new EditorViewport(sceneCameraComponent, uiFont, snapModifierFont, toolbarIcons);
            propertiesPanel = new PropertiesPanel(uiFont, EditorContentManager);
            loggerPanel = new LoggerPanel(uiFont);
            previewPanel = new PreviewPanel(uiFont);
            EditorKeyboardFocusService.RegisterGroup(sceneHierarchyPanel);
            EditorKeyboardFocusService.RegisterGroup(assetBrowserPanel);
            EditorKeyboardFocusService.RegisterGroup(mainViewport);
            EditorKeyboardFocusService.RegisterGroup(propertiesPanel);
            EditorKeyboardFocusService.RegisterGroup(loggerPanel);
            EditorKeyboardFocusService.RegisterGroup(previewPanel);
            assetPickerModal = new AssetPickerModal(uiFont, this.projectPath);
            ComponentPersistenceRegistry persistenceRegistry = new ComponentPersistenceRegistry();
            persistenceRegistry.Register(new MeshComponentPersistenceDescriptor());
            SceneSavePathResolver = new SceneSavePathResolver(this.projectPath);
            SceneSaveService = new SceneSaveService(this.projectPath, persistenceRegistry);
            SceneCreationService = new EditorSceneCreationService();
            ReparentService = new EditorEntityReparentService();
            saveFileDialog = new SaveFileDialog(uiFont, this.projectPath);
            openFileDialog = new OpenFileDialog(uiFont, this.projectPath);
            reparentEntityDialog = new ReparentEntityDialog(uiFont);
            unsavedChangesDialog = new UnsavedChangesDialog(uiFont);
            SceneFileLoadService = new SceneFileLoadService(
                this.projectPath,
                persistenceRegistry,
                new EditorSceneAssetReferenceResolver(EditorContentManager, this.projectPath));
            CurrentScenePath = string.Empty;
            PendingOpenScenePath = string.Empty;
            PendingSceneTransition = SceneTransitionKind.None;
            IsSceneDirty = false;
            RefreshWindowTitle();
            assetBrowserPanel.AssetSelected += HandleAssetSelected;
            assetBrowserPanel.SelectionCleared += HandleAssetSelectionCleared;
            propertiesPanel.ImportSettingsApplyRequested += HandleImportSettingsApplyRequested;
            EditorSelectionService.SelectionChanged += HandleSelectionChanged;
            EditorAssetPickerService.PickRequested += HandleAssetPickRequested;
            EditorSceneMutationService.SceneMutated += HandleSceneMutated;
            sceneHierarchyPanel.ReparentRequested += HandleSceneHierarchyReparentRequested;
            titleBar.NewMapRequested += HandleNewMapRequested;
            titleBar.OpenMapRequested += HandleOpenMapRequested;
            titleBar.SaveMapRequested += HandleSaveMapRequested;
            titleBar.SaveMapAsRequested += HandleSaveMapAsRequested;
            titleBar.AddEmptyRequested += HandleAddEmptyRequested;
            titleBar.AddCubeRequested += HandleAddCubeRequested;
            titleBar.AddPlaneRequested += HandleAddPlaneRequested;
            saveFileDialog.SaveRequested += HandleSceneSaveRequested;
            openFileDialog.OpenRequested += HandleSceneOpenRequested;
            reparentEntityDialog.ConfirmRequested += HandleReparentEntityDialogConfirmed;
            reparentEntityDialog.CancelRequested += HandleReparentEntityDialogCancelRequested;
            unsavedChangesDialog.SaveRequested += HandleUnsavedChangesSaveRequested;
            unsavedChangesDialog.DontSaveRequested += HandleUnsavedChangesDontSaveRequested;
            unsavedChangesDialog.CancelRequested += HandleUnsavedChangesCancelRequested;

            sceneHierarchyPanel.Size = new int2(280, 600);
            assetBrowserPanel.Size = new int2(500, 240);
            propertiesPanel.Size = new int2(280, 600);
            previewPanel.Size = new int2(propertiesPanel.Size.X, 240);

            dockingManager.Layout.Add(sceneHierarchyPanel);
            dockingManager.Layout.Add(assetBrowserPanel);
            dockingManager.Layout.Add(mainViewport);
            dockingManager.Layout.Add(propertiesPanel);
            dockingManager.Layout.Add(loggerPanel);
            dockingManager.Layout.Add(previewPanel);

            dockingManager.Layout.DockAsRoot(mainViewport);
            dockingManager.Layout.DockRelative(assetBrowserPanel, mainViewport, DockInsertDirection.Bottom, 0.7f);
            dockingManager.Layout.DockRelative(sceneHierarchyPanel, mainViewport, DockInsertDirection.Left, 0.3f);
            dockingManager.Layout.DockRelative(propertiesPanel, mainViewport, DockInsertDirection.Right, 0.75f);
            dockingManager.Layout.DockRelative(loggerPanel, assetBrowserPanel, DockInsertDirection.Fill, 0.5f);
            dockingManager.Layout.DockRelative(previewPanel, assetBrowserPanel, DockInsertDirection.Right, 0.75f);

            ShaderCompileTarget runtimeTarget = ResolveRuntimeShaderTarget(render3D);
            shaderModuleManager = BuildShaderModuleManager(runtimeTarget);
            EditorShaderPackageService.Initialize(shaderModuleManager, runtimeTarget, EditorContentManager);
            shaderModuleManager.ShaderBuilt += HandleShaderBuilt;
            shaderModuleManager.Start();

            RuntimeMaterial transformGizmoMaterial = BuildTransformGizmoNormalMaterial();
            RuntimeMaterial transformGizmoHighlightMaterial = BuildTransformGizmoHighlightMaterial();
            TransformTranslationGizmoFactory.Create(render3D, sceneCameraComponent, transformGizmoMaterial, transformGizmoHighlightMaterial);
            TransformRotationGizmoFactory.Create(render3D, sceneCameraComponent, transformGizmoMaterial, transformGizmoHighlightMaterial);
            TransformScaleGizmoFactory.Create(render3D, sceneCameraComponent, transformGizmoMaterial, transformGizmoHighlightMaterial);
            BuildStartScene();
            sceneHierarchyPanel.RefreshHierarchy();

            UpdateLayout(renderWidth, renderHeight);
        }

        /// <summary>
        /// Gets the core editor instance.
        /// </summary>
        public EditorCore Core => core;

        /// <summary>
        /// Gets the editor title bar UI.
        /// </summary>
        public EditorTitleBar TitleBar => titleBar;

        /// <summary>
        /// Gets the docking manager that arranges editor panels.
        /// </summary>
        public DockingManager DockingManager => dockingManager;

        /// <summary>
        /// Gets the asset import manager for the current project.
        /// </summary>
        public AssetImportManager AssetImportManager => assetImportManager;

        /// <summary>
        /// Gets the current docking cursor state.
        /// </summary>
        public DockingCursorState DockingCursorState => dockingManager.CursorState;

        /// <summary>
        /// Gets the minimum host size required for the dock layout.
        /// </summary>
        public int2 MinimumHostSize => dockingManager.MinimumHostSize;

        /// <summary>
        /// Gets the minimum window size required to fit the dock layout and title bar.
        /// </summary>
        public int2 MinimumWindowSize {
            get {
                int2 hostSize = dockingManager.MinimumHostSize;
                int minWidth = Math.Max(1, hostSize.X);
                int minHeight = Math.Max(1, hostSize.Y + titleBar.Height);
                return new int2(minWidth, minHeight);
            }
        }

        /// <summary>
        /// Gets the UI camera component.
        /// </summary>
        public CameraComponent UiCamera => uiCameraComponent;

        /// <summary>
        /// Gets the scene camera component.
        /// </summary>
        public CameraComponent SceneCamera => sceneCameraComponent;

        /// <summary>
        /// Gets the primary dockable viewport panel.
        /// </summary>
        public EditorViewport MainViewport => mainViewport;

        /// <summary>
        /// Gets the scene hierarchy panel.
        /// </summary>
        public SceneHierarchyPanel SceneHierarchyPanel => sceneHierarchyPanel;

        /// <summary>
        /// Gets the latest pointer position in window coordinates.
        /// </summary>
        public int2 PointerPosition => core.InputManager.GetMousePosition();

        /// <summary>
        /// Raised when the editor session recomputes the host window title.
        /// </summary>
        public event Action<string> TitleChanged;

        /// <summary>
        /// Gets the current host window title composed from the active scene and project.
        /// </summary>
        public string WindowTitle => titleBar.Title;

        /// <summary>
        /// Executes the editor update loop for input and entities.
        /// </summary>
        public void Update() {
            core.Update();
        }

        /// <summary>
        /// Runs a full editor frame update, including layout, docking, and rendering.
        /// </summary>
        /// <param name="renderWidth">Current render width.</param>
        /// <param name="renderHeight">Current render height.</param>
        public void UpdateFrame(int renderWidth, int renderHeight) {
            Update();
            UpdateLayout(renderWidth, renderHeight);
            bool layoutDirty = UpdateDocking(renderWidth, renderHeight);
            if (layoutDirty) {
                UpdateLayout(renderWidth, renderHeight);
            }
            RefreshHierarchy();
            Draw();
        }

        /// <summary>
        /// Executes the editor draw loop.
        /// </summary>
        public void Draw() {
            core.Draw();
        }

        /// <summary>
        /// Refreshes the scene hierarchy listing from the object manager.
        /// </summary>
        public void RefreshHierarchy() {
            sceneHierarchyPanel.RefreshHierarchy();
        }

        /// <summary>
        /// Updates layout for title bar, cameras, and dock panels.
        /// </summary>
        /// <param name="renderWidth">Current render width.</param>
        /// <param name="renderHeight">Current render height.</param>
        public void UpdateLayout(int renderWidth, int renderHeight) {
            int width = Math.Max(1, renderWidth);
            int height = Math.Max(1, renderHeight);

            titleBar.UpdateLayout(width, height);
            uiCameraComponent.Viewport = new float4(0, 0, width, height);

            int availableHeight = Math.Max(0, height - titleBar.Height);
            dockingManager.Layout.Layout(new int2(width, availableHeight), new float3(0, titleBar.Height, 0));
            EditorKeyboardFocusService.SetDockOrder(dockingManager.Layout.GetVisibleDockablesInTraversalOrder());
            gizmoCameraComponent.Viewport = sceneCameraComponent.Viewport;
            assetPickerModal.UpdateLayout(width, height);
            saveFileDialog.UpdateLayout(width, height);
            openFileDialog.UpdateLayout(width, height);
            reparentEntityDialog.UpdateLayout(width, height);
            unsavedChangesDialog.UpdateLayout(width, height);
            mainViewport.RefreshInputBlockers();
            UpdateDockInputBlockers();
        }

        /// <summary>
        /// Shows the asset picker modal and routes the selected asset to the provided callback.
        /// </summary>
        /// <param name="onPicked">Callback invoked when an asset is chosen.</param>
        public void ShowAssetPicker(Action<AssetBrowserEntry> onPicked) {
            assetPickerModal.Show(onPicked);
        }

        /// <summary>
        /// Hides the asset picker modal if it is visible.
        /// </summary>
        public void HideAssetPicker() {
            assetPickerModal.Hide();
        }

        /// <summary>
        /// Updates docking interactions for the current frame.
        /// </summary>
        /// <param name="renderWidth">Current render width.</param>
        /// <param name="renderHeight">Current render height.</param>
        /// <returns>True when the docking layout changed.</returns>
        public bool UpdateDocking(int renderWidth, int renderHeight) {
            int width = Math.Max(1, renderWidth);
            int height = Math.Max(1, renderHeight);
            int availableHeight = Math.Max(0, height - titleBar.Height);
            int2 hostSize = new int2(width, availableHeight);
            float3 origin = new float3(0, titleBar.Height, 0);

            int2 pointer = core.InputManager.GetMousePosition();
            return dockingManager.Update(pointer, core.InputManager.GetMouseLeftButtonState(), hostSize, origin);
        }

        /// <summary>
        /// Updates input blockers for docked panels so viewport input is suppressed under UI regions.
        /// </summary>
        void UpdateDockInputBlockers() {
            IReadOnlyList<DockableEntity> dockables = dockingManager.Layout.Dockables;
            for (int i = 0; i < dockables.Count; i++) {
                DockableEntity dockable = dockables[i];
                if (!dockable.Enabled || ReferenceEquals(dockable, mainViewport)) {
                    EditorInputCaptureService.ClearBlocker(dockable);
                    continue;
                }

                int width = Math.Max(0, dockable.Size.X);
                int height = Math.Max(0, dockable.Size.Y + DockableEntity.TitleBarHeight);
                if (width <= 0 || height <= 0) {
                    EditorInputCaptureService.ClearBlocker(dockable);
                    continue;
                }

                int2 position = new int2((int)Math.Round(dockable.Position.X), (int)Math.Round(dockable.Position.Y));
                EditorInputCaptureService.SetBlocker(dockable, position, new int2(width, height));
            }
        }

        /// <summary>
        /// Enables or disables keyboard input capture for the host window.
        /// </summary>
        /// <param name="isActive">True to capture keyboard input; false to stop capture.</param>
        public void SetKeyboardActive(bool isActive) {
            core.InputManager.SetKeyboardActive(isActive);
        }

        /// <summary>
        /// Disposes engine resources owned by the session.
        /// </summary>
        public void Dispose() {
            assetBrowserPanel.AssetSelected -= HandleAssetSelected;
            assetBrowserPanel.SelectionCleared -= HandleAssetSelectionCleared;
            propertiesPanel.ImportSettingsApplyRequested -= HandleImportSettingsApplyRequested;
            EditorSelectionService.SelectionChanged -= HandleSelectionChanged;
            EditorAssetPickerService.PickRequested -= HandleAssetPickRequested;
            EditorSceneMutationService.SceneMutated -= HandleSceneMutated;
            sceneHierarchyPanel.ReparentRequested -= HandleSceneHierarchyReparentRequested;
            titleBar.NewMapRequested -= HandleNewMapRequested;
            titleBar.OpenMapRequested -= HandleOpenMapRequested;
            titleBar.SaveMapRequested -= HandleSaveMapRequested;
            titleBar.SaveMapAsRequested -= HandleSaveMapAsRequested;
            titleBar.AddEmptyRequested -= HandleAddEmptyRequested;
            titleBar.AddCubeRequested -= HandleAddCubeRequested;
            titleBar.AddPlaneRequested -= HandleAddPlaneRequested;
            saveFileDialog.SaveRequested -= HandleSceneSaveRequested;
            openFileDialog.OpenRequested -= HandleSceneOpenRequested;
            reparentEntityDialog.ConfirmRequested -= HandleReparentEntityDialogConfirmed;
            reparentEntityDialog.CancelRequested -= HandleReparentEntityDialogCancelRequested;
            unsavedChangesDialog.SaveRequested -= HandleUnsavedChangesSaveRequested;
            unsavedChangesDialog.DontSaveRequested -= HandleUnsavedChangesDontSaveRequested;
            unsavedChangesDialog.CancelRequested -= HandleUnsavedChangesCancelRequested;
            mainViewport.ClearInputBlockers();
            EditorViewportToolService.ClearToolMode(sceneCameraComponent);
            assetPickerModal.Hide();
            saveFileDialog.Hide();
            openFileDialog.Hide();
            reparentEntityDialog.Hide();
            unsavedChangesDialog.Hide();
            shaderModuleManager.ShaderBuilt -= HandleShaderBuilt;
            shaderModuleManager.Dispose();
            loggerPanel.Detach();
            EditorKeyboardFocusService.Reset();
            core.Dispose();
        }

        /// <summary>
        /// Handles asset picker requests from editor UI.
        /// </summary>
        /// <param name="request">Request describing the pick operation.</param>
        void HandleAssetPickRequested(AssetPickerRequest request) {
            if (request == null) {
                throw new ArgumentNullException(nameof(request));
            }

            if (string.IsNullOrWhiteSpace(request.ExtensionFilter)) {
                assetPickerModal.Show(request.OnPicked);
                return;
            }

            assetPickerModal.Show(request.OnPicked, request.ExtensionFilter);
        }

        /// <summary>
        /// Handles the main `New Map` command from the editor title bar.
        /// </summary>
        void HandleNewMapRequested() {
            RequestSceneTransition(SceneTransitionKind.NewMap, string.Empty);
        }

        /// <summary>
        /// Handles the main `Open Map...` command from the editor title bar.
        /// </summary>
        void HandleOpenMapRequested() {
            RequestSceneTransition(SceneTransitionKind.OpenMap, string.Empty);
        }

        /// <summary>
        /// Records one pending scene transition and either continues immediately or shows the unsaved-changes guard.
        /// </summary>
        /// <param name="transitionKind">Transition that should continue once the guard is resolved.</param>
        /// <param name="openPath">Absolute scene path that should be opened when resuming an open-map transition.</param>
        void RequestSceneTransition(SceneTransitionKind transitionKind, string openPath) {
            PendingSceneTransition = transitionKind;
            PendingOpenScenePath = openPath ?? string.Empty;
            reparentEntityDialog.Hide();

            if (!IsSceneDirty) {
                ContinuePendingSceneTransition();
                return;
            }

            unsavedChangesDialog.Show();
        }

        /// <summary>
        /// Continues the transition currently stored in pending scene state.
        /// </summary>
        void ContinuePendingSceneTransition() {
            SceneTransitionKind pendingTransition = PendingSceneTransition;
            string pendingOpenPath = PendingOpenScenePath;

            PendingSceneTransition = SceneTransitionKind.None;
            PendingOpenScenePath = string.Empty;
            unsavedChangesDialog.Hide();

            if (pendingTransition == SceneTransitionKind.NewMap) {
                ResetToNewScene();
                return;
            } else if (pendingTransition == SceneTransitionKind.OpenMap) {
                if (string.IsNullOrWhiteSpace(pendingOpenPath)) {
                    openFileDialog.Show(SceneSavePathResolver.DefaultSceneDirectory);
                    return;
                }

                LoadSceneIntoSession(pendingOpenPath);
            }
        }

        /// <summary>
        /// Handles the Add Empty command from the editor title bar.
        /// </summary>
        void HandleAddEmptyRequested() {
            CreateAndSelectSceneEntity(SceneCreationService.CreateEmpty);
        }

        /// <summary>
        /// Handles the Add Cube command from the editor title bar.
        /// </summary>
        void HandleAddCubeRequested() {
            CreateAndSelectSceneEntity(SceneCreationService.CreateCube);
        }

        /// <summary>
        /// Handles the Add Plane command from the editor title bar.
        /// </summary>
        void HandleAddPlaneRequested() {
            CreateAndSelectSceneEntity(SceneCreationService.CreatePlane);
        }

        /// <summary>
        /// Creates one scene entity, refreshes the hierarchy, and selects the result.
        /// </summary>
        /// <param name="createEntity">Factory that builds the new scene entity.</param>
        void CreateAndSelectSceneEntity(Func<EditorEntity> createEntity) {
            if (createEntity == null) {
                throw new ArgumentNullException(nameof(createEntity));
            }

            Entity previousSelection = EditorSelectionService.SelectedEntity;

            try {
                EditorEntity entity = createEntity();
                sceneHierarchyPanel.RefreshHierarchy();
                EditorSelectionService.SetSelectedEntity(entity);
                EditorSceneMutationService.MarkSceneMutated();
            } catch (Exception ex) {
                Logger.WriteError($"Scene entity creation failed: {ex.Message}");
                if (previousSelection == null) {
                    EditorSelectionService.ClearSelection();
                } else {
                    EditorSelectionService.SetSelectedEntity(previousSelection);
                }
            }
        }

        /// <summary>
        /// Handles the main `Save Map` command from the editor title bar.
        /// </summary>
        void HandleSaveMapRequested() {
            if (string.IsNullOrWhiteSpace(CurrentScenePath)) {
                ShowSceneSaveDialog();
                return;
            }

            HandleSceneSaveRequested(CurrentScenePath);
        }

        /// <summary>
        /// Handles the main `Save Map As...` command from the editor title bar.
        /// </summary>
        void HandleSaveMapAsRequested() {
            ShowSceneSaveDialog();
        }

        /// <summary>
        /// Shows the save-file dialog using the current scene path to seed the folder and file name.
        /// </summary>
        void ShowSceneSaveDialog() {
            string initialRelativeDirectory = SceneSavePathResolver.GetInitialRelativeDirectory(CurrentScenePath);
            string suggestedFileName = SceneSavePathResolver.GetSuggestedFileName(CurrentScenePath);
            saveFileDialog.Show(initialRelativeDirectory, suggestedFileName);
        }

        /// <summary>
        /// Handles one confirmed scene path from the open-file dialog.
        /// </summary>
        /// <param name="fullPath">Absolute path to the selected scene file.</param>
        void HandleSceneOpenRequested(string fullPath) {
            if (string.IsNullOrWhiteSpace(fullPath)) {
                throw new ArgumentException("Scene path must be provided.", nameof(fullPath));
            }

            RequestSceneTransition(SceneTransitionKind.OpenMap, Path.GetFullPath(fullPath));
        }

        /// <summary>
        /// Saves the current editor scene to the provided path and updates tracked scene state.
        /// </summary>
        /// <param name="fullPath">Absolute `.helen` path selected by the user.</param>
        void HandleSceneSaveRequested(string fullPath) {
            if (string.IsNullOrWhiteSpace(fullPath)) {
                throw new ArgumentException("Scene path must be provided.", nameof(fullPath));
            }

            try {
                SceneSaveService.Save(fullPath);
                CurrentScenePath = Path.GetFullPath(fullPath);
                MarkSceneClean();
                RefreshWindowTitle();
                assetBrowserPanel.RefreshEntries();
                saveFileDialog.Hide();
                if (PendingSceneTransition != SceneTransitionKind.None) {
                    ContinuePendingSceneTransition();
                }
            } catch (Exception ex) {
                Logger.WriteError($"Scene save failed: {ex.Message}");
                saveFileDialog.ShowError(ex.Message);
            }
        }

        /// <summary>
        /// Loads one `.helen` scene into the active editor session and swaps it into the live scene on success.
        /// </summary>
        /// <param name="fullPath">Absolute path to the scene file that should be opened.</param>
        void LoadSceneIntoSession(string fullPath) {
            if (string.IsNullOrWhiteSpace(fullPath)) {
                throw new ArgumentException("Scene path must be provided.", nameof(fullPath));
            }

            List<EditorEntity> existingSceneEntities = CaptureUserSceneEntities();
            try {
                IReadOnlyList<EditorEntity> loadedRoots = SceneFileLoadService.Load(fullPath);
                ClearUserSceneEntities(existingSceneEntities);
                AttachLoadedRoots(loadedRoots);
                CurrentScenePath = Path.GetFullPath(fullPath);
                MarkSceneClean();
                RefreshWindowTitle();
                EditorSelectionService.ClearSelection();
                sceneHierarchyPanel.RefreshHierarchy();
                assetBrowserPanel.RefreshEntries();
                openFileDialog.Hide();
                reparentEntityDialog.Hide();
            } catch (Exception ex) {
                Logger.WriteError($"Scene open failed: {ex.Message}");
                openFileDialog.ShowError(ex.Message);
            }
        }

        /// <summary>
        /// Resets the session to one new empty scene.
        /// </summary>
        void ResetToNewScene() {
            ClearUserSceneEntities();
            CurrentScenePath = string.Empty;
            MarkSceneClean();
            RefreshWindowTitle();
            EditorSelectionService.ClearSelection();
            sceneHierarchyPanel.RefreshHierarchy();
            openFileDialog.Hide();
            reparentEntityDialog.Hide();
        }

        /// <summary>
        /// Starts the scene-hierarchy reparent workflow for the requested entity.
        /// </summary>
        /// <param name="entity">Entity that should be reparented.</param>
        void HandleSceneHierarchyReparentRequested(Entity entity) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }

            List<Entity> candidateParents = BuildReparentCandidateEntities(entity);
            reparentEntityDialog.Show(entity, candidateParents);
        }

        /// <summary>
        /// Applies a confirmed entity reparent operation from the modal dialog.
        /// </summary>
        /// <param name="selection">Confirmed target entity and destination parent.</param>
        void HandleReparentEntityDialogConfirmed(ReparentEntityDialogSelection selection) {
            if (selection == null) {
                throw new ArgumentNullException(nameof(selection));
            }

            try {
                bool changed = ReparentService.Reparent(selection.TargetEntity, selection.ParentEntity);
                sceneHierarchyPanel.RefreshHierarchy();
                EditorSelectionService.SetSelectedEntity(selection.TargetEntity);
                if (changed) {
                    EditorSceneMutationService.MarkSceneMutated();
                }

                reparentEntityDialog.Hide();
            } catch (Exception ex) {
                Logger.WriteError($"Scene reparent failed: {ex.Message}");
                reparentEntityDialog.ShowError(ex.Message);
            }
        }

        /// <summary>
        /// Cancels the active scene-hierarchy reparent workflow.
        /// </summary>
        void HandleReparentEntityDialogCancelRequested() {
            reparentEntityDialog.Hide();
        }

        /// <summary>
        /// Handles the Save action from the unsaved-changes dialog.
        /// </summary>
        void HandleUnsavedChangesSaveRequested() {
            if (string.IsNullOrWhiteSpace(CurrentScenePath)) {
                unsavedChangesDialog.Hide();
                ShowSceneSaveDialog();
                return;
            }

            HandleSceneSaveRequested(CurrentScenePath);
        }

        /// <summary>
        /// Handles the Don't Save action from the unsaved-changes dialog.
        /// </summary>
        void HandleUnsavedChangesDontSaveRequested() {
            ContinuePendingSceneTransition();
        }

        /// <summary>
        /// Handles the Cancel action from the unsaved-changes dialog.
        /// </summary>
        void HandleUnsavedChangesCancelRequested() {
            PendingSceneTransition = SceneTransitionKind.None;
            PendingOpenScenePath = string.Empty;
            unsavedChangesDialog.Hide();
        }

        /// <summary>
        /// Marks the current scene as dirty after one user-authored mutation.
        /// </summary>
        void HandleSceneMutated() {
            IsSceneDirty = true;
        }

        /// <summary>
        /// Marks the current scene as clean after one successful save, load, or reset.
        /// </summary>
        void MarkSceneClean() {
            IsSceneDirty = false;
        }

        /// <summary>
        /// Initializes startup scene content for a new editor session.
        /// New sessions begin empty and wait for the user to add scene entities explicitly.
        /// </summary>
        void BuildStartScene() {
            if (helengine.Core.Instance == null || helengine.Core.Instance.RenderManager3D == null) {
                throw new InvalidOperationException("Viewport grid initialization requires an active 3D render manager.");
            }

            EditorViewportGridFactory.Create(helengine.Core.Instance.RenderManager3D);
        }

        /// <summary>
        /// Removes every user-authored scene entity from the live session while preserving editor infrastructure.
        /// </summary>
        void ClearUserSceneEntities() {
            ClearUserSceneEntities(CaptureUserSceneEntities());
        }

        /// <summary>
        /// Captures the current user-authored scene entities so they can be removed later without touching newly loaded entities.
        /// </summary>
        /// <returns>Snapshot of the current user-authored scene entities.</returns>
        List<EditorEntity> CaptureUserSceneEntities() {
            List<Entity> liveEntities = new List<Entity>(helengine.Core.Instance.ObjectManager.Entities);
            List<EditorEntity> capturedEntities = new List<EditorEntity>(liveEntities.Count);
            for (int i = 0; i < liveEntities.Count; i++) {
                if (liveEntities[i] is not EditorEntity editorEntity) {
                    continue;
                }
                if (!IsUserSceneEntity(editorEntity)) {
                    continue;
                }

                capturedEntities.Add(editorEntity);
            }

            return capturedEntities;
        }

        /// <summary>
        /// Removes the provided user-authored scene entities from the live session.
        /// </summary>
        /// <param name="entities">Previously captured user-authored scene entities to remove.</param>
        void ClearUserSceneEntities(IReadOnlyList<EditorEntity> entities) {
            if (entities == null) {
                throw new ArgumentNullException(nameof(entities));
            }

            for (int i = 0; i < entities.Count; i++) {
                EditorEntity editorEntity = entities[i];
                if (editorEntity == null) {
                    continue;
                }
                if (!IsUserSceneEntity(editorEntity)) {
                    continue;
                }

                editorEntity.Enabled = false;
                helengine.Core.Instance.ObjectManager.RemoveEntity(editorEntity);
            }
        }

        /// <summary>
        /// Determines whether one editor entity belongs to the user-authored scene rather than editor infrastructure.
        /// </summary>
        /// <param name="editorEntity">Editor entity to evaluate.</param>
        /// <returns>True when the entity belongs to the user-authored scene.</returns>
        bool IsUserSceneEntity(EditorEntity editorEntity) {
            if (editorEntity == null) {
                return false;
            }
            if (editorEntity.InternalEntity) {
                return false;
            }
            if (editorEntity.LayerMask != EditorLayerMasks.SceneObjects) {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Builds the visible scene-entity hierarchy for one scene-hierarchy reparent request.
        /// Invalid targets remain visible so the dialog can disable them instead of hiding them.
        /// </summary>
        /// <param name="targetEntity">Entity that should be reparented.</param>
        /// <returns>Visible scene entities that should appear in the reparent hierarchy picker.</returns>
        List<Entity> BuildReparentCandidateEntities(Entity targetEntity) {
            if (targetEntity == null) {
                throw new ArgumentNullException(nameof(targetEntity));
            }

            List<Entity> candidateParents = new List<Entity>();
            List<Entity> entities = helengine.Core.Instance.ObjectManager.Entities;
            for (int i = 0; i < entities.Count; i++) {
                Entity candidate = entities[i];
                if (!IsVisibleSceneEntity(candidate)) {
                    continue;
                }

                candidateParents.Add(candidate);
            }

            return candidateParents;
        }

        /// <summary>
        /// Returns true when the provided entity should appear as one selectable scene item in hierarchy workflows.
        /// </summary>
        /// <param name="entity">Entity to evaluate.</param>
        /// <returns>True when the entity belongs to the visible user-authored scene.</returns>
        bool IsVisibleSceneEntity(Entity entity) {
            Entity current = entity;
            while (current != null) {
                if (current is EditorEntity editorEntity && editorEntity.InternalEntity) {
                    return false;
                }

                current = current.Parent;
            }

            return true;
        }

        /// <summary>
        /// Attaches loaded root entities to the live scene by enabling them after the old scene has been cleared.
        /// </summary>
        /// <param name="roots">Loaded root entities that should become active in the session.</param>
        void AttachLoadedRoots(IReadOnlyList<EditorEntity> roots) {
            if (roots == null) {
                throw new ArgumentNullException(nameof(roots));
            }

            for (int i = 0; i < roots.Count; i++) {
                EditorEntity root = roots[i];
                if (root == null) {
                    throw new InvalidOperationException("Loaded scene contained a null root entity.");
                }

                root.Enabled = true;
            }
        }

        /// <summary>
        /// Builds the default material used by transform gizmo meshes.
        /// </summary>
        /// <returns>Runtime material instance.</returns>
        RuntimeMaterial BuildTransformGizmoNormalMaterial() {
            return BuildBuiltInRuntimeMaterial(TransformGizmoShaderFileName);
        }

        /// <summary>
        /// Builds the highlighted material used by transform gizmo meshes.
        /// </summary>
        /// <returns>Runtime material instance.</returns>
        RuntimeMaterial BuildTransformGizmoHighlightMaterial() {
            return BuildBuiltInRuntimeMaterial(TransformGizmoHighlightShaderFileName);
        }

        /// <summary>
        /// Builds a runtime material from one built-in editor shader source file.
        /// </summary>
        /// <param name="shaderFileName">Built-in editor shader source file name.</param>
        /// <returns>Runtime material instance.</returns>
        RuntimeMaterial BuildBuiltInRuntimeMaterial(string shaderFileName) {
            if (string.IsNullOrWhiteSpace(shaderFileName)) {
                throw new ArgumentException("Shader file name must be provided.", nameof(shaderFileName));
            }

            ShaderAsset shaderAsset = EditorBuiltInShaderAssetLibrary.LoadShaderAsset(core.RenderManager3D, shaderFileName);
            string shaderName = Path.GetFileNameWithoutExtension(shaderFileName);
            if (string.IsNullOrWhiteSpace(shaderName)) {
                throw new InvalidOperationException("Built-in shader name could not be resolved.");
            }

            if (string.IsNullOrWhiteSpace(shaderAsset.Id)) {
                throw new InvalidOperationException("Shader asset id must be provided.");
            }

            var materialAsset = new MaterialAsset {
                Id = string.Concat(shaderName, ".material"),
                ShaderAssetId = shaderAsset.Id,
                VertexProgram = string.Concat(shaderName, ".vs"),
                PixelProgram = string.Concat(shaderName, ".ps"),
                Variant = DefaultRuntimeShaderVariant
            };

            return core.RenderManager3D.BuildMaterialFromRaw(materialAsset, shaderAsset);
        }

        /// <summary>
        /// Handles asset selections from the browser to display import settings.
        /// </summary>
        /// <param name="entry">Selected asset entry.</param>
        void HandleAssetSelected(AssetBrowserEntry entry) {
            if (entry == null) {
                throw new ArgumentNullException(nameof(entry));
            }

            if (entry.IsDirectory) {
                return;
            }

            if (entry.IsGenerated) {
                propertiesPanel.ShowGeneratedAssetSummary(entry);
                previewPanel.ClearPreview();
                return;
            }

            if (entry.EntryKind == AssetEntryKind.Scene) {
                propertiesPanel.ShowSceneAssetSummary(entry);
                previewPanel.ClearPreview();
                return;
            }

            if (IsMaterialAssetEntry(entry)) {
                try {
                    MaterialAsset materialAsset = LoadMaterialAsset(entry.FullPath);
                    propertiesPanel.ShowMaterialSettings(entry, materialAsset);
                } catch (Exception ex) {
                    propertiesPanel.ShowImportError(entry, ex.Message);
                }
                return;
            }

            try {
                AssetImportSettings settings;
                if (!assetImportManager.TryLoadOrCreateImportSettings(entry.FullPath, out settings)) {
                    propertiesPanel.ShowEmpty();
                    previewPanel.ClearPreview();
                    return;
                }

                assetImportManager.SaveImportSettings(entry.FullPath, settings);
                IReadOnlyList<string> importerIds = assetImportManager.GetImporterIdsForExtension(entry.Extension);
                if (importerIds.Count == 0) {
                    propertiesPanel.ShowImportError(entry, "No importers are registered for this asset type.");
                    previewPanel.ClearPreview();
                    return;
                }

                propertiesPanel.ShowImportSettings(entry, settings, importerIds);
                UpdatePreview(entry);
            } catch (Exception ex) {
                propertiesPanel.ShowImportError(entry, ex.Message);
                previewPanel.ClearPreview();
            }
        }

        /// <summary>
        /// Handles shader build notifications to refresh runtime shader resources.
        /// </summary>
        /// <param name="shaderName">Shader name that was rebuilt.</param>
        /// <param name="packagePath">Package path containing the updated shader.</param>
        void HandleShaderBuilt(string shaderName, string packagePath) {
            if (string.IsNullOrWhiteSpace(packagePath)) {
                return;
            }

            try {
                ShaderAsset shaderAsset = EditorShaderPackageService.LoadShaderAssetFromPackage(packagePath);
                string shaderAssetId = string.IsNullOrWhiteSpace(shaderAsset.Id) ? shaderName : shaderAsset.Id;
                core.RenderManager3D.InvalidateShaderResources(shaderAssetId, shaderAsset);
            } catch (Exception ex) {
                Logger.WriteError($"Shader reload failed for '{shaderName}': {ex.Message}");
            }
        }

        /// <summary>
        /// Applies a pending importer selection to the selected asset settings.
        /// </summary>
        /// <param name="entry">Selected asset entry.</param>
        /// <param name="importerId">Importer identifier to apply.</param>
        void HandleImportSettingsApplyRequested(AssetBrowserEntry entry, string importerId) {
            if (entry == null) {
                throw new ArgumentNullException(nameof(entry));
            }

            if (string.IsNullOrWhiteSpace(importerId)) {
                throw new ArgumentException("Importer id must be provided.", nameof(importerId));
            }

            if (entry.IsDirectory) {
                return;
            }

            try {
                AssetImportSettings settings = assetImportManager.LoadOrCreateImportSettings(entry.FullPath);
                settings.ImporterId = importerId;
                assetImportManager.SaveImportSettings(entry.FullPath, settings);

                IReadOnlyList<string> importerIds = assetImportManager.GetImporterIdsForExtension(entry.Extension);
                propertiesPanel.ShowImportSettings(entry, settings, importerIds);
            } catch (Exception ex) {
                propertiesPanel.ShowImportError(entry, ex.Message);
            }
        }

        /// <summary>
        /// Determines whether the selected entry is a material asset.
        /// </summary>
        /// <param name="entry">Entry to evaluate.</param>
        /// <returns>True when the entry is a material asset.</returns>
        bool IsMaterialAssetEntry(AssetBrowserEntry entry) {
            if (entry == null) {
                return false;
            }

            string extension = entry.Extension;
            return string.Equals(extension, EditorFileTemplateRegistry.MaterialExtension, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Loads a material asset from disk.
        /// </summary>
        /// <param name="path">Path to the material asset.</param>
        /// <returns>Material asset instance.</returns>
        MaterialAsset LoadMaterialAsset(string path) {
            if (string.IsNullOrWhiteSpace(path)) {
                throw new ArgumentException("Material path must be provided.", nameof(path));
            }

            return EditorContentManager.Load<MaterialAsset>(path, EditorContentProcessorIds.MaterialAsset);
        }

        /// <summary>
        /// Clears property and preview panels when no asset is selected.
        /// </summary>
        void HandleAssetSelectionCleared() {
            propertiesPanel.ShowEmpty();
            previewPanel.ClearPreview();
        }

        /// <summary>
        /// Updates the properties panel when the selection changes.
        /// </summary>
        /// <param name="args">Selection change data.</param>
        void HandleSelectionChanged(EditorSelectionChangedEventArgs args) {
            if (args == null) {
                throw new ArgumentNullException(nameof(args));
            }

            if (args.HasSelection) {
                propertiesPanel.ShowEntityProperties(args.SelectedEntity);
            } else {
                propertiesPanel.ShowEmpty();
            }
        }

        /// <summary>
        /// Updates the preview panel based on the selected asset.
        /// </summary>
        /// <param name="entry">Selected asset entry.</param>
        void UpdatePreview(AssetBrowserEntry entry) {
            if (entry == null) {
                throw new ArgumentNullException(nameof(entry));
            }

            if (!assetImportManager.IsTextureExtension(entry.Extension)) {
                previewPanel.ClearPreview();
                return;
            }

            TextureAsset texture;
            if (assetImportManager.TryLoadTextureAsset(entry.FullPath, out texture)) {
                previewPanel.ShowTexture(texture);
            } else {
                previewPanel.ClearPreview();
            }
        }

        /// <summary>
        /// Builds a shader module manager for the current project path.
        /// </summary>
        /// <returns>Configured shader module manager.</returns>
        ShaderModuleManager BuildShaderModuleManager(ShaderCompileTarget runtimeTarget) {
            string projectRoot = ResolveProjectRootPath(projectPath);
            string shaderRootPath = ResolveShaderRootPath(projectRoot);
            string packageOutputPath = ResolveShaderPackageOutputPath(projectRoot);
            ShaderPackageBuildOptions buildOptions = BuildShaderPackageOptions(runtimeTarget);
            var options = new ShaderModuleManagerOptions(
                shaderRootPath,
                packageOutputPath,
                buildOptions,
                runtimeTarget,
                ShaderBuildDelayMilliseconds);
            return new ShaderModuleManager(options);
        }

        /// <summary>
        /// Builds the default shader package build options for the editor.
        /// </summary>
        /// <returns>Shader package build options.</returns>
        ShaderPackageBuildOptions BuildShaderPackageOptions(ShaderCompileTarget runtimeTarget) {
            ShaderTargetBuildOptions targetOptions;
            switch (runtimeTarget) {
                case ShaderCompileTarget.DirectX11:
                    targetOptions = new ShaderTargetBuildOptions(ShaderCompileTarget.DirectX11, new ShaderModel(4, 0));
                    break;
                case ShaderCompileTarget.Vulkan:
                    targetOptions = new ShaderTargetBuildOptions(ShaderCompileTarget.Vulkan, new ShaderModel(4, 0));
                    break;
                default:
                    throw new InvalidOperationException("Unsupported runtime shader target.");
            }

            ShaderTargetBuildOptions[] targets = new[] { targetOptions };
            ShaderDefine[] defines = Array.Empty<ShaderDefine>();
            return new ShaderPackageBuildOptions(
                targets,
                ShaderBindingPolicies.Default,
                true,
                false,
                false,
                defines);
        }

        /// <summary>
        /// Resolves the runtime shader target from the active renderer instance.
        /// </summary>
        /// <param name="render3D">Renderer instance used by the editor session.</param>
        /// <returns>Shader compile target that matches the runtime renderer.</returns>
        ShaderCompileTarget ResolveRuntimeShaderTarget(RenderManager3D render3D) {
            if (render3D == null) {
                throw new ArgumentNullException(nameof(render3D));
            }

            if (render3D is helengine.directx11.DirectX11Renderer3D) {
                return ShaderCompileTarget.DirectX11;
            }

            if (render3D is helengine.vulkan.VulkanRenderer3D) {
                return ShaderCompileTarget.Vulkan;
            }

            throw new InvalidOperationException("Unsupported renderer for shader runtime target resolution.");
        }

        /// <summary>
        /// Resolves the shader root path for the current project.
        /// </summary>
        /// <param name="projectRoot">Project root path.</param>
        /// <returns>Absolute shader root path.</returns>
        string ResolveShaderRootPath(string projectRoot) {
            if (string.IsNullOrWhiteSpace(projectRoot)) {
                throw new InvalidOperationException("Project root path is required to locate shader sources.");
            }

            return ResolveAssetsRootPath(projectRoot);
        }

        /// <summary>
        /// Resolves the assets root path for the current project.
        /// </summary>
        /// <param name="projectRoot">Project root path.</param>
        /// <returns>Absolute assets root path.</returns>
        string ResolveAssetsRootPath(string projectRoot) {
            if (string.IsNullOrWhiteSpace(projectRoot)) {
                throw new InvalidOperationException("Project root path is required to locate assets.");
            }

            string assetsRootPath = Path.Combine(projectRoot, "assets");
            return Path.GetFullPath(assetsRootPath);
        }

        /// <summary>
        /// Resolves the shader package output path for the current project.
        /// </summary>
        /// <param name="projectRoot">Project root path.</param>
        /// <returns>Absolute shader package output path.</returns>
        string ResolveShaderPackageOutputPath(string projectRoot) {
            if (string.IsNullOrWhiteSpace(projectRoot)) {
                throw new InvalidOperationException("Project root path is required to locate shader output.");
            }

            string outputPath = Path.Combine(projectRoot, "shader-cache");
            return Path.GetFullPath(outputPath);
        }

        /// <summary>
        /// Recomputes the host title, updates the editor title bar, and notifies the window host.
        /// </summary>
        void RefreshWindowTitle() {
            string title = BuildWindowTitle();
            titleBar.Title = title;
            TitleChanged?.Invoke(title);
        }

        /// <summary>
        /// Builds the host window title from the current scene file and open project.
        /// </summary>
        /// <returns>Window title text shown by the editor host.</returns>
        string BuildWindowTitle() {
            string title = $"helengine - {ProjectDisplayName}";
            if (string.IsNullOrWhiteSpace(CurrentScenePath)) {
                return title;
            }

            return $"{ResolveSceneDisplayName(CurrentScenePath)} - {title}";
        }

        /// <summary>
        /// Resolves the display name for one saved scene path.
        /// </summary>
        /// <param name="scenePath">Absolute scene path.</param>
        /// <returns>Scene file name without its extension.</returns>
        string ResolveSceneDisplayName(string scenePath) {
            if (string.IsNullOrWhiteSpace(scenePath)) {
                throw new InvalidOperationException("Scene path must be provided.");
            }

            return Path.GetFileNameWithoutExtension(scenePath);
        }

        /// <summary>
        /// Resolves one project directory or project file path to the canonical `.heproj` file path.
        /// </summary>
        /// <param name="projectPath">Project root directory or project file path.</param>
        /// <returns>Validated absolute canonical `.heproj` file path.</returns>
        string ResolveCanonicalProjectFilePath(string projectPath) {
            if (string.IsNullOrWhiteSpace(projectPath)) {
                throw new InvalidOperationException("Project path must be provided.");
            }

            ProjectFilePathResolver resolver = new ProjectFilePathResolver();
            return resolver.Resolve(projectPath);
        }

        /// <summary>
        /// Resolves the project display name from a project file path or root directory path.
        /// </summary>
        /// <param name="projectPath">Project root directory or project file path.</param>
        /// <returns>Display name that should appear in the host window title.</returns>
        string ResolveProjectDisplayName(string projectPath) {
            string canonicalProjectFilePath = ResolveCanonicalProjectFilePath(projectPath);
            return ResolveProjectDisplayNameFromCanonicalProjectFile(canonicalProjectFilePath);
        }

        /// <summary>
        /// Resolves the project display name from one validated canonical project file path.
        /// </summary>
        /// <param name="canonicalProjectFilePath">Validated absolute canonical `.heproj` file path.</param>
        /// <returns>Display name that should appear in the host window title.</returns>
        string ResolveProjectDisplayNameFromCanonicalProjectFile(string canonicalProjectFilePath) {
            string fileName = Path.GetFileName(canonicalProjectFilePath);
            if (string.IsNullOrWhiteSpace(fileName)) {
                throw new InvalidOperationException("Project path must resolve to a display name.");
            }

            return fileName;
        }

        /// <summary>
        /// Resolves the project root directory from a project root or project file path.
        /// </summary>
        /// <param name="projectPath">Project root directory or project file path.</param>
        /// <returns>Absolute path to the project root directory.</returns>
        string ResolveProjectRootPath(string projectPath) {
            string canonicalProjectFilePath = ResolveCanonicalProjectFilePath(projectPath);
            return ResolveProjectRootPathFromCanonicalProjectFile(canonicalProjectFilePath);
        }

        /// <summary>
        /// Resolves the project root directory from one validated canonical project file path.
        /// </summary>
        /// <param name="canonicalProjectFilePath">Validated absolute canonical `.heproj` file path.</param>
        /// <returns>Absolute path to the project root directory.</returns>
        string ResolveProjectRootPathFromCanonicalProjectFile(string canonicalProjectFilePath) {
            string directory = Path.GetDirectoryName(canonicalProjectFilePath);
            if (string.IsNullOrWhiteSpace(directory)) {
                throw new InvalidOperationException("Project file path does not include a directory.");
            }

            return Path.GetFullPath(directory);
        }

        /// <summary>
        /// Initializes asset import management and generates missing import settings.
        /// </summary>
        /// <param name="importers">Asset importers to register.</param>
        /// <returns>Initialized asset import manager.</returns>
        AssetImportManager InitializeAssetImports(
            IReadOnlyList<IAssetImporterRegistration> importers) {
            if (importers == null) {
                throw new ArgumentNullException(nameof(importers));
            }

            string projectRootPath = ResolveProjectRootPath(projectPath);
            string projectAssetsRootPath = ResolveAssetsRootPath(projectRootPath);
            ContentManager projectContentManager = core.GetContentManager(projectAssetsRootPath);
            var manager = new AssetImportManager(projectRootPath, projectContentManager);
            for (int i = 0; i < importers.Count; i++) {
                IAssetImporterRegistration registration = importers[i];
                if (registration == null) {
                    throw new InvalidOperationException("Importer registrations must not be null.");
                }

                registration.Register(manager);
            }

            manager.GenerateMissingImportSettings();
            manager.ImportTexturesMissingCache();
            manager.ImportModelsMissingCache();
            return manager;
        }
    }
}
