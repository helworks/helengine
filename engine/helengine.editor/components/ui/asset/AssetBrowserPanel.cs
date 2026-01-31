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
        /// Font used to render toolbar and row labels.
        /// </summary>
        readonly FontAsset Font;
        /// <summary>
        /// Root entity hosting the content below the title bar.
        /// </summary>
        readonly EditorEntity ContentRoot;
        /// <summary>
        /// Shared asset browser view used for toolbar and list rendering.
        /// </summary>
        readonly AssetBrowserView BrowserView;
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
        public AssetBrowserPanel(FontAsset font, string projectPath) : base(font) {
            Font = font;
            Title = "Assets";
            MinSize = new int2(260, 180);

            byte toolbarOrder = Core.Instance.ObjectManager.GetRenderOrderForLayer2D(1);
            byte rowBackgroundOrder = Core.Instance.ObjectManager.GetRenderOrderForLayer2D(1);
            byte iconBackgroundOrder = Core.Instance.ObjectManager.GetRenderOrderForLayer2D(1);
            byte textOrder = Core.Instance.ObjectManager.GetRenderOrderForLayer2D(2);

            ContentRoot = new EditorEntity {
                LayerMask = LayerMask,
                Position = new float3(0, TitleBarHeight, 0.05f)
            };
            AddChild(ContentRoot);

            BrowserView = new AssetBrowserView(
                Font,
                projectPath,
                LayerMask,
                toolbarOrder,
                rowBackgroundOrder,
                iconBackgroundOrder,
                textOrder);
            ContentRoot.AddChild(BrowserView.Entity);

            BrowserView.AssetActivated += HandleAssetActivated;
            BrowserView.SelectionCleared += HandleSelectionCleared;

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
    }
}
