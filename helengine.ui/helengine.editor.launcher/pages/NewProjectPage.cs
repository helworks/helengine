using System.IO;
using System.Windows.Forms;

namespace helengine.editor.launcher.pages {
    /// <summary>
    /// New project creation page with input fields
    /// </summary>
    public class NewProjectPage : LauncherPage {
        Action<string, string>? onCreateProject; // projectName, projectLocation
        Action? onCancel;
        
        // UI entities
        Entity? titleEntity;
        Entity? projectNameLabelEntity;
        Entity? projectNameTextBoxEntity;
        Entity? projectLocationLabelEntity;
        Entity? projectLocationTextBoxEntity;
        Entity? createButtonEntity;
        Entity? backButtonEntity;
        Entity? browseLocationButtonEntity;
        
        // Components
        TextBoxComponent? projectNameTextBox;
        TextBoxComponent? projectLocationTextBox;
        ButtonComponent? createButton;
        ButtonComponent? cancelButton;
        ButtonComponent? browseLocationButton;

        // Layout
        private const int HorizontalOffset = 20;
        
        public NewProjectPage(FontAsset font, Action<string, string>? onCreateProject = null, Action? onCancel = null) : base(font) {
            this.onCreateProject = onCreateProject;
            this.onCancel = onCancel;
        }
        
        public override void CreatePage() {
            pageEntities.Clear();
            
            // Create page title
            CreateTitle();
            
            // Create input fields
            CreateInputFields();
            
            // Create action buttons
            CreateActionButtons();
        }
        
        private void CreateTitle() {
            titleEntity = new Entity();
            titleEntity.LayerMask = 0b1000000000000000;
            titleEntity.Enabled = true;
            titleEntity.InitComponents();
            
            var titleText = new TextComponent();
            titleText.Text = "Create New Project";
            titleText.Font = font;
            titleText.Color = new byte4(255, 255, 255, 255);
            titleText.RenderOrder2D = 3;
            titleEntity.AddComponent(titleText);

            AddPageEntity(titleEntity, 50 + HorizontalOffset, 30);

            // Back arrow button (top-left)
            backButtonEntity = new Entity();
            backButtonEntity.LayerMask = 0b1000000000000000;
            backButtonEntity.Enabled = true;
            backButtonEntity.InitComponents();

            var backAnchor = new AnchorComponent();
            backButtonEntity.AddComponent(backAnchor);
            // Anchor after shown to ensure distances are from on-screen position
            OnShown(() => backAnchor.EnableAnchoring(left: true, top: true));

            var backButton = new ButtonComponent(
                "←",
                new int2(40, 40),
                font,
                () => onCancel?.Invoke()
            );
            backButtonEntity.AddComponent(backButton);

            AddPageEntity(backButtonEntity, 10, 10);
        }
        
        private void CreateInputFields() {
            // Project Name Label
            projectNameLabelEntity = new Entity();
            projectNameLabelEntity.LayerMask = 0b1000000000000000;
            projectNameLabelEntity.Enabled = true;
            projectNameLabelEntity.InitComponents();
            
            var nameLabel = new TextComponent();
            nameLabel.Text = "Project Name:";
            nameLabel.Font = font;
            nameLabel.Color = new byte4(255, 255, 255, 255);
            nameLabel.RenderOrder2D = 3;
            projectNameLabelEntity.AddComponent(nameLabel);
            AddPageEntity(projectNameLabelEntity, 50 + HorizontalOffset, 80);
            
            // Project Name TextBox
            projectNameTextBoxEntity = new Entity();
            projectNameTextBoxEntity.LayerMask = 0b1000000000000000;
            projectNameTextBoxEntity.Enabled = true;
            projectNameTextBoxEntity.InitComponents();
            
            projectNameTextBox = new TextBoxComponent(
                new int2(300, 30),
                font,
                "Enter project name"
            );
            projectNameTextBoxEntity.AddComponent(projectNameTextBox);
            AddPageEntity(projectNameTextBoxEntity, 50 + HorizontalOffset, 110);
            
            // Project Location Label
            projectLocationLabelEntity = new Entity();
            projectLocationLabelEntity.LayerMask = 0b1000000000000000;
            projectLocationLabelEntity.Enabled = true;
            projectLocationLabelEntity.InitComponents();
            
            var locationLabel = new TextComponent();
            locationLabel.Text = "Project Location:";
            locationLabel.Font = font;
            locationLabel.Color = new byte4(255, 255, 255, 255);
            locationLabel.RenderOrder2D = 3;
            projectLocationLabelEntity.AddComponent(locationLabel);
            AddPageEntity(projectLocationLabelEntity, 50 + HorizontalOffset, 160);
            
            // Project Location TextBox
            projectLocationTextBoxEntity = new Entity();
            projectLocationTextBoxEntity.LayerMask = 0b1000000000000000;
            projectLocationTextBoxEntity.Enabled = true;
            projectLocationTextBoxEntity.InitComponents();
            
            projectLocationTextBox = new TextBoxComponent(
                new int2(300, 30),
                font,
                "C:\\Projects"
            );
            projectLocationTextBoxEntity.AddComponent(projectLocationTextBox);
            AddPageEntity(projectLocationTextBoxEntity, 50 + HorizontalOffset, 190);

            // Browse button for project location
            browseLocationButtonEntity = new Entity();
            browseLocationButtonEntity.LayerMask = 0b1000000000000000;
            browseLocationButtonEntity.Enabled = true;
            browseLocationButtonEntity.InitComponents();

            browseLocationButton = new ButtonComponent(
                "...",
                new int2(50, 30),
                font,
                OnBrowseLocationClick
            );
            browseLocationButtonEntity.AddComponent(browseLocationButton);
            AddPageEntity(browseLocationButtonEntity, 50 + HorizontalOffset + 300 + 10, 190);
        }
        
        private void CreateActionButtons() {
            // Create Button
            createButtonEntity = new Entity();
            createButtonEntity.LayerMask = 0b1000000000000000;
            createButtonEntity.Enabled = true;
            createButtonEntity.InitComponents();
            
            createButton = new ButtonComponent(
                "Create",
                new int2(100, 40),
                font,
                OnCreateClick
            );
            createButtonEntity.AddComponent(createButton);
            AddPageEntity(createButtonEntity, 50 + HorizontalOffset, 240);
        }
        
        private void OnCreateClick() {
            string projectName = projectNameTextBox?.Text?.Trim() ?? "";
            string projectLocation = projectLocationTextBox?.Text?.Trim() ?? "";
            
            if (string.IsNullOrEmpty(projectName)) {
                MessageBox.Show("Please enter a project name.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            
            if (string.IsNullOrEmpty(projectLocation)) {
                MessageBox.Show("Please enter a project location.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            
            onCreateProject?.Invoke(projectName, projectLocation);
        }

        private void OnBrowseLocationClick() {
            using var dialog = new FolderBrowserDialog();
            dialog.Description = "Select a folder for your project";
            dialog.UseDescriptionForTitle = true;
            dialog.ShowNewFolderButton = true;
            dialog.AutoUpgradeEnabled = true;

            string currentPath = projectLocationTextBox?.Text?.Trim() ?? string.Empty;
            if (!string.IsNullOrEmpty(currentPath) && Directory.Exists(currentPath)) {
                dialog.SelectedPath = currentPath;
            }

            if (dialog.ShowDialog() == DialogResult.OK && !string.IsNullOrEmpty(dialog.SelectedPath)) {
                if (projectLocationTextBox != null) {
                    projectLocationTextBox.Text = dialog.SelectedPath;
                }
            }
        }
        
        public override void OnNavigateTo(string targetPage) {
            if (targetPage == "main") {
                onCancel?.Invoke();
            }
        }
        
        // Base class now manages positions via AddPageEntity
        
        /// <summary>
        /// Set the action callbacks for the buttons
        /// </summary>
        public void SetCallbacks(Action<string, string>? onCreateProject, Action? onCancel) {
            this.onCreateProject = onCreateProject;
            this.onCancel = onCancel;
        }
        
        /// <summary>
        /// Clear the input fields
        /// </summary>
        public void ClearInputs() {
            if (projectNameTextBox != null) {
                projectNameTextBox.Text = "";
            }
            if (projectLocationTextBox != null) {
                projectLocationTextBox.Text = "C:\\Projects";
            }
        }
    }
}
