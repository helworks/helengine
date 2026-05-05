using helengine.platforms;
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
            OpenMap,

            /// <summary>
            /// The editor host should close after the unsaved-changes guard resolves.
            /// </summary>
            Exit
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
        /// Canonical absolute `.heproj` path for the open project.
        /// </summary>
        readonly string CanonicalProjectFilePath;
        /// <summary>
        /// Project file name shown in the host window title.
        /// </summary>
        readonly string ProjectDisplayName;
        /// <summary>
        /// Exact engine version required by the current project file.
        /// </summary>
        readonly string RequiredEngineVersion;
        /// <summary>
        /// Game project name loaded from the canonical project document.
        /// </summary>
        readonly string ProjectName;
        /// <summary>
        /// Human-visible project version loaded from the canonical project document.
        /// </summary>
        readonly string ProjectVersion;
        /// <summary>
        /// Supported platform identifiers declared by the current project's `.heproj` file.
        /// </summary>
        IReadOnlyList<string> ProjectSupportedPlatforms;
        /// <summary>
        /// Importer registrations supplied by the editor host.
        /// </summary>
        readonly IReadOnlyList<IAssetImporterRegistration> Importers;
        /// <summary>
        /// Service used to persist editor-local active-platform state for the current project.
        /// </summary>
        EditorProjectLocalSettingsService ProjectLocalSettingsService;
        /// <summary>
        /// Font used for UI elements and title bars.
        /// </summary>
        FontAsset uiFont;
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
        /// Resolves the active preview source for the current selection snapshot.
        /// </summary>
        readonly PreviewSourceResolver previewSourceResolver;
        /// <summary>
        /// Modal used to pick an asset for editor fields.
        /// </summary>
        AssetPickerModal assetPickerModal;
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
        /// Service that loads and saves per-platform material settings sidecars.
        /// </summary>
        readonly MaterialAssetSettingsService materialAssetSettingsService;
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
        /// Refreshes live scene mesh components after file-system model assets are reprocessed.
        /// </summary>
        readonly EditorSceneModelRefreshService SceneModelRefreshService;
        /// <summary>
        /// Modal dialog used to choose scene save destinations.
        /// </summary>
        SaveFileDialog saveFileDialog;
        /// <summary>
        /// Modal dialog used to choose scene files to open.
        /// </summary>
        OpenFileDialog openFileDialog;
        /// <summary>
        /// Modal dialog used to choose a new parent for one scene entity.
        /// </summary>
        ReparentEntityDialog reparentEntityDialog;
        /// <summary>
        /// Modal dialog used to change the project's supported build platforms.
        /// </summary>
        BuildSettingsDialog buildSettingsDialog;
        /// <summary>
        /// Modal dialog used to edit per-platform build and graphics profiles.
        /// </summary>
        ProfilesDialog profilesDialog;
        /// <summary>
        /// Modal dialog used to configure local build map selections and the queued build list.
        /// </summary>
        BuildDialog buildDialog;
        /// <summary>
        /// Modal dialog used to choose a source platform before copying build settings.
        /// </summary>
        BuildDialogCopySettingsDialog buildDialogCopySettingsDialog;
        /// <summary>
        /// Service that generates and opens the game solution for scripting.
        /// </summary>
        readonly EditorGameSolutionService gameSolutionService;
        /// <summary>
        /// Service that persists editor-local platform profile settings.
        /// </summary>
        readonly EditorProfileSettingsService profileSettingsService;
        /// <summary>
        /// Service that builds and hot-reloads the generated game scripting assembly.
        /// </summary>
        readonly EditorGameScriptHotReloadService scriptHotReloadService;
        /// <summary>
        /// Modal dialog used to confirm whether pending scene transitions should save dirty changes.
        /// </summary>
        UnsavedChangesDialog unsavedChangesDialog;
        /// <summary>
        /// Modal dialog used to edit editor-global preferences such as UI scale.
        /// </summary>
        EditorPreferencesDialog preferencesDialog;
        /// <summary>
        /// Raised when the editor host should close after a pending dirty-state prompt completes.
        /// </summary>
        public event Action CloseRequested;
        /// <summary>
        /// Raised when the user confirms one new editor UI scale selection.
        /// </summary>
        public event Action<EditorUiScaleSettings> UiScaleSettingsChanged;
        /// <summary>
        /// Resolves the available platform list that can be selected in Build Settings.
        /// </summary>
        readonly AvailablePlatformProviderResolver availablePlatformProviderResolver;
        /// <summary>
        /// Loads dynamic platform builders and their metadata.
        /// </summary>
        readonly EditorPlatformCatalogService platformCatalogService;
        /// <summary>
        /// Persists local build dialog state in `user_settings/build_config.json`.
        /// </summary>
        readonly EditorBuildConfigService buildConfigService;
        /// <summary>
        /// Executes and persists queued local build items for the current project.
        /// </summary>
        readonly EditorBuildQueueService buildQueueService;
        /// <summary>
        /// Enumerates project scene ids used by the build dialog map checklist.
        /// </summary>
        readonly EditorProjectSceneCatalogService sceneCatalogService;
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
        /// Currently selected asset browser entry used for preview resolution.
        /// </summary>
        AssetBrowserEntry SelectedAssetEntry;
        /// <summary>
        /// Currently selected scene entity used for preview resolution.
        /// </summary>
        Entity SelectedSceneEntity;
        /// <summary>
        /// Active platform identifier currently selected for editor-local asset processing workflows.
        /// </summary>
        string ActiveProjectPlatform;
        /// <summary>
        /// Host-provided folder picker used by build-planning dialogs.
        /// </summary>
        Func<string> BrowseOutputFolderResolver;
        /// <summary>
        /// Current editor-global UI scale settings reflected by the preferences dialog.
        /// </summary>
        EditorUiScaleSettings CurrentUiScaleSettings;
        /// <summary>
        /// Current scaled editor UI metrics applied to scale-aware editor chrome.
        /// </summary>
        EditorUiMetrics CurrentUiMetrics;
        /// <summary>
        /// Most recent host layout width used to relayout the session after live UI scale changes.
        /// </summary>
        int LastLayoutWidth;
        /// <summary>
        /// Most recent host layout height used to relayout the session after live UI scale changes.
        /// </summary>
        int LastLayoutHeight;

        /// <summary>
        /// Initializes a new editor session and sets up cameras, docking, and starter content.
        /// </summary>
        /// <param name="core">Editor core instance that owns shared state.</param>
        /// <param name="projectPath">Path to the project root or project file being edited.</param>
        /// <param name="initialUiScaleSettings">Validated global editor UI scale settings resolved by the host before session startup.</param>
        /// <param name="initialUiMetrics">Scaled editor UI metrics resolved by the host before session startup.</param>
        /// <param name="uiFont">Font used for editor UI text.</param>
        /// <param name="snapModifierFont">Font used for the viewport snap modifier labels.</param>
        /// <param name="render3D">3D renderer instance.</param>
        /// <param name="render2D">2D renderer instance.</param>
        /// <param name="input">Platform-specific input backend instance.</param>
        /// <param name="renderWidth">Initial render width in pixels.</param>
        /// <param name="renderHeight">Initial render height in pixels.</param>
        /// <param name="toolbarIcons">Toolbar icon textures used by the main viewport tool buttons.</param>
        /// <param name="titleBarIcon">Runtime texture rendered in the editor title bar's left icon slot.</param>
        /// <param name="importers">Asset importers to register for import settings.</param>
        /// <param name="browseOutputFolderResolver">Host callback that opens a folder picker for build output selection.</param>
        public EditorSession(
            EditorCore core,
            string projectPath,
            EditorUiScaleSettings initialUiScaleSettings,
            EditorUiMetrics initialUiMetrics,
            FontAsset uiFont,
            FontAsset snapModifierFont,
            RenderManager3D render3D,
            RenderManager2D render2D,
            IInputBackend input,
            int renderWidth,
            int renderHeight,
            EditorViewportToolbarIconSet toolbarIcons,
            RuntimeTexture titleBarIcon,
            IReadOnlyList<IAssetImporterRegistration> importers,
            Func<string> browseOutputFolderResolver) {
            this.core = core ?? throw new ArgumentNullException(nameof(core));
            BrowseOutputFolderResolver = browseOutputFolderResolver ?? throw new ArgumentNullException(nameof(browseOutputFolderResolver));
            CanonicalProjectFilePath = ResolveCanonicalProjectFilePath(projectPath);
            this.projectPath = ResolveProjectRootPathFromCanonicalProjectFile(CanonicalProjectFilePath);
            ProjectDisplayName = ResolveProjectDisplayNameFromCanonicalProjectFile(CanonicalProjectFilePath);
            CurrentUiScaleSettings = initialUiScaleSettings ?? throw new ArgumentNullException(nameof(initialUiScaleSettings));
            CurrentUiMetrics = initialUiMetrics ?? throw new ArgumentNullException(nameof(initialUiMetrics));
            ProjectFileDocument projectDocument = LoadProjectDocument(CanonicalProjectFilePath);
            RequiredEngineVersion = ResolveRequiredEngineVersion(projectDocument);
            ProjectName = ResolveProjectName(projectDocument);
            ProjectVersion = ResolveProjectVersion(projectDocument);
            ProjectSupportedPlatforms = LoadProjectSupportedPlatforms(projectDocument);
            ProjectLocalSettingsService = new EditorProjectLocalSettingsService(this.projectPath, ProjectSupportedPlatforms);
            ActiveProjectPlatform = ProjectLocalSettingsService.LoadActivePlatform();
            availablePlatformProviderResolver = CreateAvailablePlatformProviderResolver();
            platformCatalogService = CreatePlatformCatalogService();
            EditorContentManager = this.core.GetContentManager();
            this.uiFont = uiFont ?? throw new ArgumentNullException(nameof(uiFont));
            snapModifierFont = snapModifierFont ?? throw new ArgumentNullException(nameof(snapModifierFont));
            toolbarIcons = toolbarIcons ?? throw new ArgumentNullException(nameof(toolbarIcons));
            Importers = importers ?? throw new ArgumentNullException(nameof(importers));
            this.core.DefaultFontAsset = this.uiFont;

            EditorKeyboardFocusService.Reset();
            core.Initialize(render3D, render2D, input);
            EditorComponentAddCatalog.Initialize();
            core.Input.SetKeyboardActive(true);

            EditorProjectPaths.Initialize(this.projectPath);

            assetImportManager = InitializeAssetImports(Importers);
            materialAssetSettingsService = new MaterialAssetSettingsService();
            GeneratedAssetProviderRegistry.Register(new EngineGeneratedAssetProvider());
            previewSourceResolver = new PreviewSourceResolver(assetImportManager, render2D, render3D);

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
            sceneCameraComponent.LayerMask = EditorLayerMasks.SceneObjects | EditorLayerMasks.SceneGrid | EditorLayerMasks.SceneCameraVisuals;
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
            hiddenCameraComponent.LayerMask = EditorLayerMasks.SceneObjects | EditorLayerMasks.SceneCameraVisuals;
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

            titleBar = new EditorTitleBar(uiFont, CurrentUiMetrics, Math.Max(1, renderWidth), Math.Max(1, renderHeight), BuildWindowTitle(), titleBarIcon);

            dockingManager = new DockingManager();
            EditorFileSystemModelResolver fileSystemModelResolver = new EditorFileSystemModelResolver(assetImportManager);
            sceneHierarchyPanel = new SceneHierarchyPanel(uiFont, CurrentUiMetrics);
            assetBrowserPanel = new AssetBrowserPanel(uiFont, this.projectPath);
            mainViewport = new EditorViewport(sceneCameraComponent, uiFont, snapModifierFont, toolbarIcons);
            propertiesPanel = new PropertiesPanel(uiFont, EditorContentManager, fileSystemModelResolver, titleBar.Entity, scriptHotReloadService);
            loggerPanel = new LoggerPanel(uiFont, CurrentUiMetrics);
            previewPanel = new PreviewPanel(uiFont, CurrentUiMetrics);
            EditorKeyboardFocusService.RegisterGroup(sceneHierarchyPanel);
            EditorKeyboardFocusService.RegisterGroup(assetBrowserPanel);
            EditorKeyboardFocusService.RegisterGroup(mainViewport);
            EditorKeyboardFocusService.RegisterGroup(propertiesPanel);
            EditorKeyboardFocusService.RegisterGroup(loggerPanel);
            EditorKeyboardFocusService.RegisterGroup(previewPanel);
            assetPickerModal = new AssetPickerModal(uiFont, CurrentUiMetrics, this.projectPath);
            ComponentPersistenceRegistry persistenceRegistry = new ComponentPersistenceRegistry();
            persistenceRegistry.Register(new MeshComponentPersistenceDescriptor());
            persistenceRegistry.Register(new CameraComponentPersistenceDescriptor());
            persistenceRegistry.Register(new TextComponentPersistenceDescriptor());
            persistenceRegistry.Register(new RoundedRectComponentPersistenceDescriptor());
            persistenceRegistry.Register(new FPSComponentPersistenceDescriptor());
            persistenceRegistry.Register(new DirectionalLightComponentPersistenceDescriptor());
            persistenceRegistry.Register(new PointLightComponentPersistenceDescriptor());
            persistenceRegistry.Register(new SpotLightComponentPersistenceDescriptor());
            persistenceRegistry.Register(new DemoMenuBuildComponentPersistenceDescriptor());
            persistenceRegistry.Register(new DemoMenuPanelComponentPersistenceDescriptor());
            persistenceRegistry.Register(new DemoMenuItemComponentPersistenceDescriptor());
            persistenceRegistry.Register(new DemoMenuSelectedDescriptionComponentPersistenceDescriptor());
            SceneSavePathResolver = new SceneSavePathResolver(this.projectPath);
            SceneSaveService = new SceneSaveService(this.projectPath, persistenceRegistry);
            SceneCreationService = new EditorSceneCreationService();
            ReparentService = new EditorEntityReparentService();
            SceneModelRefreshService = new EditorSceneModelRefreshService(fileSystemModelResolver);
            buildConfigService = new EditorBuildConfigService(this.projectPath);
            profileSettingsService = new EditorProfileSettingsService(this.projectPath);
            buildQueueService = new EditorBuildQueueService(
                buildConfigService,
                CreateBuildExecutorRouter());
            sceneCatalogService = new EditorProjectSceneCatalogService(this.projectPath);
            saveFileDialog = new SaveFileDialog(uiFont, CurrentUiMetrics, this.projectPath);
            openFileDialog = new OpenFileDialog(uiFont, CurrentUiMetrics, this.projectPath);
            reparentEntityDialog = new ReparentEntityDialog(uiFont, CurrentUiMetrics);
            buildSettingsDialog = new BuildSettingsDialog(uiFont, CurrentUiMetrics);
            profilesDialog = new ProfilesDialog(uiFont, CurrentUiMetrics);
            buildDialog = new BuildDialog(uiFont, CurrentUiMetrics);
            buildDialogCopySettingsDialog = new BuildDialogCopySettingsDialog(uiFont, CurrentUiMetrics);
            gameSolutionService = new EditorGameSolutionService(this.projectPath, ProjectName, new EditorVisualStudioLauncher());
            scriptHotReloadService = new EditorGameScriptHotReloadService(
                gameSolutionService,
                new EditorDotNetScriptBuildTool(),
                new EditorGameScriptAssemblyHost(this.projectPath));
            unsavedChangesDialog = new UnsavedChangesDialog(uiFont, CurrentUiMetrics);
            preferencesDialog = new EditorPreferencesDialog(uiFont, CurrentUiMetrics);
            SceneFileLoadService = new SceneFileLoadService(
                this.projectPath,
                persistenceRegistry,
                new EditorSceneAssetReferenceResolver(EditorContentManager, this.projectPath, fileSystemModelResolver));
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
            titleBar.PreferencesRequested += HandlePreferencesRequested;
            titleBar.BuildRequested += HandleBuildRequested;
            titleBar.BuildSettingsRequested += HandleBuildSettingsRequested;
            titleBar.ProfilesRequested += HandleProfilesRequested;
            titleBar.BuildScriptsRequested += HandleBuildScriptsRequested;
            titleBar.OpenInIDERequested += HandleOpenInIDERequested;
            titleBar.AddEmptyRequested += HandleAddEmptyRequested;
            titleBar.AddCubeRequested += HandleAddCubeRequested;
            titleBar.AddPlaneRequested += HandleAddPlaneRequested;
            titleBar.AddCameraRequested += HandleAddCameraRequested;
            titleBar.AddSpotLightRequested += HandleAddSpotLightRequested;
            titleBar.AddPointLightRequested += HandleAddPointLightRequested;
            titleBar.AddDirectionalLightRequested += HandleAddDirectionalLightRequested;
            AttachScaleSensitiveDialogHandlers();

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
            dockingManager.Layout.DockRelative(sceneHierarchyPanel, mainViewport, DockInsertDirection.Right, 0.7f);
            dockingManager.Layout.DockRelative(propertiesPanel, sceneHierarchyPanel, DockInsertDirection.Fill, 0.75f);
            dockingManager.Layout.DockRelative(loggerPanel, assetBrowserPanel, DockInsertDirection.Fill, 0.5f);
            dockingManager.Layout.DockRelative(previewPanel, assetBrowserPanel, DockInsertDirection.Right, 0.75f);

            ShaderCompileTarget runtimeTarget = ResolveRuntimeShaderTarget(render3D);
            shaderModuleManager = BuildShaderModuleManager(runtimeTarget);
            EditorShaderPackageService.Initialize(shaderModuleManager, runtimeTarget, EditorContentManager);
            shaderModuleManager.ShaderBuilt += HandleShaderBuilt;
            shaderModuleManager.Start();

            RuntimeMaterial transformGizmoMaterial = BuildTransformGizmoNormalMaterial();
            RuntimeMaterial transformGizmoHighlightMaterial = BuildTransformGizmoHighlightMaterial();
            RuntimeMaterial transformGizmoPlaneMaterial = TransformGizmoPlaneMaterialFactory.CreateNormal(render3D);
            RuntimeMaterial transformGizmoPlaneHighlightMaterial = TransformGizmoPlaneMaterialFactory.CreateHighlight(render3D);
            TransformTranslationGizmoFactory.Create(
                render3D,
                sceneCameraComponent,
                transformGizmoMaterial,
                transformGizmoHighlightMaterial,
                transformGizmoPlaneMaterial,
                transformGizmoPlaneHighlightMaterial);
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
        public int2 PointerPosition => core.Input.GetMousePosition();

        /// <summary>
        /// Gets the cursor requested by the interactable currently hovered inside the editor.
        /// </summary>
        public PointerCursorKind HoverCursor => core.PointerInteractionSystem.HoverCursor;

        /// <summary>
        /// Gets the supported platform identifiers declared by the current project's `.heproj` file.
        /// </summary>
        public IReadOnlyList<string> SupportedPlatforms => ProjectSupportedPlatforms;

        /// <summary>
        /// Gets the editor-local active platform currently selected for the open project.
        /// </summary>
        public string CurrentProjectPlatform => ActiveProjectPlatform;

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
        /// Persists one new active platform selection for the open project.
        /// </summary>
        /// <param name="platformId">Supported platform identifier to persist.</param>
        public void SetActiveProjectPlatform(string platformId) {
            if (!IsInstalledPlatform(platformId)) {
                throw new InvalidOperationException($"Platform '{platformId}' is not installed for the current engine.");
            }

            ProjectLocalSettingsService.SaveActivePlatform(platformId);
            ActiveProjectPlatform = platformId;
            assetImportManager.CurrentPlatformId = platformId;
        }

        /// <summary>
        /// Updates layout for title bar, cameras, and dock panels.
        /// </summary>
        /// <param name="renderWidth">Current render width.</param>
        /// <param name="renderHeight">Current render height.</param>
        public void UpdateLayout(int renderWidth, int renderHeight) {
            int width = Math.Max(1, renderWidth);
            int height = Math.Max(1, renderHeight);
            LastLayoutWidth = width;
            LastLayoutHeight = height;

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
            buildSettingsDialog.UpdateLayout(width, height);
            profilesDialog.UpdateLayout(width, height);
            buildDialog.UpdateLayout(width, height);
            buildDialogCopySettingsDialog.UpdateLayout(width, height);
            unsavedChangesDialog.UpdateLayout(width, height);
            preferencesDialog.UpdateLayout(width, height);
            propertiesPanel.UpdateModalLayout(width, height);
            mainViewport.RefreshInputBlockers();
            UpdateDockInputBlockers();
        }

        /// <summary>
        /// Reapplies the current editor-global UI scale to scale-aware chrome and recreates hidden modal dialogs using the updated fonts.
        /// </summary>
        /// <param name="settings">Validated editor-global UI scale settings to preserve in the live session.</param>
        /// <param name="metrics">Updated scaled editor UI metrics resolved by the host.</param>
        /// <param name="uiFont">Updated UI font created by the host for the resolved metrics.</param>
        /// <param name="snapModifierFont">Updated snap-modifier font created by the host for the resolved metrics.</param>
        public void ApplyUiScale(EditorUiScaleSettings settings, EditorUiMetrics metrics, FontAsset uiFont, FontAsset snapModifierFont) {
            if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            }
            if (metrics == null) {
                throw new ArgumentNullException(nameof(metrics));
            }
            if (uiFont == null) {
                throw new ArgumentNullException(nameof(uiFont));
            }
            if (snapModifierFont == null) {
                throw new ArgumentNullException(nameof(snapModifierFont));
            }

            CurrentUiScaleSettings = settings;
            CurrentUiMetrics = metrics;
            this.uiFont = uiFont;

            if (core != null) {
                core.DefaultFontAsset = uiFont;
            }
            if (titleBar != null) {
                titleBar.ApplyUiMetrics(uiFont, metrics);
            }
            if (sceneHierarchyPanel != null) {
                sceneHierarchyPanel.ApplyUiMetrics(uiFont, metrics);
            }
            if (loggerPanel != null) {
                loggerPanel.ApplyUiMetrics(uiFont, metrics);
            }
            if (previewPanel != null) {
                previewPanel.ApplyUiMetrics(uiFont, metrics);
            }

            RecreateScaleSensitiveDialogs();

            if (LastLayoutWidth > 0 && LastLayoutHeight > 0) {
                UpdateLayout(LastLayoutWidth, LastLayoutHeight);
            }
        }

        /// <summary>
        /// Attaches session event handlers to the scale-sensitive modal dialogs currently owned by the session.
        /// </summary>
        void AttachScaleSensitiveDialogHandlers() {
            if (saveFileDialog != null) {
                saveFileDialog.SaveRequested += HandleSceneSaveRequested;
            }
            if (openFileDialog != null) {
                openFileDialog.OpenRequested += HandleSceneOpenRequested;
            }
            if (reparentEntityDialog != null) {
                reparentEntityDialog.ConfirmRequested += HandleReparentEntityDialogConfirmed;
                reparentEntityDialog.CancelRequested += HandleReparentEntityDialogCancelRequested;
            }
            if (buildSettingsDialog != null) {
                buildSettingsDialog.ConfirmRequested += HandleBuildSettingsDialogConfirmed;
                buildSettingsDialog.CancelRequested += HandleBuildSettingsDialogCancelRequested;
            }
            if (profilesDialog != null) {
                profilesDialog.ConfirmRequested += HandleProfilesDialogConfirmed;
                profilesDialog.CancelRequested += HandleProfilesDialogCancelRequested;
            }
            if (buildDialog != null) {
                buildDialog.AddRequested += HandleBuildDialogAddRequested;
                buildDialog.CopySettingsRequested += HandleBuildDialogCopySettingsRequested;
                buildDialog.BrowseOutputFolderRequested += HandleBuildDialogBrowseOutputFolderRequested;
                buildDialog.BuildQueueRequested += HandleBuildDialogBuildQueueRequested;
                buildDialog.RemoveQueueItemRequested += HandleBuildDialogRemoveQueueItemRequested;
                buildDialog.CancelRequested += HandleBuildDialogCancelRequested;
            }
            if (buildDialogCopySettingsDialog != null) {
                buildDialogCopySettingsDialog.ConfirmRequested += HandleBuildDialogCopySettingsConfirmed;
                buildDialogCopySettingsDialog.CancelRequested += HandleBuildDialogCopySettingsCanceled;
            }
            if (unsavedChangesDialog != null) {
                unsavedChangesDialog.SaveRequested += HandleUnsavedChangesSaveRequested;
                unsavedChangesDialog.DontSaveRequested += HandleUnsavedChangesDontSaveRequested;
                unsavedChangesDialog.CancelRequested += HandleUnsavedChangesCancelRequested;
            }
            if (preferencesDialog != null) {
                preferencesDialog.ConfirmRequested += HandlePreferencesDialogConfirmed;
                preferencesDialog.CancelRequested += HandlePreferencesDialogCanceled;
            }
        }

        /// <summary>
        /// Detaches session event handlers from the current scale-sensitive modal dialogs.
        /// </summary>
        void DetachScaleSensitiveDialogHandlers() {
            if (saveFileDialog != null) {
                saveFileDialog.SaveRequested -= HandleSceneSaveRequested;
            }
            if (openFileDialog != null) {
                openFileDialog.OpenRequested -= HandleSceneOpenRequested;
            }
            if (reparentEntityDialog != null) {
                reparentEntityDialog.ConfirmRequested -= HandleReparentEntityDialogConfirmed;
                reparentEntityDialog.CancelRequested -= HandleReparentEntityDialogCancelRequested;
            }
            if (buildSettingsDialog != null) {
                buildSettingsDialog.ConfirmRequested -= HandleBuildSettingsDialogConfirmed;
                buildSettingsDialog.CancelRequested -= HandleBuildSettingsDialogCancelRequested;
            }
            if (profilesDialog != null) {
                profilesDialog.ConfirmRequested -= HandleProfilesDialogConfirmed;
                profilesDialog.CancelRequested -= HandleProfilesDialogCancelRequested;
            }
            if (buildDialog != null) {
                buildDialog.AddRequested -= HandleBuildDialogAddRequested;
                buildDialog.CopySettingsRequested -= HandleBuildDialogCopySettingsRequested;
                buildDialog.BrowseOutputFolderRequested -= HandleBuildDialogBrowseOutputFolderRequested;
                buildDialog.BuildQueueRequested -= HandleBuildDialogBuildQueueRequested;
                buildDialog.RemoveQueueItemRequested -= HandleBuildDialogRemoveQueueItemRequested;
                buildDialog.CancelRequested -= HandleBuildDialogCancelRequested;
            }
            if (buildDialogCopySettingsDialog != null) {
                buildDialogCopySettingsDialog.ConfirmRequested -= HandleBuildDialogCopySettingsConfirmed;
                buildDialogCopySettingsDialog.CancelRequested -= HandleBuildDialogCopySettingsCanceled;
            }
            if (unsavedChangesDialog != null) {
                unsavedChangesDialog.SaveRequested -= HandleUnsavedChangesSaveRequested;
                unsavedChangesDialog.DontSaveRequested -= HandleUnsavedChangesDontSaveRequested;
                unsavedChangesDialog.CancelRequested -= HandleUnsavedChangesCancelRequested;
            }
            if (preferencesDialog != null) {
                preferencesDialog.ConfirmRequested -= HandlePreferencesDialogConfirmed;
                preferencesDialog.CancelRequested -= HandlePreferencesDialogCanceled;
            }
        }

        /// <summary>
        /// Hides currently owned scale-sensitive modal dialogs before disposal or recreation.
        /// </summary>
        void HideScaleSensitiveDialogs() {
            if (assetPickerModal != null) {
                assetPickerModal.Hide();
            }
            if (saveFileDialog != null) {
                saveFileDialog.Hide();
            }
            if (openFileDialog != null) {
                openFileDialog.Hide();
            }
            if (reparentEntityDialog != null) {
                reparentEntityDialog.Hide();
            }
            if (buildSettingsDialog != null) {
                buildSettingsDialog.Hide();
            }
            if (profilesDialog != null) {
                profilesDialog.Hide();
            }
            if (buildDialog != null) {
                buildDialog.Hide();
            }
            if (buildDialogCopySettingsDialog != null) {
                buildDialogCopySettingsDialog.Hide();
            }
            if (unsavedChangesDialog != null) {
                unsavedChangesDialog.Hide();
            }
            if (preferencesDialog != null) {
                preferencesDialog.Hide();
            }
        }

        /// <summary>
        /// Recreates scale-sensitive modal dialogs using the current fonts and metrics so newly opened dialogs match the live session scale.
        /// </summary>
        void RecreateScaleSensitiveDialogs() {
            HideScaleSensitiveDialogs();
            DetachScaleSensitiveDialogHandlers();
            DisposeScaleSensitiveDialogs();

            if (uiFont == null || CurrentUiMetrics == null) {
                return;
            }

            if (!string.IsNullOrWhiteSpace(projectPath)) {
                assetPickerModal = new AssetPickerModal(uiFont, CurrentUiMetrics, projectPath);
                saveFileDialog = new SaveFileDialog(uiFont, CurrentUiMetrics, projectPath);
                openFileDialog = new OpenFileDialog(uiFont, CurrentUiMetrics, projectPath);
            } else {
                assetPickerModal = null;
                saveFileDialog = null;
                openFileDialog = null;
            }
            reparentEntityDialog = new ReparentEntityDialog(uiFont, CurrentUiMetrics);
            buildSettingsDialog = new BuildSettingsDialog(uiFont, CurrentUiMetrics);
            profilesDialog = new ProfilesDialog(uiFont, CurrentUiMetrics);
            buildDialog = new BuildDialog(uiFont, CurrentUiMetrics);
            buildDialogCopySettingsDialog = new BuildDialogCopySettingsDialog(uiFont, CurrentUiMetrics);
            unsavedChangesDialog = new UnsavedChangesDialog(uiFont, CurrentUiMetrics);
            preferencesDialog = new EditorPreferencesDialog(uiFont, CurrentUiMetrics);
            AttachScaleSensitiveDialogHandlers();
        }

        /// <summary>
        /// Disposes the current scale-sensitive modal dialogs so stale entity trees are removed before recreation.
        /// </summary>
        void DisposeScaleSensitiveDialogs() {
            if (assetPickerModal != null) {
                assetPickerModal.Dispose();
            }
            if (saveFileDialog != null) {
                saveFileDialog.Dispose();
            }
            if (openFileDialog != null) {
                openFileDialog.Dispose();
            }
            if (reparentEntityDialog != null) {
                reparentEntityDialog.Dispose();
            }
            if (buildSettingsDialog != null) {
                buildSettingsDialog.Dispose();
            }
            if (profilesDialog != null) {
                profilesDialog.Dispose();
            }
            if (buildDialog != null) {
                buildDialog.Dispose();
            }
            if (buildDialogCopySettingsDialog != null) {
                buildDialogCopySettingsDialog.Dispose();
            }
            if (unsavedChangesDialog != null) {
                unsavedChangesDialog.Dispose();
            }
            if (preferencesDialog != null) {
                preferencesDialog.Dispose();
            }
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

            int2 pointer = core.Input.GetMousePosition();
            return dockingManager.Update(pointer, core.Input.GetMouseLeftButtonState(), hostSize, origin);
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
            core.Input.SetKeyboardActive(isActive);
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
            titleBar.PreferencesRequested -= HandlePreferencesRequested;
            titleBar.BuildRequested -= HandleBuildRequested;
            titleBar.BuildSettingsRequested -= HandleBuildSettingsRequested;
            titleBar.ProfilesRequested -= HandleProfilesRequested;
            titleBar.BuildScriptsRequested -= HandleBuildScriptsRequested;
            titleBar.OpenInIDERequested -= HandleOpenInIDERequested;
            titleBar.AddEmptyRequested -= HandleAddEmptyRequested;
            titleBar.AddCubeRequested -= HandleAddCubeRequested;
            titleBar.AddPlaneRequested -= HandleAddPlaneRequested;
            titleBar.AddCameraRequested -= HandleAddCameraRequested;
            titleBar.AddSpotLightRequested -= HandleAddSpotLightRequested;
            titleBar.AddPointLightRequested -= HandleAddPointLightRequested;
            titleBar.AddDirectionalLightRequested -= HandleAddDirectionalLightRequested;
            DetachScaleSensitiveDialogHandlers();
            scriptHotReloadService.Dispose();
            mainViewport.ClearInputBlockers();
            EditorViewportToolService.ClearToolMode(sceneCameraComponent);
            HideScaleSensitiveDialogs();
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
            if (reparentEntityDialog != null) {
                reparentEntityDialog.Hide();
            }

            if (!IsSceneDirty) {
                ContinuePendingSceneTransition();
                return;
            }

            if (unsavedChangesDialog != null) {
                unsavedChangesDialog.Show();
            }
        }

        /// <summary>
        /// Requests that the editor host close, prompting for unsaved changes first when needed.
        /// </summary>
        /// <returns>True when the close request was deferred behind the unsaved-changes dialog.</returns>
        public bool RequestClose() {
            if (!IsSceneDirty) {
                PendingSceneTransition = SceneTransitionKind.None;
                PendingOpenScenePath = string.Empty;
                if (reparentEntityDialog != null) {
                    reparentEntityDialog.Hide();
                }
                if (unsavedChangesDialog != null) {
                    unsavedChangesDialog.Hide();
                }
                return false;
            }

            PendingSceneTransition = SceneTransitionKind.Exit;
            PendingOpenScenePath = string.Empty;
            if (reparentEntityDialog != null) {
                reparentEntityDialog.Hide();
            }
            if (unsavedChangesDialog != null) {
                unsavedChangesDialog.Show();
            }
            return true;
        }

        /// <summary>
        /// Continues the transition currently stored in pending scene state.
        /// </summary>
        void ContinuePendingSceneTransition() {
            SceneTransitionKind pendingTransition = PendingSceneTransition;
            string pendingOpenPath = PendingOpenScenePath;

            PendingSceneTransition = SceneTransitionKind.None;
            PendingOpenScenePath = string.Empty;
            if (unsavedChangesDialog != null) {
                unsavedChangesDialog.Hide();
            }

            if (pendingTransition == SceneTransitionKind.NewMap) {
                ResetToNewScene();
                return;
            } else if (pendingTransition == SceneTransitionKind.OpenMap) {
                if (string.IsNullOrWhiteSpace(pendingOpenPath)) {
                    openFileDialog.Show(SceneSavePathResolver.DefaultSceneDirectory);
                    return;
                }

                LoadSceneIntoSession(pendingOpenPath);
            } else if (pendingTransition == SceneTransitionKind.Exit) {
                if (CloseRequested != null) {
                    CloseRequested();
                }
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
        /// Handles the Add Camera command from the editor title bar.
        /// </summary>
        void HandleAddCameraRequested() {
            CreateAndSelectSceneEntity(SceneCreationService.CreateCamera);
        }

        /// <summary>
        /// Handles the Add Spot Light command from the editor title bar.
        /// </summary>
        void HandleAddSpotLightRequested() {
            CreateAndSelectSceneEntity(SceneCreationService.CreateSpotLight);
        }

        /// <summary>
        /// Handles the Add Point Light command from the editor title bar.
        /// </summary>
        void HandleAddPointLightRequested() {
            CreateAndSelectSceneEntity(SceneCreationService.CreatePointLight);
        }

        /// <summary>
        /// Handles the Add Directional Light command from the editor title bar.
        /// </summary>
        void HandleAddDirectionalLightRequested() {
            CreateAndSelectSceneEntity(SceneCreationService.CreateDirectionalLight);
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
        /// Opens the editor preferences dialog using the current UI scale settings.
        /// </summary>
        void HandlePreferencesRequested() {
            preferencesDialog.Show(CurrentUiScaleSettings);
        }

        /// <summary>
        /// Applies one confirmed editor UI scale selection and notifies the host.
        /// </summary>
        /// <param name="settings">Confirmed editor UI scale settings.</param>
        void HandlePreferencesDialogConfirmed(EditorUiScaleSettings settings) {
            if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            }

            CurrentUiScaleSettings = settings;
            preferencesDialog.Hide();
            if (UiScaleSettingsChanged != null) {
                UiScaleSettingsChanged(settings);
            }
        }

        /// <summary>
        /// Cancels the preferences workflow and hides the dialog.
        /// </summary>
        void HandlePreferencesDialogCanceled() {
            preferencesDialog.Hide();
        }

        /// <summary>
        /// Opens Build Settings using the currently available platforms for the active engine version.
        /// </summary>
        void HandleBuildSettingsRequested() {
            IReadOnlyList<AvailablePlatformDescriptor> availablePlatforms = availablePlatformProviderResolver.LoadPlatforms(RequiredEngineVersion);
            if (buildDialogCopySettingsDialog != null) {
                buildDialogCopySettingsDialog.Hide();
            }
            if (buildSettingsDialog != null) {
                buildSettingsDialog.Show(availablePlatforms, SupportedPlatforms);
            }
        }

        /// <summary>
        /// Applies one confirmed Build Settings selection to the canonical project file and local active-platform state.
        /// </summary>
        /// <param name="selection">Supported-platform selection confirmed by the dialog.</param>
        void HandleBuildSettingsDialogConfirmed(BuildSettingsSelection selection) {
            if (selection == null) {
                throw new ArgumentNullException(nameof(selection));
            }

            SaveProjectSupportedPlatforms(selection.SelectedPlatformIds);
            ApplySupportedPlatforms(selection.SelectedPlatformIds);
            buildSettingsDialog.Hide();
        }

        /// <summary>
        /// Cancels the Build Settings workflow and hides the dialog.
        /// </summary>
        void HandleBuildSettingsDialogCancelRequested() {
            buildSettingsDialog.Hide();
        }

        /// <summary>
        /// Opens the profiles dialog using the current active platform and persisted profile settings.
        /// </summary>
        void HandleProfilesRequested() {
            EditorProfileSettingsDocument profileSettings = profileSettingsService.Load(SupportedPlatforms);
            buildDialogCopySettingsDialog.Hide();
            profilesDialog.Show(profileSettings, SupportedPlatforms, ActiveProjectPlatform, ResolvePlatformSelectionModel(ActiveProjectPlatform));
        }

        /// <summary>
        /// Persists the confirmed profiles selection and updates the active platform when needed.
        /// </summary>
        /// <param name="selection">Confirmed platform profile selection.</param>
        void HandleProfilesDialogConfirmed(ProfilesDialogSelection selection) {
            if (selection == null) {
                throw new ArgumentNullException(nameof(selection));
            }

            profileSettingsService.Save(selection.ProfileSettingsDocument);
            if (!string.Equals(ActiveProjectPlatform, selection.ActivePlatformId, StringComparison.OrdinalIgnoreCase) && IsInstalledPlatform(selection.ActivePlatformId)) {
                SetActiveProjectPlatform(selection.ActivePlatformId);
            }

            profilesDialog.Hide();
        }

        /// <summary>
        /// Cancels the profiles workflow and hides the dialog.
        /// </summary>
        void HandleProfilesDialogCancelRequested() {
            profilesDialog.Hide();
        }

        /// <summary>
        /// Opens the local Build dialog using the current scene to seed first-use map selections.
        /// </summary>
        void HandleBuildRequested() {
            IReadOnlyList<string> sceneIds = sceneCatalogService.GetSceneIds();
            string currentSceneId = sceneCatalogService.ResolveSceneId(CurrentScenePath);
            EditorBuildConfigDocument buildConfig = buildConfigService.Load(SupportedPlatforms, currentSceneId);
            buildDialogCopySettingsDialog.Hide();
            buildDialog.Show(SupportedPlatforms, sceneIds, ActiveProjectPlatform, buildConfig, ResolvePlatformSelectionModel(ActiveProjectPlatform));
        }

        /// <summary>
        /// Builds the generated scripting solution and reloads the resulting assembly.
        /// </summary>
        void HandleBuildScriptsRequested() {
            EditorBuildExecutionResult result = scriptHotReloadService.BuildAndReload();
            if (result.Succeeded) {
                Logger.WriteLine(result.Message);
                return;
            }

            Logger.WriteError(result.Message);
        }

        /// <summary>
        /// Generates the game solution and opens it in Visual Studio.
        /// </summary>
        void HandleOpenInIDERequested() {
            try {
                gameSolutionService.OpenSolutionInIde();
            } catch (Exception ex) {
                Logger.WriteError($"Open in IDE failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Resolves the local build configuration currently being edited, loading persisted state when the dialog has not been shown yet.
        /// </summary>
        /// <returns>Mutable local build configuration document used by build-queue workflows.</returns>
        EditorBuildConfigDocument ResolveCurrentBuildConfig() {
            if (buildDialog.BuildConfig != null) {
                return buildDialog.BuildConfig;
            }

            string currentSceneId = sceneCatalogService.ResolveSceneId(CurrentScenePath);
            return buildConfigService.Load(SupportedPlatforms, currentSceneId);
        }

        /// <summary>
        /// Resolves one platform selection model for the supplied platform id, if the platform exposes a builder assembly.
        /// </summary>
        /// <param name="platformId">Stable platform identifier.</param>
        /// <returns>Builder-provided selection model or null when unavailable.</returns>
        EditorPlatformBuildSelectionModel ResolvePlatformSelectionModel(string platformId) {
            try {
                return platformCatalogService.ResolveSelectionModel(platformId);
            } catch {
                return null;
            }
        }

        /// <summary>
        /// Persists one queued build item from the active Build dialog tab and refreshes the visible queue.
        /// </summary>
        /// <param name="request">Queued build request captured from the active tab.</param>
        void HandleBuildDialogAddRequested(BuildDialogAddRequest request) {
            if (request == null) {
                throw new ArgumentNullException(nameof(request));
            }

            if (string.IsNullOrWhiteSpace(request.OutputDirectoryPath)) {
                return;
            }

            EditorBuildConfigDocument buildConfig = ResolveCurrentBuildConfig();
            buildConfig.QueueItems.Add(new EditorBuildQueueItemDocument {
                QueueItemId = Guid.NewGuid().ToString("N"),
                PlatformId = request.PlatformId,
                SelectedSceneIds = new List<string>(request.SelectedSceneIds),
                OutputDirectoryPath = request.OutputDirectoryPath,
                DebugBuild = request.DebugBuild,
                SelectedBuildProfileId = request.SelectedBuildProfileId,
                SelectedGraphicsProfileId = request.SelectedGraphicsProfileId,
                SelectedBuildOptionValues = new Dictionary<string, string>(request.SelectedBuildOptionValues),
                SelectedGraphicsOptionValues = new Dictionary<string, string>(request.SelectedGraphicsOptionValues),
                SelectedCodegenProfileId = request.SelectedCodegenProfileId,
                SelectedStorageProfileId = request.SelectedStorageProfileId,
                SelectedMediaProfileId = request.SelectedMediaProfileId,
                SelectedCodegenOptionValues = new Dictionary<string, string>(request.SelectedCodegenOptionValues),
                SelectedCodeModuleIds = new List<string>(request.SelectedCodeModuleIds),
                Status = EditorBuildQueueItemStatus.Pending,
                StatusMessage = string.Empty
            });
            buildConfigService.Save(buildConfig);
            buildDialogCopySettingsDialog.Hide();
            buildDialog.Show(SupportedPlatforms, sceneCatalogService.GetSceneIds(), request.PlatformId, buildConfig, ResolvePlatformSelectionModel(request.PlatformId));
        }

        /// <summary>
        /// Removes one queued build item from persisted local build state and refreshes the visible queue.
        /// </summary>
        /// <param name="queueItemId">Persisted queued build item id to remove.</param>
        void HandleBuildDialogRemoveQueueItemRequested(string queueItemId) {
            if (string.IsNullOrWhiteSpace(queueItemId)) {
                throw new ArgumentException("Queue item id is required.", nameof(queueItemId));
            }

            EditorBuildConfigDocument buildConfig = ResolveCurrentBuildConfig();
            for (int index = 0; index < buildConfig.QueueItems.Count; index++) {
                if (buildConfig.QueueItems[index].QueueItemId == queueItemId) {
                    buildConfig.QueueItems.RemoveAt(index);
                    break;
                }
            }

            buildConfigService.Save(buildConfig);
            buildDialogCopySettingsDialog.Hide();
            buildDialog.Show(SupportedPlatforms, sceneCatalogService.GetSceneIds(), ActiveProjectPlatform, buildConfig, ResolvePlatformSelectionModel(ActiveProjectPlatform));
        }

        /// <summary>
        /// Opens the host folder picker for the Build dialog and writes the chosen output path back into the active platform row.
        /// </summary>
        void HandleBuildDialogBrowseOutputFolderRequested() {
            if (BrowseOutputFolderResolver == null) {
                throw new InvalidOperationException("Missing host output-folder picker resolver.");
            }

            string selectedOutputFolder = BrowseOutputFolderResolver();
            if (string.IsNullOrWhiteSpace(selectedOutputFolder)) {
                return;
            }

            buildDialog.SetOutputDirectoryPath(selectedOutputFolder);
        }

        /// <summary>
        /// Executes all pending queued builds sequentially and refreshes the dialog with persisted results.
        /// </summary>
        void HandleBuildDialogBuildQueueRequested() {
            EditorBuildConfigDocument buildConfig = ResolveCurrentBuildConfig();
            buildConfigService.Save(buildConfig);
            buildQueueService.RunPending(buildConfig, SupportedPlatforms);
            buildDialogCopySettingsDialog.Hide();
            buildDialog.Show(SupportedPlatforms, sceneCatalogService.GetSceneIds(), ActiveProjectPlatform, buildConfig, ResolvePlatformSelectionModel(ActiveProjectPlatform));
        }

        /// <summary>
        /// Cancels the Build dialog without changing project-shared platform state.
        /// </summary>
        void HandleBuildDialogCancelRequested() {
            buildDialogCopySettingsDialog.Hide();
            buildDialog.Hide();
        }

        /// <summary>
        /// Opens the compact chooser used to copy settings from another platform into the active build tab.
        /// </summary>
        void HandleBuildDialogCopySettingsRequested() {
            List<string> copySourcePlatformIds = new List<string>(SupportedPlatforms.Count);
            for (int index = 0; index < SupportedPlatforms.Count; index++) {
                string platformId = SupportedPlatforms[index];
                if (!string.Equals(platformId, ActiveProjectPlatform, StringComparison.OrdinalIgnoreCase)) {
                    copySourcePlatformIds.Add(platformId);
                }
            }

            buildDialogCopySettingsDialog.Show(copySourcePlatformIds);
        }

        /// <summary>
        /// Applies one confirmed source platform to the active build tab and hides the chooser modal.
        /// </summary>
        /// <param name="sourcePlatformId">Source platform id chosen by the chooser dialog.</param>
        void HandleBuildDialogCopySettingsConfirmed(string sourcePlatformId) {
            if (string.IsNullOrWhiteSpace(sourcePlatformId)) {
                throw new ArgumentException("Source platform id is required.", nameof(sourcePlatformId));
            }

            buildDialog.CopyMapListFrom(sourcePlatformId);
            buildDialogCopySettingsDialog.Hide();
        }

        /// <summary>
        /// Cancels the build-copy chooser without changing the active build configuration.
        /// </summary>
        void HandleBuildDialogCopySettingsCanceled() {
            buildDialogCopySettingsDialog.Hide();
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
        /// Reapplies the active theme background color to the main scene viewport clear settings.
        /// </summary>
        void ApplySceneViewportBackground() {
            if (sceneCameraComponent == null) {
                return;
            }

            byte4 backgroundColor = ThemeManager.Current.Colors.BackgroundPrimary;
            sceneCameraComponent.ClearSettings = new CameraClearSettings(
                true,
                new float4(
                    backgroundColor.X / 255f,
                    backgroundColor.Y / 255f,
                    backgroundColor.Z / 255f,
                    backgroundColor.W / 255f),
                sceneCameraComponent.ClearSettings.ClearDepthEnabled,
                sceneCameraComponent.ClearSettings.ClearDepth,
                sceneCameraComponent.ClearSettings.ClearStencilEnabled,
                sceneCameraComponent.ClearSettings.ClearStencil);
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
                if (reparentEntityDialog != null) {
                    reparentEntityDialog.Hide();
                }
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
            if (sceneHierarchyPanel != null) {
                sceneHierarchyPanel.RefreshHierarchy();
            }
            if (openFileDialog != null) {
                openFileDialog.Hide();
            }
            if (reparentEntityDialog != null) {
                reparentEntityDialog.Hide();
            }
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
                if (unsavedChangesDialog != null) {
                    unsavedChangesDialog.Hide();
                }
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
            if (unsavedChangesDialog != null) {
                unsavedChangesDialog.Hide();
            }
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

            SelectedAssetEntry = entry;
            if (entry.IsGenerated) {
                propertiesPanel.ShowGeneratedAssetSummary(entry);
                RefreshPreviewSource();
                return;
            }

            if (entry.EntryKind == AssetEntryKind.Scene) {
                propertiesPanel.ShowSceneAssetSummary(entry);
                RefreshPreviewSource();
                return;
            }

            if (IsMaterialAssetEntry(entry)) {
                try {
                    MaterialAsset materialAsset = LoadMaterialAsset(entry.FullPath);
                    AssetImportSettings settings = materialAssetSettingsService.LoadOrCreate(
                        entry.FullPath,
                        materialAsset,
                        SupportedPlatforms,
                        ResolvePlatformSelectionModel);
                    if (materialAssetSettingsService.ApplyPlatformCompatibilityFields(materialAsset, settings, CurrentProjectPlatform)) {
                        SaveMaterialAsset(entry.FullPath, materialAsset);
                    }

                    propertiesPanel.ShowMaterialSettings(
                        entry,
                        materialAsset,
                        settings,
                        SupportedPlatforms,
                        CurrentProjectPlatform,
                        ResolvePlatformSelectionModel);
                } catch (Exception ex) {
                    propertiesPanel.ShowImportError(entry, ex.Message);
                }
                RefreshPreviewSource();
                return;
            }

            try {
                AssetImportSettings settings;
                if (!assetImportManager.TryLoadOrCreateImportSettings(entry.FullPath, out settings)) {
                    propertiesPanel.ShowEmpty();
                    RefreshPreviewSource();
                    return;
                }

                assetImportManager.SaveImportSettings(entry.FullPath, settings);
                IReadOnlyList<string> importerIds = assetImportManager.GetImporterIdsForExtension(entry.Extension);
                if (importerIds.Count == 0) {
                    propertiesPanel.ShowImportError(entry, "No importers are registered for this asset type.");
                    RefreshPreviewSource();
                    return;
                }

                propertiesPanel.ShowImportSettings(entry, settings, importerIds, SupportedPlatforms, CurrentProjectPlatform);
                RefreshPreviewSource();
            } catch (Exception ex) {
                propertiesPanel.ShowImportError(entry, ex.Message);
                RefreshPreviewSource();
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
        /// <param name="request">Importer and processor settings to apply.</param>
        void HandleImportSettingsApplyRequested(AssetBrowserEntry entry, AssetImportSettingsApplyRequest request) {
            if (entry == null) {
                throw new ArgumentNullException(nameof(entry));
            }
            if (request == null) {
                throw new ArgumentNullException(nameof(request));
            }

            if (entry.IsDirectory) {
                return;
            }

            try {
                AssetImportSettings settings = assetImportManager.LoadOrCreateImportSettings(entry.FullPath);
                settings.Importer.ImporterId = request.ImporterId;
                settings.Processor = request.ProcessorSettings;
                SetActiveProjectPlatform(request.SelectedPlatformId);
                assetImportManager.SaveImportSettings(entry.FullPath, settings);
                SceneModelRefreshService.RefreshFileSystemModel(entry.FullPath, entry.RelativePath);

                IReadOnlyList<string> importerIds = assetImportManager.GetImporterIdsForExtension(entry.Extension);
                propertiesPanel.ShowImportSettings(entry, settings, importerIds, SupportedPlatforms, CurrentProjectPlatform);
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
        /// Saves one material asset back to disk.
        /// </summary>
        /// <param name="path">Path to the material asset.</param>
        /// <param name="materialAsset">Material asset instance to serialize.</param>
        void SaveMaterialAsset(string path, MaterialAsset materialAsset) {
            if (string.IsNullOrWhiteSpace(path)) {
                throw new ArgumentException("Material path must be provided.", nameof(path));
            } else if (materialAsset == null) {
                throw new ArgumentNullException(nameof(materialAsset));
            }

            using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
            AssetSerializer.Serialize(stream, materialAsset);
        }

        /// <summary>
        /// Clears property and preview panels when no asset is selected.
        /// </summary>
        void HandleAssetSelectionCleared() {
            SelectedAssetEntry = null;
            propertiesPanel.ShowEmpty();
            RefreshPreviewSource();
        }

        /// <summary>
        /// Updates the properties panel when the selection changes.
        /// </summary>
        /// <param name="args">Selection change data.</param>
        void HandleSelectionChanged(EditorSelectionChangedEventArgs args) {
            if (args == null) {
                throw new ArgumentNullException(nameof(args));
            }

            SelectedSceneEntity = args.HasSelection ? args.SelectedEntity : null;
            if (args.HasSelection) {
                propertiesPanel.ShowEntityProperties(args.SelectedEntity);
            } else {
                propertiesPanel.ShowEmpty();
            }

            RefreshPreviewSource();
        }

        /// <summary>
        /// Recomputes the active preview source from the current selection snapshot.
        /// </summary>
        void RefreshPreviewSource() {
            if (previewPanel == null) {
                return;
            }

            if (previewSourceResolver == null) {
                previewPanel.ClearPreview();
                return;
            }

            IPreviewSource previewSource;
            if (previewSourceResolver.TryResolve(SelectedAssetEntry, SelectedSceneEntity, out previewSource)) {
                previewPanel.SetPreviewSource(previewSource);
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

            string outputPath = Path.Combine(projectRoot, "cache", "shader-cache");
            return Path.GetFullPath(outputPath);
        }

        /// <summary>
        /// Recomputes the host title, updates the editor title bar, and notifies the window host.
        /// </summary>
        void RefreshWindowTitle() {
            string title = BuildWindowTitle();
            if (titleBar != null) {
                titleBar.Title = title;
            }
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
        /// Loads one canonical project document from the validated `.heproj` file path.
        /// </summary>
        /// <param name="canonicalProjectFilePath">Validated absolute canonical `.heproj` file path.</param>
        /// <returns>Canonical project document loaded from disk.</returns>
        ProjectFileDocument LoadProjectDocument(string canonicalProjectFilePath) {
            ProjectFileReader reader = new ProjectFileReader();
            ProjectFileReadResult readResult = reader.ReadAsync(canonicalProjectFilePath).GetAwaiter().GetResult();
            if (!readResult.Succeeded) {
                throw new InvalidOperationException(readResult.Errors[0].Message);
            }

            return readResult.Document;
        }

        /// <summary>
        /// Resolves the exact required engine version declared by one loaded project document.
        /// </summary>
        /// <param name="projectDocument">Loaded canonical project document.</param>
        /// <returns>Exact required engine version declared by the project.</returns>
        string ResolveRequiredEngineVersion(ProjectFileDocument projectDocument) {
            if (projectDocument == null) {
                throw new ArgumentNullException(nameof(projectDocument));
            }
            if (string.IsNullOrWhiteSpace(projectDocument.RequiredEngineVersion)) {
                throw new InvalidOperationException("Project file must declare a required engine version.");
            }

            return projectDocument.RequiredEngineVersion;
        }

        /// <summary>
        /// Resolves the game project name declared by one loaded project document.
        /// </summary>
        /// <param name="projectDocument">Loaded canonical project document.</param>
        /// <returns>Game project name used for generated scripting solution files.</returns>
        string ResolveProjectName(ProjectFileDocument projectDocument) {
            if (projectDocument == null) {
                throw new ArgumentNullException(nameof(projectDocument));
            }
            if (string.IsNullOrWhiteSpace(projectDocument.Name)) {
                throw new InvalidOperationException("Project file must declare a project name.");
            }

            return projectDocument.Name;
        }

        /// <summary>
        /// Resolves the human-visible project version declared by one loaded project document.
        /// </summary>
        /// <param name="projectDocument">Loaded canonical project document.</param>
        /// <returns>Project version used for build metadata and queue reporting.</returns>
        string ResolveProjectVersion(ProjectFileDocument projectDocument) {
            if (projectDocument == null) {
                throw new ArgumentNullException(nameof(projectDocument));
            }
            if (string.IsNullOrWhiteSpace(projectDocument.Version)) {
                throw new InvalidOperationException("Project file must declare a project version.");
            }

            return projectDocument.Version;
        }

        /// <summary>
        /// Loads the supported platform identifiers declared by one canonical `.heproj` file.
        /// </summary>
        /// <param name="canonicalProjectFilePath">Validated absolute canonical `.heproj` file path.</param>
        /// <returns>Supported platform identifiers preserved from the project file.</returns>
        IReadOnlyList<string> LoadProjectSupportedPlatforms(string canonicalProjectFilePath) {
            ProjectFileDocument projectDocument = LoadProjectDocument(canonicalProjectFilePath);
            return LoadProjectSupportedPlatforms(projectDocument);
        }

        /// <summary>
        /// Loads the supported platform identifiers declared by one loaded project document.
        /// </summary>
        /// <param name="projectDocument">Loaded canonical project document.</param>
        /// <returns>Supported platform identifiers preserved from the project file.</returns>
        IReadOnlyList<string> LoadProjectSupportedPlatforms(ProjectFileDocument projectDocument) {
            if (projectDocument == null) {
                throw new ArgumentNullException(nameof(projectDocument));
            }
            if (projectDocument.SupportedPlatforms == null || projectDocument.SupportedPlatforms.Count == 0) {
                throw new InvalidOperationException("Project file must declare at least one supported platform.");
            }

            return projectDocument.SupportedPlatforms.AsReadOnly();
        }

        /// <summary>
        /// Creates the available-platform resolver used by Build Settings.
        /// </summary>
        /// <returns>Resolver that loads platforms from development overrides, launcher state, or built-in fallback sources.</returns>
        AvailablePlatformProviderResolver CreateAvailablePlatformProviderResolver() {
            EditorSourceBuildWorkspaceLocator workspaceLocator = new EditorSourceBuildWorkspaceLocator();
            string helEngineRootPath = workspaceLocator.ResolveHelEngineRootPath();
            PlatformDiscoveryOptions options = new PlatformDiscoveryOptions(Path.Combine(helEngineRootPath, "user_settings"));
            WindowsLauncherInstallRootLocator launcherInstallRootLocator = new WindowsLauncherInstallRootLocator();
            return new AvailablePlatformProviderResolver(options, launcherInstallRootLocator);
        }

        /// <summary>
        /// Creates the dynamic platform catalog service used to load builder metadata.
        /// </summary>
        /// <returns>Platform catalog backed by the current platform resolver.</returns>
        EditorPlatformCatalogService CreatePlatformCatalogService() {
            IReadOnlyList<AvailablePlatformDescriptor> platforms = availablePlatformProviderResolver.LoadPlatforms(RequiredEngineVersion);
            return new EditorPlatformCatalogService(platforms);
        }

        /// <summary>
        /// Creates the build executor router from the dynamically discovered platform catalog.
        /// </summary>
        /// <returns>Router keyed by platform identifier.</returns>
        EditorBuildExecutorRouter CreateBuildExecutorRouter() {
            IReadOnlyList<AvailablePlatformDescriptor> platforms = availablePlatformProviderResolver.LoadPlatforms(RequiredEngineVersion);
            Dictionary<string, IEditorBuildExecutor> executorsByPlatformId = new(StringComparer.OrdinalIgnoreCase);

            for (int index = 0; index < platforms.Count; index++) {
                AvailablePlatformDescriptor platform = platforms[index];
                if (!platform.IsInstalled || string.IsNullOrWhiteSpace(platform.BuilderAssemblyPath)) {
                    continue;
                }

                executorsByPlatformId[platform.Id] = new EditorPlatformBuildExecutor(
                    projectPath,
                    RequiredEngineVersion,
                    ProjectName,
                    ProjectVersion,
                    Importers,
                    platform,
                    uiFont);
            }

            return new EditorBuildExecutorRouter(executorsByPlatformId);
        }

        /// <summary>
        /// Persists one new supported-platform list to the canonical project file.
        /// </summary>
        /// <param name="supportedPlatforms">Supported platforms selected in Build Settings.</param>
        void SaveProjectSupportedPlatforms(IReadOnlyList<string> supportedPlatforms) {
            if (supportedPlatforms == null) {
                throw new ArgumentNullException(nameof(supportedPlatforms));
            }
            if (supportedPlatforms.Count == 0) {
                throw new InvalidOperationException("At least one supported platform must be selected.");
            }

            ProjectFileDocument projectDocument = LoadProjectDocument(CanonicalProjectFilePath);
            projectDocument.SupportedPlatforms = new List<string>(supportedPlatforms);
            ProjectFileWriter writer = new ProjectFileWriter();
            writer.WriteAsync(CanonicalProjectFilePath, projectDocument).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Applies one new supported-platform list to the live editor session and local project settings.
        /// </summary>
        /// <param name="supportedPlatforms">Supported platforms selected in Build Settings.</param>
        void ApplySupportedPlatforms(IReadOnlyList<string> supportedPlatforms) {
            if (supportedPlatforms == null) {
                throw new ArgumentNullException(nameof(supportedPlatforms));
            }
            if (supportedPlatforms.Count == 0) {
                throw new InvalidOperationException("At least one supported platform must be selected.");
            }

            ProjectSupportedPlatforms = new List<string>(supportedPlatforms).AsReadOnly();
            ProjectLocalSettingsService = new EditorProjectLocalSettingsService(projectPath, ProjectSupportedPlatforms);
            ActiveProjectPlatform = ResolveNextActiveProjectPlatform(ProjectSupportedPlatforms);
            ProjectLocalSettingsService.SaveActivePlatform(ActiveProjectPlatform);
            assetImportManager.CurrentPlatformId = ActiveProjectPlatform;
        }

        /// <summary>
        /// Resolves the active platform that should remain selected after supported platforms change.
        /// </summary>
        /// <param name="supportedPlatforms">Updated supported platform identifiers.</param>
        /// <returns>Current platform when still supported; otherwise the first supported platform.</returns>
        string ResolveNextActiveProjectPlatform(IReadOnlyList<string> supportedPlatforms) {
            if (supportedPlatforms == null) {
                throw new ArgumentNullException(nameof(supportedPlatforms));
            }
            if (supportedPlatforms.Count == 0) {
                throw new InvalidOperationException("At least one supported platform must be provided.");
            }

            for (int i = 0; i < supportedPlatforms.Count; i++) {
                if (string.Equals(supportedPlatforms[i], ActiveProjectPlatform, StringComparison.OrdinalIgnoreCase) && IsInstalledPlatform(supportedPlatforms[i])) {
                    return supportedPlatforms[i];
                }
            }

            for (int i = 0; i < supportedPlatforms.Count; i++) {
                if (IsInstalledPlatform(supportedPlatforms[i])) {
                    return supportedPlatforms[i];
                }
            }

            throw new InvalidOperationException("At least one supported platform must be installed for the current engine.");
        }

        /// <summary>
        /// Returns true when the supplied platform exists in the current engine catalog and has an installed payload.
        /// </summary>
        /// <param name="platformId">Platform identifier to inspect.</param>
        /// <returns>True when the platform is installed for the current engine; otherwise false.</returns>
        bool IsInstalledPlatform(string platformId) {
            if (availablePlatformProviderResolver == null) {
                return false;
            }
            if (string.IsNullOrWhiteSpace(platformId)) {
                return false;
            }

            IReadOnlyList<AvailablePlatformDescriptor> availablePlatforms = availablePlatformProviderResolver.LoadPlatforms(RequiredEngineVersion);
            for (int i = 0; i < availablePlatforms.Count; i++) {
                if (string.Equals(availablePlatforms[i].Id, platformId, StringComparison.OrdinalIgnoreCase)) {
                    return availablePlatforms[i].IsInstalled;
                }
            }

            return false;
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
            manager.CurrentPlatformId = ActiveProjectPlatform;
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

