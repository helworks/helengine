using helengine;
using helengine.editor;
using helengine.sharpdx;
using Nucleus.Platform.Windows.Controls;

namespace helengine.editor.launcher {
    public partial class LauncherForm : ResizableForm {
        private TitleBarControl titleBarControl;
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
        private const int ButtonWidth = 300;
        private const int ButtonHeight = 60;
        private const int ButtonSpacing = 20;

        public LauncherForm() {
            InitializeComponent();
            
            // Set minimum size for the launcher
            MinimumSize = new Size(600, 400);
            
            // Set initial size if not already set
            if (Size.Width < MinimumSize.Width || Size.Height < MinimumSize.Height) {
                Size = new Size(800, 600);
            }
            
            initialize();
        }

        private void SetupTitleBar() {
            // Create and configure the title bar
            titleBarControl = new TitleBarControl();
            titleBarControl.Text = "helengine project manager";
            titleBarControl.Location = new Point(0, 0);
            titleBarControl.Width = this.Width;
            titleBarControl.TitleBarHeight = 24;

            this.Controls.Add(titleBarControl);
        }

        private void CreateLauncherUI() {
            // Create font asset for UI text
            Font font = new Font("Consolas", 16, FontStyle.Regular);
            uiFont = GDIFontProcessor.ImportFont(font);

            // Calculate center positions
            int centerX = ClientSize.Width / 2;
            int centerY = ClientSize.Height / 2;

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
            float3 createButtonPos = new float3(centerX - ButtonWidth/2, centerY - ButtonHeight/2 - ButtonSpacing, 0);
            
            createProjectButtonEntity = new Entity();
            createProjectButtonEntity.LayerMask = 0b1000000000000000;
            createProjectButtonEntity.Position = createButtonPos;
            createProjectButtonEntity.Enabled = true;
            createProjectButtonEntity.InitComponents();

            createProjectButton = new ButtonComponent(
                "create project",
                new int2(ButtonWidth, ButtonHeight),
                uiFont,
                OnCreateProjectClick
            );
            createProjectButtonEntity.AddComponent(createProjectButton);

            // Browse Project Button using ButtonComponent
            float3 browseButtonPos = new float3(centerX - ButtonWidth/2, centerY + ButtonHeight/2 + ButtonSpacing, 0);
            
            browseProjectButtonEntity = new Entity();
            browseProjectButtonEntity.LayerMask = 0b1000000000000000;
            browseProjectButtonEntity.Position = browseButtonPos;
            browseProjectButtonEntity.Enabled = true;
            browseProjectButtonEntity.InitComponents();

            browseProjectButton = new ButtonComponent(
                "browse project",
                new int2(ButtonWidth, ButtonHeight),
                uiFont,
                OnBrowseProjectClick
            );
            browseProjectButtonEntity.AddComponent(browseProjectButton);
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
            
            // Update titlebar width
            if (titleBarControl != null) {
                titleBarControl.Width = this.Width;
            }

            // Notify render manager about resize for swap chain recreation
            if (Core.Instance?.RenderManager != null) {
                Core.Instance.RenderManager.OnWindowResize(this.Handle, ClientSize.Width, ClientSize.Height);
            }

            // Update camera viewport for proper rendering
            UpdateCameraViewport();

            // Recalculate UI positions on resize
            if (titleEntity != null) {
                int centerX = ClientSize.Width / 2;
                int centerY = ClientSize.Height / 2;
                
                // Title stays in top-left
                titleEntity.Position = new float3(20, 30, 0);
                
                // Update button positions using ButtonComponent
                float3 createButtonPos = new float3(centerX - ButtonWidth/2, centerY - ButtonHeight/2 - ButtonSpacing, 0);
                float3 browseButtonPos = new float3(centerX - ButtonWidth/2, centerY + ButtonHeight/2 + ButtonSpacing, 0);
                
                createProjectButton?.UpdatePosition(createButtonPos);
                browseProjectButton?.UpdatePosition(browseButtonPos);
            }
        }
    }
}
