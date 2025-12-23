namespace helengine.editor {
    /// <summary>
    /// Coordinates editor core initialization, docked UI setup, and scene bootstrapping for a host window.
    /// </summary>
    public class EditorSession {
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
        /// Main viewport dock panel.
        /// </summary>
        readonly DockableViewport mainViewport;
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
        /// Initializes a new editor session and sets up cameras, docking, and starter content.
        /// </summary>
        /// <param name="core">Editor core instance that owns shared state.</param>
        /// <param name="projectPath">Path to the project being edited.</param>
        /// <param name="uiFont">Font used for editor UI text.</param>
        /// <param name="titleText">Initial window title text.</param>
        /// <param name="render3D">3D renderer instance.</param>
        /// <param name="render2D">2D renderer instance.</param>
        /// <param name="input">Input manager instance.</param>
        /// <param name="renderWidth">Initial render width in pixels.</param>
        /// <param name="renderHeight">Initial render height in pixels.</param>
        public EditorSession(
            EditorCore core,
            string projectPath,
            FontAsset uiFont,
            string titleText,
            RenderManager3D render3D,
            RenderManager2D render2D,
            InputManager input,
            int renderWidth,
            int renderHeight) {
            this.core = core;
            this.projectPath = projectPath ?? string.Empty;
            this.uiFont = uiFont;

            core.Initialize(render3D, render2D, input);
            core.InputManager.SetKeyboardActive(true);

            uiCameraEntity = new EditorEntity();
            uiCameraEntity.Position = new float3(0, 3, -8);
            uiCameraComponent = new CameraComponent();
            uiCameraComponent.LayerMask = 0b1000000000000000;
            uiCameraComponent.CameraDrawOrder = 255;
            uiCameraEntity.AddComponent(uiCameraComponent);

            sceneCameraEntity = new EditorEntity();
            sceneCameraEntity.Position = new float3(0, 3, -8);
            sceneCameraComponent = new CameraComponent();
            sceneCameraComponent.LayerMask = 0b0100000000000000;
            sceneCameraEntity.AddComponent(sceneCameraComponent);
            sceneCameraEntity.AddComponent(new EditorViewportCameraController(sceneCameraComponent));

            float3 toOrigin = float3.Normalize(new float3(-sceneCameraEntity.Position.X, -sceneCameraEntity.Position.Y, -sceneCameraEntity.Position.Z));
            double yaw = Math.Atan2(toOrigin.X, -toOrigin.Z);
            double pitch = Math.Asin(toOrigin.Y);
            float4 orientation;
            float4.CreateFromYawPitchRoll((float)yaw, (float)pitch, 0f, out orientation);
            sceneCameraEntity.Orientation = orientation;

            titleBar = new EditorTitleBar(uiFont, Math.Max(1, renderWidth), titleText ?? string.Empty);

            dockingManager = new DockingManager();
            sceneHierarchyPanel = new SceneHierarchyPanel(uiFont);
            assetBrowserPanel = new AssetBrowserPanel(uiFont, this.projectPath);
            mainViewport = new DockableViewport(sceneCameraComponent, uiFont);
            propertiesPanel = new PropertiesPanel(uiFont);
            loggerPanel = new LoggerPanel(uiFont);

            sceneHierarchyPanel.Size = new int2(280, 600);
            assetBrowserPanel.Size = new int2(500, 240);
            propertiesPanel.Size = new int2(280, 600);

            dockingManager.Layout.Add(sceneHierarchyPanel);
            dockingManager.Layout.Add(assetBrowserPanel);
            dockingManager.Layout.Add(mainViewport);
            dockingManager.Layout.Add(propertiesPanel);
            dockingManager.Layout.Add(loggerPanel);

            dockingManager.Layout.DockAsRoot(mainViewport);
            dockingManager.Layout.DockRelative(assetBrowserPanel, mainViewport, DockInsertDirection.Bottom, 0.7f);
            dockingManager.Layout.DockRelative(sceneHierarchyPanel, mainViewport, DockInsertDirection.Left, 0.3f);
            dockingManager.Layout.DockRelative(propertiesPanel, mainViewport, DockInsertDirection.Right, 0.75f);
            dockingManager.Layout.DockRelative(loggerPanel, assetBrowserPanel, DockInsertDirection.Fill, 0.5f);

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
        public DockableViewport MainViewport => mainViewport;

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
            loggerPanel.Detach();
            core.Dispose();
        }

        /// <summary>
        /// Creates a starter scene with a cube and a ground plane.
        /// </summary>
        void BuildStartScene() {
            Core coreInstance = helengine.Core.Instance;
            EditorEntity cube = new EditorEntity();
            cube.LayerMask = 0b0100000000000000;
            MeshComponent mesh = new MeshComponent();
            cube.AddComponent(mesh);
            ModelAsset modelData = ModelUtils.GenerateCubeMesh(float3.Zero, float3.One);
            RuntimeModel renderData = coreInstance.RenderManager3D.BuildModelFromRaw(modelData);
            mesh.Model = renderData;

            EditorEntity plane = new EditorEntity();
            plane.LayerMask = 0b0100000000000000;
            plane.Scale = new float3(10, 1, 10);
            MeshComponent planeMesh = new MeshComponent();
            plane.AddComponent(planeMesh);
            ModelAsset planeModelData = ModelUtils.GeneratePlaneMesh(float3.Zero, float3.One);
            RuntimeModel planeRenderData = coreInstance.RenderManager3D.BuildModelFromRaw(planeModelData);
            planeMesh.Model = planeRenderData;
        }
    }
}
