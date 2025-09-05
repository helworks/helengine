using helengine;
using helengine.editor;
using helengine.sharpdx;
using Nucleus.Platform.Windows.Controls;

namespace helengine.editor.launcher {
    public partial class LauncherForm : ResizableForm {
        private EnhancedTitleBarControl titleBarControl;
        private Thread thread;
        private bool closed;

        // UI entities using helengine.core components
        private Entity titleEntity;
        private Entity createProjectButtonEntity;
        private Entity browseProjectButtonEntity;
        private ButtonComponent createProjectButton;
        private ButtonComponent browseProjectButton;
        private FontAsset uiFont;
        private Entity sceneCamEntity;
        private CameraComponent sceneCamera;

        // Button constants
        private const int ButtonWidth = 200;
        private const int ButtonHeight = 60;
        private const int ButtonSpacing = 20;

        public LauncherForm() {
            InitializeComponent();

            // Set minimum size for the launcher
            MinimumSize = new Size(600, 400);

            Size = new Size(1280, 720);

            initialize();
        }

        private void SetupTitleBar() {
            // Create and configure the enhanced title bar
            titleBarControl = new EnhancedTitleBarControl();
            titleBarControl.Text = "helengine project manager";
            titleBarControl.Location = new Point(0, 0);
            titleBarControl.Width = this.ClientSize.Width;
            titleBarControl.TitleBarHeight = 24;
            titleBarControl.EnableMaximize = true;

            // Ensure proper anchoring for smooth resize (redundant but explicit)
            titleBarControl.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;

            this.Controls.Add(titleBarControl);
        }

        private void CreateLauncherUI() {
            // Create font asset for UI text
            Font font = new Font("Consolas", 16, FontStyle.Regular);
            uiFont = GDIFontProcessor.ImportFont(font);

            // Create UI camera for rendering UI elements
            sceneCamEntity = new Entity();
            sceneCamera = new CameraComponent();
            sceneCamera.LayerMask = 0b1000000000000000; // UI layer
            sceneCamera.Viewport = new float4(0, 0, ClientSize.Width, ClientSize.Height);
            sceneCamEntity.InitComponents();
            sceneCamEntity.AddComponent(sceneCamera);

            // Title (top-left position)
            titleEntity = new Entity();
            titleEntity.LayerMask = 0b1000000000000000;
            titleEntity.Position = new float3(20, 30, 0); // Top-left with padding
            titleEntity.Enabled = true;
            titleEntity.InitComponents();

            var titleText = new TextComponent();
            titleText.Text = "helengine";
            titleText.Color = ThemeManager.Colors.TextPrimary;
            titleText.Size = new int2(200, 40);
            titleText.RenderOrder2D = 1;
            titleText.Font = uiFont;
            titleEntity.AddComponent(titleText);

            // Create Project Button using ButtonComponent
            createProjectButtonEntity = new Entity();
            createProjectButtonEntity.LayerMask = 0b1000000000000000;
            createProjectButtonEntity.Position = new float3(750, 50, 0);
            createProjectButtonEntity.Enabled = true;
            createProjectButtonEntity.InitComponents();

            var anchorProjectButton = new AnchorComponent();
            createProjectButtonEntity.AddComponent(anchorProjectButton);
            anchorProjectButton.EnableAnchoring(right: true, top: true);

            createProjectButton = new ButtonComponent(
                "create project",
                new int2(ButtonWidth, ButtonHeight),
                uiFont,
                OnCreateProjectClick
            );
            createProjectButtonEntity.AddComponent(createProjectButton);

            // Browse Project Button using ButtonComponent
            //browseProjectButtonEntity = new Entity();
            //browseProjectButtonEntity.LayerMask = 0b1000000000000000;
            //browseProjectButtonEntity.Position = new float3(950, 150, 0);
            //browseProjectButtonEntity.Enabled = true;
            //browseProjectButtonEntity.InitComponents();

            //var anchorBrowseProject = new AnchorComponent();
            //browseProjectButtonEntity.AddComponent(anchorBrowseProject);
            //anchorBrowseProject.EnableAnchoring(right: true, top: true);

            //browseProjectButton = new ButtonComponent(
            //    "browse project",
            //    new int2(ButtonWidth, ButtonHeight),
            //    uiFont,
            //    OnBrowseProjectClick
            //);
            //browseProjectButtonEntity.AddComponent(browseProjectButton);
        }

        private void UpdateCameraViewport() {
            if (sceneCamera != null) {
                sceneCamera.Viewport = new float4(0, 0, ClientSize.Width, ClientSize.Height);
            }
        }

        private void OnCreateProjectClick() {
            // Handle create project action
            MessageBox.Show("Create Project clicked!", "helengine", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void OnBrowseProjectClick() {
            // Handle browse project action
            MessageBox.Show("Browse Project clicked!", "helengine", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void initialize() {
            SetupTitleBar();

            Core core = new Core();
            core.Initialize(new SharpDXRenderManager(), new InputManagerWindows(this.Handle));

            core.RenderManager.AddWindow(this.Handle, ClientSize.Width - 1, ClientSize.Height);

            // Create the launcher UI using helengine.core components
            CreateLauncherUI();

            thread = new Thread(threadUpdate);
            thread.Start();
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
        }

        protected override void OnDeactivate(EventArgs e) {
            base.OnDeactivate(e);
        }

        protected override void OnResize(EventArgs e) {
            base.OnResize(e);

            // Notify render manager about resize for swap chain recreation
            if (Core.Instance?.RenderManager != null) {
                Core.Instance.RenderManager.OnWindowResize(this.Handle, ClientSize.Width, ClientSize.Height);
            }

            // Update camera viewport for proper rendering
            UpdateCameraViewport();
        }
    }
}
