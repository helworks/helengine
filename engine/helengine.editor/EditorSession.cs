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
        /// Editor core driving updates and rendering.
        /// </summary>
        readonly EditorCore core;
        /// <summary>
        /// Project path used for asset browsing.
        /// </summary>
        readonly string projectPath;
        /// <summary>
        /// Font used for UI elements and title bars.
        /// </summary>
        readonly FontAsset uiFont;
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
        /// Initializes a new editor session and sets up cameras, docking, and starter content.
        /// </summary>
        /// <param name="core">Editor core instance that owns shared state.</param>
        /// <param name="projectPath">Path to the project root or project file being edited.</param>
        /// <param name="uiFont">Font used for editor UI text.</param>
        /// <param name="titleText">Initial window title text.</param>
        /// <param name="render3D">3D renderer instance.</param>
        /// <param name="render2D">2D renderer instance.</param>
        /// <param name="input">Input manager instance.</param>
        /// <param name="renderWidth">Initial render width in pixels.</param>
        /// <param name="renderHeight">Initial render height in pixels.</param>
        /// <param name="importers">Asset importers to register for import settings.</param>
        public EditorSession(
            EditorCore core,
            string projectPath,
            FontAsset uiFont,
            string titleText,
            RenderManager3D render3D,
            RenderManager2D render2D,
            InputManager input,
            int renderWidth,
            int renderHeight,
            IReadOnlyList<IAssetImporterRegistration> importers) {
            this.core = core;
            this.projectPath = ResolveProjectRootPath(projectPath);
            this.uiFont = uiFont;

            core.Initialize(render3D, render2D, input);
            core.InputManager.SetKeyboardActive(true);

            assetImportManager = InitializeAssetImports(importers);

            uiCameraEntity = new EditorEntity();
            uiCameraEntity.InternalEntity = true;
            uiCameraEntity.Position = new float3(0, 3, -8);
            uiCameraComponent = new CameraComponent();
            uiCameraComponent.LayerMask = 0b1000000000000000;
            uiCameraComponent.CameraDrawOrder = 255;
            uiCameraComponent.ClearSettings = new CameraClearSettings(false, new float4(0f, 0f, 0f, 0f), false, 1.0f, false, 0);
            uiCameraEntity.AddComponent(uiCameraComponent);

            sceneCameraEntity = new EditorEntity();
            sceneCameraEntity.InternalEntity = true;
            sceneCameraEntity.Position = new float3(0, 3, -8);
            sceneCameraComponent = new CameraComponent();
            sceneCameraComponent.LayerMask = 0b0100000000000000;
            sceneCameraComponent.ClearSettings = new CameraClearSettings(true, new float4(0.39215687f, 0.58431375f, 0.92941177f, 1f), true, 1.0f, false, 0);
            sceneCameraEntity.AddComponent(sceneCameraComponent);
            sceneCameraEntity.AddComponent(new EditorViewportCameraController(sceneCameraComponent));

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
            hiddenCameraEntity.LayerMask = sceneCameraComponent.LayerMask;
            hiddenCameraComponent = new CameraComponent();
            hiddenCameraComponent.LayerMask = sceneCameraComponent.LayerMask;
            hiddenCameraComponent.Viewport = new float4(0, 0, 640, 360);
            hiddenCameraComponent.ClearSettings = new CameraClearSettings(true, new float4(0f, 0f, 0f, 0f), true, 1.0f, false, 0);
            hiddenCameraTarget = render3D.CreateRenderTarget(640, 360);
            hiddenCameraComponent.RenderTarget = hiddenCameraTarget;
            hiddenCameraEntity.AddComponent(hiddenCameraComponent);
            if (render3D is not helengine.directx11.DirectX11Renderer3D pickerRenderer) {
                throw new InvalidOperationException("Editor picker requires a DirectX11 renderer.");
            }
            sceneCameraEntity.AddComponent(new EditorViewportPicker(sceneCameraComponent, hiddenCameraEntity, hiddenCameraComponent, pickerRenderer));

            titleBar = new EditorTitleBar(uiFont, Math.Max(1, renderWidth), titleText ?? string.Empty);

            dockingManager = new DockingManager();
            sceneHierarchyPanel = new SceneHierarchyPanel(uiFont);
            assetBrowserPanel = new AssetBrowserPanel(uiFont, this.projectPath);
            mainViewport = new EditorViewport(sceneCameraComponent, uiFont);
            propertiesPanel = new PropertiesPanel(uiFont);
            loggerPanel = new LoggerPanel(uiFont);
            previewPanel = new PreviewPanel(uiFont);
            assetBrowserPanel.AssetSelected += HandleAssetSelected;
            assetBrowserPanel.SelectionCleared += HandleAssetSelectionCleared;
            propertiesPanel.ImportSettingsApplyRequested += HandleImportSettingsApplyRequested;

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

            shaderModuleManager = BuildShaderModuleManager();
            shaderModuleManager.Start();

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
        /// Updates the title bar text.
        /// </summary>
        /// <param name="titleText">New window title text.</param>
        public void SetTitle(string titleText) {
            titleBar.Title = titleText ?? string.Empty;
        }

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

            titleBar.UpdateLayout(width);
            uiCameraComponent.Viewport = new float4(0, 0, width, height);

            int availableHeight = Math.Max(0, height - titleBar.Height);
            dockingManager.Layout.Layout(new int2(width, availableHeight), new float3(0, titleBar.Height, 0));
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
            shaderModuleManager.Dispose();
            loggerPanel.Detach();
            core.Dispose();
        }

        /// <summary>
        /// Creates a starter scene with a cube and a ground plane.
        /// </summary>
        void BuildStartScene() {
            Core coreInstance = helengine.Core.Instance;
            RuntimeMaterial defaultMaterial = BuildDefaultMeshMaterial();
            EditorEntity cube = new EditorEntity();
            cube.Name = "Cube";
            cube.LayerMask = 0b0100000000000000;
            MeshComponent mesh = new MeshComponent();
            cube.AddComponent(mesh);
            ModelAsset modelData = ModelUtils.GenerateCubeMesh(float3.Zero, float3.One);
            RuntimeModel renderData = coreInstance.RenderManager3D.BuildModelFromRaw(modelData);
            mesh.Model = renderData;
            mesh.Material = defaultMaterial;

            EditorEntity plane = new EditorEntity();
            plane.Name = "Ground";
            plane.LayerMask = 0b0100000000000000;
            plane.Scale = new float3(10, 1, 10);
            MeshComponent planeMesh = new MeshComponent();
            plane.AddComponent(planeMesh);
            ModelAsset planeModelData = ModelUtils.GeneratePlaneMesh(float3.Zero, float3.One);
            RuntimeModel planeRenderData = coreInstance.RenderManager3D.BuildModelFromRaw(planeModelData);
            planeMesh.Model = planeRenderData;
            planeMesh.Material = defaultMaterial;
        }

        /// <summary>
        /// Builds the default material used for starter 3D meshes.
        /// </summary>
        /// <returns>Runtime material instance.</returns>
        RuntimeMaterial BuildDefaultMeshMaterial() {
            string shaderPath = ResolveBuiltInShaderPath("MiniCube.fx");
            string shaderDirectory = Path.GetDirectoryName(shaderPath);
            if (string.IsNullOrWhiteSpace(shaderDirectory)) {
                throw new InvalidOperationException("Built-in shader directory could not be resolved.");
            }

            string shaderName = Path.GetFileNameWithoutExtension(shaderPath);
            if (string.IsNullOrWhiteSpace(shaderName)) {
                throw new InvalidOperationException("Built-in shader name could not be resolved.");
            }

            var shaderBuilder = new helengine.directx11.DirectX11ShaderAssetBuilder(shaderDirectory, new ShaderModel(4, 0));
            ShaderAsset shaderAsset = shaderBuilder.BuildFromFile(shaderPath, shaderName);

            if (string.IsNullOrWhiteSpace(shaderAsset.Id)) {
                throw new InvalidOperationException("Shader asset id must be provided.");
            }

            var materialAsset = new MaterialAsset {
                Id = string.Concat(shaderName, ".material"),
                ShaderAssetId = shaderAsset.Id,
                VertexProgram = string.Concat(shaderName, ".vs"),
                PixelProgram = string.Concat(shaderName, ".ps"),
                Variant = "default"
            };

            return core.RenderManager3D.BuildMaterialFromRaw(materialAsset, shaderAsset);
        }

        /// <summary>
        /// Resolves the absolute path to a built-in shader file.
        /// </summary>
        /// <param name="shaderFileName">Shader file name to resolve.</param>
        /// <returns>Absolute shader path.</returns>
        string ResolveBuiltInShaderPath(string shaderFileName) {
            if (string.IsNullOrWhiteSpace(shaderFileName)) {
                throw new ArgumentException("Shader file name must be provided.", nameof(shaderFileName));
            }

            string baseDirectory = AppContext.BaseDirectory;
            if (string.IsNullOrWhiteSpace(baseDirectory)) {
                throw new InvalidOperationException("Base directory could not be resolved.");
            }

            string shaderPath = Path.Combine(baseDirectory, "shaders", shaderFileName);
            return Path.GetFullPath(shaderPath);
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
        /// Clears property and preview panels when no asset is selected.
        /// </summary>
        void HandleAssetSelectionCleared() {
            propertiesPanel.ShowEmpty();
            previewPanel.ClearPreview();
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
        ShaderModuleManager BuildShaderModuleManager() {
            string projectRoot = ResolveProjectRootPath(projectPath);
            string shaderRootPath = ResolveShaderRootPath(projectRoot);
            string packageOutputPath = ResolveShaderPackageOutputPath(projectRoot);
            ShaderPackageBuildOptions buildOptions = BuildShaderPackageOptions();
            var options = new ShaderModuleManagerOptions(
                shaderRootPath,
                packageOutputPath,
                buildOptions,
                ShaderCompileTarget.DirectX11,
                ShaderBuildDelayMilliseconds);
            return new ShaderModuleManager(options);
        }

        /// <summary>
        /// Builds the default shader package build options for the editor.
        /// </summary>
        /// <returns>Shader package build options.</returns>
        ShaderPackageBuildOptions BuildShaderPackageOptions() {
            ShaderTargetBuildOptions[] targets = new[] {
                new ShaderTargetBuildOptions(ShaderCompileTarget.DirectX11, new ShaderModel(4, 0))
            };
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
        /// Resolves the project root directory from a project root or project file path.
        /// </summary>
        /// <param name="projectPath">Project root directory or project file path.</param>
        /// <returns>Absolute path to the project root directory.</returns>
        string ResolveProjectRootPath(string projectPath) {
            if (string.IsNullOrWhiteSpace(projectPath)) {
                throw new InvalidOperationException("Project path must be provided.");
            }

            if (Directory.Exists(projectPath)) {
                return Path.GetFullPath(projectPath);
            }

            if (File.Exists(projectPath)) {
                string directory = Path.GetDirectoryName(projectPath);
                if (string.IsNullOrWhiteSpace(directory)) {
                    throw new InvalidOperationException("Project file path does not include a directory.");
                }

                return Path.GetFullPath(directory);
            }

            return Path.GetFullPath(projectPath);
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
            var manager = new AssetImportManager(projectRootPath);
            for (int i = 0; i < importers.Count; i++) {
                IAssetImporterRegistration registration = importers[i];
                if (registration == null) {
                    throw new InvalidOperationException("Importer registrations must not be null.");
                }

                registration.Register(manager);
            }

            manager.GenerateMissingImportSettings();
            manager.ImportTexturesMissingCache();
            return manager;
        }
    }
}
