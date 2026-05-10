namespace helengine.editor {
    /// <summary>
    /// Shared modal dialog used to select a project asset from the browser.
    /// </summary>
    public sealed class AssetPickerModal : EditorDialogBase {
        /// <summary>
        /// Default minimum width for the picker content area.
        /// </summary>
        public const int MinPanelWidth = 360;

        /// <summary>
        /// Default minimum height for the picker dialog, including the shared title bar.
        /// </summary>
        public const int MinPanelHeight = 260;

        /// <summary>
        /// Default maximum width for the picker dialog.
        /// </summary>
        public const int MaxPanelWidth = 920;

        /// <summary>
        /// Default maximum height for the picker dialog, including the shared title bar.
        /// </summary>
        public const int MaxPanelHeight = 720;

        /// <summary>
        /// Padding applied inside the modal content area.
        /// </summary>
        public const int PanelPadding = 16;

        /// <summary>
        /// Spacing between the title bar and the browser toolbar.
        /// </summary>
        public const int SectionSpacing = 10;

        /// <summary>
        /// Height of each row in the asset list.
        /// </summary>
        public const int RowHeight = AssetBrowserView.RowHeight;

        /// <summary>
        /// Height of the toolbar area above the list.
        /// </summary>
        public const int ToolbarHeight = AssetBrowserView.ToolbarHeight;

        /// <summary>
        /// Font used for header and list labels.
        /// </summary>
        readonly FontAsset Font;

        /// <summary>
        /// Shared scaled metrics used to size the picker shell and spacing.
        /// </summary>
        readonly EditorUiMetrics Metrics;

        /// <summary>
        /// Shared asset browser view hosting the toolbar and list.
        /// </summary>
        readonly AssetBrowserView BrowserView;

        /// <summary>
        /// Render order used for panel surfaces.
        /// </summary>
        readonly byte PanelOrder;

        /// <summary>
        /// Render order used for text labels.
        /// </summary>
        readonly byte TextOrder;

        /// <summary>
        /// Callback invoked when the user picks an asset.
        /// </summary>
        Action<AssetBrowserEntry> PickedCallback;

        /// <summary>
        /// Tracks whether the modal has completed initialization.
        /// </summary>
        bool IsInitialized;

        /// <summary>
        /// Initializes a new asset picker modal for the provided project path.
        /// </summary>
        /// <param name="font">Font used for labels.</param>
        /// <param name="projectPath">Path to the project root.</param>
        public AssetPickerModal(FontAsset font, string projectPath)
            : this(font, EditorUiMetrics.Default, projectPath) {
        }

        /// <summary>
        /// Initializes a new asset picker modal for the provided project path using one shared metrics source.
        /// </summary>
        /// <param name="font">Font used for labels.</param>
        /// <param name="metrics">Scaled editor UI metrics used to size the picker.</param>
        /// <param name="projectPath">Path to the project root.</param>
        public AssetPickerModal(FontAsset font, EditorUiMetrics metrics, string projectPath)
            : base("AssetPickerModal", "Select Asset", font, metrics, MinPanelWidth, MinPanelHeight, EditorTitleBar.HeightPixels) {
            if (font == null) {
                throw new ArgumentNullException(nameof(font));
            }
            if (metrics == null) {
                throw new ArgumentNullException(nameof(metrics));
            }
            if (string.IsNullOrWhiteSpace(projectPath)) {
                throw new ArgumentException("Project path must be provided.", nameof(projectPath));
            }

            Font = font;
            Metrics = metrics;
            PanelOrder = RenderOrder2D.ModalBackground;
            TextOrder = RenderOrder2D.ModalForeground;

            DialogIsResizable = false;
            SetDialogMinimumSize(GetMinimumPanelWidthPixels(), GetMinimumPanelHeightPixels());

            BrowserView = new AssetBrowserView(
                Font,
                projectPath,
                LayerMask,
                PanelOrder,
                PanelOrder,
                PanelOrder,
                TextOrder);
            BrowserView.SetToolbarButtonRenderOrders(TextOrder, TextOrder);
            BrowserView.AssetActivated += HandleAssetActivated;
            DialogContentRoot.AddChild(BrowserView.Entity);

            Enabled = false;
            IsInitialized = true;
        }

        /// <summary>
        /// Gets a value indicating whether the modal is currently visible.
        /// </summary>
        public new bool IsVisible => Enabled;

        /// <summary>
        /// Shows the modal and registers the callback to receive the picked asset.
        /// </summary>
        /// <param name="onPicked">Callback invoked when an asset is selected.</param>
        public void Show(Action<AssetBrowserEntry> onPicked) {
            if (onPicked == null) {
                throw new ArgumentNullException(nameof(onPicked));
            }

            PickedCallback = onPicked;
            BrowserView.ClearExtensionFilter();
            Enabled = true;
            ShowDialogImmediately();
            LayoutBrowserView(DialogWidth, DialogHeight);
            BrowserView.RefreshEntries();
        }

        /// <summary>
        /// Shows the modal with an extension filter and registers the callback to receive the picked asset.
        /// </summary>
        /// <param name="onPicked">Callback invoked when an asset is selected.</param>
        /// <param name="extensionFilter">Extension filter for assets.</param>
        public void Show(Action<AssetBrowserEntry> onPicked, string extensionFilter) {
            if (onPicked == null) {
                throw new ArgumentNullException(nameof(onPicked));
            }

            PickedCallback = onPicked;
            BrowserView.SetExtensionFilter(extensionFilter);
            Enabled = true;
            ShowDialogImmediately();
            LayoutBrowserView(DialogWidth, DialogHeight);
            BrowserView.RefreshEntries();
        }

        /// <summary>
        /// Hides the modal and clears any pending pick callback.
        /// </summary>
        public void Hide() {
            PickedCallback = null;
            BrowserView.ClearExtensionFilter();
            if (!Enabled) {
                return;
            }

            Enabled = false;
            HideDialogImmediately();
            ResetDialogPositioning();
        }

        /// <summary>
        /// Updates modal sizing and layout to fit the provided window dimensions.
        /// </summary>
        /// <param name="windowWidth">Current window width.</param>
        /// <param name="windowHeight">Current window height.</param>
        public void UpdateLayout(int windowWidth, int windowHeight) {
            if (!IsInitialized) {
                return;
            }

            SetDialogSize(ResolveDialogWidth(windowWidth), ResolveDialogHeight(windowHeight));
            if (!UpdateDialogFrame(windowWidth, windowHeight)) {
                return;
            }

            LayoutBrowserView(DialogWidth, DialogHeight);
        }

        /// <summary>
        /// Closes the modal when the shared dialog shell requests dismissal.
        /// </summary>
        protected override void OnCloseRequested() {
            Hide();
        }

        /// <summary>
        /// Updates the asset browser view placement within the dialog content area.
        /// </summary>
        /// <param name="dialogWidth">Dialog width in pixels.</param>
        /// <param name="dialogHeight">Dialog height in pixels.</param>
        void LayoutBrowserView(int dialogWidth, int dialogHeight) {
            int contentWidth = Math.Max(0, dialogWidth - GetPanelPaddingPixels() * 2);
            int contentHeight = Math.Max(0, dialogHeight - GetPanelPaddingPixels() - EditorTitleBar.HeightPixels - GetSectionSpacingPixels() - GetPanelPaddingPixels());

            BrowserView.Entity.Position = new float3(GetPanelPaddingPixels(), GetPanelPaddingPixels() + GetSectionSpacingPixels(), 0.2f);
            BrowserView.UpdateLayout(contentWidth, contentHeight);
        }

        /// <summary>
        /// Handles activation events from the shared asset browser view.
        /// </summary>
        /// <param name="entry">Activated asset entry.</param>
        void HandleAssetActivated(AssetBrowserEntry entry) {
            if (entry == null) {
                throw new ArgumentNullException(nameof(entry));
            }

            NotifyAssetPicked(entry);
        }

        /// <summary>
        /// Notifies listeners that one asset was picked and hides the modal.
        /// </summary>
        /// <param name="entry">Picked asset entry.</param>
        void NotifyAssetPicked(AssetBrowserEntry entry) {
            if (entry == null) {
                throw new ArgumentNullException(nameof(entry));
            }

            Action<AssetBrowserEntry> callback = PickedCallback;
            PickedCallback = null;
            Hide();
            if (callback != null) {
                callback(entry);
            }
        }

        /// <summary>
        /// Gets the scaled minimum panel width.
        /// </summary>
        /// <returns>Scaled minimum panel width in pixels.</returns>
        int GetMinimumPanelWidthPixels() {
            return Metrics.ScalePixels(MinPanelWidth);
        }

        /// <summary>
        /// Gets the scaled minimum panel height.
        /// </summary>
        /// <returns>Scaled minimum panel height in pixels.</returns>
        int GetMinimumPanelHeightPixels() {
            return Metrics.ScalePixels(MinPanelHeight);
        }

        /// <summary>
        /// Gets the scaled maximum panel width.
        /// </summary>
        /// <returns>Scaled maximum panel width in pixels.</returns>
        int GetMaximumPanelWidthPixels() {
            return Metrics.ScalePixels(MaxPanelWidth);
        }

        /// <summary>
        /// Gets the scaled maximum panel height.
        /// </summary>
        /// <returns>Scaled maximum panel height in pixels.</returns>
        int GetMaximumPanelHeightPixels() {
            return Metrics.ScalePixels(MaxPanelHeight);
        }

        /// <summary>
        /// Gets the scaled panel padding.
        /// </summary>
        /// <returns>Scaled panel padding in pixels.</returns>
        int GetPanelPaddingPixels() {
            return Metrics.ScalePixels(PanelPadding);
        }

        /// <summary>
        /// Gets the scaled section spacing between the title bar and browser content.
        /// </summary>
        /// <returns>Scaled section spacing in pixels.</returns>
        int GetSectionSpacingPixels() {
            return Metrics.ScalePixels(SectionSpacing);
        }

        /// <summary>
        /// Resolves the dialog width used by the responsive asset picker layout.
        /// </summary>
        /// <param name="windowWidth">Current window width.</param>
        /// <returns>Desired dialog width in pixels.</returns>
        int ResolveDialogWidth(int windowWidth) {
            int width = Math.Max(1, windowWidth);
            int maxWidth = Math.Max(GetMinimumPanelWidthPixels(), width - GetPanelPaddingPixels() * 2);
            int dialogWidth = Math.Min(GetMaximumPanelWidthPixels(), maxWidth);
            return Math.Min(dialogWidth, width);
        }

        /// <summary>
        /// Resolves the dialog height used by the responsive asset picker layout.
        /// </summary>
        /// <param name="windowHeight">Current window height.</param>
        /// <returns>Desired dialog height in pixels.</returns>
        int ResolveDialogHeight(int windowHeight) {
            int height = Math.Max(1, windowHeight);
            int maxHeight = Math.Max(GetMinimumPanelHeightPixels(), height - GetPanelPaddingPixels() * 2);
            int dialogHeight = Math.Min(GetMaximumPanelHeightPixels(), maxHeight);
            return Math.Min(dialogHeight, height);
        }
    }
}
