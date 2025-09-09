using helengine.editor.launcher.pages;
using helengine.sharpdx;
using Nucleus.Platform.Windows.Controls;

namespace helengine.editor.launcher {
    public partial class LauncherForm : ResizableForm {
        TitleBarControl titleBarControl;
        Thread thread;
        bool closed;

        // Page management system
        PageManager? pageManager;
        MainPage? mainPage;
        NewProjectPage? newProjectPage;
        FontAsset uiFont;
        Entity sceneCamEntity;
        CameraComponent sceneCamera;

        public LauncherForm() {
            InitializeComponent();

            // Set minimum size for the launcher
            MinimumSize = new Size(600, 400);

            Size = new Size(1280, 720);

            initialize();
        }

        private void SetupTitleBar() {
            titleBarControl = new TitleBarControl();
            titleBarControl.Text = "helengine project manager";
            titleBarControl.Location = new Point(0, 0);
            titleBarControl.Width = this.ClientSize.Width;
            titleBarControl.TitleBarHeight = 24;
            titleBarControl.EnableMaximize = true;

            this.Controls.Add(titleBarControl);
        }

        private void CreateLauncherUI() {
            // Create font asset for UI text
            uiFont = GDIFontProcessor.ImportFont(new Font("Consolas", 16, FontStyle.Regular));

            // Create UI camera for rendering UI elements
            sceneCamEntity = new Entity();
            sceneCamera = new CameraComponent();
            sceneCamera.LayerMask = 0b1000000000000000; // UI layer
            sceneCamera.Viewport = new float4(0, 24, ClientSize.Width, ClientSize.Height - 24);
            sceneCamEntity.InitComponents();
            sceneCamEntity.AddComponent(sceneCamera);

            // Initialize page management system
            pageManager = new PageManager(ClientSize.Width);
            
            // Create pages
            mainPage = new MainPage(uiFont, 
                onCreateProject: () => NavigateToNewProject(),
                onBrowseProject: () => OnBrowseProjectClick()
            );
            
            newProjectPage = new NewProjectPage(uiFont,
                onCreateProject: (name, location) => OnCreateConfirmClick(name, location),
                onCancel: () => NavigateToMain()
            );
            
            // Register pages
            pageManager.RegisterPage("main", mainPage);
            pageManager.RegisterPage("newproject", newProjectPage);
            
            // Show initial page
            pageManager.ShowInitialPage("main");
        }

        private void UpdateCameraViewport() {
            if (sceneCamera != null) {
                int top = titleBarControl?.TitleBarHeight ?? 0;
                sceneCamera.Viewport = new float4(0, top, ClientSize.Width, ClientSize.Height - top);
            }
        }

        private void NavigateToNewProject() {
            // Forward transition (main -> newproject): slide in from right
            pageManager?.NavigateTo("newproject", reverse: false);
        }

        private void NavigateToMain() {
            // Reverse transition (newproject -> main): slide back to the right
            pageManager?.NavigateTo("main", reverse: true);
            newProjectPage?.ClearInputs(); // Clear form when returning to main
        }

        private void OnBrowseProjectClick() {
            // Handle browse project action
            MessageBox.Show("Browse Project clicked!", "helengine", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void OnCreateConfirmClick(string projectName, string projectLocation) {
            // TODO: Create the actual project
            MessageBox.Show($"Creating project '{projectName}' at '{projectLocation}'", "Project Created", 
                MessageBoxButtons.OK, MessageBoxIcon.Information);

            NavigateToMain();
        }

        private void initialize() {
            SetupTitleBar();

            Core core = new Core();
            core.Initialize(new SharpDXRenderManager(), new InputManagerWindows(this.Handle));

            // Enable keyboard input for ESC/back handling
            (Core.Instance.InputManager.Keyboard as KeyboardWindows)?.SetActive(true);

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
                        pageManager?.Update(); // Update page animations
                        Core.Instance.Draw();
                    });
                } catch { }
            }
        }

        protected override void OnClosed(EventArgs e) {
            base.OnClosed(e);

            closed = true;
            pageManager?.Dispose();
            Core.Instance.Dispose();
        }

        protected override void OnActivated(EventArgs e) {
            base.OnActivated(e);
            (Core.Instance?.InputManager.Keyboard as KeyboardWindows)?.SetActive(true);
        }

        protected override void OnDeactivate(EventArgs e) {
            base.OnDeactivate(e);
            (Core.Instance?.InputManager.Keyboard as KeyboardWindows)?.SetActive(false);
        }

        protected override void OnResize(EventArgs e) {
            base.OnResize(e);

            // Notify render manager about resize for swap chain recreation
            if (Core.Instance?.RenderManager != null) {
                Core.Instance.RenderManager.OnWindowResize(this.Handle, ClientSize.Width, ClientSize.Height);
            }

            // Update camera viewport for proper rendering
            UpdateCameraViewport();
            
            // Update page manager screen size for animations
            pageManager?.UpdateScreenSize(ClientSize.Width);
        }
    }
}
