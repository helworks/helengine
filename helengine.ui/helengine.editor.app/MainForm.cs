using helengine.editor;
using helengine.sharpdx;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace helengine.editor.app {
    /// <summary>
    /// Main editor host form for Helengine, wiring up rendering and dockable UI.
    /// </summary>
    public partial class MainForm : Form {
        private Thread thread;
        private bool closed;
        private bool initialized;
        private const int TitleBarHeight = 36;
        private const int TitleBarDoubleClickMs = 350;
        private const int TitleBarDoubleClickDistance = 6;

        private CameraComponent? uiCameraComponent;
        private CameraComponent? sceneCameraComponent;
        private DockableViewport? mainViewport;
        private DockLayoutEngine? dockLayout;
        private DockPreviewOverlay? dockPreviewOverlay;
        private DockableEntity? lastDragging;
        private bool dockHintValid;
        private DockRegion dockHintRegion;
        private float3 dockHintPos;
        private int2 dockHintSize;
        private FontAsset? uiFont;

        private EditorEntity? titleBarEntity;
        private SpriteComponent? titleBarBackground;
        private InteractableComponent? titleBarHitRegion;
        private TextComponent? titleTextComponent;
        private readonly List<(EditorEntity entity, int width)> menuButtons = new();
        private readonly List<(EditorEntity entity, int width)> windowControlButtons = new();
        private long lastTitleBarClickTicks;
        private int2 lastTitleBarClickPos;

        public MainForm() {
            InitializeComponent();
            ControlBox = false;
            FormBorderStyle = FormBorderStyle.None;

            initialize();
        }

        public MainForm(string projectPath) : this() {
            if (!string.IsNullOrWhiteSpace(projectPath)) {
                string title = $"helengine - {Path.GetFileName(projectPath)}";
                Text = title;
                if (titleTextComponent != null) {
                    titleTextComponent.Text = title;
                }
            }
        }

        private void makeStartScene() {
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

        private void initialize() {
            EditorCore core = new EditorCore(null);
            var rm3d = new SharpDXRenderManager3D();
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

            BuildTitleBar(uiFont, ClientSize.Width);

            dockLayout = new DockLayoutEngine();

            mainViewport = new DockableViewport(sceneCameraComponent, uiFont);
            mainViewport.Dock = DockRegion.Fill;
            dockLayout.Add(mainViewport);
            dockPreviewOverlay = new DockPreviewOverlay();

            makeStartScene();

            UpdateLayout();

            thread = new Thread(threadUpdate);
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

        private void threadUpdate() {
            TimeSpan span = TimeSpan.FromMilliseconds(1000 / 120.0);
            for (; ; ) {
                Thread.Sleep(span);
                if (closed) {
                    break;
                }

                try {
                    Invoke(() => {
                        UpdateLayout();
                        UpdateDockPreview();
                        Core.Instance.Update();
                        Core.Instance.Draw();
                    });
                } catch { }
            }
        }

        protected override void OnClosed(EventArgs e) {
            base.OnClosed(e);

            closed = true;
            Core.Instance.Dispose();
        }

        protected override void OnActivated(EventArgs e) {
            base.OnActivated(e);

            //Keyboard.SetActive(true);
        }

        protected override void OnDeactivate(EventArgs e) {
            base.OnDeactivate(e);

            //Keyboard.SetActive(false);
        }

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

        void StartWindowDrag() {
            ReleaseCapture();
            SendMessage(Handle, WM_NCLBUTTONDOWN, HTCAPTION, 0);
        }

        void BuildTitleBar(FontAsset font, int windowWidth) {
            titleBarEntity = new EditorEntity();
            titleBarEntity.LayerMask = 0b1000000000000000;
            titleBarEntity.Position = new float3(0, 0, 0);

            titleBarBackground = new SpriteComponent();
            titleBarBackground.Texture = TextureUtils.PixelTexture;
            titleBarBackground.Color = ThemeManager.Colors.SurfacePrimary;
            titleBarBackground.Size = new int2(windowWidth, TitleBarHeight);
            titleBarBackground.RenderOrder2D = 1;
            titleBarEntity.AddComponent(titleBarBackground);

            titleBarHitRegion = new InteractableComponent();
            titleBarHitRegion.Size = new int2(windowWidth, TitleBarHeight);
            titleBarHitRegion.CursorEvent += (pos, delta, state) => {
                if (state == PointerInteraction.Press) {
                    long now = Environment.TickCount64;
                    long elapsed = now - lastTitleBarClickTicks;
                    bool isDoubleClick = elapsed <= TitleBarDoubleClickMs &&
                                         Math.Abs(pos.X - lastTitleBarClickPos.X) <= TitleBarDoubleClickDistance &&
                                         Math.Abs(pos.Y - lastTitleBarClickPos.Y) <= TitleBarDoubleClickDistance;

                    lastTitleBarClickTicks = now;
                    lastTitleBarClickPos = pos;

                    if (isDoubleClick) {
                        ToggleWindowState();
                    } else {
                        StartWindowDrag();
                    }
                }
            };
            titleBarEntity.AddComponent(titleBarHitRegion);

            menuButtons.Clear();
            float x = 8f;
            string[] labels = { "File", "Edit", "View", "Window", "Help" };
            foreach (var label in labels) {
                int width = ComputeButtonWidth(font, label);
                var buttonEntity = new EditorEntity {
                    LayerMask = titleBarEntity.LayerMask,
                    Position = new float3(x, 6, 0)
                };
                var button = new ButtonComponent(label, new int2(width, 24), font, null, 0f);
                buttonEntity.AddComponent(button);
                titleBarEntity.AddChild(buttonEntity);
                menuButtons.Add((buttonEntity, width));
                x += width + 6;
            }

            var titleEntity = new EditorEntity {
                LayerMask = titleBarEntity.LayerMask,
                Position = new float3(x + 10, 8, 0)
            };
            titleTextComponent = new TextComponent {
                Font = font,
                Text = "helengine editor",
                Color = new byte4(255, 255, 255, 255),
                Size = new int2(300, 20),
                RenderOrder2D = 3
            };
            titleEntity.AddComponent(titleTextComponent);
            titleBarEntity.AddChild(titleEntity);

            windowControlButtons.Clear();
            int closeWidth = ComputeButtonWidth(font, "X");
            int maxWidth = ComputeButtonWidth(font, "Max");
            int minWidth = ComputeButtonWidth(font, "-");
            AddWindowControl(font, "-", minWidth, () => WindowState = FormWindowState.Minimized);
            AddWindowControl(font, "Max", maxWidth, ToggleWindowState);
            AddWindowControl(font, "X", closeWidth, Close);

            UpdateTitleBarLayout(windowWidth);
        }

        void UpdateLayout() {
            if (uiFont == null) {
                return;
            }

            int renderWidth = Math.Max(1, ClientSize.Width);
            int renderHeight = Math.Max(1, ClientSize.Height);

            UpdateTitleBarLayout(ClientSize.Width);

            if (uiCameraComponent != null) {
                uiCameraComponent.Viewport = new float4(0, 0, renderWidth, renderHeight);
            }

            if (dockLayout != null) {
                int availableHeight = Math.Max(0, renderHeight - TitleBarHeight);
                dockLayout.Layout(new int2(renderWidth, availableHeight), new float3(0, TitleBarHeight, 0));
            }
        }

        void UpdateDockPreview() {
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

            bool hasDocked = false;
            for (int i = 0; i < dockables.Count; i++) {
                var de = dockables[i];
                if (de == dragging) {
                    continue;
                }

                if (de.Dock != DockRegion.Floating) {
                    hasDocked = true;
                    break;
                }
            }

            var mouse = Core.Instance.InputManager.Mouse.GetState();
            int2 pointer = new int2(mouse.X, mouse.Y);

            int renderWidth = Math.Max(1, ClientSize.Width);
            int renderHeight = Math.Max(1, ClientSize.Height);
            int availableHeight = Math.Max(0, renderHeight - TitleBarHeight);
            int2 hostSize = new int2(renderWidth, availableHeight);
            float3 origin = new float3(0, TitleBarHeight, 0);

            bool fillOnly = !hasDocked;

            if (dockLayout.TryGetDockHint(pointer, hostSize, origin, fillOnly, out var region, out var pos, out var size)) {
                dockPreviewOverlay.Show(pos, size);
                dockHintValid = true;
                dockHintRegion = region;
                dockHintPos = pos;
                dockHintSize = size;
                lastDragging = dragging;
            } else {
                dockPreviewOverlay.Hide();
                dockHintValid = false;
                lastDragging = dragging;
            }
        }

        void ApplyDockHint(DockableEntity entity) {
            if (!dockHintValid) {
                return;
            }

            entity.Dock = dockHintRegion;
            int contentHeight = Math.Max(1, dockHintSize.Y - DockableEntity.TitleBarHeight);
            entity.Size = new int2(dockHintSize.X, contentHeight);
            dockHintValid = false;
            dockPreviewOverlay?.Hide();
            UpdateLayout();
        }

        void UpdateTitleBarLayout(int windowWidth) {
            // Extend by 1px to avoid gaps from viewport rounding when maximizing/restoring
            int fullWidth = windowWidth + 1;

            if (titleBarBackground != null) {
                titleBarBackground.Size = new int2(fullWidth, TitleBarHeight);
            }

            if (titleBarHitRegion != null) {
                titleBarHitRegion.Size = new int2(fullWidth, TitleBarHeight);
            }

            float x = 8f;
            foreach (var (entity, width) in menuButtons) {
                entity.Position = new float3(x, 6, 0);
                x += width + 6;
            }

            if (titleTextComponent?.Parent != null) {
                titleTextComponent.Parent.Position = new float3(x + 10, 8, 0);
            }

            int totalControlsWidth = 0;
            foreach (var (_, width) in windowControlButtons) {
                totalControlsWidth += width + 6;
            }

            float controlX = Math.Max(x + 20, windowWidth - totalControlsWidth - 8);
            foreach (var (entity, width) in windowControlButtons) {
                entity.Position = new float3(controlX, 6, 0);
                controlX += width + 6;
            }
        }

        void AddWindowControl(FontAsset font, string label, int width, Action onClick) {
            var buttonEntity = new EditorEntity {
                LayerMask = 0b1000000000000000,
                Position = new float3(0, 6, 0)
            };
            var button = new ButtonComponent(label, new int2(width, 24), font, onClick, 0f);
            buttonEntity.AddComponent(button);
            titleBarEntity?.AddChild(buttonEntity);
            windowControlButtons.Add((buttonEntity, width));
        }

        int ComputeButtonWidth(FontAsset font, string label) {
            var tight = font.MeasureTight(label);
            return Math.Max(40, (int)MathF.Ceiling(tight.Width) + 16);
        }

        void ToggleWindowState() {
            if (WindowState == FormWindowState.Maximized) {
                WindowState = FormWindowState.Normal;
            } else {
                WindowState = FormWindowState.Maximized;
            }
        }

        const int WM_NCLBUTTONDOWN = 0xA1;
        const int HTCAPTION = 0x2;

        [DllImport("user32.dll")]
        static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);
    }
}
