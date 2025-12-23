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
        /// <summary>
        /// Window message identifier for hit testing.
        /// </summary>
        const int WmNcHitTest = 0x84;
        /// <summary>
        /// Hit test result for client area.
        /// </summary>
        const int HtClient = 1;
        /// <summary>
        /// Hit test result for left border.
        /// </summary>
        const int HtLeft = 10;
        /// <summary>
        /// Hit test result for right border.
        /// </summary>
        const int HtRight = 11;
        /// <summary>
        /// Hit test result for top border.
        /// </summary>
        const int HtTop = 12;
        /// <summary>
        /// Hit test result for top-left corner.
        /// </summary>
        const int HtTopLeft = 13;
        /// <summary>
        /// Hit test result for top-right corner.
        /// </summary>
        const int HtTopRight = 14;
        /// <summary>
        /// Hit test result for bottom border.
        /// </summary>
        const int HtBottom = 15;
        /// <summary>
        /// Hit test result for bottom-left corner.
        /// </summary>
        const int HtBottomLeft = 16;
        /// <summary>
        /// Hit test result for bottom-right corner.
        /// </summary>
        const int HtBottomRight = 17;
        /// <summary>
        /// Thickness in pixels for resizing the borderless window.
        /// </summary>
        const int ResizeBorderThickness = 6;

        /// <summary>
        /// Background thread that drives the editor update loop.
        /// </summary>
        private Thread thread;
        /// <summary>
        /// Tracks whether the form has been closed to stop the loop.
        /// </summary>
        private bool closed;
        /// <summary>
        /// Tracks whether initialization has completed to guard resize logic.
        /// </summary>
        private bool initialized;
        /// <summary>
        /// Stores the project path used to locate project assets.
        /// </summary>
        string projectPath = string.Empty;

        /// <summary>
        /// Camera used for 2D UI rendering.
        /// </summary>
        private CameraComponent? uiCameraComponent;
        /// <summary>
        /// Camera used for rendering the scene viewport.
        /// </summary>
        private CameraComponent? sceneCameraComponent;
        /// <summary>
        /// Dockable viewport used for 3D scene rendering.
        /// </summary>
        private DockableViewport? mainViewport;
        /// <summary>
        /// Dockable panel that shows the scene hierarchy.
        /// </summary>
        private SceneHierarchyPanel? sceneHierarchyPanel;
        /// <summary>
        /// Dockable assets panel that mirrors the project assets folder.
        /// </summary>
        AssetBrowserPanel? assetBrowserPanel;
        /// <summary>
        /// Docking manager that handles docking layout, previews, and resizing.
        /// </summary>
        DockingManager? dockingManager;
        /// <summary>
        /// UI font used for title bars and panel content.
        /// </summary>
        private FontAsset? uiFont;
        /// <summary>
        /// Custom title bar UI component.
        /// </summary>
        private EditorTitleBar? titleBar;

        /// <summary>
        /// Initializes a new instance of the main editor form and configures custom chrome.
        /// </summary>
        public MainForm() {
            InitializeWindowFrame();
            InitializeEditor();
        }

        /// <summary>
        /// Initializes the main editor form for a specific project path.
        /// </summary>
        /// <param name="projectPath">Path to the project to open.</param>
        public MainForm(string projectPath) {
            InitializeWindowFrame();
            this.projectPath = projectPath ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(this.projectPath)) {
                string title = $"helengine - {Path.GetFileName(this.projectPath)}";
                SetWindowTitle(title);
            }

            InitializeEditor();
        }

        /// <summary>
        /// Gets the height of the active title bar, falling back to the default when uninitialized.
        /// </summary>
        private int TitleBarHeight => titleBar?.Height ?? EditorTitleBar.HeightPixels;

        /// <summary>
        /// Initializes the form shell and window chrome settings.
        /// </summary>
        void InitializeWindowFrame() {
            InitializeComponent();
            ControlBox = false;
            FormBorderStyle = FormBorderStyle.None;
        }

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

            dockingManager = new DockingManager();

            sceneHierarchyPanel = new SceneHierarchyPanel(uiFont);
            assetBrowserPanel = new AssetBrowserPanel(uiFont, projectPath);
            mainViewport = new DockableViewport(sceneCameraComponent, uiFont);
            sceneHierarchyPanel.Size = new int2(280, 600);
            assetBrowserPanel.Size = new int2(500, 240);
            dockingManager.Layout.Add(sceneHierarchyPanel);
            dockingManager.Layout.Add(assetBrowserPanel);
            dockingManager.Layout.Add(mainViewport);

            if (mainViewport != null && sceneHierarchyPanel != null && assetBrowserPanel != null && dockingManager != null) {
                dockingManager.Layout.DockAsRoot(mainViewport);
                dockingManager.Layout.DockRelative(assetBrowserPanel, mainViewport, DockInsertDirection.Bottom, 0.7f);
                dockingManager.Layout.DockRelative(sceneHierarchyPanel, mainViewport, DockInsertDirection.Left, 0.3f);
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
                        UpdateDocking();
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

            if (UpdateMinimumWindowSize()) {
                return;
            }

            if (titleBar != null) {
                titleBar.UpdateLayout(ClientSize.Width);
            }

            if (uiCameraComponent != null) {
                uiCameraComponent.Viewport = new float4(0, 0, renderWidth, renderHeight);
            }

            if (dockingManager != null) {
                int availableHeight = Math.Max(0, renderHeight - TitleBarHeight);
                dockingManager.Layout.Layout(new int2(renderWidth, availableHeight), new float3(0, TitleBarHeight, 0));
            }
        }

        /// <summary>
        /// Updates docking interactions and applies cursor feedback.
        /// </summary>
        void UpdateDocking() {
            if (dockingManager == null || Core.Instance?.InputManager == null) {
                return;
            }

            var mouse = Core.Instance.InputManager.Mouse.GetState();
            int2 pointer = new int2(mouse.X, mouse.Y);

            int renderWidth = Math.Max(1, ClientSize.Width);
            int renderHeight = Math.Max(1, ClientSize.Height);
            int availableHeight = Math.Max(0, renderHeight - TitleBarHeight);
            int2 hostSize = new int2(renderWidth, availableHeight);
            float3 origin = new float3(0, TitleBarHeight, 0);

            bool layoutDirty = dockingManager.Update(pointer, mouse.LeftButton, hostSize, origin);

            switch (dockingManager.CursorState) {
                case DockingCursorState.VerticalSplit:
                    Cursor = Cursors.VSplit;
                    break;
                case DockingCursorState.HorizontalSplit:
                    Cursor = Cursors.HSplit;
                    break;
                default:
                    if (TryGetWindowResizeCursor(pointer, out var resizeCursor)) {
                        Cursor = resizeCursor;
                    } else {
                        Cursor = Cursors.Default;
                    }
                    break;
            }

            if (layoutDirty) {
                UpdateLayout();
            }
        }

        /// <summary>
        /// Applies the minimum window size needed to fit docked panels and the title bar.
        /// </summary>
        /// <returns>True when the window size was adjusted.</returns>
        bool UpdateMinimumWindowSize() {
            if (dockingManager == null) {
                return false;
            }

            int2 minHost = dockingManager.MinimumHostSize;
            int minWidth = Math.Max(1, minHost.X);
            int minHeight = Math.Max(1, minHost.Y + TitleBarHeight);
            MinimumSize = new Size(minWidth, minHeight);

            int targetWidth = Math.Max(Width, minWidth);
            int targetHeight = Math.Max(Height, minHeight);
            if (targetWidth != Width || targetHeight != Height) {
                Size = new Size(targetWidth, targetHeight);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Enables borderless window resizing by returning the appropriate hit test results.
        /// </summary>
        /// <param name="m">Windows message payload.</param>
        protected override void WndProc(ref Message m) {
            base.WndProc(ref m);

            if (m.Msg != WmNcHitTest || WindowState == FormWindowState.Maximized) {
                return;
            }

            if ((int)m.Result != HtClient) {
                return;
            }

            Point clientPoint = GetHitTestPoint(m.LParam);
            int hitTest = GetResizeHitTest(clientPoint);
            if (hitTest != HtClient) {
                m.Result = (IntPtr)hitTest;
            }
        }

        /// <summary>
        /// Converts a hit test message lParam into client coordinates.
        /// </summary>
        /// <param name="lParam">Raw lParam from the hit test message.</param>
        /// <returns>Point in client coordinates.</returns>
        Point GetHitTestPoint(IntPtr lParam) {
            int value = lParam.ToInt32();
            int x = (short)(value & 0xFFFF);
            int y = (short)((value >> 16) & 0xFFFF);
            return PointToClient(new Point(x, y));
        }

        /// <summary>
        /// Determines the resize hit test result for a client point.
        /// </summary>
        /// <param name="clientPoint">Point in client coordinates.</param>
        /// <returns>Hit test result for resizing, or client when not on an edge.</returns>
        int GetResizeHitTest(Point clientPoint) {
            int width = ClientSize.Width;
            int height = ClientSize.Height;

            bool left = clientPoint.X <= ResizeBorderThickness;
            bool right = clientPoint.X >= width - ResizeBorderThickness;
            bool top = clientPoint.Y <= ResizeBorderThickness;
            bool bottom = clientPoint.Y >= height - ResizeBorderThickness;

            if (left && top) {
                return HtTopLeft;
            }
            if (right && top) {
                return HtTopRight;
            }
            if (left && bottom) {
                return HtBottomLeft;
            }
            if (right && bottom) {
                return HtBottomRight;
            }
            if (left) {
                return HtLeft;
            }
            if (right) {
                return HtRight;
            }
            if (top) {
                return HtTop;
            }
            if (bottom) {
                return HtBottom;
            }

            return HtClient;
        }

        /// <summary>
        /// Determines whether the cursor should display a window resize indicator.
        /// </summary>
        /// <param name="pointer">Pointer position in client coordinates.</param>
        /// <param name="cursor">Resolved cursor for the resize handle.</param>
        /// <returns>True when a resize cursor should be shown.</returns>
        bool TryGetWindowResizeCursor(int2 pointer, out Cursor cursor) {
            cursor = Cursors.Default;
            if (WindowState == FormWindowState.Maximized) {
                return false;
            }

            int hitTest = GetResizeHitTest(new Point(pointer.X, pointer.Y));
            cursor = GetResizeCursor(hitTest);
            return hitTest != HtClient;
        }

        /// <summary>
        /// Maps hit test results to Windows resize cursors.
        /// </summary>
        /// <param name="hitTest">Hit test value.</param>
        /// <returns>Cursor that represents the resize direction.</returns>
        Cursor GetResizeCursor(int hitTest) {
            switch (hitTest) {
                case HtLeft:
                case HtRight:
                    return Cursors.SizeWE;
                case HtTop:
                case HtBottom:
                    return Cursors.SizeNS;
                case HtTopLeft:
                case HtBottomRight:
                    return Cursors.SizeNWSE;
                case HtTopRight:
                case HtBottomLeft:
                    return Cursors.SizeNESW;
                default:
                    return Cursors.Default;
            }
        }
    }
}
