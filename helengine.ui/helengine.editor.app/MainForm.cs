using helengine.editor;
using helengine.editor.windows;
using helengine.sharpdx;
using System;
using System.IO;

namespace helengine.editor.app {
    /// <summary>
    /// Main editor host form for Helengine, wiring up rendering and dockable UI.
    /// </summary>
    public partial class MainForm : Form {
        private Thread thread;
        private bool closed;
        private bool initialized;

        private CameraComponent? uiCameraComponent;
        private CameraComponent? sceneCameraComponent;
        private DockableViewport? mainViewport;
        private SceneHierarchyPanel? sceneHierarchyPanel;
        private DockLayoutEngine? dockLayout;
        private DockPreviewOverlay? dockPreviewOverlay;
        private DockableEntity? lastDragging;
        private bool dockHintValid;
        private DockHint dockHint;
        private FontAsset? uiFont;
        private EditorTitleBar? titleBar;

        /// <summary>
        /// Initializes a new instance of the main editor form and configures custom chrome.
        /// </summary>
        public MainForm() {
            InitializeComponent();
            ControlBox = false;
            FormBorderStyle = FormBorderStyle.None;

            InitializeEditor();
        }

        /// <summary>
        /// Initializes the main editor form for a specific project path.
        /// </summary>
        /// <param name="projectPath">Path to the project to open.</param>
        public MainForm(string projectPath) : this() {
            if (!string.IsNullOrWhiteSpace(projectPath)) {
                string title = $"helengine - {Path.GetFileName(projectPath)}";
                SetWindowTitle(title);
            }
        }

        /// <summary>
        /// Gets the height of the active title bar, falling back to the default when uninitialized.
        /// </summary>
        private int TitleBarHeight => titleBar?.Height ?? EditorTitleBar.HeightPixels;

        /// <summary>
        /// Updates the form title text and keeps the title bar in sync.
        /// </summary>
        /// <param name="title">Title text to display.</param>
        private void SetWindowTitle(string title) {
            Text = title;
            if (titleBar != null) {
                titleBar.Title = title;
            }
        }

        /// <summary>
        /// Creates a simple starter scene with a cube and plane to exercise rendering.
        /// </summary>
        private void MakeStartScene() {
            Core core = Core.Instance;
            EditorEntity cube = new EditorEntity();
            cube.LayerMask = 0b0100000000000000;
            MeshComponent mesh = new MeshComponent();
            cube.AddComponent(mesh);
            ModelAsset modelData = ModelUtils.GenerateCubeMesh(float3.Zero, float3.One);
            RuntimeModel renderData = core.RenderManager3D.BuildModelFromRaw(modelData);
            mesh.Model = renderData;

            EditorEntity plane = new EditorEntity();
            plane.LayerMask = 0b0100000000000000;
            plane.Scale = new float3(10, 1, 10);
            MeshComponent planeMesh = new MeshComponent();
            plane.AddComponent(planeMesh);
            ModelAsset planeModelData = ModelUtils.GeneratePlaneMesh(float3.Zero, float3.One);
            RuntimeModel planeRenderData = core.RenderManager3D.BuildModelFromRaw(planeModelData);
            planeMesh.Model = planeRenderData;
        }

        /// <summary>
        /// Sets up rendering, input, cameras, UI chrome, and the initial layout.
        /// </summary>
        private void InitializeEditor() {
            EditorCore core = new EditorCore(null);
            var rm3d = new SharpDXRenderer3D();
            core.Initialize(rm3d, rm3d.Render2D, new InputManagerWindows(this.Handle));

            int renderWidth = Math.Max(1, ClientSize.Width);
            int renderHeight = Math.Max(1, ClientSize.Height);
            core.RenderManager3D.AddWindow(this.Handle, renderWidth, renderHeight);

            uiFont = GDIFontProcessor.ImportFont(new Font("Consolas", 12, FontStyle.Regular, GraphicsUnit.Pixel));

            EditorEntity uiCam = new EditorEntity();
            uiCam.Position = new float3(0, 3, -8);
            uiCameraComponent = new CameraComponent();
            uiCameraComponent.LayerMask = 0b1000000000000000;
            uiCameraComponent.Viewport = new float4(0, 0, renderWidth, renderHeight);
            uiCam.AddComponent(uiCameraComponent);

            EditorEntity sceneCam = new EditorEntity();
            sceneCam.Position = new float3(0, 3, -8);
            sceneCameraComponent = new CameraComponent();
            sceneCameraComponent.LayerMask = 0b0100000000000000;
            sceneCam.AddComponent(sceneCameraComponent);

            titleBar = new EditorTitleBar(uiFont, ClientSize.Width, Text);
            TitleBarWindowAdapter.Attach(titleBar, this);
            SetWindowTitle(Text);

            dockLayout = new DockLayoutEngine();

            sceneHierarchyPanel = new SceneHierarchyPanel(uiFont);
            mainViewport = new DockableViewport(sceneCameraComponent, uiFont);
            sceneHierarchyPanel.Size = new int2(280, 600);
            dockLayout.Add(sceneHierarchyPanel);
            dockLayout.Add(mainViewport);
            dockPreviewOverlay = new DockPreviewOverlay();

            if (mainViewport != null && sceneHierarchyPanel != null) {
                dockLayout.DockAsRoot(mainViewport);
                dockLayout.DockRelative(sceneHierarchyPanel, mainViewport, DockInsertDirection.Left, 0.3f);
            }

            MakeStartScene();
            sceneHierarchyPanel.RefreshHierarchy();

            UpdateLayout();

            thread = new Thread(RunEditorLoop);
            thread.Start();

            initialized = true;

            //EditorEntity fpsView = new EditorEntity();
            //fpsView.LayerMask = 0b00000010;
            //TextComponent fpsText = new TextComponent();
            //fpsText.Size = new int2(100, 100);
            //fpsText.Text = "FPS 200";
            //fpsView.Position = new float3(500, 100, 0);
            //fpsView.AddComponent(fpsText);
            //fpsText.Font = fontAsset;
        }

        /// <summary>
        /// Drives the editor update and draw loop on a worker thread.
        /// </summary>
        private void RunEditorLoop() {
            TimeSpan span = TimeSpan.FromMilliseconds(1000 / 120.0);
            for (; ; ) {
                Thread.Sleep(span);
                if (closed) {
                    break;
                }

                try {
                    Invoke(() => {
                        Core.Instance.Update();
                        UpdateLayout();
                        UpdateDockPreview();
                        sceneHierarchyPanel?.RefreshHierarchy();
                        Core.Instance.Draw();
                    });
                } catch { }
            }
        }

        /// <summary>
        /// Stops the editor loop and disposes engine resources when the window closes.
        /// </summary>
        /// <param name="e">Event data.</param>
        protected override void OnClosed(EventArgs e) {
            base.OnClosed(e);

            closed = true;
            Core.Instance.Dispose();
        }

        /// <summary>
        /// Handles activation to allow future input focus handling hooks.
        /// </summary>
        /// <param name="e">Event data.</param>
        protected override void OnActivated(EventArgs e) {
            base.OnActivated(e);

            //Keyboard.SetActive(true);
        }

        /// <summary>
        /// Handles window deactivation to support future focus-aware behaviors.
        /// </summary>
        /// <param name="e">Event data.</param>
        protected override void OnDeactivate(EventArgs e) {
            base.OnDeactivate(e);

            //Keyboard.SetActive(false);
        }

        /// <summary>
        /// Resizes render targets and UI layout when the window size changes.
        /// </summary>
        /// <param name="e">Event data.</param>
        protected override void OnResize(EventArgs e) {
            base.OnResize(e);
            if (!initialized) {
                return;
            }

            var rm3d = Core.Instance?.RenderManager3D;
            if (rm3d != null) {
                rm3d.OnWindowResize(Handle, ClientSize.Width, ClientSize.Height);
            }
            UpdateLayout();
        }

        /// <summary>
        /// Updates camera viewports, title bar layout, and dock layout sizing.
        /// </summary>
        private void UpdateLayout() {
            if (uiFont == null) {
                return;
            }

            int renderWidth = Math.Max(1, ClientSize.Width);
            int renderHeight = Math.Max(1, ClientSize.Height);

            if (titleBar != null) {
                titleBar.UpdateLayout(ClientSize.Width);
            }

            if (uiCameraComponent != null) {
                uiCameraComponent.Viewport = new float4(0, 0, renderWidth, renderHeight);
            }

            if (dockLayout != null) {
                int availableHeight = Math.Max(0, renderHeight - TitleBarHeight);
                dockLayout.Layout(new int2(renderWidth, availableHeight), new float3(0, TitleBarHeight, 0));
            }
        }

        /// <summary>
        /// Evaluates docking hints for dragging panels and shows the preview overlay.
        /// </summary>
        private void UpdateDockPreview() {
            if (dockLayout == null || dockPreviewOverlay == null || Core.Instance?.InputManager == null) {
                return;
            }

            DockableEntity? dragging = null;
            var dockables = dockLayout.Dockables;
            for (int i = 0; i < dockables.Count; i++) {
                var de = dockables[i];
                if (de.IsDragging) {
                    dragging = de;
                    break;
                }
            }

            if (dragging == null) {
                if (lastDragging != null && dockHintValid) {
                    ApplyDockHint(lastDragging);
                }
                dockPreviewOverlay.Hide();
                lastDragging = null;
                dockHintValid = false;
                return;
            }

            var mouse = Core.Instance.InputManager.Mouse.GetState();
            int2 pointer = new int2(mouse.X, mouse.Y);

            int renderWidth = Math.Max(1, ClientSize.Width);
            int renderHeight = Math.Max(1, ClientSize.Height);
            int availableHeight = Math.Max(0, renderHeight - TitleBarHeight);
            int2 hostSize = new int2(renderWidth, availableHeight);
            float3 origin = new float3(0, TitleBarHeight, 0);

            if (dragging != null && dragging.ConsumeUndockRequest()) {
                dockLayout.Undock(dragging);
                UpdateLayout();
            }

            bool fillOnly = !dockLayout.HasDocked;

            if (dockLayout.TryGetDockHint(pointer, hostSize, origin, fillOnly, out var hint)) {
                dockPreviewOverlay.Show(hint.Position, hint.Size);
                dockHintValid = true;
                dockHint = hint;
                lastDragging = dragging;
            } else {
                dockPreviewOverlay.Hide();
                dockHintValid = false;
                lastDragging = dragging;
            }
        }

        /// <summary>
        /// Applies the pending dock hint to a floating dockable entity.
        /// </summary>
        /// <param name="entity">Dockable entity to dock.</param>
        private void ApplyDockHint(DockableEntity entity) {
            if (!dockHintValid) {
                return;
            }

            if (dockLayout != null) {
                dockLayout.Dock(entity, dockHint);
            }
            dockHintValid = false;
            dockPreviewOverlay?.Hide();
            UpdateLayout();
        }
    }
}
