using helengine;

namespace helengine.editor.launcher.pages {
    /// <summary>
    /// Main launcher page with project buttons
    /// </summary>
    public class MainPage : LauncherPage {
        private Action? onCreateProject;
        private Action? onBrowseProject;
        
        // UI entities
        private Entity? titleEntity;
        private Entity? createProjectButtonEntity;
        private Entity? browseProjectButtonEntity;
        private ButtonComponent? createProjectButton;
        private ButtonComponent? browseProjectButton;
        
        // Layout constants
        private const int ButtonWidth = 200;
        private const int ButtonHeight = 60;
        private const int ButtonSpacing = 20;
        
        public MainPage(FontAsset font, Action? onCreateProject = null, Action? onBrowseProject = null) : base(font) {
            this.onCreateProject = onCreateProject;
            this.onBrowseProject = onBrowseProject;
        }
        
        public override void CreatePage() {
            pageEntities.Clear();
            
            // Create title
            CreateTitle();
            
            // Create project buttons
            CreateProjectButtons();
        }
        
        private void CreateTitle() {
            titleEntity = new Entity();
            titleEntity.LayerMask = 0b1000000000000000;
            titleEntity.Position = GetPosition(20, 30);
            titleEntity.Enabled = true;
            titleEntity.InitComponents();
            
            var titleText = new TextComponent();
            titleText.Text = "helena engine";
            titleText.Font = font;
            titleText.Color = new byte4(255, 255, 255, 255);
            titleText.RenderOrder2D = 3;
            titleEntity.AddComponent(titleText);
            
            pageEntities.Add(titleEntity);
        }
        
        private void CreateProjectButtons() {
            // Create Project Button (top-right)
            createProjectButtonEntity = new Entity();
            createProjectButtonEntity.LayerMask = 0b1000000000000000;
            createProjectButtonEntity.Position = GetPosition(950, 50);
            createProjectButtonEntity.Enabled = true;
            createProjectButtonEntity.InitComponents();
            
            var anchorCreateProject = new AnchorComponent();
            createProjectButtonEntity.AddComponent(anchorCreateProject);
            anchorCreateProject.EnableAnchoring(right: true, top: true);
            
            createProjectButton = new ButtonComponent(
                "create project",
                new int2(ButtonWidth, ButtonHeight),
                font,
                () => onCreateProject?.Invoke()
            );
            createProjectButtonEntity.AddComponent(createProjectButton);
            
            pageEntities.Add(createProjectButtonEntity);
            
            // Browse Project Button (below create project button)
            browseProjectButtonEntity = new Entity();
            browseProjectButtonEntity.LayerMask = 0b1000000000000000;
            browseProjectButtonEntity.Position = GetPosition(950, 130); // 50 + 60 + 20 spacing
            browseProjectButtonEntity.Enabled = true;
            browseProjectButtonEntity.InitComponents();
            
            var anchorBrowseProject = new AnchorComponent();
            browseProjectButtonEntity.AddComponent(anchorBrowseProject);
            anchorBrowseProject.EnableAnchoring(right: true, top: true);
            
            browseProjectButton = new ButtonComponent(
                "browse project",
                new int2(ButtonWidth, ButtonHeight),
                font,
                () => onBrowseProject?.Invoke()
            );
            browseProjectButtonEntity.AddComponent(browseProjectButton);
            
            pageEntities.Add(browseProjectButtonEntity);
        }
        
        public override void OnNavigateTo(string targetPage) {
            if (targetPage == "newproject") {
                onCreateProject?.Invoke();
            } else if (targetPage == "browse") {
                onBrowseProject?.Invoke();
            }
        }
        
        protected override void UpdatePagePosition() {
            // Update title position
            if (titleEntity != null) {
                titleEntity.Position = GetPosition(20, 30);
            }
            
            // Update button positions
            if (createProjectButtonEntity != null) {
                createProjectButtonEntity.Position = GetPosition(950, 50);
            }
            
            if (browseProjectButtonEntity != null) {
                browseProjectButtonEntity.Position = GetPosition(950, 130);
            }
        }
        
        /// <summary>
        /// Set the action callbacks for the buttons
        /// </summary>
        public void SetCallbacks(Action? onCreateProject, Action? onBrowseProject) {
            this.onCreateProject = onCreateProject;
            this.onBrowseProject = onBrowseProject;
        }
    }
}
