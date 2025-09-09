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
            titleEntity.Enabled = true;
            titleEntity.InitComponents();
            
            var titleText = new TextComponent();
            titleText.Text = "helengine";
            titleText.Font = font;
            titleText.Color = new byte4(255, 255, 255, 255);
            titleText.RenderOrder2D = 3;
            titleEntity.AddComponent(titleText);
            
            AddPageEntity(titleEntity, 20, 30);
        }
        
        private void CreateProjectButtons() {
            // Create Project Button (top-right)
            createProjectButtonEntity = new Entity();
            createProjectButtonEntity.LayerMask = 0b1000000000000000;
            createProjectButtonEntity.Enabled = true;
            createProjectButtonEntity.InitComponents();
            
            var anchorCreateProject = new AnchorComponent();
            createProjectButtonEntity.AddComponent(anchorCreateProject);
            // Defer anchoring until the page is fully shown to avoid capturing off-screen distances
            OnShown(() => anchorCreateProject.EnableAnchoring(right: true, top: true));
            
            createProjectButton = new ButtonComponent(
                "create project",
                new int2(ButtonWidth, ButtonHeight),
                font,
                () => onCreateProject?.Invoke()
            );
            createProjectButtonEntity.AddComponent(createProjectButton);
            AddPageEntity(createProjectButtonEntity, 830, 20);
            
            // Browse Project Button (below create project button)
            browseProjectButtonEntity = new Entity();
            browseProjectButtonEntity.LayerMask = 0b1000000000000000;
            browseProjectButtonEntity.Enabled = true;
            browseProjectButtonEntity.InitComponents();
            
            var anchorBrowseProject = new AnchorComponent();
            browseProjectButtonEntity.AddComponent(anchorBrowseProject);
            OnShown(() => anchorBrowseProject.EnableAnchoring(right: true, top: true));
            
            browseProjectButton = new ButtonComponent(
                "browse project",
                new int2(ButtonWidth, ButtonHeight),
                font,
                () => onBrowseProject?.Invoke()
            );
            browseProjectButtonEntity.AddComponent(browseProjectButton);
            AddPageEntity(browseProjectButtonEntity, 1050, 20);
        }
        
        public override void OnNavigateTo(string targetPage) {
            if (targetPage == "newproject") {
                onCreateProject?.Invoke();
            } else if (targetPage == "browse") {
                onBrowseProject?.Invoke();
            }
        }
        
        // Base class now manages positions via AddPageEntity
        
        /// <summary>
        /// Set the action callbacks for the buttons
        /// </summary>
        public void SetCallbacks(Action? onCreateProject, Action? onBrowseProject) {
            this.onCreateProject = onCreateProject;
            this.onBrowseProject = onBrowseProject;
        }
    }
}
