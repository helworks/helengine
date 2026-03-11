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
        /// Built-in runtime shader name used for Vulkan starter-scene materials.
        /// </summary>
        const string DefaultRuntimeShaderName = "EditorDefaultMesh";
        /// <summary>
        /// Built-in runtime shader variant.
        /// </summary>
        const string DefaultRuntimeShaderVariant = "default";
        /// <summary>
        /// Built-in runtime shader vertex entry point.
        /// </summary>
        const string DefaultRuntimeVertexEntryPoint = "VS";
        /// <summary>
        /// Built-in runtime shader pixel entry point.
        /// </summary>
        const string DefaultRuntimePixelEntryPoint = "PS";
        /// <summary>
        /// Draw order used by the main scene camera.
        /// </summary>
        const byte SceneCameraDrawOrder = 0;
        /// <summary>
        /// Draw order used by the gizmo overlay camera.
        /// </summary>
        const byte GizmoCameraDrawOrder = 1;
        /// <summary>
        /// Built-in HLSL source used to generate Vulkan starter-scene materials.
        /// </summary>
        const string DefaultRuntimeShaderSource =
            "cbuffer TransformBuffer : register(b0)\n" +
            "{\n" +
            "    float4x4 worldViewProj;\n" +
            "};\n" +
            "\n" +
            "struct VS_IN\n" +
            "{\n" +
            "    float3 pos : POSITION;\n" +
            "    float3 normal : NORMAL;\n" +
            "    float2 texCoord : TEXCOORD0;\n" +
            "};\n" +
            "\n" +
            "struct PS_IN\n" +
            "{\n" +
            "    float4 pos : SV_POSITION;\n" +
            "    float3 normal : NORMAL;\n" +
            "};\n" +
            "\n" +
            "PS_IN VS(VS_IN input)\n" +
            "{\n" +
            "    PS_IN output;\n" +
            "    output.pos = mul(float4(input.pos, 1.0f), worldViewProj);\n" +
            "    output.normal = input.normal;\n" +
            "    return output;\n" +
            "}\n" +
            "\n" +
            "float4 PS(PS_IN input) : SV_Target\n" +
            "{\n" +
            "    float3 displayNormal = normalize(input.normal) * 0.5 + 0.5;\n" +
            "    return float4(displayNormal, 1.0);\n" +
            "}\n";
        /// <summary>
        /// Built-in runtime shader name used for Vulkan transform-gizmo materials.
        /// </summary>
        const string TransformGizmoRuntimeShaderName = "EditorTransformGizmo";
        /// <summary>
        /// Built-in HLSL source used to generate Vulkan transform-gizmo materials.
        /// </summary>
        const string TransformGizmoRuntimeShaderSource =
            "cbuffer TransformBuffer : register(b0)\n" +
            "{\n" +
            "    float4x4 worldViewProj;\n" +
            "};\n" +
            "\n" +
            "struct VS_IN\n" +
            "{\n" +
            "    float3 pos : POSITION;\n" +
            "    float3 normal : NORMAL;\n" +
            "    float2 texCoord : TEXCOORD0;\n" +
            "};\n" +
            "\n" +
            "struct PS_IN\n" +
            "{\n" +
            "    float4 pos : SV_POSITION;\n" +
            "    float3 normal : NORMAL;\n" +
            "    float2 marker : TEXCOORD0;\n" +
            "};\n" +
            "\n" +
            "float3 DecodeHandleColor(float2 marker)\n" +
            "{\n" +
            "    if (marker.x > 0.85f && marker.y > 0.85f)\n" +
            "    {\n" +
            "        return float3(1.00f, 0.90f, 0.20f);\n" +
            "    }\n" +
            "\n" +
            "    if (marker.x > 0.45f && marker.x < 0.55f && marker.y > 0.85f)\n" +
            "    {\n" +
            "        return float3(1.00f, 0.35f, 0.95f);\n" +
            "    }\n" +
            "\n" +
            "    if (marker.x > 0.85f && marker.y > 0.45f && marker.y < 0.55f)\n" +
            "    {\n" +
            "        return float3(0.25f, 0.95f, 0.95f);\n" +
            "    }\n" +
            "\n" +
            "    if (marker.y > 0.5f)\n" +
            "    {\n" +
            "        return float3(0.20f, 0.50f, 1.00f);\n" +
            "    }\n" +
            "\n" +
            "    if (marker.x > 0.5f)\n" +
            "    {\n" +
            "        return float3(0.20f, 0.95f, 0.35f);\n" +
            "    }\n" +
            "\n" +
            "    return float3(1.00f, 0.30f, 0.30f);\n" +
            "}\n" +
            "\n" +
            "PS_IN VS(VS_IN input)\n" +
            "{\n" +
            "    PS_IN output;\n" +
            "    output.pos = mul(float4(input.pos, 1.0f), worldViewProj);\n" +
            "    output.normal = input.normal;\n" +
            "    output.marker = input.texCoord;\n" +
            "    return output;\n" +
            "}\n" +
            "\n" +
            "float4 PS(PS_IN input) : SV_Target\n" +
            "{\n" +
            "    float3 normal = normalize(input.normal);\n" +
            "    float3 lightDirection0 = normalize(float3(0.45f, 0.85f, -0.30f));\n" +
            "    float3 lightDirection1 = normalize(float3(-0.60f, 0.55f, 0.65f));\n" +
            "    float diffuse0 = saturate(dot(normal, lightDirection0));\n" +
            "    float diffuse1 = saturate(dot(normal, lightDirection1));\n" +
            "    float lighting = 0.22f + diffuse0 * 0.72f + diffuse1 * 0.28f;\n" +
            "    float3 handleColor = DecodeHandleColor(input.marker);\n" +
            "    return float4(handleColor * lighting, 1.0f);\n" +
            "}\n";
        /// <summary>
        /// Built-in runtime shader name used for Vulkan transform-gizmo highlight materials.
        /// </summary>
        const string TransformGizmoHighlightRuntimeShaderName = "EditorTransformGizmoHighlight";
        /// <summary>
        /// Built-in HLSL source used to generate Vulkan transform-gizmo highlight materials.
        /// </summary>
        const string TransformGizmoHighlightRuntimeShaderSource =
            "cbuffer TransformBuffer : register(b0)\n" +
            "{\n" +
            "    float4x4 worldViewProj;\n" +
            "};\n" +
            "\n" +
            "struct VS_IN\n" +
            "{\n" +
            "    float3 pos : POSITION;\n" +
            "    float3 normal : NORMAL;\n" +
            "    float2 texCoord : TEXCOORD0;\n" +
            "};\n" +
            "\n" +
            "struct PS_IN\n" +
            "{\n" +
            "    float4 pos : SV_POSITION;\n" +
            "    float3 normal : NORMAL;\n" +
            "    float2 marker : TEXCOORD0;\n" +
            "};\n" +
            "\n" +
            "float3 DecodeHandleColor(float2 marker)\n" +
            "{\n" +
            "    if (marker.x > 0.85f && marker.y > 0.85f)\n" +
            "    {\n" +
            "        return float3(1.00f, 0.90f, 0.20f);\n" +
            "    }\n" +
            "\n" +
            "    if (marker.x > 0.45f && marker.x < 0.55f && marker.y > 0.85f)\n" +
            "    {\n" +
            "        return float3(1.00f, 0.35f, 0.95f);\n" +
            "    }\n" +
            "\n" +
            "    if (marker.x > 0.85f && marker.y > 0.45f && marker.y < 0.55f)\n" +
            "    {\n" +
            "        return float3(0.25f, 0.95f, 0.95f);\n" +
            "    }\n" +
            "\n" +
            "    if (marker.y > 0.5f)\n" +
            "    {\n" +
            "        return float3(0.20f, 0.50f, 1.00f);\n" +
            "    }\n" +
            "\n" +
            "    if (marker.x > 0.5f)\n" +
            "    {\n" +
            "        return float3(0.20f, 0.95f, 0.35f);\n" +
            "    }\n" +
            "\n" +
            "    return float3(1.00f, 0.30f, 0.30f);\n" +
            "}\n" +
            "\n" +
            "PS_IN VS(VS_IN input)\n" +
            "{\n" +
            "    PS_IN output;\n" +
            "    output.pos = mul(float4(input.pos, 1.0f), worldViewProj);\n" +
            "    output.normal = input.normal;\n" +
            "    output.marker = input.texCoord;\n" +
            "    return output;\n" +
            "}\n" +
            "\n" +
            "float4 PS(PS_IN input) : SV_Target\n" +
            "{\n" +
            "    float3 normal = normalize(input.normal);\n" +
            "    float3 lightDirection0 = normalize(float3(0.45f, 0.85f, -0.30f));\n" +
            "    float3 lightDirection1 = normalize(float3(-0.60f, 0.55f, 0.65f));\n" +
            "    float diffuse0 = saturate(dot(normal, lightDirection0));\n" +
            "    float diffuse1 = saturate(dot(normal, lightDirection1));\n" +
            "    float lighting = 0.22f + diffuse0 * 0.72f + diffuse1 * 0.28f;\n" +
            "    float3 handleColor = DecodeHandleColor(input.marker);\n" +
            "    return float4(1.0f, 1.0f, 1.0f, 1.0f);\n" +
            "}\n";
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
        /// <param name="toolbarIcons">Toolbar icon textures used by the main viewport tool buttons.</param>
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
            EditorViewportToolbarIconSet toolbarIcons,
            IReadOnlyList<IAssetImporterRegistration> importers) {
            this.core = core;
            this.projectPath = ResolveProjectRootPath(projectPath);
            this.uiFont = uiFont;
            toolbarIcons = toolbarIcons ?? throw new ArgumentNullException(nameof(toolbarIcons));

            core.Initialize(render3D, render2D, input);
            core.InputManager.SetKeyboardActive(true);

            EditorProjectPaths.Initialize(this.projectPath);

            assetImportManager = InitializeAssetImports(importers);

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
            sceneCameraComponent.LayerMask = EditorLayerMasks.SceneObjects;
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

            titleBar = new EditorTitleBar(uiFont, Math.Max(1, renderWidth), titleText ?? string.Empty);

            dockingManager = new DockingManager();
            sceneHierarchyPanel = new SceneHierarchyPanel(uiFont);
            assetBrowserPanel = new AssetBrowserPanel(uiFont, this.projectPath);
            mainViewport = new EditorViewport(sceneCameraComponent, uiFont, toolbarIcons);
            propertiesPanel = new PropertiesPanel(uiFont);
            loggerPanel = new LoggerPanel(uiFont);
            previewPanel = new PreviewPanel(uiFont);
            assetPickerModal = new AssetPickerModal(uiFont, this.projectPath);
            assetBrowserPanel.AssetSelected += HandleAssetSelected;
            assetBrowserPanel.SelectionCleared += HandleAssetSelectionCleared;
            propertiesPanel.ImportSettingsApplyRequested += HandleImportSettingsApplyRequested;
            EditorSelectionService.SelectionChanged += HandleSelectionChanged;
            EditorAssetPickerService.PickRequested += HandleAssetPickRequested;

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
            EditorShaderPackageService.Initialize(shaderModuleManager, runtimeTarget);
            shaderModuleManager.ShaderBuilt += HandleShaderBuilt;
            shaderModuleManager.Start();

            RuntimeMaterial defaultMeshMaterial = BuildDefaultMeshMaterial();
            RuntimeMaterial transformGizmoMaterial = BuildTransformGizmoNormalMaterial();
            RuntimeMaterial transformGizmoHighlightMaterial = BuildTransformGizmoHighlightMaterial();
            TransformTranslationGizmoFactory.Create(render3D, sceneCameraComponent, transformGizmoMaterial, transformGizmoHighlightMaterial);
            TransformRotationGizmoFactory.Create(render3D, sceneCameraComponent, transformGizmoMaterial, transformGizmoHighlightMaterial);
            TransformScaleGizmoFactory.Create(render3D, sceneCameraComponent, transformGizmoMaterial, transformGizmoHighlightMaterial);
            BuildStartScene(defaultMeshMaterial);
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
            gizmoCameraComponent.Viewport = sceneCameraComponent.Viewport;
            assetPickerModal.UpdateLayout(width, height);
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
            mainViewport.ClearInputBlockers();
            EditorViewportToolService.ClearToolMode(sceneCameraComponent);
            assetPickerModal.Hide();
            shaderModuleManager.ShaderBuilt -= HandleShaderBuilt;
            shaderModuleManager.Dispose();
            loggerPanel.Detach();
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
        /// Creates a starter scene with a cube and a ground plane.
        /// </summary>
        /// <param name="defaultMaterial">Material used by starter-scene meshes.</param>
        void BuildStartScene(RuntimeMaterial defaultMaterial) {
            if (defaultMaterial == null) {
                throw new ArgumentNullException(nameof(defaultMaterial));
            }

            Core coreInstance = helengine.Core.Instance;
            EditorEntity cube = new EditorEntity();
            cube.Name = "Cube";
            cube.LayerMask = EditorLayerMasks.SceneObjects;
            MeshComponent mesh = new MeshComponent();
            cube.AddComponent(mesh);
            ModelAsset modelData = ModelUtils.GenerateCubeMesh(float3.Zero, float3.One);
            RuntimeModel renderData = coreInstance.RenderManager3D.BuildModelFromRaw(modelData);
            mesh.Model = renderData;
            mesh.Material = defaultMaterial;

            EditorEntity plane = new EditorEntity();
            plane.Name = "Ground";
            plane.LayerMask = EditorLayerMasks.SceneObjects;
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
            if (core.RenderManager3D is helengine.directx11.DirectX11Renderer3D) {
                return BuildDirectX11DefaultMeshMaterial();
            }

            if (core.RenderManager3D is helengine.vulkan.VulkanRenderer3D) {
                return BuildVulkanDefaultMeshMaterial();
            }

            throw new InvalidOperationException("Unsupported renderer backend for default mesh material creation.");
        }

        /// <summary>
        /// Builds the default material used by transform gizmo meshes.
        /// </summary>
        /// <returns>Runtime material instance.</returns>
        RuntimeMaterial BuildTransformGizmoNormalMaterial() {
            if (core.RenderManager3D is helengine.directx11.DirectX11Renderer3D) {
                return BuildDirectX11TransformGizmoNormalMaterial();
            }

            if (core.RenderManager3D is helengine.vulkan.VulkanRenderer3D) {
                return BuildVulkanTransformGizmoNormalMaterial();
            }

            throw new InvalidOperationException("Unsupported renderer backend for transform gizmo material creation.");
        }

        /// <summary>
        /// Builds the highlighted material used by transform gizmo meshes.
        /// </summary>
        /// <returns>Runtime material instance.</returns>
        RuntimeMaterial BuildTransformGizmoHighlightMaterial() {
            if (core.RenderManager3D is helengine.directx11.DirectX11Renderer3D) {
                return BuildDirectX11TransformGizmoHighlightMaterial();
            }

            if (core.RenderManager3D is helengine.vulkan.VulkanRenderer3D) {
                return BuildVulkanTransformGizmoHighlightMaterial();
            }

            throw new InvalidOperationException("Unsupported renderer backend for transform gizmo highlight material creation.");
        }

        /// <summary>
        /// Builds the starter-scene material for the DirectX11 renderer.
        /// </summary>
        /// <returns>Runtime material instance.</returns>
        RuntimeMaterial BuildDirectX11DefaultMeshMaterial() {
            return BuildDirectX11MaterialFromBuiltInShader("MiniCube.fx");
        }

        /// <summary>
        /// Builds the default transform-gizmo material for the DirectX11 renderer.
        /// </summary>
        /// <returns>Runtime material instance.</returns>
        RuntimeMaterial BuildDirectX11TransformGizmoNormalMaterial() {
            return BuildDirectX11MaterialFromBuiltInShader("TransformGizmo.fx");
        }

        /// <summary>
        /// Builds the highlighted transform-gizmo material for the DirectX11 renderer.
        /// </summary>
        /// <returns>Runtime material instance.</returns>
        RuntimeMaterial BuildDirectX11TransformGizmoHighlightMaterial() {
            return BuildDirectX11MaterialFromBuiltInShader("TransformGizmoHighlight.fx");
        }

        /// <summary>
        /// Builds a DirectX11 runtime material from a built-in shader file.
        /// </summary>
        /// <param name="shaderFileName">Built-in shader file name.</param>
        /// <returns>Runtime material instance.</returns>
        RuntimeMaterial BuildDirectX11MaterialFromBuiltInShader(string shaderFileName) {
            if (string.IsNullOrWhiteSpace(shaderFileName)) {
                throw new ArgumentException("Shader file name must be provided.", nameof(shaderFileName));
            }

            string shaderPath = ResolveBuiltInShaderPath(shaderFileName);
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
                Variant = DefaultRuntimeShaderVariant
            };

            return core.RenderManager3D.BuildMaterialFromRaw(materialAsset, shaderAsset);
        }

        /// <summary>
        /// Builds the starter-scene material for the Vulkan renderer.
        /// </summary>
        /// <returns>Runtime material instance.</returns>
        RuntimeMaterial BuildVulkanDefaultMeshMaterial() {
            ShaderAsset shaderAsset = BuildRuntimeShaderAsset(
                ShaderCompileTarget.Vulkan,
                DefaultRuntimeShaderName,
                DefaultRuntimeShaderSource,
                DefaultRuntimeVertexEntryPoint,
                DefaultRuntimePixelEntryPoint);

            string shaderName = DefaultRuntimeShaderName;
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
        /// Builds the default transform-gizmo material for the Vulkan renderer.
        /// </summary>
        /// <returns>Runtime material instance.</returns>
        RuntimeMaterial BuildVulkanTransformGizmoNormalMaterial() {
            ShaderAsset shaderAsset = BuildRuntimeShaderAsset(
                ShaderCompileTarget.Vulkan,
                TransformGizmoRuntimeShaderName,
                TransformGizmoRuntimeShaderSource,
                DefaultRuntimeVertexEntryPoint,
                DefaultRuntimePixelEntryPoint);

            string shaderName = TransformGizmoRuntimeShaderName;
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
        /// Builds the highlighted transform-gizmo material for the Vulkan renderer.
        /// </summary>
        /// <returns>Runtime material instance.</returns>
        RuntimeMaterial BuildVulkanTransformGizmoHighlightMaterial() {
            ShaderAsset shaderAsset = BuildRuntimeShaderAsset(
                ShaderCompileTarget.Vulkan,
                TransformGizmoHighlightRuntimeShaderName,
                TransformGizmoHighlightRuntimeShaderSource,
                DefaultRuntimeVertexEntryPoint,
                DefaultRuntimePixelEntryPoint);

            string shaderName = TransformGizmoHighlightRuntimeShaderName;
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
        /// Builds a runtime shader asset for the specified target from in-memory HLSL source.
        /// </summary>
        /// <param name="target">Shader compile target.</param>
        /// <param name="shaderName">Logical shader name.</param>
        /// <param name="source">HLSL shader source.</param>
        /// <param name="vertexEntryPoint">Vertex entry point.</param>
        /// <param name="pixelEntryPoint">Pixel entry point.</param>
        /// <returns>Compiled shader asset.</returns>
        ShaderAsset BuildRuntimeShaderAsset(
            ShaderCompileTarget target,
            string shaderName,
            string source,
            string vertexEntryPoint,
            string pixelEntryPoint) {
            if (string.IsNullOrWhiteSpace(shaderName)) {
                throw new ArgumentException("Shader name must be provided.", nameof(shaderName));
            }

            if (string.IsNullOrWhiteSpace(source)) {
                throw new ArgumentException("Shader source must be provided.", nameof(source));
            }

            if (string.IsNullOrWhiteSpace(vertexEntryPoint)) {
                throw new ArgumentException("Vertex entry point must be provided.", nameof(vertexEntryPoint));
            }

            if (string.IsNullOrWhiteSpace(pixelEntryPoint)) {
                throw new ArgumentException("Pixel entry point must be provided.", nameof(pixelEntryPoint));
            }

            ShaderCompileService compileService = CreateRuntimeShaderCompileService(target);
            string sourcePath = string.Concat(shaderName, ".hlsl");
            ShaderSourceInfo sourceInfo = new ShaderSourceInfo(sourcePath, source);
            ShaderCompileOptions compileOptions = new ShaderCompileOptions(
                ShaderBindingPolicies.Default,
                true,
                false,
                false);
            ShaderDefine[] defines = Array.Empty<ShaderDefine>();

            string vertexProgramName = string.Concat(shaderName, ".vs");
            string pixelProgramName = string.Concat(shaderName, ".ps");

            ShaderCompileResult vertexResult = CompileRuntimeShaderProgram(
                compileService,
                sourceInfo,
                target,
                ShaderStage.Vertex,
                vertexProgramName,
                vertexEntryPoint,
                compileOptions,
                defines);
            ShaderCompileResult pixelResult = CompileRuntimeShaderProgram(
                compileService,
                sourceInfo,
                target,
                ShaderStage.Pixel,
                pixelProgramName,
                pixelEntryPoint,
                compileOptions,
                defines);

            ValidateRuntimeCompileResult(vertexResult, "vertex");
            ValidateRuntimeCompileResult(pixelResult, "pixel");

            string targetName = ShaderTargetNames.GetTargetName(target);
            ShaderProgramDefinition[] programs = new[] {
                vertexResult.ProgramDefinition,
                pixelResult.ProgramDefinition
            };
            ShaderProgramBinary[] binaries = new[] {
                new ShaderProgramBinary(vertexProgramName, ShaderStage.Vertex, targetName, DefaultRuntimeShaderVariant, vertexResult.Binary.Bytecode),
                new ShaderProgramBinary(pixelProgramName, ShaderStage.Pixel, targetName, DefaultRuntimeShaderVariant, pixelResult.Binary.Bytecode)
            };
            var moduleDefinition = new ShaderModuleDefinition(shaderName, programs, binaries);
            ShaderAsset shaderAsset = ShaderAsset.FromDefinition(moduleDefinition, target);

            if (string.IsNullOrWhiteSpace(shaderAsset.Id)) {
                throw new InvalidOperationException("Runtime shader asset id must be provided.");
            }

            return shaderAsset;
        }

        /// <summary>
        /// Creates a compile service configured for the specified runtime target.
        /// </summary>
        /// <param name="target">Runtime shader compile target.</param>
        /// <returns>Configured compile service.</returns>
        ShaderCompileService CreateRuntimeShaderCompileService(ShaderCompileTarget target) {
            string rootPath = ResolveAssetsRootPath(ResolveProjectRootPath(projectPath));
            var includeResolver = new ShaderFilesystemIncludeResolver(rootPath);
            var cache = new ShaderMemoryCompileCache();
            var hasher = new ShaderSourceHasher();
            var compileService = new ShaderCompileService(includeResolver, cache, hasher);

            switch (target) {
                case ShaderCompileTarget.DirectX11:
                    compileService.RegisterBackend(new helengine.directx11.DirectX11ShaderBackend());
                    break;
                case ShaderCompileTarget.Vulkan:
                    compileService.RegisterBackend(new helengine.vulkan.VulkanShaderBackend());
                    break;
                default:
                    throw new InvalidOperationException("Unsupported runtime shader target.");
            }

            return compileService;
        }

        /// <summary>
        /// Compiles a runtime shader stage.
        /// </summary>
        /// <param name="compileService">Compile service used for compilation.</param>
        /// <param name="sourceInfo">Shader source and source path.</param>
        /// <param name="target">Shader compile target.</param>
        /// <param name="stage">Shader stage to compile.</param>
        /// <param name="programName">Program name for the compiled stage.</param>
        /// <param name="entryPoint">Entry point for the compiled stage.</param>
        /// <param name="compileOptions">Compile options.</param>
        /// <param name="defines">Define set.</param>
        /// <returns>Compile result for the stage.</returns>
        ShaderCompileResult CompileRuntimeShaderProgram(
            ShaderCompileService compileService,
            ShaderSourceInfo sourceInfo,
            ShaderCompileTarget target,
            ShaderStage stage,
            string programName,
            string entryPoint,
            ShaderCompileOptions compileOptions,
            IReadOnlyList<ShaderDefine> defines) {
            var request = new ShaderCompileRequest(
                sourceInfo,
                programName,
                entryPoint,
                stage,
                target,
                new ShaderModel(4, 0),
                DefaultRuntimeShaderVariant,
                defines,
                compileOptions);
            return compileService.Compile(request);
        }

        /// <summary>
        /// Validates runtime compile results and throws on failure diagnostics.
        /// </summary>
        /// <param name="result">Compile result to validate.</param>
        /// <param name="stageName">Display stage name for diagnostics.</param>
        void ValidateRuntimeCompileResult(ShaderCompileResult result, string stageName) {
            if (result == null) {
                throw new ArgumentNullException(nameof(result));
            }

            if (result.Success) {
                return;
            }

            string message = string.Concat("Runtime ", stageName, " shader compilation failed.");
            if (result.Diagnostics.Count > 0 && !string.IsNullOrWhiteSpace(result.Diagnostics[0].Message)) {
                message = result.Diagnostics[0].Message;
            }

            throw new InvalidOperationException(message);
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

            if (!File.Exists(path)) {
                throw new FileNotFoundException("Material file was not found.", path);
            }

            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                Asset asset = AssetSerializer.Deserialize(stream);
                if (asset is MaterialAsset materialAsset) {
                    return materialAsset;
                }
            }

            throw new InvalidOperationException("Selected asset is not a material asset.");
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




