using helengine;

namespace helengine.editor.launcher.pages {
    /// <summary>
    /// New project creation page with input fields
    /// </summary>
    public class NewProjectPage : LauncherPage {
        private Action<string, string>? onCreateProject; // projectName, projectLocation
        private Action? onCancel;
        
        // UI entities
        private Entity? titleEntity;
        private Entity? projectNameLabelEntity;
        private Entity? projectNameTextBoxEntity;
        private Entity? projectLocationLabelEntity;
        private Entity? projectLocationTextBoxEntity;
        private Entity? createButtonEntity;
        private Entity? cancelButtonEntity;
        
        // Components
        private TextBoxComponent? projectNameTextBox;
        private TextBoxComponent? projectLocationTextBox;
        private ButtonComponent? createButton;
        private ButtonComponent? cancelButton;
        
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
            titleEntity.Position = GetPosition(50, 30);
            titleEntity.Enabled = true;
            titleEntity.InitComponents();
            
            var titleText = new TextComponent();
            titleText.Text = "Create New Project";
            titleText.Font = font;
            titleText.Color = new byte4(255, 255, 255, 255);
            titleText.RenderOrder2D = 3;
            titleEntity.AddComponent(titleText);
            
            pageEntities.Add(titleEntity);
        }
        
        private void CreateInputFields() {
            // Project Name Label
            projectNameLabelEntity = new Entity();
            projectNameLabelEntity.LayerMask = 0b1000000000000000;
            projectNameLabelEntity.Position = GetPosition(50, 80);
            projectNameLabelEntity.Enabled = true;
            projectNameLabelEntity.InitComponents();
            
            var nameLabel = new TextComponent();
            nameLabel.Text = "Project Name:";
            nameLabel.Font = font;
            nameLabel.Color = new byte4(255, 255, 255, 255);
            nameLabel.RenderOrder2D = 3;
            projectNameLabelEntity.AddComponent(nameLabel);
            pageEntities.Add(projectNameLabelEntity);
            
            // Project Name TextBox
            projectNameTextBoxEntity = new Entity();
            projectNameTextBoxEntity.LayerMask = 0b1000000000000000;
            projectNameTextBoxEntity.Position = GetPosition(50, 110);
            projectNameTextBoxEntity.Enabled = true;
            projectNameTextBoxEntity.InitComponents();
            
            projectNameTextBox = new TextBoxComponent(
                new int2(300, 30),
                font,
                "Enter project name"
            );
            projectNameTextBoxEntity.AddComponent(projectNameTextBox);
            pageEntities.Add(projectNameTextBoxEntity);
            
            // Project Location Label
            projectLocationLabelEntity = new Entity();
            projectLocationLabelEntity.LayerMask = 0b1000000000000000;
            projectLocationLabelEntity.Position = GetPosition(50, 160);
            projectLocationLabelEntity.Enabled = true;
            projectLocationLabelEntity.InitComponents();
            
            var locationLabel = new TextComponent();
            locationLabel.Text = "Project Location:";
            locationLabel.Font = font;
            locationLabel.Color = new byte4(255, 255, 255, 255);
            locationLabel.RenderOrder2D = 3;
            projectLocationLabelEntity.AddComponent(locationLabel);
            pageEntities.Add(projectLocationLabelEntity);
            
            // Project Location TextBox
            projectLocationTextBoxEntity = new Entity();
            projectLocationTextBoxEntity.LayerMask = 0b1000000000000000;
            projectLocationTextBoxEntity.Position = GetPosition(50, 190);
            projectLocationTextBoxEntity.Enabled = true;
            projectLocationTextBoxEntity.InitComponents();
            
            projectLocationTextBox = new TextBoxComponent(
                new int2(300, 30),
                font,
                "C:\\Projects"
            );
            projectLocationTextBoxEntity.AddComponent(projectLocationTextBox);
            pageEntities.Add(projectLocationTextBoxEntity);
        }
        
        private void CreateActionButtons() {
            // Create Button
            createButtonEntity = new Entity();
            createButtonEntity.LayerMask = 0b1000000000000000;
            createButtonEntity.Position = GetPosition(50, 240);
            createButtonEntity.Enabled = true;
            createButtonEntity.InitComponents();
            
            createButton = new ButtonComponent(
                "Create",
                new int2(100, 40),
                font,
                OnCreateClick
            );
            createButtonEntity.AddComponent(createButton);
            pageEntities.Add(createButtonEntity);
            
            // Cancel Button
            cancelButtonEntity = new Entity();
            cancelButtonEntity.LayerMask = 0b1000000000000000;
            cancelButtonEntity.Position = GetPosition(170, 240);
            cancelButtonEntity.Enabled = true;
            cancelButtonEntity.InitComponents();
            
            cancelButton = new ButtonComponent(
                "Cancel",
                new int2(100, 40),
                font,
                () => onCancel?.Invoke()
            );
            cancelButtonEntity.AddComponent(cancelButton);
            pageEntities.Add(cancelButtonEntity);
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
        
        public override void OnNavigateTo(string targetPage) {
            if (targetPage == "main") {
                onCancel?.Invoke();
            }
        }
        
        protected override void UpdatePagePosition() {
            // Update all entity positions during animation
            if (titleEntity != null) {
                titleEntity.Position = GetPosition(50, 30);
            }
            
            if (projectNameLabelEntity != null) {
                projectNameLabelEntity.Position = GetPosition(50, 80);
            }
            
            if (projectNameTextBoxEntity != null) {
                projectNameTextBoxEntity.Position = GetPosition(50, 110);
            }
            
            if (projectLocationLabelEntity != null) {
                projectLocationLabelEntity.Position = GetPosition(50, 160);
            }
            
            if (projectLocationTextBoxEntity != null) {
                projectLocationTextBoxEntity.Position = GetPosition(50, 190);
            }
            
            if (createButtonEntity != null) {
                createButtonEntity.Position = GetPosition(50, 240);
            }
            
            if (cancelButtonEntity != null) {
                cancelButtonEntity.Position = GetPosition(170, 240);
            }
        }
        
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
