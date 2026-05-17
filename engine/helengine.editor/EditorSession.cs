using helengine.baseplatform.Definitions;
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
        /// Stable workspace panel type identifier for viewport panels.
        /// </summary>
        const string ViewportPanelTypeId = "viewport";
        /// <summary>
        /// Stable workspace panel type identifier for scene hierarchy panels.
        /// </summary>
        const string SceneHierarchyPanelTypeId = "scene-hierarchy";
        /// <summary>
        /// Stable workspace panel type identifier for asset browser panels.
        /// </summary>
        const string AssetBrowserPanelTypeId = "asset-browser";
        /// <summary>
        /// Stable workspace panel type identifier for properties panels.
        /// </summary>
        const string PropertiesPanelTypeId = "properties";
        /// <summary>
        /// Stable workspace panel type identifier for logger panels.
        /// </summary>
        const string LoggerPanelTypeId = "logger";
        /// <summary>
        /// Stable workspace panel type identifier for preview panels.
        /// </summary>
        const string PreviewPanelTypeId = "preview";
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
        /// Supported platform identifiers declared by the current project's `settings/platforms.json` file.
        /// </summary>
        IReadOnlyList<string> ProjectSupportedPlatforms;
        /// <summary>
        /// Service used to persist project-shared supported platform identifiers.
        /// </summary>
        EditorProjectPlatformsService projectPlatformsService;
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
        /// Font used by viewport snap modifier labels.
        /// </summary>
        readonly FontAsset SnapModifierFont;
        /// <summary>
        /// Runtime textures used by viewport toolbar controls.
        /// </summary>
        readonly EditorViewportToolbarIconSet ViewportToolbarIcons;
        /// <summary>
        /// Content manager used to load editor and project asset files.
        /// </summary>
        readonly ContentManager EditorContentManager;
        /// <summary>
        /// Title bar UI for the editor.
        /// </summary>
        readonly EditorTitleBar titleBar;
        /// <summary>
        /// Panel type registry used by workspace panel creation commands.
        /// </summary>
        EditorWorkspacePanelRegistry PanelRegistry;
        /// <summary>
        /// Live panel instances tracked by the workspace system.
        /// </summary>
        List<EditorWorkspacePanelInstance> PanelInstances;
        /// <summary>
        /// Persists workspace layout slots for the current project.
        /// </summary>
        EditorWorkspaceLayoutService WorkspaceLayoutService;
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
        /// Resolver used to rebuild file-backed scene asset references into runtime assets.
        /// </summary>
        readonly EditorSceneAssetReferenceResolver sceneAssetReferenceResolver;
        /// <summary>
        /// Factory used to convert asset-browser entries into stable scene asset references.
        /// </summary>
        readonly SceneAssetReferenceFactory sceneAssetReferenceFactory;
        /// <summary>
        /// Modal used to pick an asset for editor fields.
        /// </summary>
        AssetPickerModal assetPickerModal;
        /// <summary>
        /// Main viewport dock panel.
        /// </summary>
        readonly EditorViewport mainViewport;
        /// <summary>
        /// Editor-only component that renders the authored 2D canvas into the world-space viewport plane.
        /// </summary>
        readonly EditorViewportCanvasPlanePreviewComponent canvasPlanePreviewComponent;
        /// <summary>
        /// Scene-owned canvas profile shared by viewport previews and scene settings UI.
        /// </summary>
        readonly EditorSceneCanvasProfileState sceneCanvasProfileState;
        /// <summary>
        /// UI camera entity used for 2D rendering.
        /// </summary>
        readonly EditorEntity uiCameraEntity;
        /// <summary>
        /// Modal UI camera entity used for dialog-shell rendering above panel-content cameras.
        /// </summary>
        readonly EditorEntity modalUiCameraEntity;
        /// <summary>
        /// Scene camera entity used for 3D rendering.
        /// </summary>
        readonly EditorEntity sceneCameraEntity;
        /// <summary>
        /// UI camera component.
        /// </summary>
        readonly CameraComponent uiCameraComponent;
        /// <summary>
        /// Modal UI camera component.
        /// </summary>
        readonly CameraComponent modalUiCameraComponent;
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
        /// Tracks the most recently focused viewport instance for add-to-scene placement.
        /// </summary>
        EditorWorkspacePanelInstance LastFocusedViewportInstance;
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
        /// Modal dialog used to edit the project's supported platforms and explicit active platform.
        /// </summary>
        PlatformsDialog platformsDialog;
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
        /// Modal dialog used to edit scene-level authoring settings such as the shared canvas profile.
        /// </summary>
        SceneSettingsDialog sceneSettingsDialog;
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
        /// Raised when the user confirms one editor-global preferences selection.
        /// </summary>
        public event Action<EditorPreferencesSettings> PreferencesChanged;
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
        /// Scene-level settings tracked for the active editor scene.
        /// </summary>
        SceneSettingsAsset CurrentSceneSettings;
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
        /// Tracks which previewable selection type was clicked most recently.
        /// </summary>
        PreviewPanelBindingKind LatestPreviewSelectionKind;
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
        /// Current editor-global theme identifier reflected by the preferences dialog.
        /// </summary>
        string CurrentThemeId;
        /// <summary>
        /// Current editor-global preferences reflected by the preferences dialog.
        /// </summary>
        EditorPreferencesSettings CurrentEditorPreferences;
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
        /// <param name="initialEditorPreferences">Validated editor-global preferences resolved by the host before session startup.</param>
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
            EditorPreferencesSettings initialEditorPreferences,
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
            CurrentEditorPreferences = initialEditorPreferences ?? throw new ArgumentNullException(nameof(initialEditorPreferences));
            CurrentUiScaleSettings = CurrentEditorPreferences.UiScale;
            CurrentThemeId = CurrentEditorPreferences.ThemeId;
            CurrentUiMetrics = initialUiMetrics ?? throw new ArgumentNullException(nameof(initialUiMetrics));
            ProjectFileDocument projectDocument = LoadProjectDocument(CanonicalProjectFilePath);
            RequiredEngineVersion = ResolveRequiredEngineVersion(projectDocument);
            ProjectName = ResolveProjectName(projectDocument);
            ProjectVersion = ResolveProjectVersion(projectDocument);
            projectPlatformsService = new EditorProjectPlatformsService(this.projectPath);
            ProjectSupportedPlatforms = projectPlatformsService.Load().SupportedPlatforms.AsReadOnly();
            ProjectLocalSettingsService = new EditorProjectLocalSettingsService(this.projectPath, ProjectSupportedPlatforms);
            ActiveProjectPlatform = ProjectLocalSettingsService.LoadActivePlatform();
            availablePlatformProviderResolver = CreateAvailablePlatformProviderResolver();
            platformCatalogService = CreatePlatformCatalogService();
            EditorContentManager = new ContentManager(ResolveAssetsRootPath(this.projectPath));
            EditorContentManagerConfiguration.ConfigureEditorContentManager(EditorContentManager);
            this.uiFont = uiFont ?? throw new ArgumentNullException(nameof(uiFont));
            SnapModifierFont = snapModifierFont ?? throw new ArgumentNullException(nameof(snapModifierFont));
            ViewportToolbarIcons = toolbarIcons ?? throw new ArgumentNullException(nameof(toolbarIcons));
            Importers = importers ?? throw new ArgumentNullException(nameof(importers));
            this.core.SetDefaultFontAssetForEditor(this.uiFont);

            EditorKeyboardFocusService.Reset();
            core.Initialize(render3D, render2D, input, CreateEditorPlatformInfo());
            EditorComponentAddCatalog.Initialize();
            core.Input.SetKeyboardActive(true);

            EditorProjectPaths.Initialize(this.projectPath);

            assetImportManager = InitializeAssetImports(Importers);
            materialAssetSettingsService = new MaterialAssetSettingsService();
            GeneratedAssetProviderRegistry.Register(new EngineGeneratedAssetProvider());
            sceneCanvasProfileState = new EditorSceneCanvasProfileState();
            previewSourceResolver = new PreviewSourceResolver(assetImportManager, render2D, render3D, sceneCanvasProfileState);

            uiCameraEntity = new EditorEntity();
            uiCameraEntity.InternalEntity = true;
            uiCameraEntity.Position = new float3(0, 3, -8);
            uiCameraComponent = new CameraComponent();
            uiCameraComponent.LayerMask = EditorLayerMasks.EditorUi;
            uiCameraComponent.CameraDrawOrder = EditorUiCameraDrawOrders.SharedUi;
            uiCameraComponent.ClearSettings = new CameraClearSettings(false, new float4(0f, 0f, 0f, 0f), false, 1.0f, false, 0);
            uiCameraEntity.AddComponent(uiCameraComponent);

            modalUiCameraEntity = new EditorEntity();
            modalUiCameraEntity.InternalEntity = true;
            modalUiCameraEntity.Position = new float3(0, 3, -8);
            modalUiCameraComponent = new CameraComponent();
            modalUiCameraComponent.LayerMask = EditorLayerMasks.EditorModalUi;
            modalUiCameraComponent.CameraDrawOrder = EditorUiCameraDrawOrders.ModalUi;
            modalUiCameraComponent.ClearSettings = new CameraClearSettings(false, new float4(0f, 0f, 0f, 0f), false, 1.0f, false, 0);
            modalUiCameraEntity.AddComponent(modalUiCameraComponent);

            ViewportWorkspacePanelController primaryViewportController = CreatePrimaryViewportController();
            EditorViewportWorkspaceState primaryViewportState = primaryViewportController.ViewportState;
            mainViewport = primaryViewportState.Viewport;
            sceneCameraEntity = primaryViewportState.SceneCameraEntity;
            sceneCameraComponent = primaryViewportState.SceneCamera;
            gizmoCameraComponent = primaryViewportState.GizmoCamera;
            hiddenCameraEntity = primaryViewportState.PickerCameraEntity;
            hiddenCameraComponent = primaryViewportState.PickerCamera;
            hiddenCameraTarget = primaryViewportState.PickerRenderTarget;
            canvasPlanePreviewComponent = primaryViewportState.CanvasPlanePreviewComponent;
            ApplyEditorTheme(CurrentThemeId);
            keyboardFocusEntity = new EditorEntity {
                InternalEntity = true,
                Enabled = true,
                LayerMask = EditorLayerMasks.EditorUi
            };
            var keyboardFocusUpdateComponent = new EditorKeyboardFocusUpdateComponent {
                UpdateOrder = core.ObjectManager.GetUpdateOrderForLayer(1),
                SaveShortcutRequested = HandleGlobalSaveShortcut
            };
            keyboardFocusEntity.AddComponent(keyboardFocusUpdateComponent);

            titleBar = new EditorTitleBar(uiFont, CurrentUiMetrics, Math.Max(1, renderWidth), Math.Max(1, renderHeight), BuildWindowTitle(), titleBarIcon);
            PanelRegistry = new EditorWorkspacePanelRegistry();
            PanelInstances = new List<EditorWorkspacePanelInstance>();
            WorkspaceLayoutService = new EditorWorkspaceLayoutService(ResolveProjectRootPath(this.projectPath));
            InitializePanelRegistry();

            dockingManager = new DockingManager();
            EditorFileSystemModelResolver fileSystemModelResolver = new EditorFileSystemModelResolver(assetImportManager);
            EditorFileSystemFontResolver fileSystemFontResolver = new EditorFileSystemFontResolver(assetImportManager);
            sceneHierarchyPanel = new SceneHierarchyPanel(uiFont, CurrentUiMetrics);
            assetBrowserPanel = new AssetBrowserPanel(uiFont, this.projectPath, CurrentUiMetrics);
            propertiesPanel = new PropertiesPanel(uiFont, EditorContentManager, fileSystemModelResolver, titleBar.Entity, scriptHotReloadService, CurrentUiMetrics, fileSystemFontResolver);
            loggerPanel = new LoggerPanel(uiFont, CurrentUiMetrics);
            previewPanel = new PreviewPanel(uiFont, CurrentUiMetrics);
            assetPickerModal = new AssetPickerModal(uiFont, CurrentUiMetrics, this.projectPath);
            gameSolutionService = new EditorGameSolutionService(this.projectPath, ProjectName, new EditorVisualStudioLauncher());
            EditorGameScriptAssemblyHost scriptAssemblyHost = new EditorGameScriptAssemblyHost(this.projectPath);
            scriptHotReloadService = new EditorGameScriptHotReloadService(
                gameSolutionService,
                new EditorDotNetScriptBuildTool(),
                scriptAssemblyHost);
            ComponentPersistenceRegistry persistenceRegistry = CreateComponentPersistenceRegistry(scriptHotReloadService.ScriptTypeResolver);
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
            platformsDialog = new PlatformsDialog(uiFont, CurrentUiMetrics);
            profilesDialog = new ProfilesDialog(uiFont, CurrentUiMetrics);
            buildDialog = new BuildDialog(uiFont, CurrentUiMetrics);
            buildDialogCopySettingsDialog = new BuildDialogCopySettingsDialog(uiFont, CurrentUiMetrics);
            unsavedChangesDialog = new UnsavedChangesDialog(uiFont, CurrentUiMetrics);
            sceneSettingsDialog = new SceneSettingsDialog(uiFont, CurrentUiMetrics);
            preferencesDialog = new EditorPreferencesDialog(uiFont, CurrentUiMetrics);
            sceneAssetReferenceFactory = new SceneAssetReferenceFactory();
            sceneAssetReferenceResolver = new EditorSceneAssetReferenceResolver(EditorContentManager, this.projectPath, fileSystemModelResolver, fileSystemFontResolver);
            SceneFileLoadService = new SceneFileLoadService(
                this.projectPath,
                persistenceRegistry,
                sceneAssetReferenceResolver);
            CurrentScenePath = string.Empty;
            CurrentSceneSettings = new SceneSettingsAsset();
            sceneCanvasProfileState.ApplySceneSettings(CurrentSceneSettings);
            PendingOpenScenePath = string.Empty;
            PendingSceneTransition = SceneTransitionKind.None;
            IsSceneDirty = false;
            RefreshWindowTitle();
            EditorSelectionService.SelectionChanged += HandleSelectionChanged;
            EditorAssetPickerService.PickRequested += HandleAssetPickRequested;
            EditorSceneMutationService.SceneMutated += HandleSceneMutated;
            titleBar.NewMapRequested += HandleNewMapRequested;
            titleBar.OpenMapRequested += HandleOpenMapRequested;
            titleBar.SaveMapRequested += HandleSaveMapRequested;
            titleBar.SaveMapAsRequested += HandleSaveMapAsRequested;
            titleBar.SceneSettingsRequested += HandleSceneSettingsRequested;
            titleBar.PreferencesRequested += HandlePreferencesRequested;
            titleBar.BuildRequested += HandleBuildRequested;
            titleBar.PlatformsRequested += HandlePlatformsRequested;
            titleBar.ProfilesRequested += HandleProfilesRequested;
            titleBar.BuildScriptsRequested += HandleBuildScriptsRequested;
            titleBar.OpenInIDERequested += HandleOpenInIDERequested;
            titleBar.ProjectMenuItemRequested += HandleProjectMenuItemRequested;
            titleBar.UiMenuActionRequested += HandleUiMenuActionRequested;
            titleBar.AddEmptyRequested += HandleAddEmptyRequested;
            titleBar.AddCubeRequested += HandleAddCubeRequested;
            titleBar.AddPlaneRequested += HandleAddPlaneRequested;
            titleBar.AddCameraRequested += HandleAddCameraRequested;
            titleBar.AddSpotLightRequested += HandleAddSpotLightRequested;
            titleBar.AddPointLightRequested += HandleAddPointLightRequested;
            titleBar.AddDirectionalLightRequested += HandleAddDirectionalLightRequested;
            titleBar.AddAmbientLightRequested += HandleAddAmbientLightRequested;
            AttachScaleSensitiveDialogHandlers();
            EditorBuildExecutionResult startupProjectLibraryLoadResult = LoadProjectLibrariesOnStartup(
                scriptHotReloadService,
                titleBar.ApplyProjectMenus);
            if (!startupProjectLibraryLoadResult.Succeeded) {
                Logger.WriteError(startupProjectLibraryLoadResult.Message);
            }

            sceneHierarchyPanel.Size = new int2(280, 600);
            assetBrowserPanel.Size = new int2(500, 240);
            propertiesPanel.Size = new int2(280, 600);
            previewPanel.Size = new int2(propertiesPanel.Size.X, 240);
            RegisterExistingWorkspacePanelInstance(
                ViewportPanelTypeId,
                "viewport-primary",
                primaryViewportController);
            RegisterExistingWorkspacePanelInstance(SceneHierarchyPanelTypeId, "scene-hierarchy-primary", new SessionWorkspacePanelController(sceneHierarchyPanel, SessionWorkspacePanelController.NoState, SessionWorkspacePanelController.NoRestore, sceneHierarchyPanel.Detach));
            RegisterExistingWorkspacePanelInstance(AssetBrowserPanelTypeId, "asset-browser-primary", new SessionWorkspacePanelController(assetBrowserPanel, SessionWorkspacePanelController.NoState, SessionWorkspacePanelController.NoRestore, SessionWorkspacePanelController.NoDispose));
            RegisterExistingWorkspacePanelInstance(PropertiesPanelTypeId, "properties-primary", new SessionWorkspacePanelController(propertiesPanel, SessionWorkspacePanelController.NoState, SessionWorkspacePanelController.NoRestore, SessionWorkspacePanelController.NoDispose));
            RegisterExistingWorkspacePanelInstance(LoggerPanelTypeId, "logger-primary", new SessionWorkspacePanelController(loggerPanel, SessionWorkspacePanelController.NoState, SessionWorkspacePanelController.NoRestore, loggerPanel.Detach));
            RegisterExistingWorkspacePanelInstance(PreviewPanelTypeId, "preview-primary", CreatePreviewPanelSessionController(previewPanel));

            dockingManager.Layout.Add(sceneHierarchyPanel);
            dockingManager.Layout.Add(assetBrowserPanel);
            dockingManager.Layout.Add(mainViewport);
            dockingManager.Layout.Add(propertiesPanel);
            dockingManager.Layout.Add(loggerPanel);
            dockingManager.Layout.Add(previewPanel);

            dockingManager.Layout.DockAsRoot(mainViewport);
            dockingManager.Layout.DockRelative(assetBrowserPanel, mainViewport, DockInsertDirection.Bottom, 0.7f);
            dockingManager.Layout.DockRelative(sceneHierarchyPanel, mainViewport, DockInsertDirection.Right, 0.7f);
            dockingManager.Layout.DockRelative(propertiesPanel, sceneHierarchyPanel, DockInsertDirection.Bottom, 0.5f);
            dockingManager.Layout.DockRelative(loggerPanel, assetBrowserPanel, DockInsertDirection.Fill, 0.5f);
            dockingManager.Layout.DockRelative(previewPanel, assetBrowserPanel, DockInsertDirection.Right, 0.75f);

            ShaderCompileTarget runtimeTarget = ResolveRuntimeShaderTarget(render3D);
            shaderModuleManager = BuildShaderModuleManager(runtimeTarget);
            EditorShaderPackageService.Initialize(shaderModuleManager, runtimeTarget, EditorContentManager);
            shaderModuleManager.ShaderBuilt += HandleShaderBuilt;
            shaderModuleManager.Start();
            BuildStartScene();
            RefreshHierarchy();

            UpdateLayout(renderWidth, renderHeight);
            PromptForPlatformSelectionIfRequired();
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
        /// Returns tracked live panel instances for one panel type during tests.
        /// </summary>
        /// <param name="panelTypeId">Stable panel type identifier.</param>
        /// <returns>Tracked live panel instances for the requested type.</returns>
        internal IReadOnlyList<EditorWorkspacePanelInstance> GetPanelInstancesForTest(string panelTypeId) {
            if (string.IsNullOrWhiteSpace(panelTypeId)) {
                throw new ArgumentException("Panel type identifier must be provided.", nameof(panelTypeId));
            }

            return PanelInstances
                .Where(instance => string.Equals(instance.PanelTypeId, panelTypeId, StringComparison.OrdinalIgnoreCase))
                .ToArray();
        }

        /// <summary>
        /// Routes one UI menu action through the workspace panel command handler during tests.
        /// </summary>
        /// <param name="action">UI menu action to process.</param>
        internal void HandleUiMenuActionForTest(EditorTitleBarUiMenuAction action) {
            HandleUiMenuActionRequested(action);
        }

        /// <summary>
        /// Returns all tracked scene hierarchy panels currently managed by the workspace system.
        /// </summary>
        /// <returns>Tracked scene hierarchy panels.</returns>
        IReadOnlyList<SceneHierarchyPanel> GetSceneHierarchyPanels() {
            if (PanelInstances == null) {
                return sceneHierarchyPanel == null ? Array.Empty<SceneHierarchyPanel>() : new[] { sceneHierarchyPanel };
            }

            return PanelInstances
                .Where(instance => string.Equals(instance.PanelTypeId, SceneHierarchyPanelTypeId, StringComparison.OrdinalIgnoreCase))
                .Select(instance => (SceneHierarchyPanel)instance.Dockable)
                .ToArray();
        }

        /// <summary>
        /// Returns all tracked asset browser panels currently managed by the workspace system.
        /// </summary>
        /// <returns>Tracked asset browser panels.</returns>
        IReadOnlyList<AssetBrowserPanel> GetAssetBrowserPanels() {
            if (PanelInstances == null) {
                return assetBrowserPanel == null ? Array.Empty<AssetBrowserPanel>() : new[] { assetBrowserPanel };
            }

            return PanelInstances
                .Where(instance => string.Equals(instance.PanelTypeId, AssetBrowserPanelTypeId, StringComparison.OrdinalIgnoreCase))
                .Select(instance => (AssetBrowserPanel)instance.Dockable)
                .ToArray();
        }

        /// <summary>
        /// Returns all tracked properties panels currently managed by the workspace system.
        /// </summary>
        /// <returns>Tracked properties panels.</returns>
        IReadOnlyList<PropertiesPanel> GetPropertiesPanels() {
            if (PanelInstances == null) {
                return propertiesPanel == null ? Array.Empty<PropertiesPanel>() : new[] { propertiesPanel };
            }

            return PanelInstances
                .Where(instance => string.Equals(instance.PanelTypeId, PropertiesPanelTypeId, StringComparison.OrdinalIgnoreCase))
                .Select(instance => (PropertiesPanel)instance.Dockable)
                .ToArray();
        }

        /// <summary>
        /// Returns all tracked preview panels currently managed by the workspace system.
        /// </summary>
        /// <returns>Tracked preview panels.</returns>
        IReadOnlyList<PreviewPanel> GetPreviewPanels() {
            if (PanelInstances == null) {
                return previewPanel == null ? Array.Empty<PreviewPanel>() : new[] { previewPanel };
            }

            return PanelInstances
                .Where(instance => string.Equals(instance.PanelTypeId, PreviewPanelTypeId, StringComparison.OrdinalIgnoreCase))
                .Select(instance => (PreviewPanel)instance.Dockable)
                .ToArray();
        }

        /// <summary>
        /// Returns all tracked logger panels currently managed by the workspace system.
        /// </summary>
        /// <returns>Tracked logger panels.</returns>
        IReadOnlyList<LoggerPanel> GetLoggerPanels() {
            if (PanelInstances == null) {
                return loggerPanel == null ? Array.Empty<LoggerPanel>() : new[] { loggerPanel };
            }

            return PanelInstances
                .Where(instance => string.Equals(instance.PanelTypeId, LoggerPanelTypeId, StringComparison.OrdinalIgnoreCase))
                .Select(instance => (LoggerPanel)instance.Dockable)
                .ToArray();
        }

        /// <summary>
        /// Returns all viewport panels currently managed by the session.
        /// </summary>
        /// <returns>Viewport panels managed by the session.</returns>
        IReadOnlyList<EditorViewport> GetViewportPanels() {
            if (PanelInstances == null) {
                return mainViewport == null ? Array.Empty<EditorViewport>() : new[] { mainViewport };
            }

            return GetViewportPanelInstances()
                .Select(instance => (EditorViewport)instance.Dockable)
                .ToArray();
        }

        /// <summary>
        /// Synchronizes gizmo overlay camera viewports for every workspace-managed viewport stack.
        /// </summary>
        void SynchronizeViewportOverlayCameras() {
            if (PanelInstances == null) {
                gizmoCameraComponent.Viewport = sceneCameraComponent.Viewport;
                return;
            }

            IReadOnlyList<EditorWorkspacePanelInstance> viewportInstances = GetViewportPanelInstances();
            for (int index = 0; index < viewportInstances.Count; index++) {
                if (viewportInstances[index].Controller is ViewportWorkspacePanelController viewportController) {
                    viewportController.ViewportState.GizmoCamera.Viewport = viewportController.ViewportState.SceneCamera.Viewport;
                }
            }
        }

        /// <summary>
        /// Returns the currently tracked viewport workspace panel instances.
        /// </summary>
        /// <returns>Tracked viewport workspace panel instances.</returns>
        IReadOnlyList<EditorWorkspacePanelInstance> GetViewportPanelInstances() {
            if (PanelInstances == null) {
                return Array.Empty<EditorWorkspacePanelInstance>();
            }

            return PanelInstances
                .Where(instance => string.Equals(instance.PanelTypeId, ViewportPanelTypeId, StringComparison.OrdinalIgnoreCase))
                .ToArray();
        }

        /// <summary>
        /// Returns the first tracked viewport workspace controller when one exists.
        /// </summary>
        /// <returns>Tracked viewport workspace controller, or null when no viewport instance is tracked.</returns>
        ViewportWorkspacePanelController GetPrimaryViewportController() {
            IReadOnlyList<EditorWorkspacePanelInstance> viewportInstances = GetViewportPanelInstances();
            for (int index = 0; index < viewportInstances.Count; index++) {
                if (viewportInstances[index].Controller is ViewportWorkspacePanelController viewportController) {
                    return viewportController;
                }
            }

            return null;
        }

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
        public CameraComponent SceneCamera {
            get {
                if (PanelInstances == null) {
                    return sceneCameraComponent;
                }

                ViewportWorkspacePanelController viewportController = GetPrimaryViewportController();
                if (viewportController != null) {
                    return viewportController.ViewportState.SceneCamera;
                }

                return null;
            }
        }

        /// <summary>
        /// Gets the primary dockable viewport panel.
        /// </summary>
        public EditorViewport MainViewport {
            get {
                if (PanelInstances == null) {
                    return mainViewport;
                }

                ViewportWorkspacePanelController viewportController = GetPrimaryViewportController();
                if (viewportController != null) {
                    return viewportController.ViewportState.Viewport;
                }

                return null;
            }
        }

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
            IReadOnlyList<SceneHierarchyPanel> panels = GetSceneHierarchyPanels();
            for (int index = 0; index < panels.Count; index++) {
                panels[index].RefreshHierarchy();
            }
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
            RefreshWindowTitle();
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
            modalUiCameraComponent.Viewport = new float4(0, 0, width, height);

            int availableHeight = Math.Max(0, height - titleBar.Height);
            dockingManager.Layout.Layout(new int2(width, availableHeight), new float3(0, titleBar.Height, 0));
            EditorKeyboardFocusService.SetDockOrder(dockingManager.Layout.GetVisibleDockablesInTraversalOrder());
            SynchronizeViewportOverlayCameras();
            assetPickerModal.UpdateLayout(width, height);
            saveFileDialog.UpdateLayout(width, height);
            openFileDialog.UpdateLayout(width, height);
            reparentEntityDialog.UpdateLayout(width, height);
            platformsDialog.UpdateLayout(width, height);
            profilesDialog.UpdateLayout(width, height);
            buildDialog.UpdateLayout(width, height);
            buildDialogCopySettingsDialog.UpdateLayout(width, height);
            unsavedChangesDialog.UpdateLayout(width, height);
            sceneSettingsDialog.UpdateLayout(width, height);
            preferencesDialog.UpdateLayout(width, height);
            IReadOnlyList<PropertiesPanel> propertiesPanels = GetPropertiesPanels();
            for (int index = 0; index < propertiesPanels.Count; index++) {
                propertiesPanels[index].UpdateModalLayout(width, height);
            }
            IReadOnlyList<EditorViewport> viewports = GetViewportPanels();
            for (int index = 0; index < viewports.Count; index++) {
                viewports[index].RefreshInputBlockers();
            }
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
            if (!string.IsNullOrWhiteSpace(CurrentThemeId)) {
                CurrentEditorPreferences = new EditorPreferencesSettings(CurrentUiScaleSettings, CurrentThemeId);
            }
            CurrentUiMetrics = metrics;
            this.uiFont = uiFont;

            if (core != null) {
                core.SetDefaultFontAssetForEditor(uiFont);
            }
            if (titleBar != null) {
                titleBar.ApplyUiMetrics(uiFont, metrics);
            }
            IReadOnlyList<SceneHierarchyPanel> sceneHierarchyPanels = GetSceneHierarchyPanels();
            for (int index = 0; index < sceneHierarchyPanels.Count; index++) {
                sceneHierarchyPanels[index].ApplyUiMetrics(uiFont, metrics);
            }
            IReadOnlyList<AssetBrowserPanel> assetBrowserPanels = GetAssetBrowserPanels();
            for (int index = 0; index < assetBrowserPanels.Count; index++) {
                assetBrowserPanels[index].ApplyUiMetrics(uiFont, metrics);
            }
            IReadOnlyList<EditorViewport> viewports = GetViewportPanels();
            for (int index = 0; index < viewports.Count; index++) {
                viewports[index].ApplyUiMetrics(uiFont, snapModifierFont, metrics);
            }
            IReadOnlyList<PropertiesPanel> propertiesPanels = GetPropertiesPanels();
            for (int index = 0; index < propertiesPanels.Count; index++) {
                propertiesPanels[index].ApplyUiMetrics(uiFont, metrics);
            }
            IReadOnlyList<LoggerPanel> loggerPanels = GetLoggerPanels();
            for (int index = 0; index < loggerPanels.Count; index++) {
                loggerPanels[index].ApplyUiMetrics(uiFont, metrics);
            }
            IReadOnlyList<PreviewPanel> previewPanels = GetPreviewPanels();
            for (int index = 0; index < previewPanels.Count; index++) {
                previewPanels[index].ApplyUiMetrics(uiFont, metrics);
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
            if (platformsDialog != null) {
                platformsDialog.ConfirmRequested += HandlePlatformsDialogConfirmed;
                platformsDialog.CancelRequested += HandlePlatformsDialogCancelRequested;
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
            if (sceneSettingsDialog != null) {
                sceneSettingsDialog.ConfirmRequested += HandleSceneSettingsDialogConfirmed;
                sceneSettingsDialog.CancelRequested += HandleSceneSettingsDialogCanceled;
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
            if (platformsDialog != null) {
                platformsDialog.ConfirmRequested -= HandlePlatformsDialogConfirmed;
                platformsDialog.CancelRequested -= HandlePlatformsDialogCancelRequested;
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
            if (sceneSettingsDialog != null) {
                sceneSettingsDialog.ConfirmRequested -= HandleSceneSettingsDialogConfirmed;
                sceneSettingsDialog.CancelRequested -= HandleSceneSettingsDialogCanceled;
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
            if (platformsDialog != null) {
                platformsDialog.Hide();
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
            if (sceneSettingsDialog != null) {
                sceneSettingsDialog.Hide();
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
            platformsDialog = new PlatformsDialog(uiFont, CurrentUiMetrics);
            profilesDialog = new ProfilesDialog(uiFont, CurrentUiMetrics);
            buildDialog = new BuildDialog(uiFont, CurrentUiMetrics);
            buildDialogCopySettingsDialog = new BuildDialogCopySettingsDialog(uiFont, CurrentUiMetrics);
            unsavedChangesDialog = new UnsavedChangesDialog(uiFont, CurrentUiMetrics);
            sceneSettingsDialog = new SceneSettingsDialog(uiFont, CurrentUiMetrics);
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
            if (platformsDialog != null) {
                platformsDialog.Dispose();
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
            if (sceneSettingsDialog != null) {
                sceneSettingsDialog.Dispose();
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
                if (!dockable.Enabled || dockable is EditorViewport) {
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
            titleBar.SceneSettingsRequested -= HandleSceneSettingsRequested;
            titleBar.PreferencesRequested -= HandlePreferencesRequested;
            titleBar.BuildRequested -= HandleBuildRequested;
            titleBar.PlatformsRequested -= HandlePlatformsRequested;
            titleBar.ProfilesRequested -= HandleProfilesRequested;
            titleBar.BuildScriptsRequested -= HandleBuildScriptsRequested;
            titleBar.OpenInIDERequested -= HandleOpenInIDERequested;
            titleBar.ProjectMenuItemRequested -= HandleProjectMenuItemRequested;
            titleBar.UiMenuActionRequested -= HandleUiMenuActionRequested;
            titleBar.AddEmptyRequested -= HandleAddEmptyRequested;
            titleBar.AddCubeRequested -= HandleAddCubeRequested;
            titleBar.AddPlaneRequested -= HandleAddPlaneRequested;
            titleBar.AddCameraRequested -= HandleAddCameraRequested;
            titleBar.AddSpotLightRequested -= HandleAddSpotLightRequested;
            titleBar.AddPointLightRequested -= HandleAddPointLightRequested;
            titleBar.AddDirectionalLightRequested -= HandleAddDirectionalLightRequested;
            titleBar.AddAmbientLightRequested -= HandleAddAmbientLightRequested;
            DetachScaleSensitiveDialogHandlers();
            scriptHotReloadService.Dispose();
            IReadOnlyList<EditorViewport> viewports = GetViewportPanels();
            for (int index = 0; index < viewports.Count; index++) {
                viewports[index].ClearInputBlockers();
            }
            HideScaleSensitiveDialogs();
            shaderModuleManager.ShaderBuilt -= HandleShaderBuilt;
            shaderModuleManager.Dispose();
            DetachTrackedWorkspacePanelsForDispose();
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
        void InitializePanelRegistry() {
            PanelRegistry.Register(new EditorWorkspacePanelTypeDescriptor(
                ViewportPanelTypeId,
                "Viewport",
                new int2(CurrentUiMetrics.ScalePixels(640), CurrentUiMetrics.ScalePixels(360)),
                CreateViewportPanelController));
            PanelRegistry.Register(new EditorWorkspacePanelTypeDescriptor(
                SceneHierarchyPanelTypeId,
                "Scene",
                new int2(CurrentUiMetrics.ScalePixels(280), CurrentUiMetrics.ScalePixels(600)),
                CreateSceneHierarchyPanelController));
            PanelRegistry.Register(new EditorWorkspacePanelTypeDescriptor(
                AssetBrowserPanelTypeId,
                "Assets",
                new int2(CurrentUiMetrics.ScalePixels(500), CurrentUiMetrics.ScalePixels(240)),
                CreateAssetBrowserPanelController));
            PanelRegistry.Register(new EditorWorkspacePanelTypeDescriptor(
                PropertiesPanelTypeId,
                "Properties",
                new int2(CurrentUiMetrics.ScalePixels(280), CurrentUiMetrics.ScalePixels(600)),
                CreatePropertiesPanelController));
            PanelRegistry.Register(new EditorWorkspacePanelTypeDescriptor(
                LoggerPanelTypeId,
                "Logger",
                new int2(CurrentUiMetrics.ScalePixels(320), CurrentUiMetrics.ScalePixels(220)),
                CreateLoggerPanelController));
            PanelRegistry.Register(new EditorWorkspacePanelTypeDescriptor(
                PreviewPanelTypeId,
                "Preview",
                new int2(CurrentUiMetrics.ScalePixels(320), CurrentUiMetrics.ScalePixels(240)),
                CreatePreviewPanelController));
        }

        /// <summary>
        /// Handles UI workspace menu actions raised by the editor title bar.
        /// </summary>
        /// <param name="action">Action raised by the title bar UI menu.</param>
        void HandleUiMenuActionRequested(EditorTitleBarUiMenuAction action) {
            if (action == EditorTitleBarUiMenuAction.ShowViewport) {
                CreateWorkspacePanelInstance(ViewportPanelTypeId);
                return;
            }
            if (action == EditorTitleBarUiMenuAction.ShowSceneHierarchy) {
                CreateWorkspacePanelInstance(SceneHierarchyPanelTypeId);
                return;
            }
            if (action == EditorTitleBarUiMenuAction.ShowAssetBrowser) {
                CreateWorkspacePanelInstance(AssetBrowserPanelTypeId);
                return;
            }
            if (action == EditorTitleBarUiMenuAction.ShowProperties) {
                CreateWorkspacePanelInstance(PropertiesPanelTypeId);
                return;
            }
            if (action == EditorTitleBarUiMenuAction.ShowLogger) {
                CreateWorkspacePanelInstance(LoggerPanelTypeId);
                return;
            }
            if (action == EditorTitleBarUiMenuAction.ShowPreview) {
                CreateWorkspacePanelInstance(PreviewPanelTypeId);
                return;
            }
            if (TryResolveWorkspaceSlotNumber(action, out int slotNumber)) {
                if (IsWorkspaceSaveAction(action)) {
                    SaveWorkspaceSlot(slotNumber);
                    return;
                }

                LoadWorkspaceSlot(slotNumber);
            }
        }

        /// <summary>
        /// Creates one tracked workspace panel instance for the requested type.
        /// </summary>
        /// <param name="panelTypeId">Stable panel type identifier.</param>
        /// <returns>Created live panel instance.</returns>
        EditorWorkspacePanelInstance CreateWorkspacePanelInstance(string panelTypeId) {
            return CreateWorkspacePanelInstance(panelTypeId, Guid.NewGuid().ToString("N"));
        }

        /// <summary>
        /// Creates one tracked workspace panel instance for the requested type and stable instance identifier.
        /// </summary>
        /// <param name="panelTypeId">Stable panel type identifier.</param>
        /// <param name="instanceId">Stable instance identifier used by workspace persistence.</param>
        /// <returns>Created live panel instance.</returns>
        EditorWorkspacePanelInstance CreateWorkspacePanelInstance(string panelTypeId, string instanceId) {
            EditorWorkspacePanelTypeDescriptor descriptor = PanelRegistry.GetDescriptor(panelTypeId);
            IEditorWorkspacePanelController controller = descriptor.CreateController(this);
            Action closeRequestedHandler = () => HandleWorkspacePanelCloseRequested(controller.Dockable);
            EditorWorkspacePanelInstance instance = new EditorWorkspacePanelInstance(
                instanceId,
                descriptor.PanelTypeId,
                descriptor.DisplayTitle,
                controller,
                closeRequestedHandler);

            AttachWorkspacePanelInstance(instance, descriptor.DefaultSize, true);
            controller.Dockable.Position = ResolveCenteredFloatingPanelPosition(descriptor.DefaultSize);
            InitializeWorkspacePanelInstance(instance);
            return instance;
        }

        /// <summary>
        /// Registers one existing panel instance created during session startup with the workspace tracking system.
        /// </summary>
        /// <param name="panelTypeId">Stable panel type identifier.</param>
        /// <param name="instanceId">Stable instance identifier used by workspace persistence.</param>
        /// <param name="controller">Controller that owns the existing dockable panel instance.</param>
        void RegisterExistingWorkspacePanelInstance(string panelTypeId, string instanceId, IEditorWorkspacePanelController controller) {
            if (controller == null) {
                throw new ArgumentNullException(nameof(controller));
            }

            EditorWorkspacePanelTypeDescriptor descriptor = PanelRegistry.GetDescriptor(panelTypeId);
            Action closeRequestedHandler = () => HandleWorkspacePanelCloseRequested(controller.Dockable);
            EditorWorkspacePanelInstance instance = new EditorWorkspacePanelInstance(
                instanceId,
                panelTypeId,
                descriptor.DisplayTitle,
                controller,
                closeRequestedHandler);

            AttachWorkspacePanelInstance(instance, controller.Dockable.Size, false);
            InitializeWorkspacePanelInstance(instance);
        }

        /// <summary>
        /// Attaches one workspace panel instance to tracking, focus, and close handling.
        /// </summary>
        /// <param name="instance">Workspace panel instance to attach.</param>
        /// <param name="size">Initial size assigned to the dockable panel.</param>
        /// <param name="addToLayout">True when the panel should be added to the dock layout tracking list.</param>
        void AttachWorkspacePanelInstance(EditorWorkspacePanelInstance instance, int2 size, bool addToLayout) {
            if (instance == null) {
                throw new ArgumentNullException(nameof(instance));
            }

            instance.Dockable.Title = instance.DisplayTitle;
            instance.Dockable.Size = size;
            instance.Dockable.Enabled = true;
            instance.Dockable.CloseRequested += instance.CloseRequestedHandler;
            WireWorkspacePanelEvents(instance);
            if (addToLayout) {
                dockingManager.Layout.Add(instance.Dockable);
            }

            EditorKeyboardFocusService.RegisterGroup(instance.Dockable);
            PanelInstances.Add(instance);
            RefreshWorkspaceDockOrder();
        }

        /// <summary>
        /// Applies initial session state to one newly attached workspace panel instance.
        /// </summary>
        /// <param name="instance">Workspace panel instance to initialize.</param>
        void InitializeWorkspacePanelInstance(EditorWorkspacePanelInstance instance) {
            if (instance == null) {
                throw new ArgumentNullException(nameof(instance));
            }

            if (string.Equals(instance.PanelTypeId, SceneHierarchyPanelTypeId, StringComparison.OrdinalIgnoreCase)) {
                ((SceneHierarchyPanel)instance.Dockable).RefreshHierarchy();
                return;
            }
            if (string.Equals(instance.PanelTypeId, AssetBrowserPanelTypeId, StringComparison.OrdinalIgnoreCase)) {
                ((AssetBrowserPanel)instance.Dockable).RefreshEntries();
                return;
            }
            if (string.Equals(instance.PanelTypeId, PropertiesPanelTypeId, StringComparison.OrdinalIgnoreCase)) {
                UpdatePropertiesPanelState((PropertiesPanel)instance.Dockable);
                return;
            }
            if (string.Equals(instance.PanelTypeId, PreviewPanelTypeId, StringComparison.OrdinalIgnoreCase)) {
                UpdatePreviewPanelState((PreviewPanel)instance.Dockable);
            }
        }

        /// <summary>
        /// Wires session-level event handlers for one tracked workspace panel instance.
        /// </summary>
        /// <param name="instance">Workspace panel instance to wire.</param>
        void WireWorkspacePanelEvents(EditorWorkspacePanelInstance instance) {
            if (instance == null) {
                throw new ArgumentNullException(nameof(instance));
            }

            if (string.Equals(instance.PanelTypeId, SceneHierarchyPanelTypeId, StringComparison.OrdinalIgnoreCase)) {
                ((SceneHierarchyPanel)instance.Dockable).ReparentRequested += HandleSceneHierarchyReparentRequested;
                return;
            }
            if (string.Equals(instance.PanelTypeId, AssetBrowserPanelTypeId, StringComparison.OrdinalIgnoreCase)) {
                AssetBrowserPanel panel = (AssetBrowserPanel)instance.Dockable;
                panel.AssetSelected += HandleAssetSelected;
                panel.SelectionCleared += HandleAssetSelectionCleared;
                panel.AddToSceneRequested += HandleAddToSceneRequested;
                return;
            }
            if (string.Equals(instance.PanelTypeId, ViewportPanelTypeId, StringComparison.OrdinalIgnoreCase)) {
                ((EditorViewport)instance.Dockable).ViewportContentFocusedChanged += HandleViewportContentFocusedChanged;
                return;
            }
            if (string.Equals(instance.PanelTypeId, PropertiesPanelTypeId, StringComparison.OrdinalIgnoreCase)) {
                ((PropertiesPanel)instance.Dockable).ImportSettingsApplyRequested += HandleImportSettingsApplyRequested;
            }
        }

        /// <summary>
        /// Unwires session-level event handlers for one tracked workspace panel instance.
        /// </summary>
        /// <param name="instance">Workspace panel instance to unwire.</param>
        void UnwireWorkspacePanelEvents(EditorWorkspacePanelInstance instance) {
            if (instance == null) {
                throw new ArgumentNullException(nameof(instance));
            }

            if (string.Equals(instance.PanelTypeId, SceneHierarchyPanelTypeId, StringComparison.OrdinalIgnoreCase)) {
                ((SceneHierarchyPanel)instance.Dockable).ReparentRequested -= HandleSceneHierarchyReparentRequested;
                return;
            }
            if (string.Equals(instance.PanelTypeId, AssetBrowserPanelTypeId, StringComparison.OrdinalIgnoreCase)) {
                AssetBrowserPanel panel = (AssetBrowserPanel)instance.Dockable;
                panel.AssetSelected -= HandleAssetSelected;
                panel.SelectionCleared -= HandleAssetSelectionCleared;
                panel.AddToSceneRequested -= HandleAddToSceneRequested;
                return;
            }
            if (string.Equals(instance.PanelTypeId, ViewportPanelTypeId, StringComparison.OrdinalIgnoreCase)) {
                ((EditorViewport)instance.Dockable).ViewportContentFocusedChanged -= HandleViewportContentFocusedChanged;
                return;
            }
            if (string.Equals(instance.PanelTypeId, PropertiesPanelTypeId, StringComparison.OrdinalIgnoreCase)) {
                ((PropertiesPanel)instance.Dockable).ImportSettingsApplyRequested -= HandleImportSettingsApplyRequested;
            }
        }

        /// <summary>
        /// Creates the primary viewport through the same workspace-controller path used by duplicate viewport panels.
        /// </summary>
        /// <returns>Viewport workspace controller that owns the primary viewport runtime stack.</returns>
        ViewportWorkspacePanelController CreatePrimaryViewportController() {
            return (ViewportWorkspacePanelController)CreateViewportPanelController(this);
        }

        /// <summary>
        /// Creates one viewport panel controller for the workspace system.
        /// </summary>
        /// <param name="session">Owning editor session.</param>
        /// <returns>Created viewport panel controller.</returns>
        IEditorWorkspacePanelController CreateViewportPanelController(EditorSession session) {
            return new ViewportWorkspacePanelController(
                session.uiFont,
                session.SnapModifierFont,
                session.ViewportToolbarIcons,
                session.sceneCanvasProfileState,
                session.CurrentUiMetrics);
        }

        /// <summary>
        /// Creates one scene hierarchy panel controller for the workspace system.
        /// </summary>
        /// <param name="session">Owning editor session.</param>
        /// <returns>Created scene hierarchy panel controller.</returns>
        IEditorWorkspacePanelController CreateSceneHierarchyPanelController(EditorSession session) {
            SceneHierarchyPanel panel = new SceneHierarchyPanel(session.uiFont, session.CurrentUiMetrics);
            return new SessionWorkspacePanelController(panel, SessionWorkspacePanelController.NoState, SessionWorkspacePanelController.NoRestore, panel.Detach);
        }

        /// <summary>
        /// Creates one asset browser panel controller for the workspace system.
        /// </summary>
        /// <param name="session">Owning editor session.</param>
        /// <returns>Created asset browser panel controller.</returns>
        IEditorWorkspacePanelController CreateAssetBrowserPanelController(EditorSession session) {
            AssetBrowserPanel panel = new AssetBrowserPanel(session.uiFont, session.projectPath, session.CurrentUiMetrics);
            return new SessionWorkspacePanelController(panel, SessionWorkspacePanelController.NoState, SessionWorkspacePanelController.NoRestore, SessionWorkspacePanelController.NoDispose);
        }

        /// <summary>
        /// Creates one properties panel controller for the workspace system.
        /// </summary>
        /// <param name="session">Owning editor session.</param>
        /// <returns>Created properties panel controller.</returns>
        IEditorWorkspacePanelController CreatePropertiesPanelController(EditorSession session) {
            EditorFileSystemModelResolver fileSystemModelResolver = new EditorFileSystemModelResolver(session.assetImportManager);
            EditorFileSystemFontResolver fileSystemFontResolver = new EditorFileSystemFontResolver(session.assetImportManager);
            PropertiesPanel panel = new PropertiesPanel(
                session.uiFont,
                session.EditorContentManager,
                fileSystemModelResolver,
                session.titleBar.Entity,
                session.scriptHotReloadService,
                session.CurrentUiMetrics,
                fileSystemFontResolver);
            return new SessionWorkspacePanelController(panel, SessionWorkspacePanelController.NoState, SessionWorkspacePanelController.NoRestore, SessionWorkspacePanelController.NoDispose);
        }

        /// <summary>
        /// Creates one logger panel controller for the workspace system.
        /// </summary>
        /// <param name="session">Owning editor session.</param>
        /// <returns>Created logger panel controller.</returns>
        IEditorWorkspacePanelController CreateLoggerPanelController(EditorSession session) {
            LoggerPanel panel = new LoggerPanel(session.uiFont, session.CurrentUiMetrics);
            return new SessionWorkspacePanelController(panel, SessionWorkspacePanelController.NoState, SessionWorkspacePanelController.NoRestore, panel.Detach);
        }

        /// <summary>
        /// Creates one preview panel controller for the workspace system.
        /// </summary>
        /// <param name="session">Owning editor session.</param>
        /// <returns>Created preview panel controller.</returns>
        IEditorWorkspacePanelController CreatePreviewPanelController(EditorSession session) {
            PreviewPanel panel = new PreviewPanel(session.uiFont, session.CurrentUiMetrics);
            return CreatePreviewPanelSessionController(panel);
        }

        /// <summary>
        /// Creates one workspace controller that persists preview-panel lock state for the supplied panel instance.
        /// </summary>
        /// <param name="panel">Preview panel whose workspace state should be managed.</param>
        /// <returns>Workspace controller that round-trips preview-panel state.</returns>
        IEditorWorkspacePanelController CreatePreviewPanelSessionController(PreviewPanel panel) {
            if (panel == null) {
                throw new ArgumentNullException(nameof(panel));
            }

            return new SessionWorkspacePanelController(
                panel,
                panel.CaptureState,
                panel.RestoreState,
                SessionWorkspacePanelController.NoDispose);
        }

        /// <summary>
        /// Handles close requests raised by tracked workspace panel instances.
        /// </summary>
        /// <param name="dockable">Dockable entity requesting closure.</param>
        void HandleWorkspacePanelCloseRequested(DockableEntity dockable) {
            CloseWorkspacePanel(dockable);
        }

        /// <summary>
        /// Closes one tracked workspace panel and releases its resources.
        /// </summary>
        /// <param name="dockable">Dockable entity to close.</param>
        void CloseWorkspacePanel(DockableEntity dockable) {
            if (dockable == null) {
                throw new ArgumentNullException(nameof(dockable));
            }

            EditorWorkspacePanelInstance instance = PanelInstances.FirstOrDefault(candidate => ReferenceEquals(candidate.Dockable, dockable));
            if (instance == null) {
                return;
            }

            dockable.CloseRequested -= instance.CloseRequestedHandler;
            UnwireWorkspacePanelEvents(instance);
            dockingManager.Layout.Remove(dockable);
            dockable.Enabled = false;
            EditorKeyboardFocusService.UnregisterGroup(dockable);
            if (ReferenceEquals(LastFocusedViewportInstance, instance)) {
                LastFocusedViewportInstance = null;
            }
            PanelInstances.Remove(instance);
            instance.Controller.Dispose();
            RefreshWorkspaceDockOrder();
        }

        /// <summary>
        /// Saves the current tracked workspace state into one slot.
        /// </summary>
        /// <param name="slotNumber">One-based workspace slot number.</param>
        void SaveWorkspaceSlot(int slotNumber) {
            WorkspaceLayoutService.SaveSlot(slotNumber, CaptureWorkspaceSlotDocument());
        }

        /// <summary>
        /// Loads one previously saved workspace slot into the current session.
        /// </summary>
        /// <param name="slotNumber">One-based workspace slot number.</param>
        void LoadWorkspaceSlot(int slotNumber) {
            EditorWorkspaceSlotDocument slot = WorkspaceLayoutService.LoadSlot(slotNumber);
            if (slot == null) {
                return;
            }

            RestoreWorkspaceSlot(slot);
        }

        /// <summary>
        /// Captures the current tracked workspace state into one serializable slot document.
        /// </summary>
        /// <returns>Serializable slot document for the current workspace state.</returns>
        EditorWorkspaceSlotDocument CaptureWorkspaceSlotDocument() {
            EditorWorkspaceSlotDocument slot = new EditorWorkspaceSlotDocument();
            slot.SchemaVersion = 1;

            for (int index = 0; index < PanelInstances.Count; index++) {
                EditorWorkspacePanelInstance instance = PanelInstances[index];
                slot.Panels.Add(new EditorWorkspacePanelDocument {
                    InstanceId = instance.InstanceId,
                    PanelTypeId = instance.PanelTypeId,
                    IsDocked = instance.Dockable.IsDocked,
                    Title = instance.Dockable.Title,
                    State = instance.Controller.CaptureState()
                });
                if (!instance.Dockable.IsDocked) {
                    slot.FloatingPanels.Add(new EditorWorkspaceFloatingPanelDocument {
                        InstanceId = instance.InstanceId,
                        X = (int)Math.Round(instance.Dockable.Position.X),
                        Y = (int)Math.Round(instance.Dockable.Position.Y),
                        Width = instance.Dockable.Size.X,
                        Height = instance.Dockable.Size.Y
                    });
                }
            }

            slot.DockRoot = ConvertDockSnapshotNodeToDocument(dockingManager.Layout.CaptureSnapshot(ResolveWorkspaceInstanceId).Root);
            return slot;
        }

        /// <summary>
        /// Restores the session workspace from one previously captured slot document.
        /// </summary>
        /// <param name="slot">Serializable slot document to restore.</param>
        void RestoreWorkspaceSlot(EditorWorkspaceSlotDocument slot) {
            if (slot == null) {
                throw new ArgumentNullException(nameof(slot));
            }

            Dictionary<string, EditorWorkspaceFloatingPanelDocument> floatingPanelsByInstanceId = slot.FloatingPanels.ToDictionary(panel => panel.InstanceId, StringComparer.OrdinalIgnoreCase);
            HashSet<string> restoredInstanceIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            CloseAllWorkspacePanels();

            for (int index = 0; index < slot.Panels.Count; index++) {
                EditorWorkspacePanelDocument panel = slot.Panels[index];
                if (!PanelRegistry.TryGetDescriptor(panel.PanelTypeId, out _)) {
                    continue;
                }

                EditorWorkspacePanelInstance instance = CreateWorkspacePanelInstance(panel.PanelTypeId, panel.InstanceId);
                restoredInstanceIds.Add(panel.InstanceId);
                instance.Controller.RestoreState(panel.State);
                instance.Dockable.Title = string.IsNullOrWhiteSpace(panel.Title) ? instance.DisplayTitle : panel.Title;

                if (floatingPanelsByInstanceId.TryGetValue(panel.InstanceId, out EditorWorkspaceFloatingPanelDocument floatingPanel)) {
                    instance.Dockable.Position = new float3(floatingPanel.X, floatingPanel.Y, 0f);
                    instance.Dockable.Size = new int2(floatingPanel.Width, floatingPanel.Height);
                }
            }

            dockingManager.Layout.RestoreSnapshot(
                new EditorWorkspaceDockSnapshot {
                    Root = ConvertDockDocumentNodeToSnapshot(FilterDockDocumentNode(slot.DockRoot, restoredInstanceIds))
                },
                ResolveWorkspaceDockable);
            RefreshWorkspaceDockOrder();
            RefreshPreviewSource();
        }

        /// <summary>
        /// Closes all currently tracked workspace panel instances.
        /// </summary>
        void CloseAllWorkspacePanels() {
            EditorWorkspacePanelInstance[] instances = PanelInstances.ToArray();
            for (int index = 0; index < instances.Length; index++) {
                CloseWorkspacePanel(instances[index].Dockable);
            }
        }

        /// <summary>
        /// Detaches every still-tracked workspace panel instance during session disposal.
        /// </summary>
        void DetachTrackedWorkspacePanelsForDispose() {
            EditorWorkspacePanelInstance[] instances = PanelInstances.ToArray();
            for (int index = 0; index < instances.Length; index++) {
                UnwireWorkspacePanelEvents(instances[index]);
                EditorKeyboardFocusService.UnregisterGroup(instances[index].Dockable);
                instances[index].Controller.Dispose();
            }
        }

        /// <summary>
        /// Resolves one tracked dockable back to its stable workspace instance identifier.
        /// </summary>
        /// <param name="dockable">Tracked dockable entity.</param>
        /// <returns>Stable workspace instance identifier.</returns>
        string ResolveWorkspaceInstanceId(DockableEntity dockable) {
            EditorWorkspacePanelInstance instance = PanelInstances.FirstOrDefault(candidate => ReferenceEquals(candidate.Dockable, dockable));
            if (instance == null) {
                throw new InvalidOperationException("Dockable is not tracked by the workspace panel system.");
            }

            return instance.InstanceId;
        }

        /// <summary>
        /// Resolves one tracked workspace instance identifier back to its dockable entity.
        /// </summary>
        /// <param name="instanceId">Stable workspace instance identifier.</param>
        /// <returns>Tracked dockable entity.</returns>
        DockableEntity ResolveWorkspaceDockable(string instanceId) {
            EditorWorkspacePanelInstance instance = PanelInstances.FirstOrDefault(candidate => string.Equals(candidate.InstanceId, instanceId, StringComparison.OrdinalIgnoreCase));
            if (instance == null) {
                throw new InvalidOperationException($"Workspace instance '{instanceId}' is not tracked by the current session.");
            }

            return instance.Dockable;
        }

        /// <summary>
        /// Filters one persisted dock tree so it contains only workspace panel instances restored in the current session.
        /// </summary>
        /// <param name="node">Persisted dock node to filter.</param>
        /// <param name="restoredInstanceIds">Stable panel instance identifiers restored in the current session.</param>
        /// <returns>Filtered dock node, or null when the node no longer contains any restored panel instances.</returns>
        EditorWorkspaceDockNodeDocument FilterDockDocumentNode(EditorWorkspaceDockNodeDocument node, HashSet<string> restoredInstanceIds) {
            if (node == null) {
                return null;
            }

            if (restoredInstanceIds == null) {
                throw new ArgumentNullException(nameof(restoredInstanceIds));
            }

            if (node is EditorWorkspaceDockLeafNodeDocument leaf) {
                List<string> filteredInstanceIds = leaf.InstanceIds
                    .Where(instanceId => restoredInstanceIds.Contains(instanceId))
                    .ToList();
                if (filteredInstanceIds.Count == 0) {
                    return null;
                }

                string activeInstanceId = filteredInstanceIds.Contains(leaf.ActiveInstanceId) ? leaf.ActiveInstanceId : filteredInstanceIds[0];
                return new EditorWorkspaceDockLeafNodeDocument {
                    InstanceIds = filteredInstanceIds,
                    ActiveInstanceId = activeInstanceId
                };
            }

            if (node is EditorWorkspaceDockSplitNodeDocument split) {
                EditorWorkspaceDockNodeDocument first = FilterDockDocumentNode(split.First, restoredInstanceIds);
                EditorWorkspaceDockNodeDocument second = FilterDockDocumentNode(split.Second, restoredInstanceIds);
                if (first == null && second == null) {
                    return null;
                }
                if (first == null) {
                    return second;
                }
                if (second == null) {
                    return first;
                }

                return new EditorWorkspaceDockSplitNodeDocument {
                    IsVertical = split.IsVertical,
                    SplitFraction = split.SplitFraction,
                    First = first,
                    Second = second
                };
            }

            throw new InvalidOperationException("Unsupported workspace dock document node type.");
        }

        /// <summary>
        /// Converts one captured dock snapshot node into its persisted document counterpart.
        /// </summary>
        /// <param name="node">Captured dock snapshot node.</param>
        /// <returns>Persisted dock node document.</returns>
        EditorWorkspaceDockNodeDocument ConvertDockSnapshotNodeToDocument(EditorWorkspaceDockNodeSnapshot node) {
            if (node == null) {
                return null;
            }
            if (node is EditorWorkspaceDockLeafSnapshot leafSnapshot) {
                return new EditorWorkspaceDockLeafNodeDocument {
                    ActiveInstanceId = leafSnapshot.ActiveInstanceId,
                    InstanceIds = new List<string>(leafSnapshot.InstanceIds)
                };
            }

            EditorWorkspaceDockSplitSnapshot splitSnapshot = node as EditorWorkspaceDockSplitSnapshot;
            if (splitSnapshot == null) {
                throw new InvalidOperationException("Unsupported dock snapshot node type.");
            }

            return new EditorWorkspaceDockSplitNodeDocument {
                IsVertical = splitSnapshot.IsVertical,
                SplitFraction = splitSnapshot.SplitFraction,
                First = ConvertDockSnapshotNodeToDocument(splitSnapshot.First),
                Second = ConvertDockSnapshotNodeToDocument(splitSnapshot.Second)
            };
        }

        /// <summary>
        /// Converts one persisted dock node document back into its runtime snapshot counterpart.
        /// </summary>
        /// <param name="node">Persisted dock node document.</param>
        /// <returns>Runtime dock snapshot node.</returns>
        EditorWorkspaceDockNodeSnapshot ConvertDockDocumentNodeToSnapshot(EditorWorkspaceDockNodeDocument node) {
            if (node == null) {
                return null;
            }
            if (node is EditorWorkspaceDockLeafNodeDocument leafDocument) {
                return new EditorWorkspaceDockLeafSnapshot {
                    ActiveInstanceId = leafDocument.ActiveInstanceId,
                    InstanceIds = new List<string>(leafDocument.InstanceIds)
                };
            }

            EditorWorkspaceDockSplitNodeDocument splitDocument = node as EditorWorkspaceDockSplitNodeDocument;
            if (splitDocument == null) {
                throw new InvalidOperationException("Unsupported dock document node type.");
            }

            return new EditorWorkspaceDockSplitSnapshot {
                IsVertical = splitDocument.IsVertical,
                SplitFraction = splitDocument.SplitFraction,
                First = ConvertDockDocumentNodeToSnapshot(splitDocument.First),
                Second = ConvertDockDocumentNodeToSnapshot(splitDocument.Second)
            };
        }

        /// <summary>
        /// Resolves the slot number associated with one UI menu action.
        /// </summary>
        /// <param name="action">Workspace UI menu action.</param>
        /// <param name="slotNumber">Resolved one-based slot number when successful.</param>
        /// <returns>True when the action maps to a slot; otherwise false.</returns>
        bool TryResolveWorkspaceSlotNumber(EditorTitleBarUiMenuAction action, out int slotNumber) {
            slotNumber = 0;
            if (action == EditorTitleBarUiMenuAction.SaveSlot1 || action == EditorTitleBarUiMenuAction.LoadSlot1) {
                slotNumber = 1;
                return true;
            }
            if (action == EditorTitleBarUiMenuAction.SaveSlot2 || action == EditorTitleBarUiMenuAction.LoadSlot2) {
                slotNumber = 2;
                return true;
            }
            if (action == EditorTitleBarUiMenuAction.SaveSlot3 || action == EditorTitleBarUiMenuAction.LoadSlot3) {
                slotNumber = 3;
                return true;
            }
            if (action == EditorTitleBarUiMenuAction.SaveSlot4 || action == EditorTitleBarUiMenuAction.LoadSlot4) {
                slotNumber = 4;
                return true;
            }
            if (action == EditorTitleBarUiMenuAction.SaveSlot5 || action == EditorTitleBarUiMenuAction.LoadSlot5) {
                slotNumber = 5;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Determines whether one workspace UI menu action saves a slot instead of loading it.
        /// </summary>
        /// <param name="action">Workspace UI menu action.</param>
        /// <returns>True when the action saves a slot; otherwise false.</returns>
        bool IsWorkspaceSaveAction(EditorTitleBarUiMenuAction action) {
            return action == EditorTitleBarUiMenuAction.SaveSlot1 ||
                   action == EditorTitleBarUiMenuAction.SaveSlot2 ||
                   action == EditorTitleBarUiMenuAction.SaveSlot3 ||
                   action == EditorTitleBarUiMenuAction.SaveSlot4 ||
                   action == EditorTitleBarUiMenuAction.SaveSlot5;
        }

        /// <summary>
        /// Resolves the default centered floating position for one newly created panel instance.
        /// </summary>
        /// <param name="panelSize">Requested panel size.</param>
        /// <returns>Centered floating origin inside the available workspace area.</returns>
        float3 ResolveCenteredFloatingPanelPosition(int2 panelSize) {
            if (LastLayoutWidth <= 0 || LastLayoutHeight <= 0) {
                return float3.Zero;
            }

            int titleBarHeight = titleBar == null ? 0 : titleBar.Height;
            int availableHeight = Math.Max(0, LastLayoutHeight - titleBarHeight);
            int x = Math.Max(0, (LastLayoutWidth - panelSize.X) / 2);
            int y = titleBarHeight + Math.Max(0, (availableHeight - panelSize.Y) / 2);
            return new float3(x, y, 0f);
        }

        /// <summary>
        /// Refreshes keyboard traversal order from the current dock layout.
        /// </summary>
        void RefreshWorkspaceDockOrder() {
            EditorKeyboardFocusService.SetDockOrder(dockingManager.Layout.GetVisibleDockablesInTraversalOrder());
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
        /// Handles the Add Ambient Light command from the editor title bar.
        /// </summary>
        void HandleAddAmbientLightRequested() {
            CreateAndSelectSceneEntity(SceneCreationService.CreateAmbientLight);
        }

        /// <summary>
        /// Handles the asset-browser Add to scene request for one model entry.
        /// </summary>
        /// <param name="entry">Model asset entry selected in the asset browser.</param>
        void HandleAddToSceneRequested(AssetBrowserEntry entry) {
            if (entry == null) {
                throw new ArgumentNullException(nameof(entry));
            }
            if (entry.IsDirectory || entry.EntryKind != AssetEntryKind.Model) {
                return;
            }

            CreateAndSelectSceneEntity(() => CreateModelSceneEntity(entry));
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
                RefreshHierarchy();
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
        /// Handles the editor-global Ctrl+S shortcut by routing into the existing Save Map flow when editor-global input is not blocked.
        /// </summary>
        void HandleGlobalSaveShortcut() {
            if (unsavedChangesDialog != null && unsavedChangesDialog.Enabled) {
                return;
            }

            if (saveFileDialog != null && saveFileDialog.Enabled) {
                return;
            }

            if (openFileDialog != null && openFileDialog.Enabled) {
                return;
            }

            if (reparentEntityDialog != null && reparentEntityDialog.Enabled) {
                return;
            }

            if (platformsDialog != null && platformsDialog.Enabled) {
                return;
            }

            if (profilesDialog != null && profilesDialog.Enabled) {
                return;
            }

            if (buildDialog != null && buildDialog.Enabled) {
                return;
            }

            if (buildDialogCopySettingsDialog != null && buildDialogCopySettingsDialog.Enabled) {
                return;
            }

            if (sceneSettingsDialog != null && sceneSettingsDialog.Enabled) {
                return;
            }

            if (preferencesDialog != null && preferencesDialog.Enabled) {
                return;
            }

            if (assetPickerModal != null && assetPickerModal.Enabled) {
                return;
            }

            HandleSaveMapRequested();
        }

        /// <summary>
        /// Handles the main `Save Map As...` command from the editor title bar.
        /// </summary>
        void HandleSaveMapAsRequested() {
            ShowSceneSaveDialog();
        }

        /// <summary>
        /// Opens the editor preferences dialog using the current editor-global preferences.
        /// </summary>
        void HandlePreferencesRequested() {
            preferencesDialog.Show(CurrentEditorPreferences);
        }

        /// <summary>
        /// Opens the scene settings dialog using the current scene-owned canvas profile.
        /// </summary>
        void HandleSceneSettingsRequested() {
            sceneSettingsDialog.Show(CurrentSceneSettings);
        }

        /// <summary>
        /// Applies one confirmed editor-global preferences selection and notifies the host.
        /// </summary>
        /// <param name="settings">Confirmed editor-global preferences settings.</param>
        void HandlePreferencesDialogConfirmed(EditorPreferencesSettings settings) {
            if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            }

            CurrentEditorPreferences = settings;
            CurrentUiScaleSettings = settings.UiScale;
            CurrentThemeId = settings.ThemeId;
            ApplyEditorTheme(CurrentThemeId);
            preferencesDialog.Hide();
            if (PreferencesChanged != null) {
                PreferencesChanged(settings);
            }
            if (UiScaleSettingsChanged != null) {
                UiScaleSettingsChanged(settings.UiScale);
            }
        }

        /// <summary>
        /// Cancels the preferences workflow and hides the dialog.
        /// </summary>
        void HandlePreferencesDialogCanceled() {
            preferencesDialog.Hide();
        }

        /// <summary>
        /// Applies confirmed scene settings and marks the current scene dirty when the canvas profile changed.
        /// </summary>
        /// <param name="sceneSettings">Confirmed scene settings payload.</param>
        void HandleSceneSettingsDialogConfirmed(SceneSettingsAsset sceneSettings) {
            if (sceneSettings == null) {
                throw new ArgumentNullException(nameof(sceneSettings));
            }

            bool settingsChanged = !AreSceneSettingsEquivalent(CurrentSceneSettings, sceneSettings);
            CurrentSceneSettings = sceneSettings;
            sceneCanvasProfileState.ApplySceneSettings(CurrentSceneSettings);
            sceneSettingsDialog.Hide();
            if (settingsChanged) {
                EditorSceneMutationService.MarkSceneMutated();
            }
        }

        /// <summary>
        /// Cancels the scene settings workflow and hides the dialog.
        /// </summary>
        void HandleSceneSettingsDialogCanceled() {
            sceneSettingsDialog.Hide();
        }

        /// <summary>
        /// Opens Platforms using the currently available platforms for the active engine version.
        /// </summary>
        void HandlePlatformsRequested() {
            EditorProjectPlatformsDocument projectPlatforms = projectPlatformsService.Load();
            IReadOnlyList<string> availablePlatforms = ResolveInstalledPlatformIds();
            if (buildDialogCopySettingsDialog != null) {
                buildDialogCopySettingsDialog.Hide();
            }
            if (platformsDialog != null) {
                platformsDialog.Show(availablePlatforms, projectPlatforms.SupportedPlatforms, ActiveProjectPlatform);
            }
        }

        /// <summary>
        /// Applies one confirmed Platforms selection to project settings and local active-platform state.
        /// </summary>
        /// <param name="selection">Supported-platform selection confirmed by the dialog.</param>
        void HandlePlatformsDialogConfirmed(PlatformsSelection selection) {
            if (selection == null) {
                throw new ArgumentNullException(nameof(selection));
            }

            projectPlatformsService.Save(new EditorProjectPlatformsDocument {
                SupportedPlatforms = new List<string>(selection.SupportedPlatformIds)
            });
            ProjectSupportedPlatforms = selection.SupportedPlatformIds.ToArray();
            ProjectLocalSettingsService = new EditorProjectLocalSettingsService(projectPath, ProjectSupportedPlatforms);
            SetActiveProjectPlatform(selection.ActivePlatformId);
            platformsDialog.Hide();
        }

        /// <summary>
        /// Cancels the Platforms workflow and hides the dialog.
        /// </summary>
        void HandlePlatformsDialogCancelRequested() {
            if (!CanUseProjectPlatform(ActiveProjectPlatform)) {
                HandlePlatformsRequested();
                return;
            }

            platformsDialog.Hide();
        }

        /// <summary>
        /// Opens the profiles dialog using the current active platform and persisted profile settings.
        /// </summary>
        void HandleProfilesRequested() {
            IReadOnlyList<string> visiblePlatformIds = ResolveVisibleSupportedPlatforms();
            if (visiblePlatformIds.Count < 1) {
                return;
            }

            string dialogPlatformId = ResolveVisiblePlatformId(visiblePlatformIds, ActiveProjectPlatform);
            EditorProfileSettingsDocument profileSettings = profileSettingsService.Load(visiblePlatformIds);
            buildDialogCopySettingsDialog.Hide();
            profilesDialog.Show(profileSettings, visiblePlatformIds, dialogPlatformId, ResolvePlatformSelectionModel);
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
            IReadOnlyList<string> visiblePlatformIds = ResolveVisibleSupportedPlatforms();
            if (visiblePlatformIds.Count < 1) {
                return;
            }

            string dialogPlatformId = ResolveVisiblePlatformId(visiblePlatformIds, ActiveProjectPlatform);
            IReadOnlyList<string> sceneIds = sceneCatalogService.GetSceneIds();
            string currentSceneId = sceneCatalogService.ResolveSceneId(CurrentScenePath);
            EditorBuildConfigDocument buildConfig = buildConfigService.Load(visiblePlatformIds, currentSceneId);
            buildDialogCopySettingsDialog.Hide();
            buildDialog.Show(visiblePlatformIds, sceneIds, dialogPlatformId, buildConfig, ResolvePlatformSelectionModel(dialogPlatformId));
        }

        /// <summary>
        /// Builds the generated scripting solution and reloads the resulting assembly.
        /// </summary>
        void HandleBuildScriptsRequested() {
            EditorBuildExecutionResult result = scriptHotReloadService.BuildAndReload();
            if (!result.Succeeded) {
                Logger.WriteError(result.Message);
                return;
            }

            try {
                RefreshProjectMenus();
                Logger.WriteLine(result.Message);
            } catch (Exception ex) {
                Logger.WriteError($"Project menu refresh failed: {ex.Message}");
            }
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
        /// Rebuilds the title-bar contributed project menus from the current loaded editor-module catalog.
        /// </summary>
        void RefreshProjectMenus() {
            IReadOnlyList<EditorMenuItemDescriptor> menuItems = scriptHotReloadService.GetAvailableEditorMenuItems();
            ValidateProjectMenuCommands(menuItems);
            titleBar.ApplyProjectMenus(menuItems);
        }

        /// <summary>
        /// Ensures every contributed menu item references one currently available project-authored editor command.
        /// </summary>
        /// <param name="menuItems">Contributed project menu descriptors to validate.</param>
        void ValidateProjectMenuCommands(IReadOnlyList<EditorMenuItemDescriptor> menuItems) {
            if (menuItems == null) {
                throw new ArgumentNullException(nameof(menuItems));
            }

            IReadOnlyList<EditorProjectCommandDescriptor> commands = scriptHotReloadService.GetAvailableEditorCommands();
            HashSet<string> commandIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < commands.Count; index++) {
                commandIds.Add(commands[index].CommandId);
            }

            for (int index = 0; index < menuItems.Count; index++) {
                EditorMenuItemDescriptor menuItem = menuItems[index];
                if (!commandIds.Contains(menuItem.CommandId)) {
                    throw new InvalidOperationException($"Project menu item '{menuItem.MenuItemId}' references unavailable editor command '{menuItem.CommandId}'.");
                }
            }
        }

        /// <summary>
        /// Handles viewport content focus changes so add-to-scene placement can follow the last clicked viewport.
        /// </summary>
        /// <param name="viewport">Viewport whose content focus changed.</param>
        /// <param name="isFocused">True when the viewport content just gained focus.</param>
        void HandleViewportContentFocusedChanged(EditorViewport viewport, bool isFocused) {
            if (viewport == null || !isFocused) {
                return;
            }

            EditorWorkspacePanelInstance instance = PanelInstances.FirstOrDefault(candidate => ReferenceEquals(candidate.Dockable, viewport));
            if (instance == null) {
                return;
            }

            LastFocusedViewportInstance = instance;
        }

        /// <summary>
        /// Creates one scene entity for a model asset entry using the resolved runtime model and imported materials.
        /// </summary>
        /// <param name="entry">Model entry selected in the asset browser.</param>
        /// <returns>Configured model scene entity.</returns>
        EditorEntity CreateModelSceneEntity(AssetBrowserEntry entry) {
            if (entry == null) {
                throw new ArgumentNullException(nameof(entry));
            }
            if (entry.IsDirectory || entry.EntryKind != AssetEntryKind.Model) {
                throw new InvalidOperationException("Only model assets can be added to the scene.");
            }

            SceneAssetReference modelReference = sceneAssetReferenceFactory.CreateFromEntry(entry);
            if (entry.IsGenerated) {
                RuntimeModel runtimeModel = GeneratedAssetProviderRegistry.ResolveRuntimeModel(entry);
                RuntimeMaterial standardMaterial = EngineGeneratedMaterialCache.GetRuntimeMaterial(EngineGeneratedMaterialCache.StandardAssetId);
                EditorEntity entity = SceneCreationService.CreateModel(
                    BuildModelEntityName(entry),
                    runtimeModel,
                    new[] { standardMaterial },
                    modelReference,
                    new[] { BuildGeneratedStandardMaterialReference() });
                entity.Position = ResolveAddToScenePlacementPosition();
                return entity;
            }

            ModelAssetImportSettings importSettings = assetImportManager.LoadOrCreateModelImportSettings(entry.FullPath);
            if (importSettings == null || importSettings.Importer == null || string.IsNullOrWhiteSpace(importSettings.Importer.ImporterId)) {
                throw new InvalidOperationException("Model import settings could not be resolved.");
            }

            ImportedModelAssetSet importedModel = EditorContentManager.Load<ImportedModelAssetSet>(entry.FullPath, importSettings.Importer.ImporterId);
            if (importedModel == null || importedModel.ModelAsset == null) {
                throw new InvalidOperationException("Model import did not produce a runtime model asset.");
            }

            RuntimeModel runtimeImportedModel = core.RenderManager3D.BuildModelFromRaw(importedModel.ModelAsset);
            RuntimeMaterial[] runtimeMaterials = ResolveImportedModelMaterials(entry, importedModel.GeneratedMaterials);
            SceneAssetReference[] materialReferences = BuildImportedModelMaterialReferences(entry, importedModel.GeneratedMaterials);
            if (runtimeMaterials.Length == 0) {
                runtimeMaterials = new[] { EngineGeneratedMaterialCache.GetRuntimeMaterial(EngineGeneratedMaterialCache.StandardAssetId) };
                materialReferences = new[] { BuildGeneratedStandardMaterialReference() };
            }

            EditorEntity importedEntity = SceneCreationService.CreateModel(
                BuildModelEntityName(entry),
                runtimeImportedModel,
                runtimeMaterials,
                modelReference,
                materialReferences);
            importedEntity.Position = ResolveAddToScenePlacementPosition();
            return importedEntity;
        }

        /// <summary>
        /// Resolves one model asset entry to the orbit target of the most recently focused viewport, or the primary viewport when no viewport has been focused yet.
        /// </summary>
        /// <returns>World-space orbit target used for model spawning.</returns>
        float3 ResolveAddToScenePlacementPosition() {
            ViewportWorkspacePanelController viewportController = GetFocusedViewportController();
            if (viewportController == null || viewportController.ViewportState.CameraController == null) {
                return float3.Zero;
            }

            return viewportController.ViewportState.CameraController.GetOrbitTarget();
        }

        /// <summary>
        /// Resolves the viewport controller that should own the current add-to-scene placement context.
        /// </summary>
        /// <returns>Focused viewport controller, or the primary viewport controller when no viewport has been focused.</returns>
        ViewportWorkspacePanelController GetFocusedViewportController() {
            EditorWorkspacePanelInstance focusedInstance = LastFocusedViewportInstance;
            if (focusedInstance != null && PanelInstances.Contains(focusedInstance) && string.Equals(focusedInstance.PanelTypeId, ViewportPanelTypeId, StringComparison.OrdinalIgnoreCase)) {
                return (ViewportWorkspacePanelController)focusedInstance.Controller;
            }

            if (mainViewport == null) {
                return null;
            }

            EditorWorkspacePanelInstance primaryInstance = PanelInstances.FirstOrDefault(candidate => ReferenceEquals(candidate.Dockable, mainViewport));
            if (primaryInstance == null) {
                return null;
            }

            return (ViewportWorkspacePanelController)primaryInstance.Controller;
        }

        /// <summary>
        /// Resolves imported runtime materials for one imported model entry.
        /// </summary>
        /// <param name="entry">Model browser entry that owns the imported materials.</param>
        /// <param name="generatedMaterials">Generated material records returned by the importer.</param>
        /// <returns>Runtime materials ordered by imported submesh slot.</returns>
        RuntimeMaterial[] ResolveImportedModelMaterials(AssetBrowserEntry entry, ImportedModelMaterialAsset[] generatedMaterials) {
            if (entry == null) {
                throw new ArgumentNullException(nameof(entry));
            }
            if (generatedMaterials == null) {
                throw new ArgumentNullException(nameof(generatedMaterials));
            }

            RuntimeMaterial[] runtimeMaterials = new RuntimeMaterial[generatedMaterials.Length];
            for (int index = 0; index < generatedMaterials.Length; index++) {
                ImportedModelMaterialAsset generatedMaterial = generatedMaterials[index];
                if (generatedMaterial == null) {
                    throw new InvalidOperationException("Imported model material entries cannot contain null values.");
                }

                runtimeMaterials[index] = sceneAssetReferenceResolver.ResolveMaterial(BuildImportedModelMaterialReference(entry, generatedMaterial));
            }

            return runtimeMaterials;
        }

        /// <summary>
        /// Builds the stable scene references used by imported model material slots.
        /// </summary>
        /// <param name="entry">Model browser entry that owns the imported materials.</param>
        /// <param name="generatedMaterials">Generated material records returned by the importer.</param>
        /// <returns>Scene asset references ordered by imported submesh slot.</returns>
        SceneAssetReference[] BuildImportedModelMaterialReferences(AssetBrowserEntry entry, ImportedModelMaterialAsset[] generatedMaterials) {
            if (entry == null) {
                throw new ArgumentNullException(nameof(entry));
            }
            if (generatedMaterials == null) {
                throw new ArgumentNullException(nameof(generatedMaterials));
            }

            SceneAssetReference[] references = new SceneAssetReference[generatedMaterials.Length];
            for (int index = 0; index < generatedMaterials.Length; index++) {
                ImportedModelMaterialAsset generatedMaterial = generatedMaterials[index];
                if (generatedMaterial == null) {
                    throw new InvalidOperationException("Imported model material entries cannot contain null values.");
                }

                references[index] = BuildImportedModelMaterialReference(entry, generatedMaterial);
            }

            return references;
        }

        /// <summary>
        /// Builds one stable scene reference for one imported model material asset.
        /// </summary>
        /// <param name="entry">Model browser entry that owns the imported material.</param>
        /// <param name="generatedMaterial">Generated material entry produced by the importer.</param>
        /// <returns>Scene asset reference for the generated material asset.</returns>
        SceneAssetReference BuildImportedModelMaterialReference(AssetBrowserEntry entry, ImportedModelMaterialAsset generatedMaterial) {
            if (entry == null) {
                throw new ArgumentNullException(nameof(entry));
            }
            if (generatedMaterial == null) {
                throw new ArgumentNullException(nameof(generatedMaterial));
            }

            string sourceDirectoryPath = Path.GetDirectoryName(entry.FullPath);
            if (string.IsNullOrWhiteSpace(sourceDirectoryPath)) {
                throw new InvalidOperationException("Model source directory could not be resolved.");
            }

            string materialFullPath = Path.GetFullPath(Path.Combine(sourceDirectoryPath, generatedMaterial.RelativeMaterialPath));
            string assetsRootPath = ResolveAssetsRootPath(projectPath);
            string relativePath = Path.GetRelativePath(assetsRootPath, materialFullPath).Replace('\\', '/');
            return new SceneAssetReference {
                SourceKind = SceneAssetReferenceSourceKind.FileSystem,
                RelativePath = relativePath
            };
        }

        /// <summary>
        /// Builds the default generated material reference used when an imported model does not provide any authored materials.
        /// </summary>
        /// <returns>Stable scene reference for the generated standard material.</returns>
        SceneAssetReference BuildGeneratedStandardMaterialReference() {
            return new SceneAssetReference {
                SourceKind = SceneAssetReferenceSourceKind.Generated,
                RelativePath = EngineGeneratedAssetProvider.StandardMaterialRelativePath,
                ProviderId = EngineGeneratedAssetProvider.ProviderIdValue,
                AssetId = EngineGeneratedMaterialCache.StandardAssetId
            };
        }

        /// <summary>
        /// Resolves one display name for a spawned model entity.
        /// </summary>
        /// <param name="entry">Asset browser entry being added.</param>
        /// <returns>Model entity name.</returns>
        string BuildModelEntityName(AssetBrowserEntry entry) {
            if (entry == null) {
                throw new ArgumentNullException(nameof(entry));
            }

            string candidateName = Path.GetFileNameWithoutExtension(entry.Name);
            if (string.IsNullOrWhiteSpace(candidateName)) {
                candidateName = entry.Name;
            }

            return candidateName;
        }

        /// <summary>
        /// Executes the command mapped to one contributed project menu item.
        /// </summary>
        /// <param name="menuItemId">Stable contributed project menu item identifier.</param>
        void HandleProjectMenuItemRequested(string menuItemId) {
            if (string.IsNullOrWhiteSpace(menuItemId)) {
                throw new ArgumentException("Project menu item id must be provided.", nameof(menuItemId));
            }

            try {
                EditorMenuItemDescriptor menuItem = ResolveProjectMenuItem(menuItemId);
                EditorCommandExecutionService commandExecutionService = new EditorCommandExecutionService(
                    scriptHotReloadService,
                    new EditorCommandContext(
                        projectPath,
                        scriptHotReloadService.ScriptTypeResolver,
                        new EditorMenuSceneRegenerationService(projectPath, scriptHotReloadService.ScriptTypeResolver)));
                commandExecutionService.Execute(menuItem.CommandId);
                Logger.WriteLine($"Executed project menu item '{menuItemId}'.");
            } catch (Exception ex) {
                Logger.WriteError($"Project menu item '{menuItemId}' failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Resolves one contributed project menu item descriptor by stable menu item identifier.
        /// </summary>
        /// <param name="menuItemId">Stable contributed project menu item identifier.</param>
        /// <returns>Resolved contributed project menu item descriptor.</returns>
        EditorMenuItemDescriptor ResolveProjectMenuItem(string menuItemId) {
            IReadOnlyList<EditorMenuItemDescriptor> menuItems = scriptHotReloadService.GetAvailableEditorMenuItems();
            for (int index = 0; index < menuItems.Count; index++) {
                if (string.Equals(menuItems[index].MenuItemId, menuItemId, StringComparison.OrdinalIgnoreCase)) {
                    return menuItems[index];
                }
            }

            throw new InvalidOperationException($"Project menu item '{menuItemId}' is not available.");
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
            return buildConfigService.Load(ResolveVisibleSupportedPlatforms(), currentSceneId);
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
        /// Resolves platform definitions for the currently supported project platforms.
        /// </summary>
        /// <returns>Platform definitions keyed by stable platform identifier.</returns>
        IReadOnlyDictionary<string, PlatformDefinition> CreateSupportedPlatformDefinitionsById() {
            Dictionary<string, PlatformDefinition> platformDefinitionsById = new Dictionary<string, PlatformDefinition>(StringComparer.Ordinal);
            for (int i = 0; i < SupportedPlatforms.Count; i++) {
                string platformId = SupportedPlatforms[i];
                EditorPlatformBuildSelectionModel selectionModel = ResolvePlatformSelectionModel(platformId);
                if (selectionModel == null || selectionModel.Definition == null) {
                    continue;
                }

                platformDefinitionsById[platformId] = selectionModel.Definition;
            }

            return platformDefinitionsById;
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
                Status = EditorBuildQueueItemStatus.Pending,
                StatusMessage = string.Empty
            });
            buildConfigService.Save(buildConfig);
            buildDialogCopySettingsDialog.Hide();
            buildDialog.Refresh(ResolveVisibleSupportedPlatforms(), sceneCatalogService.GetSceneIds(), request.PlatformId, buildConfig, ResolvePlatformSelectionModel(request.PlatformId));
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
            IReadOnlyList<string> visiblePlatformIds = ResolveVisibleSupportedPlatforms();
            if (visiblePlatformIds.Count < 1) {
                buildDialog.Hide();
                return;
            }

            string dialogPlatformId = ResolveVisiblePlatformId(visiblePlatformIds, ActiveProjectPlatform);
            buildDialog.Refresh(visiblePlatformIds, sceneCatalogService.GetSceneIds(), dialogPlatformId, buildConfig, ResolvePlatformSelectionModel(dialogPlatformId));
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
            IReadOnlyList<string> visiblePlatformIds = ResolveVisibleSupportedPlatforms();
            EditorBuildConfigDocument buildConfig = ResolveCurrentBuildConfig();
            buildConfigService.Save(buildConfig);
            buildQueueService.RunPending(buildConfig, visiblePlatformIds);
            buildDialogCopySettingsDialog.Hide();
            if (visiblePlatformIds.Count < 1) {
                buildDialog.Hide();
                return;
            }

            string dialogPlatformId = ResolveVisiblePlatformId(visiblePlatformIds, ActiveProjectPlatform);
            buildDialog.Refresh(visiblePlatformIds, sceneCatalogService.GetSceneIds(), dialogPlatformId, buildConfig, ResolvePlatformSelectionModel(dialogPlatformId));
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
            IReadOnlyList<string> visiblePlatformIds = ResolveVisibleSupportedPlatforms();
            string dialogPlatformId = ResolveVisiblePlatformId(visiblePlatformIds, ActiveProjectPlatform);
            List<string> copySourcePlatformIds = new List<string>(visiblePlatformIds.Count);
            for (int index = 0; index < visiblePlatformIds.Count; index++) {
                string platformId = visiblePlatformIds[index];
                if (!string.Equals(platformId, dialogPlatformId, StringComparison.OrdinalIgnoreCase)) {
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
                SceneSaveService.Save(fullPath, CurrentSceneSettings);
                CurrentScenePath = Path.GetFullPath(fullPath);
                MarkSceneClean();
                IReadOnlyList<AssetBrowserPanel> assetBrowserPanels = GetAssetBrowserPanels();
                for (int index = 0; index < assetBrowserPanels.Count; index++) {
                    assetBrowserPanels[index].RefreshEntries();
                }
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
        /// Resolves and applies one editor theme through the shared theme catalog and live runtime theme manager.
        /// </summary>
        /// <param name="themeId">Stable editor theme identifier that should become active.</param>
        void ApplyEditorTheme(string themeId) {
            EditorThemeDefinition theme = EditorThemeCatalog.FindById(themeId);
            if (theme == null) {
                throw new InvalidOperationException($"Unknown editor theme '{themeId}'.");
            }

            ThemeManager.SetTheme(theme.PaletteFactory());
            CurrentThemeId = theme.Id;
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
                LoadedEditorSceneDocument loadedSceneDocument = SceneFileLoadService.Load(fullPath);
                ClearUserSceneEntities(existingSceneEntities);
                AttachLoadedRoots(loadedSceneDocument.RootEntities);
                CurrentScenePath = Path.GetFullPath(fullPath);
                CurrentSceneSettings = loadedSceneDocument.SceneSettings;
                sceneCanvasProfileState.ApplySceneSettings(CurrentSceneSettings);
                MarkSceneClean();
                EditorSelectionService.ClearSelection();
                RefreshHierarchy();
                IReadOnlyList<AssetBrowserPanel> assetBrowserPanels = GetAssetBrowserPanels();
                for (int index = 0; index < assetBrowserPanels.Count; index++) {
                    assetBrowserPanels[index].RefreshEntries();
                }
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
            CurrentSceneSettings = new SceneSettingsAsset();
            sceneCanvasProfileState.ApplySceneSettings(CurrentSceneSettings);
            MarkSceneClean();
            EditorSelectionService.ClearSelection();
            RefreshHierarchy();
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
                RefreshHierarchy();
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
            RefreshWindowTitle();
        }

        /// <summary>
        /// Marks the current scene as clean after one successful save, load, or reset.
        /// </summary>
        void MarkSceneClean() {
            IsSceneDirty = false;
            RefreshWindowTitle();
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
            LatestPreviewSelectionKind = PreviewPanelBindingKind.Asset;
            if (entry.IsGenerated) {
                IReadOnlyList<PropertiesPanel> propertiesPanels = GetPropertiesPanels();
                for (int index = 0; index < propertiesPanels.Count; index++) {
                    propertiesPanels[index].ShowGeneratedAssetSummary(entry);
                }
                RefreshPreviewSource();
                return;
            }

            if (entry.EntryKind == AssetEntryKind.Scene) {
                IReadOnlyList<PropertiesPanel> propertiesPanels = GetPropertiesPanels();
                for (int index = 0; index < propertiesPanels.Count; index++) {
                    propertiesPanels[index].ShowSceneAssetSummary(entry);
                }
                RefreshPreviewSource();
                return;
            }

            if (IsMaterialAssetEntry(entry)) {
                try {
                    MaterialAsset materialAsset = LoadMaterialAsset(entry.FullPath);
                    MaterialAssetImportSettings settings = materialAssetSettingsService.LoadOrCreate(
                        entry.FullPath,
                        SupportedPlatforms,
                        ResolvePlatformSelectionModel);

                    IReadOnlyList<PropertiesPanel> propertiesPanels = GetPropertiesPanels();
                    for (int index = 0; index < propertiesPanels.Count; index++) {
                        propertiesPanels[index].ShowMaterialSettings(
                            entry,
                            materialAsset,
                            settings,
                            SupportedPlatforms,
                            CurrentProjectPlatform,
                            ResolvePlatformSelectionModel);
                    }
                } catch (Exception ex) {
                    IReadOnlyList<PropertiesPanel> errorPanels = GetPropertiesPanels();
                    for (int index = 0; index < errorPanels.Count; index++) {
                        errorPanels[index].ShowImportError(entry, ex.Message);
                    }
                }
                RefreshPreviewSource();
                return;
            }

            try {
                IReadOnlyList<string> importerIds = assetImportManager.GetImporterIdsForExtension(entry.Extension);
                if (importerIds.Count == 0) {
                    IReadOnlyList<PropertiesPanel> errorPanels = GetPropertiesPanels();
                    for (int index = 0; index < errorPanels.Count; index++) {
                        errorPanels[index].ShowImportError(entry, "No importers are registered for this asset type.");
                    }
                    RefreshPreviewSource();
                    return;
                }

                IReadOnlyList<PropertiesPanel> propertiesPanels = GetPropertiesPanels();
                if (entry.EntryKind == AssetEntryKind.Model) {
                    ModelAssetImportSettings settings;
                    if (!assetImportManager.TryLoadOrCreateModelImportSettings(entry.FullPath, out settings)) {
                        for (int index = 0; index < propertiesPanels.Count; index++) {
                            propertiesPanels[index].ShowEmpty();
                        }
                        RefreshPreviewSource();
                        return;
                    }

                    assetImportManager.SaveModelImportSettings(entry.FullPath, settings);
                    AssetProcessorSettings processorSettings = CreateModelImportViewProcessorSettings(settings);
                    for (int index = 0; index < propertiesPanels.Count; index++) {
                        propertiesPanels[index].ShowImportSettings(entry, settings.Importer.ImporterId, processorSettings, importerIds, SupportedPlatforms, CurrentProjectPlatform, CreateSupportedPlatformDefinitionsById());
                    }
                } else if (entry.EntryKind == AssetEntryKind.Image) {
                    TextureAssetImportSettings settings;
                    if (!assetImportManager.TryLoadOrCreateTextureImportSettings(entry.FullPath, out settings)) {
                        for (int index = 0; index < propertiesPanels.Count; index++) {
                            propertiesPanels[index].ShowEmpty();
                        }
                        RefreshPreviewSource();
                        return;
                    }

                    assetImportManager.SaveTextureImportSettings(entry.FullPath, settings);
                    for (int index = 0; index < propertiesPanels.Count; index++) {
                        propertiesPanels[index].ShowImportSettings(entry, settings.Importer.ImporterId, new AssetProcessorSettings(), importerIds, SupportedPlatforms, CurrentProjectPlatform, CreateSupportedPlatformDefinitionsById());
                    }
                } else {
                    AssetImportSettings settings;
                    if (!assetImportManager.TryLoadOrCreateImportSettings(entry.FullPath, out settings)) {
                        for (int index = 0; index < propertiesPanels.Count; index++) {
                            propertiesPanels[index].ShowEmpty();
                        }
                        RefreshPreviewSource();
                        return;
                    }

                    assetImportManager.SaveImportSettings(entry.FullPath, settings);
                    for (int index = 0; index < propertiesPanels.Count; index++) {
                        propertiesPanels[index].ShowImportSettings(entry, settings.Importer.ImporterId, settings.Processor, importerIds, SupportedPlatforms, CurrentProjectPlatform, CreateSupportedPlatformDefinitionsById());
                    }
                }
                RefreshPreviewSource();
            } catch (Exception ex) {
                IReadOnlyList<PropertiesPanel> propertiesPanels = GetPropertiesPanels();
                for (int index = 0; index < propertiesPanels.Count; index++) {
                    propertiesPanels[index].ShowImportError(entry, ex.Message);
                }
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
                if (entry.EntryKind == AssetEntryKind.Model) {
                    ModelAssetImportSettings settings = assetImportManager.LoadOrCreateModelImportSettings(entry.FullPath);
                    settings.Importer.ImporterId = request.ImporterId;
                    ApplyModelImportRequestProcessorSettings(settings, request.ProcessorSettings);
                    SetActiveProjectPlatform(request.SelectedPlatformId);
                    assetImportManager.SaveModelImportSettings(entry.FullPath, settings);
                } else if (entry.EntryKind == AssetEntryKind.Image) {
                    TextureAssetImportSettings settings = assetImportManager.LoadOrCreateTextureImportSettings(entry.FullPath);
                    settings.Importer.ImporterId = request.ImporterId;
                    SetActiveProjectPlatform(request.SelectedPlatformId);
                    assetImportManager.SaveTextureImportSettings(entry.FullPath, settings);
                } else {
                    AssetImportSettings settings = assetImportManager.LoadOrCreateImportSettings(entry.FullPath);
                    settings.Importer.ImporterId = request.ImporterId;
                    settings.Processor = request.ProcessorSettings;
                    SetActiveProjectPlatform(request.SelectedPlatformId);
                    assetImportManager.SaveImportSettings(entry.FullPath, settings);
                }

                SceneModelRefreshService.RefreshFileSystemModel(entry.FullPath, entry.RelativePath);

                IReadOnlyList<string> importerIds = assetImportManager.GetImporterIdsForExtension(entry.Extension);
                IReadOnlyList<PropertiesPanel> propertiesPanels = GetPropertiesPanels();
                if (entry.EntryKind == AssetEntryKind.Model) {
                    ModelAssetImportSettings refreshedSettings = assetImportManager.LoadOrCreateModelImportSettings(entry.FullPath);
                    AssetProcessorSettings processorSettings = CreateModelImportViewProcessorSettings(refreshedSettings);
                    for (int index = 0; index < propertiesPanels.Count; index++) {
                        propertiesPanels[index].ShowImportSettings(entry, refreshedSettings.Importer.ImporterId, processorSettings, importerIds, SupportedPlatforms, CurrentProjectPlatform, CreateSupportedPlatformDefinitionsById());
                    }
                } else if (entry.EntryKind == AssetEntryKind.Image) {
                    TextureAssetImportSettings refreshedSettings = assetImportManager.LoadOrCreateTextureImportSettings(entry.FullPath);
                    for (int index = 0; index < propertiesPanels.Count; index++) {
                        propertiesPanels[index].ShowImportSettings(entry, refreshedSettings.Importer.ImporterId, new AssetProcessorSettings(), importerIds, SupportedPlatforms, CurrentProjectPlatform, CreateSupportedPlatformDefinitionsById());
                    }
                } else {
                    AssetImportSettings refreshedSettings = assetImportManager.LoadOrCreateImportSettings(entry.FullPath);
                    for (int index = 0; index < propertiesPanels.Count; index++) {
                        propertiesPanels[index].ShowImportSettings(entry, refreshedSettings.Importer.ImporterId, refreshedSettings.Processor, importerIds, SupportedPlatforms, CurrentProjectPlatform, CreateSupportedPlatformDefinitionsById());
                    }
                }
            } catch (Exception ex) {
                IReadOnlyList<PropertiesPanel> propertiesPanels = GetPropertiesPanels();
                for (int index = 0; index < propertiesPanels.Count; index++) {
                    propertiesPanels[index].ShowImportError(entry, ex.Message);
                }
            }
        }

        /// <summary>
        /// Creates one model-focused processor-settings payload for the import-settings view.
        /// </summary>
        /// <param name="settings">Typed model import settings to project into the view model.</param>
        /// <returns>Processor settings payload consumed by the model import-settings UI.</returns>
        AssetProcessorSettings CreateModelImportViewProcessorSettings(ModelAssetImportSettings settings) {
            if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            }

            AssetProcessorSettings processorSettings = new AssetProcessorSettings();
            if (settings.Processor == null || settings.Processor.Platforms == null) {
                return processorSettings;
            }

            foreach (KeyValuePair<string, ModelAssetProcessorSettings> pair in settings.Processor.Platforms) {
                if (string.IsNullOrWhiteSpace(pair.Key)) {
                    continue;
                }

                processorSettings.Platforms[pair.Key] = new AssetPlatformProcessorSettings {
                    Model = CloneModelProcessorSettings(pair.Value)
                };
            }

            return processorSettings;
        }

        /// <summary>
        /// Applies one model import-settings request payload to typed model settings.
        /// </summary>
        /// <param name="settings">Typed model settings to update.</param>
        /// <param name="processorSettings">Processor settings payload emitted by the import-settings view.</param>
        void ApplyModelImportRequestProcessorSettings(ModelAssetImportSettings settings, AssetProcessorSettings processorSettings) {
            if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            } else if (processorSettings == null) {
                throw new ArgumentNullException(nameof(processorSettings));
            }

            settings.Processor = new ModelAssetProcessorPlatformSettings();
            foreach (KeyValuePair<string, AssetPlatformProcessorSettings> pair in processorSettings.Platforms) {
                if (string.IsNullOrWhiteSpace(pair.Key)) {
                    continue;
                }

                settings.Processor.Platforms[pair.Key] = CloneModelProcessorSettings(pair.Value?.Model);
            }
        }

        /// <summary>
        /// Creates one copy of model processor settings.
        /// </summary>
        /// <param name="settings">Model processor settings to clone.</param>
        /// <returns>Cloned model processor settings.</returns>
        ModelAssetProcessorSettings CloneModelProcessorSettings(ModelAssetProcessorSettings settings) {
            ModelAssetProcessorSettings clone = new ModelAssetProcessorSettings();
            if (settings == null) {
                return clone;
            }

            clone.FlipWinding = settings.FlipWinding;
            return clone;
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

            return materialAssetSettingsService.LoadMaterialAsset(path, CurrentProjectPlatform);
        }

        /// <summary>
        /// Clears property and preview panels when no asset is selected.
        /// </summary>
        void HandleAssetSelectionCleared() {
            SelectedAssetEntry = null;
            if (LatestPreviewSelectionKind == PreviewPanelBindingKind.Asset) {
                LatestPreviewSelectionKind = HasPreviewableCamera(SelectedSceneEntity)
                    ? PreviewPanelBindingKind.Camera
                    : PreviewPanelBindingKind.None;
            }
            IReadOnlyList<PropertiesPanel> propertiesPanels = GetPropertiesPanels();
            for (int index = 0; index < propertiesPanels.Count; index++) {
                propertiesPanels[index].ShowEmpty();
            }
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
            if (args.HasSelection && HasPreviewableCamera(args.SelectedEntity)) {
                LatestPreviewSelectionKind = PreviewPanelBindingKind.Camera;
            } else if (!args.HasSelection && LatestPreviewSelectionKind == PreviewPanelBindingKind.Camera) {
                LatestPreviewSelectionKind = SelectedAssetEntry != null
                    ? PreviewPanelBindingKind.Asset
                    : PreviewPanelBindingKind.None;
            }

            if (args.HasSelection) {
                IReadOnlyList<PropertiesPanel> propertiesPanels = GetPropertiesPanels();
                for (int index = 0; index < propertiesPanels.Count; index++) {
                    propertiesPanels[index].ShowEntityProperties(args.SelectedEntity, ProjectSupportedPlatforms);
                }
            } else {
                IReadOnlyList<PropertiesPanel> propertiesPanels = GetPropertiesPanels();
                for (int index = 0; index < propertiesPanels.Count; index++) {
                    propertiesPanels[index].ShowEmpty();
                }
            }

            RefreshPreviewSource();
        }

        /// <summary>
        /// Recomputes the active preview source from the current selection snapshot.
        /// </summary>
        void RefreshPreviewSource() {
            IReadOnlyList<PreviewPanel> previewPanels = GetPreviewPanels();
            if (previewPanels.Count == 0) {
                return;
            }

            for (int index = 0; index < previewPanels.Count; index++) {
                UpdatePreviewPanelState(previewPanels[index]);
            }
        }

        /// <summary>
        /// Synchronizes one preview panel with the current asset and scene selection snapshot.
        /// </summary>
        /// <param name="panel">Preview panel that should reflect the current selection snapshot.</param>
        void UpdatePreviewPanelState(PreviewPanel panel) {
            if (panel == null) {
                throw new ArgumentNullException(nameof(panel));
            }

            if (previewSourceResolver == null) {
                panel.ClearPreview();
                return;
            }
            if (panel.IsLocked) {
                RefreshLockedPreviewPanelState(panel);
                return;
            }

            if (LatestPreviewSelectionKind == PreviewPanelBindingKind.Camera) {
                if (panel.ApplyLatestCameraSelection(SelectedSceneEntity, previewSourceResolver)) {
                    return;
                }
                if (panel.ApplyLatestAssetSelection(SelectedAssetEntry, previewSourceResolver)) {
                    return;
                }

                panel.ApplyLatestSelectionCleared();
                return;
            }
            if (LatestPreviewSelectionKind == PreviewPanelBindingKind.Asset) {
                if (panel.ApplyLatestAssetSelection(SelectedAssetEntry, previewSourceResolver)) {
                    return;
                }
                if (panel.ApplyLatestCameraSelection(SelectedSceneEntity, previewSourceResolver)) {
                    return;
                }

                panel.ApplyLatestSelectionCleared();
                return;
            }
            if (panel.ApplyLatestAssetSelection(SelectedAssetEntry, previewSourceResolver)) {
                return;
            }
            if (panel.ApplyLatestCameraSelection(SelectedSceneEntity, previewSourceResolver)) {
                return;
            }

            panel.ApplyLatestSelectionCleared();
        }

        /// <summary>
        /// Rebuilds or clears one locked preview panel based on its persisted asset or camera target.
        /// </summary>
        /// <param name="panel">Locked preview panel to refresh.</param>
        void RefreshLockedPreviewPanelState(PreviewPanel panel) {
            if (panel == null) {
                throw new ArgumentNullException(nameof(panel));
            }

            PreviewPanelStateDocument state = panel.CaptureState();
            if (!state.IsLocked) {
                return;
            }

            if (state.BindingKind == PreviewPanelBindingKind.Asset) {
                if (TryResolvePreviewAssetEntry(state.AssetRelativePath, out AssetBrowserEntry assetEntry) && panel.RestoreLockedAssetSelection(assetEntry, previewSourceResolver)) {
                    return;
                }

                panel.ClearLockedTarget();
                return;
            }
            if (state.BindingKind == PreviewPanelBindingKind.Camera) {
                if (TryResolvePreviewSceneEntity(state.SceneEntityId, out Entity selectedEntity) && panel.RestoreLockedCameraSelection(selectedEntity, previewSourceResolver)) {
                    return;
                }

                panel.ClearLockedTarget();
                return;
            }
            if (state.BindingKind == PreviewPanelBindingKind.None && panel.ActivePreviewSource != null) {
                panel.ClearPreview();
            }
        }

        /// <summary>
        /// Tries to rebuild one previewable asset entry from its persisted relative path.
        /// </summary>
        /// <param name="relativePath">Project-relative asset path captured by a preview panel.</param>
        /// <param name="assetEntry">Resolved previewable asset entry when one exists.</param>
        /// <returns>True when the asset path exists and can be previewed; otherwise false.</returns>
        bool TryResolvePreviewAssetEntry(string relativePath, out AssetBrowserEntry assetEntry) {
            assetEntry = null;
            if (string.IsNullOrWhiteSpace(relativePath)) {
                return false;
            }

            string assetsRootPath = ResolveAssetsRootPath(projectPath);
            string normalizedRelativePath = relativePath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            string fullPath = Path.GetFullPath(Path.Combine(assetsRootPath, normalizedRelativePath));
            if (!File.Exists(fullPath)) {
                return false;
            }

            string extension = Path.GetExtension(fullPath);
            if (string.IsNullOrWhiteSpace(extension)) {
                return false;
            }

            AssetEntryKind entryKind = ResolvePreviewAssetEntryKind(extension);
            if (entryKind != AssetEntryKind.Image && entryKind != AssetEntryKind.Model) {
                return false;
            }

            assetEntry = AssetBrowserEntry.CreateFileSystemFile(
                Path.GetFileName(fullPath),
                relativePath.Replace('\\', '/'),
                fullPath,
                extension,
                entryKind);
            return true;
        }

        /// <summary>
        /// Resolves one stable scene entity id back to the current live entity used by preview restoration.
        /// </summary>
        /// <param name="entityId">Stable scene entity id captured by a preview panel.</param>
        /// <param name="selectedEntity">Resolved live scene entity when one exists.</param>
        /// <returns>True when the entity id is still present in the current scene; otherwise false.</returns>
        bool TryResolvePreviewSceneEntity(uint entityId, out Entity selectedEntity) {
            selectedEntity = null;
            if (entityId == 0u || helengine.Core.Instance == null || helengine.Core.Instance.ObjectManager == null) {
                return false;
            }

            IReadOnlyList<Entity> entities = helengine.Core.Instance.ObjectManager.Entities;
            for (int index = 0; index < entities.Count; index++) {
                EntitySaveComponent saveComponent = FindEntitySaveComponent(entities[index]);
                if (saveComponent == null || saveComponent.EntityId != entityId) {
                    continue;
                }

                selectedEntity = entities[index];
                return true;
            }

            return false;
        }

        /// <summary>
        /// Classifies one persisted preview asset extension into the asset-browser kind required by the preview resolver.
        /// </summary>
        /// <param name="extension">File extension that should be classified.</param>
        /// <returns>Preview-relevant asset entry kind for the extension.</returns>
        AssetEntryKind ResolvePreviewAssetEntryKind(string extension) {
            if (string.IsNullOrWhiteSpace(extension)) {
                throw new ArgumentException("Extension must be provided.", nameof(extension));
            }
            if (assetImportManager != null && assetImportManager.IsTextureExtension(extension)) {
                return AssetEntryKind.Image;
            }
            if (assetImportManager != null && assetImportManager.GetImporterIdsForExtension(extension).Count > 0) {
                return AssetEntryKind.Model;
            }

            return AssetEntryKind.File;
        }

        /// <summary>
        /// Returns the hidden persistence component attached to the supplied entity when one exists.
        /// </summary>
        /// <param name="entity">Entity whose persistence metadata should be inspected.</param>
        /// <returns>Attached save component when present; otherwise null.</returns>
        EntitySaveComponent FindEntitySaveComponent(Entity entity) {
            if (entity == null || entity.Components == null) {
                return null;
            }

            for (int index = 0; index < entity.Components.Count; index++) {
                if (entity.Components[index] is EntitySaveComponent saveComponent) {
                    return saveComponent;
                }
            }

            return null;
        }

        /// <summary>
        /// Returns true when the provided entity can produce one camera preview.
        /// </summary>
        /// <param name="selectedEntity">Selected entity to inspect.</param>
        /// <returns>True when the entity owns one camera component.</returns>
        bool HasPreviewableCamera(Entity selectedEntity) {
            if (selectedEntity == null || selectedEntity.Components == null) {
                return false;
            }

            for (int index = 0; index < selectedEntity.Components.Count; index++) {
                if (selectedEntity.Components[index] is CameraComponent) {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Synchronizes one properties panel with the current asset or scene selection snapshot.
        /// </summary>
        /// <param name="panel">Properties panel that should reflect the current selection snapshot.</param>
        void UpdatePropertiesPanelState(PropertiesPanel panel) {
            if (panel == null) {
                throw new ArgumentNullException(nameof(panel));
            }

            if (SelectedAssetEntry != null) {
                HandlePropertiesPanelAssetState(panel, SelectedAssetEntry);
                return;
            }

            if (SelectedSceneEntity != null) {
                panel.ShowEntityProperties(SelectedSceneEntity, ProjectSupportedPlatforms);
                return;
            }

            panel.ShowEmpty();
        }

        /// <summary>
        /// Synchronizes one properties panel with the currently selected asset entry.
        /// </summary>
        /// <param name="panel">Properties panel that should reflect the selected asset.</param>
        /// <param name="entry">Selected asset entry.</param>
        void HandlePropertiesPanelAssetState(PropertiesPanel panel, AssetBrowserEntry entry) {
            if (panel == null) {
                throw new ArgumentNullException(nameof(panel));
            }
            if (entry == null) {
                throw new ArgumentNullException(nameof(entry));
            }

            if (entry.IsGenerated) {
                panel.ShowGeneratedAssetSummary(entry);
                return;
            }
            if (entry.EntryKind == AssetEntryKind.Scene) {
                panel.ShowSceneAssetSummary(entry);
                return;
            }
            if (IsMaterialAssetEntry(entry)) {
                try {
                    MaterialAsset materialAsset = LoadMaterialAsset(entry.FullPath);
                    MaterialAssetImportSettings settings = materialAssetSettingsService.LoadOrCreate(
                        entry.FullPath,
                        SupportedPlatforms,
                        ResolvePlatformSelectionModel);

                    panel.ShowMaterialSettings(
                        entry,
                        materialAsset,
                        settings,
                        SupportedPlatforms,
                        CurrentProjectPlatform,
                        ResolvePlatformSelectionModel);
                } catch (Exception ex) {
                    panel.ShowImportError(entry, ex.Message);
                }
                return;
            }

            try {
                IReadOnlyList<string> importerIds = assetImportManager.GetImporterIdsForExtension(entry.Extension);
                if (importerIds.Count == 0) {
                    panel.ShowImportError(entry, "No importers are registered for this asset type.");
                    return;
                }

                if (entry.EntryKind == AssetEntryKind.Model) {
                    ModelAssetImportSettings settings;
                    if (!assetImportManager.TryLoadOrCreateModelImportSettings(entry.FullPath, out settings)) {
                        panel.ShowEmpty();
                        return;
                    }

                    assetImportManager.SaveModelImportSettings(entry.FullPath, settings);
                    panel.ShowImportSettings(entry, settings.Importer.ImporterId, CreateModelImportViewProcessorSettings(settings), importerIds, SupportedPlatforms, CurrentProjectPlatform, CreateSupportedPlatformDefinitionsById());
                } else if (entry.EntryKind == AssetEntryKind.Image) {
                    TextureAssetImportSettings settings;
                    if (!assetImportManager.TryLoadOrCreateTextureImportSettings(entry.FullPath, out settings)) {
                        panel.ShowEmpty();
                        return;
                    }

                    assetImportManager.SaveTextureImportSettings(entry.FullPath, settings);
                    panel.ShowImportSettings(entry, settings.Importer.ImporterId, new AssetProcessorSettings(), importerIds, SupportedPlatforms, CurrentProjectPlatform, CreateSupportedPlatformDefinitionsById());
                } else {
                    AssetImportSettings settings;
                    if (!assetImportManager.TryLoadOrCreateImportSettings(entry.FullPath, out settings)) {
                        panel.ShowEmpty();
                        return;
                    }

                    assetImportManager.SaveImportSettings(entry.FullPath, settings);
                    panel.ShowImportSettings(entry, settings.Importer.ImporterId, settings.Processor, importerIds, SupportedPlatforms, CurrentProjectPlatform, CreateSupportedPlatformDefinitionsById());
                }
            } catch (Exception ex) {
                panel.ShowImportError(entry, ex.Message);
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
        /// Creates the scene component persistence registry used by editor scene save and load workflows.
        /// </summary>
        /// <param name="scriptTypeResolver">Resolver backed by the currently loaded project script assemblies.</param>
        /// <returns>Configured persistence registry.</returns>
        static ComponentPersistenceRegistry CreateComponentPersistenceRegistry(IScriptTypeResolver scriptTypeResolver) {
            ComponentPersistenceRegistry persistenceRegistry = new ComponentPersistenceRegistry(scriptTypeResolver);
            persistenceRegistry.Register(new MeshComponentPersistenceDescriptor());
            persistenceRegistry.Register(new CameraComponentPersistenceDescriptor());
            persistenceRegistry.Register(new TextComponentPersistenceDescriptor());
            persistenceRegistry.Register(new SpriteComponentPersistenceDescriptor());
            persistenceRegistry.Register(new RoundedRectComponentPersistenceDescriptor());
            persistenceRegistry.Register(new FPSComponentPersistenceDescriptor());
            persistenceRegistry.Register(new DebugComponentPersistenceDescriptor());
            persistenceRegistry.Register(new DirectionalLightComponentPersistenceDescriptor());
            persistenceRegistry.Register(new AmbientLightComponentPersistenceDescriptor());
            persistenceRegistry.Register(new PointLightComponentPersistenceDescriptor());
            persistenceRegistry.Register(new SpotLightComponentPersistenceDescriptor());
            persistenceRegistry.Register(new MenuComponentPersistenceDescriptor());
            persistenceRegistry.Register(new MenuPanelComponentPersistenceDescriptor());
            persistenceRegistry.Register(new MenuItemComponentPersistenceDescriptor());
            persistenceRegistry.Register(new MenuSelectedDescriptionComponentPersistenceDescriptor());
            return persistenceRegistry;
        }

        /// <summary>
        /// Builds and loads the available project script libraries during editor-session startup and applies the resulting project menus.
        /// </summary>
        /// <param name="scriptHotReloadService">Hot-reload service that builds and loads project script assemblies.</param>
        /// <param name="applyProjectMenus">Callback that applies the contributed project-menu descriptors to the title bar.</param>
        /// <returns>Result of the startup project-library load attempt.</returns>
        static EditorBuildExecutionResult LoadProjectLibrariesOnStartup(
            EditorGameScriptHotReloadService scriptHotReloadService,
            Action<IReadOnlyList<EditorMenuItemDescriptor>> applyProjectMenus) {
            if (scriptHotReloadService == null) {
                throw new ArgumentNullException(nameof(scriptHotReloadService));
            }
            if (applyProjectMenus == null) {
                throw new ArgumentNullException(nameof(applyProjectMenus));
            }

            EditorBuildExecutionResult result = scriptHotReloadService.BuildAndReload();
            if (!result.Succeeded) {
                return result;
            }

            applyProjectMenus(scriptHotReloadService.GetAvailableEditorMenuItems());
            return result;
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
            string platformSuffix = string.IsNullOrWhiteSpace(ActiveProjectPlatform)
                ? string.Empty
                : $" [{ActiveProjectPlatform.ToUpperInvariant()}]";
            string title = $"helengine - {ProjectDisplayName}{platformSuffix}";
            if (string.IsNullOrWhiteSpace(CurrentScenePath)) {
                return title;
            }

            string sceneDisplayName = ResolveSceneDisplayName(CurrentScenePath);
            string sceneTitle = BuildSceneDisplayTitle(sceneDisplayName);
            return $"{sceneTitle} - {title}";
        }

        /// <summary>
        /// Returns whether the currently open map has unsaved editor changes.
        /// </summary>
        /// <returns>True when the current map should display a dirty marker.</returns>
        bool IsCurrentMapDirty() {
            return IsSceneDirty;
        }

        /// <summary>
        /// Appends the current-map dirty marker to one resolved scene display name when needed.
        /// </summary>
        /// <param name="sceneDisplayName">Resolved scene display name.</param>
        /// <returns>Scene display name with the dirty marker applied when required.</returns>
        string BuildSceneDisplayTitle(string sceneDisplayName) {
            if (string.IsNullOrWhiteSpace(sceneDisplayName)) {
                throw new InvalidOperationException("Scene display name must be provided.");
            }

            return IsCurrentMapDirty()
                ? $"{sceneDisplayName}*"
                : sceneDisplayName;
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
        /// Creates the explicit runtime platform metadata injected into the editor-owned core instance.
        /// </summary>
        /// <returns>Stable editor host platform metadata used by runtime systems during editor execution.</returns>
        PlatformInfo CreateEditorPlatformInfo() {
            return new PlatformInfo("editor", RequiredEngineVersion);
        }

        /// <summary>
        /// Creates the available-platform resolver used by project platform workflows.
        /// </summary>
        /// <returns>Resolver that loads platforms from development overrides, launcher state, or built-in fallback sources.</returns>
        AvailablePlatformProviderResolver CreateAvailablePlatformProviderResolver() {
            EditorSourceBuildWorkspaceLocator workspaceLocator = new EditorSourceBuildWorkspaceLocator();
            string sharedEngineUserSettingsRootPath = workspaceLocator.ResolveSharedEngineUserSettingsRootPath();
            PlatformDiscoveryOptions options = new PlatformDiscoveryOptions(sharedEngineUserSettingsRootPath);
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
                    uiFont,
                    null,
                    scriptHotReloadService.ScriptTypeResolver);
            }

            return new EditorBuildExecutorRouter(executorsByPlatformId);
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
        /// Resolves the installed platform identifiers currently available for the active engine version.
        /// </summary>
        /// <returns>Alphabetically ordered installed platform identifiers.</returns>
        IReadOnlyList<string> ResolveInstalledPlatformIds() {
            if (availablePlatformProviderResolver == null) {
                return Array.Empty<string>();
            }

            return availablePlatformProviderResolver
                .LoadPlatforms(RequiredEngineVersion)
                .Where(platform => platform.IsInstalled)
                .Select(platform => platform.Id)
                .OrderBy(platformId => platformId, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        /// <summary>
        /// Resolves the platforms that are both project-enabled and currently installed on the local machine.
        /// </summary>
        /// <returns>Alphabetically ordered visible platform identifiers.</returns>
        IReadOnlyList<string> ResolveVisibleSupportedPlatforms() {
            IReadOnlyList<string> installedPlatformIds = ResolveInstalledPlatformIds();
            if (installedPlatformIds.Count < 1 || SupportedPlatforms.Count < 1) {
                return Array.Empty<string>();
            }

            HashSet<string> installedPlatformIdSet = new HashSet<string>(installedPlatformIds, StringComparer.OrdinalIgnoreCase);
            List<string> visiblePlatformIds = new List<string>(SupportedPlatforms.Count);
            for (int index = 0; index < SupportedPlatforms.Count; index++) {
                string platformId = SupportedPlatforms[index];
                if (installedPlatformIdSet.Contains(platformId)) {
                    visiblePlatformIds.Add(platformId);
                }
            }

            visiblePlatformIds.Sort(StringComparer.OrdinalIgnoreCase);
            return visiblePlatformIds;
        }

        /// <summary>
        /// Resolves the platform id that should be shown first in one filtered dialog without persisting any replacement.
        /// </summary>
        /// <param name="visiblePlatformIds">Platforms currently visible in the dialog.</param>
        /// <param name="preferredPlatformId">Preferred platform id, typically the user-local active platform.</param>
        /// <returns>Visible platform id to show first.</returns>
        string ResolveVisiblePlatformId(IReadOnlyList<string> visiblePlatformIds, string preferredPlatformId) {
            if (visiblePlatformIds == null) {
                throw new ArgumentNullException(nameof(visiblePlatformIds));
            }

            if (visiblePlatformIds.Count < 1) {
                throw new InvalidOperationException("At least one visible platform is required.");
            }

            if (!string.IsNullOrWhiteSpace(preferredPlatformId)) {
                for (int index = 0; index < visiblePlatformIds.Count; index++) {
                    if (string.Equals(visiblePlatformIds[index], preferredPlatformId, StringComparison.OrdinalIgnoreCase)) {
                        return visiblePlatformIds[index];
                    }
                }
            }

            return visiblePlatformIds[0];
        }

        /// <summary>
        /// Returns true when the supplied platform is both project-supported and installed for the current engine.
        /// </summary>
        /// <param name="platformId">Platform identifier to validate.</param>
        /// <returns>True when the platform can be used as the current project platform.</returns>
        bool CanUseProjectPlatform(string platformId) {
            if (string.IsNullOrWhiteSpace(platformId)) {
                return false;
            }

            for (int index = 0; index < SupportedPlatforms.Count; index++) {
                if (string.Equals(SupportedPlatforms[index], platformId, StringComparison.OrdinalIgnoreCase)) {
                    return IsInstalledPlatform(platformId);
                }
            }

            return false;
        }

        /// <summary>
        /// Forces the Platforms workflow when the current persisted project platform is no longer usable.
        /// </summary>
        void PromptForPlatformSelectionIfRequired() {
            if (CanUseProjectPlatform(ActiveProjectPlatform)) {
                return;
            }

            HandlePlatformsRequested();
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
        /// Determines whether two scene settings payloads describe the same scene-owned canvas profile.
        /// </summary>
        /// <param name="left">Left scene settings payload.</param>
        /// <param name="right">Right scene settings payload.</param>
        /// <returns>True when both payloads describe the same canvas profile.</returns>
        static bool AreSceneSettingsEquivalent(SceneSettingsAsset left, SceneSettingsAsset right) {
            if (left == null) {
                throw new ArgumentNullException(nameof(left));
            }
            if (right == null) {
                throw new ArgumentNullException(nameof(right));
            }
            if (left.CanvasProfile == null) {
                throw new InvalidOperationException("Left scene settings must include a canvas profile.");
            }
            if (right.CanvasProfile == null) {
                throw new InvalidOperationException("Right scene settings must include a canvas profile.");
            }

            return left.CanvasProfile.Width == right.CanvasProfile.Width &&
                   left.CanvasProfile.Height == right.CanvasProfile.Height;
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
            ContentManager projectContentManager = new ContentManager(projectAssetsRootPath);
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

