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

        // New Project UI entities
        private Entity projectNameLabelEntity;
        private Entity projectNameTextBoxEntity;
        private Entity projectLocationLabelEntity;
        private Entity projectLocationTextBoxEntity;
        private Entity createButtonEntity;
        private Entity cancelButtonEntity;
        private TextBoxComponent projectNameTextBox;
        private TextBoxComponent projectLocationTextBox;
        private ButtonComponent createButton;
        private ButtonComponent cancelButton;
        private bool showingNewProjectUI;

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
            browseProjectButtonEntity = new Entity();
            browseProjectButtonEntity.LayerMask = 0b1000000000000000;
            browseProjectButtonEntity.Position = new float3(950, 150, 0);
            browseProjectButtonEntity.Enabled = true;
            browseProjectButtonEntity.InitComponents();

            var anchorBrowseProject = new AnchorComponent();
            browseProjectButtonEntity.AddComponent(anchorBrowseProject);
            anchorBrowseProject.EnableAnchoring(right: true, top: true);

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
            if (!showingNewProjectUI) {
                ShowNewProjectUI();
            }
        }

        private void OnBrowseProjectClick() {
            // Handle browse project action
            MessageBox.Show("Browse Project clicked!", "helengine", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ShowNewProjectUI() {
            showingNewProjectUI = true;

            // Hide the main buttons
            createProjectButtonEntity.Enabled = false;

            // Create project name label
            projectNameLabelEntity = new Entity();
            projectNameLabelEntity.LayerMask = 0b1000000000000000;
            projectNameLabelEntity.Position = new float3(50, 80, 0);
            projectNameLabelEntity.Enabled = true;
            projectNameLabelEntity.InitComponents();

            var nameLabel = new TextComponent();
            nameLabel.Text = "Project Name:";
            nameLabel.Font = uiFont;
            nameLabel.Color = new byte4(255, 255, 255, 255);
            nameLabel.RenderOrder2D = 3;
            projectNameLabelEntity.AddComponent(nameLabel);

            // Create project name textbox
            projectNameTextBoxEntity = new Entity();
            projectNameTextBoxEntity.LayerMask = 0b1000000000000000;
            projectNameTextBoxEntity.Position = new float3(50, 110, 0);
            projectNameTextBoxEntity.Enabled = true;
            projectNameTextBoxEntity.InitComponents();

            projectNameTextBox = new TextBoxComponent(
                new int2(300, 30),
                uiFont,
                "Enter project name"
            );
            projectNameTextBoxEntity.AddComponent(projectNameTextBox);

            // Create project location label
            projectLocationLabelEntity = new Entity();
            projectLocationLabelEntity.LayerMask = 0b1000000000000000;
            projectLocationLabelEntity.Position = new float3(50, 160, 0);
            projectLocationLabelEntity.Enabled = true;
            projectLocationLabelEntity.InitComponents();

            var locationLabel = new TextComponent();
            locationLabel.Text = "Project Location:";
            locationLabel.Font = uiFont;
            locationLabel.Color = new byte4(255, 255, 255, 255);
            locationLabel.RenderOrder2D = 3;
            projectLocationLabelEntity.AddComponent(locationLabel);

            // Create project location textbox
            projectLocationTextBoxEntity = new Entity();
            projectLocationTextBoxEntity.LayerMask = 0b1000000000000000;
            projectLocationTextBoxEntity.Position = new float3(50, 190, 0);
            projectLocationTextBoxEntity.Enabled = true;
            projectLocationTextBoxEntity.InitComponents();

            projectLocationTextBox = new TextBoxComponent(
                new int2(300, 30),
                uiFont,
                "C:\\Projects"
            );
            projectLocationTextBoxEntity.AddComponent(projectLocationTextBox);

            // Create Create button
            createButtonEntity = new Entity();
            createButtonEntity.LayerMask = 0b1000000000000000;
            createButtonEntity.Position = new float3(50, 240, 0);
            createButtonEntity.Enabled = true;
            createButtonEntity.InitComponents();

            createButton = new ButtonComponent(
                "Create",
                new int2(100, 40),
                uiFont,
                OnCreateConfirmClick
            );
            createButtonEntity.AddComponent(createButton);

            // Create Cancel button
            cancelButtonEntity = new Entity();
            cancelButtonEntity.LayerMask = 0b1000000000000000;
            cancelButtonEntity.Position = new float3(170, 240, 0);
            cancelButtonEntity.Enabled = true;
            cancelButtonEntity.InitComponents();

            cancelButton = new ButtonComponent(
                "Cancel",
                new int2(100, 40),
                uiFont,
                OnCancelClick
            );
            cancelButtonEntity.AddComponent(cancelButton);
        }

        private void HideNewProjectUI() {
            showingNewProjectUI = false;

            // Show the main buttons
            createProjectButtonEntity.Enabled = true;

            // Dispose new project UI entities
            projectNameLabelEntity?.Dispose();
            projectNameTextBoxEntity?.Dispose();
            projectLocationLabelEntity?.Dispose();
            projectLocationTextBoxEntity?.Dispose();
            createButtonEntity?.Dispose();
            cancelButtonEntity?.Dispose();
        }

        private void OnCreateConfirmClick() {
            string projectName = projectNameTextBox.Text.Trim();
            string projectLocation = projectLocationTextBox.Text.Trim();

            if (string.IsNullOrEmpty(projectName)) {
                MessageBox.Show("Please enter a project name.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (string.IsNullOrEmpty(projectLocation)) {
                MessageBox.Show("Please enter a project location.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // TODO: Create the actual project
            MessageBox.Show($"Creating project '{projectName}' at '{projectLocation}'", "Project Created", 
                MessageBoxButtons.OK, MessageBoxIcon.Information);

            HideNewProjectUI();
        }

        private void OnCancelClick() {
            HideNewProjectUI();
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
