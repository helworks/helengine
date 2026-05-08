namespace helengine.editor {
    /// <summary>
    /// Displays a dockable browser that mirrors the project assets folder.
    /// </summary>
    public class AssetBrowserPanel : DockableEntity {
        /// <summary>
        /// Height of each row in the asset list.
        /// </summary>
        public const int RowHeight = AssetBrowserView.RowHeight;
        /// <summary>
        /// Height of the toolbar area above the list.
        /// </summary>
        public const int ToolbarHeight = AssetBrowserView.ToolbarHeight;
        /// <summary>
        /// Horizontal spacing between the main menu and the file template menu.
        /// </summary>
        const int FileMenuSpacing = 6;

        /// <summary>
        /// Font used to render toolbar and row labels.
        /// </summary>
        FontAsset Font;
        /// <summary>
        /// Root entity hosting the content below the title bar.
        /// </summary>
        readonly EditorEntity ContentRoot;
        /// <summary>
        /// Shared asset browser view used for toolbar and list rendering.
        /// </summary>
        readonly AssetBrowserView BrowserView;
        /// <summary>
        /// Context menu used to create assets from the browser.
        /// </summary>
        readonly ContextMenu AssetContextMenu;
        /// <summary>
        /// Context menu used to select file templates.
        /// </summary>
        readonly ContextMenu FileTemplateMenu;
        /// <summary>
        /// Opens the current asset folder in the system file explorer.
        /// </summary>
        readonly Action<string> OpenFolderAction;
        /// <summary>
        /// Menu items used to create assets.
        /// </summary>
        readonly List<ContextMenuItem> CreateAssetItems;
        /// <summary>
        /// Menu items used to create files from templates.
        /// </summary>
        readonly List<ContextMenuItem> FileTemplateItems;
        /// <summary>
        /// Gets or sets a value indicating whether the panel has completed initialization.
        /// </summary>
        bool IsInitialized;

        /// <summary>
        /// Raised when a file entry is selected in the browser.
        /// </summary>
        public event Action<AssetBrowserEntry> AssetSelected;
        /// <summary>
        /// Raised when the current selection is cleared.
        /// </summary>
        public event Action SelectionCleared;

        /// <summary>
        /// Initializes a new asset browser panel for the provided project path.
        /// </summary>
        /// <param name="font">Font used for labels.</param>
        /// <param name="projectPath">Path to the project root.</param>
        public AssetBrowserPanel(FontAsset font, string projectPath)
            : this(font, projectPath, EditorUiMetrics.Default, OpenFolderInExplorer) {
        }

        /// <summary>
        /// Initializes a new asset browser panel for the provided project path using the supplied scaled dock metrics.
        /// </summary>
        /// <param name="font">Font used for labels.</param>
        /// <param name="projectPath">Path to the project root.</param>
        /// <param name="metrics">Scaled editor UI metrics used to size the dock title bar and browser content.</param>
        public AssetBrowserPanel(FontAsset font, string projectPath, EditorUiMetrics metrics)
            : this(font, projectPath, metrics, OpenFolderInExplorer) {
        }

        /// <summary>
        /// Initializes a new asset browser panel for the provided project path using the supplied folder launcher.
        /// </summary>
        /// <param name="font">Font used for labels.</param>
        /// <param name="projectPath">Path to the project root.</param>
        /// <param name="metrics">Scaled editor UI metrics used to size the dock title bar and browser content.</param>
        /// <param name="openFolderAction">Callback used when the user chooses to open the current folder in Explorer.</param>
        public AssetBrowserPanel(FontAsset font, string projectPath, EditorUiMetrics metrics, Action<string> openFolderAction) : base(font, metrics) {
            if (openFolderAction == null) {
                throw new ArgumentNullException(nameof(openFolderAction));
            }

            Font = font;
            Title = "Assets";
            MinSize = new int2(UiMetrics.ScalePixels(260), UiMetrics.ScalePixels(180));
            OpenFolderAction = openFolderAction;

            byte toolbarOrder = RenderOrder2D.PanelSurface;
            byte rowBackgroundOrder = RenderOrder2D.PanelSurface;
            byte iconBackgroundOrder = RenderOrder2D.PanelSurface;
            byte textOrder = RenderOrder2D.PanelForeground;

            ContentRoot = new EditorEntity {
                LayerMask = LayerMask,
                Position = new float3(0, TitleBarHeightPixels, 0.05f)
            };
            AddChild(ContentRoot);

            BrowserView = new AssetBrowserView(
                Font,
                UiMetrics,
                projectPath,
                LayerMask,
                toolbarOrder,
                rowBackgroundOrder,
                iconBackgroundOrder,
                textOrder,
                true,
                this);
            ContentRoot.AddChild(BrowserView.Entity);

            BrowserView.AssetActivated += HandleAssetActivated;
            BrowserView.SelectionCleared += HandleSelectionCleared;

            byte menuBackgroundOrder = RenderOrder2D.OverlayBackground;
            byte menuTextOrder = RenderOrder2D.OverlayForeground;
            AssetContextMenu = new ContextMenu(Font, LayerMask, menuBackgroundOrder, menuTextOrder);
            AddChild(AssetContextMenu.Entity);
            FileTemplateMenu = new ContextMenu(Font, LayerMask, menuBackgroundOrder, menuTextOrder);
            AddChild(FileTemplateMenu.Entity);

            CreateAssetItems = new List<ContextMenuItem> {
                new ContextMenuItem("New File", ShowFileTemplateMenu, ShowFileTemplateMenu, false),
                new ContextMenuItem("New Folder", CreateFolder, HideFileTemplateMenu, true),
                new ContextMenuItem("Show in Explorer", ShowCurrentDirectoryInExplorer)
            };
            FileTemplateItems = new List<ContextMenuItem>(4);

            AddComponent(new AssetBrowserPanelUpdater(this));
            IsInitialized = true;
            BrowserView.UpdateLayout(Math.Max(Size.X, MinSize.X), Math.Max(Size.Y, MinSize.Y));
            RefreshRenderOrderBias();
        }

        /// <summary>
        /// Refreshes the asset list from disk and updates layout.
        /// </summary>
        public void RefreshEntries() {
            BrowserView.RefreshEntries();
            RefreshRenderOrderBias();
        }

        /// <summary>
        /// Handles layout updates when the dockable size changes.
        /// </summary>
        protected override void OnSizeChanged() {
            base.OnSizeChanged();
            if (!IsInitialized) {
                return;
            }

            BrowserView.UpdateLayout(Math.Max(Size.X, MinSize.X), Math.Max(Size.Y, MinSize.Y));
            AssetContextMenu.UpdateLayout(GetContextMenuHostSize());
            FileTemplateMenu.UpdateLayout(GetContextMenuHostSize());
        }

        /// <summary>
        /// Reapplies scaled dock metrics after one live UI scale change.
        /// </summary>
        /// <param name="font">Updated dock title and browser font.</param>
        /// <param name="metrics">Updated scaled editor UI metrics.</param>
        public override void ApplyUiMetrics(FontAsset font, EditorUiMetrics metrics) {
            if (font == null) {
                throw new ArgumentNullException(nameof(font));
            }

            Font = font;
            base.ApplyUiMetrics(font, metrics);
            BrowserView.ApplyUiMetrics(font, metrics);
        }

        /// <summary>
        /// Updates scaled content offsets after the shared dock title-bar metrics change.
        /// </summary>
        protected override void HandleUiMetricsApplied() {
            MinSize = new int2(UiMetrics.ScalePixels(260), UiMetrics.ScalePixels(180));
            ContentRoot.Position = new float3(0f, TitleBarHeightPixels, 0.05f);
            if (!IsInitialized) {
                return;
            }

            BrowserView.UpdateLayout(Math.Max(Size.X, MinSize.X), Math.Max(Size.Y, MinSize.Y));
        }

        /// <summary>
        /// Updates context menu input each frame.
        /// </summary>
        internal void UpdateContextMenuInput() {
            InputSystem input = Core.Instance.Input;
            if (!AssetContextMenu.IsVisible && FileTemplateMenu.IsVisible) {
                FileTemplateMenu.Hide();
            }
            if (!BrowserView.CanCreateFileSystemEntries) {
                AssetContextMenu.Hide();
                FileTemplateMenu.Hide();
                return;
            }
            if (!input.WasMouseRightButtonPressed()) {
                return;
            }

            int2 pointer = input.GetMousePosition();
            if (EditorInputCaptureService.IsPointerBlocked(pointer, owner => !ReferenceEquals(owner, this))) {
                return;
            }

            if (!IsPointerInsideContent(pointer)) {
                return;
            }

            int2 local = new int2(
                pointer.X - (int)Math.Round(Position.X),
                pointer.Y - (int)Math.Round(Position.Y));
            AssetContextMenu.Show(CreateAssetItems, local, GetContextMenuHostSize());
            FileTemplateMenu.Hide();
        }

        /// <summary>
        /// Hides the context menu when the panel is disabled.
        /// </summary>
        /// <param name="newEnabled">New enabled state.</param>
        protected override void ParentEnabledChange(bool newEnabled) {
            base.ParentEnabledChange(newEnabled);

            if (!newEnabled) {
                AssetContextMenu.Hide();
                FileTemplateMenu.Hide();
            }
        }

        /// <summary>
        /// Handles asset activation from the shared browser view.
        /// </summary>
        /// <param name="entry">Activated asset entry.</param>
        void HandleAssetActivated(AssetBrowserEntry entry) {
            if (entry == null) {
                throw new ArgumentNullException(nameof(entry));
            }

            AssetSelected?.Invoke(entry);
        }

        /// <summary>
        /// Handles selection clear requests from the shared browser view.
        /// </summary>
        void HandleSelectionCleared() {
            if (SelectionCleared != null) {
                SelectionCleared();
            }
        }

        /// <summary>
        /// Creates a new folder in the current asset browser directory.
        /// </summary>
        void CreateFolder() {
            string directory = BrowserView.CurrentDirectoryPath;
            if (string.IsNullOrWhiteSpace(directory)) {
                throw new InvalidOperationException("Asset directory could not be resolved.");
            }

            Directory.CreateDirectory(directory);
            string folderName = AssetCreationUtils.BuildUniqueFolderName(directory, "New Folder");
            string folderPath = Path.Combine(directory, folderName);
            Directory.CreateDirectory(folderPath);
            BrowserView.RefreshEntries();
        }

        /// <summary>
        /// Opens the current writable asset folder in the system file explorer.
        /// </summary>
        void ShowCurrentDirectoryInExplorer() {
            string directory = BrowserView.CurrentDirectoryPath;
            if (string.IsNullOrWhiteSpace(directory)) {
                throw new InvalidOperationException("Asset directory could not be resolved.");
            }

            OpenFolderAction(directory);
        }

        /// <summary>
        /// Shows the file template menu anchored to the last context menu position.
        /// </summary>
        void ShowFileTemplateMenu() {
            BuildFileTemplateItems();
            if (FileTemplateItems.Count == 0) {
                FileTemplateMenu.Hide();
                return;
            }

            int2 position = GetFileTemplateMenuPosition();
            FileTemplateMenu.Show(FileTemplateItems, position, GetContextMenuHostSize());
        }

        /// <summary>
        /// Hides the file template menu.
        /// </summary>
        void HideFileTemplateMenu() {
            FileTemplateMenu.Hide();
        }

        /// <summary>
        /// Builds the file template menu items from the registered templates.
        /// </summary>
        void BuildFileTemplateItems() {
            FileTemplateItems.Clear();

            IReadOnlyList<EditorFileTemplate> templates = EditorFileTemplateRegistry.RegisteredTemplates;
            for (int i = 0; i < templates.Count; i++) {
                EditorFileTemplate template = templates[i];
                if (template == null) {
                    continue;
                }

                EditorFileTemplate captured = template;
                FileTemplateItems.Add(new ContextMenuItem(template.Label, () => CreateFileFromTemplate(captured)));
            }
        }

        /// <summary>
        /// Creates a file from the selected template in the current folder.
        /// </summary>
        /// <param name="template">Template to apply.</param>
        void CreateFileFromTemplate(EditorFileTemplate template) {
            if (template == null) {
                throw new ArgumentNullException(nameof(template));
            }

            string directory = BrowserView.CurrentDirectoryPath;
            if (string.IsNullOrWhiteSpace(directory)) {
                throw new InvalidOperationException("Asset directory could not be resolved.");
            }

            EditorFileTemplateService.CreateFile(template, directory);
            BrowserView.RefreshEntries();
        }

        /// <summary>
        /// Computes the position for the file template menu.
        /// </summary>
        /// <returns>Menu position in panel-local coordinates.</returns>
        int2 GetFileTemplateMenuPosition() {
            int2 mainPosition = AssetContextMenu.Position;
            int2 mainSize = AssetContextMenu.Size;
            int x = mainPosition.X + mainSize.X + FileMenuSpacing;
            int y = mainPosition.Y + ContextMenu.PaddingY;
            return new int2(x, y);
        }

        /// <summary>
        /// Gets the host size used to clamp the context menu.
        /// </summary>
        /// <returns>Menu host size in pixels.</returns>
        int2 GetContextMenuHostSize() {
            return new int2(Size.X, Size.Y + TitleBarHeightPixels);
        }

        /// <summary>
        /// Determines whether the pointer is inside the panel content area.
        /// </summary>
        /// <param name="pointer">Pointer position in window coordinates.</param>
        /// <returns>True when the pointer is within the content area.</returns>
        bool IsPointerInsideContent(int2 pointer) {
            float left = Position.X;
            float top = Position.Y + TitleBarHeightPixels;
            float right = left + Size.X;
            float bottom = top + Size.Y;

            return pointer.X >= left &&
                   pointer.X < right &&
                   pointer.Y >= top &&
                   pointer.Y < bottom;
        }

        /// <summary>
        /// Opens one folder path in Windows Explorer.
        /// </summary>
        /// <param name="folderPath">Absolute folder path to reveal.</param>
        static void OpenFolderInExplorer(string folderPath) {
            if (string.IsNullOrWhiteSpace(folderPath)) {
                throw new ArgumentException("Folder path must be provided.", nameof(folderPath));
            }

            string fullFolderPath = Path.GetFullPath(folderPath);
            System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo {
                FileName = "explorer.exe",
                Arguments = "\"" + fullFolderPath + "\"",
                UseShellExecute = false
            };

            using System.Diagnostics.Process process = System.Diagnostics.Process.Start(startInfo);
            if (process == null) {
                throw new InvalidOperationException($"Failed to open '{fullFolderPath}' in Explorer.");
            }
        }
    }
}


