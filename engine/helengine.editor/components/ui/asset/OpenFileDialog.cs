namespace helengine.editor {
    /// <summary>
    /// Floating modal dialog used to choose one `.helen` scene file under the project assets folder.
    /// </summary>
    public class OpenFileDialog : EditorDialogBase {
        /// <summary>
        /// Default minimum width for the dialog panel.
        /// </summary>
        public const int MinPanelWidth = 420;
        /// <summary>
        /// Default minimum height for the dialog panel.
        /// </summary>
        public const int MinPanelHeight = 320;
        /// <summary>
        /// Default maximum width for the dialog panel at first show.
        /// </summary>
        public const int MaxPanelWidth = 920;
        /// <summary>
        /// Default maximum height for the dialog panel at first show.
        /// </summary>
        public const int MaxPanelHeight = 760;
        /// <summary>
        /// Padding applied inside the dialog panel.
        /// </summary>
        public const int PanelPadding = 16;
        /// <summary>
        /// Height of the header band used by the content layout.
        /// </summary>
        public const int HeaderHeight = 32;
        /// <summary>
        /// Height of the footer button row.
        /// </summary>
        public const int FooterHeight = 28;
        /// <summary>
        /// Height reserved for the status text row.
        /// </summary>
        public const int StatusHeight = 18;
        /// <summary>
        /// Vertical spacing between dialog sections.
        /// </summary>
        public const int SectionSpacing = 10;
        /// <summary>
        /// <summary>
        /// Maximum time in milliseconds between row activations that counts as a double-click.
        /// </summary>
        const int RowDoubleClickMs = 350;
        /// <summary>
        /// Horizontal padding inside the header text row.
        /// </summary>
        public const int HeaderPadding = 8;
        /// <summary>
        /// Fixed size used for the cancel button.
        /// </summary>
        static readonly int2 CancelButtonSize = new int2(88, 22);
        /// <summary>
        /// Fixed size used for the open button.
        /// </summary>
        static readonly int2 OpenButtonSize = new int2(88, 22);

        /// <summary>
        /// Font used for dialog labels and buttons.
        /// </summary>
        readonly FontAsset Font;
        /// <summary>
        /// Shared asset browser view used in filesystem-only mode.
        /// </summary>
        readonly AssetBrowserView BrowserView;
        /// <summary>
        /// Host entity for the validation or status text.
        /// </summary>
        readonly EditorEntity StatusHost;
        /// <summary>
        /// Validation or status text shown above the footer.
        /// </summary>
        readonly TextComponent StatusText;
        /// <summary>
        /// Host entity for the cancel button.
        /// </summary>
        readonly EditorEntity CancelButtonHost;
        /// <summary>
        /// Cancel button component.
        /// </summary>
        readonly ButtonComponent CancelButton;
        /// <summary>
        /// Host entity for the open button.
        /// </summary>
        readonly EditorEntity OpenButtonHost;
        /// <summary>
        /// Open button component.
        /// </summary>
        readonly ButtonComponent OpenButton;
        /// <summary>
        /// Cached panel size used for layout and test inspection.
        /// </summary>
        int2 PanelSize;
        /// <summary>
        /// Cached panel position used for layout and test inspection.
        /// </summary>
        int2 PanelPosition;
        /// <summary>
        /// Tracks whether the dialog has completed initialization.
        /// </summary>
        bool IsInitialized;
        /// <summary>
        /// Timestamp of the most recent row activation used for double-click detection.
        /// </summary>
        long LastActivatedTicks;
        /// <summary>
        /// Currently selected file entry from the browser.
        /// </summary>
        AssetBrowserEntry SelectedEntry;

        /// <summary>
        /// Raised when the user confirms a valid scene file path.
        /// </summary>
        public event Action<string> OpenRequested;

        /// <summary>
        /// Initializes a new open-file dialog rooted at the project assets folder.
        /// </summary>
        /// <param name="font">Font used for labels and buttons.</param>
        /// <param name="projectPath">Project root that owns the assets folder.</param>
        public OpenFileDialog(FontAsset font, string projectPath)
            : base("OpenFileDialog", "Open Map", font, MinPanelWidth, MinPanelHeight, HeaderHeight) {
            if (string.IsNullOrWhiteSpace(projectPath)) {
                throw new ArgumentException("Project path must be provided.", nameof(projectPath));
            }

            Font = font;
            PanelSize = new int2(MinPanelWidth, MinPanelHeight);
            PanelPosition = int2.Zero;
            DialogPanelBackground.FillColor = ThemeManager.Colors.SurfacePrimary;

            byte toolbarOrder = DialogPanelOrder;
            byte rowBackgroundOrder = DialogPanelOrder;
            byte iconBackgroundOrder = DialogPanelOrder;

            BrowserView = new AssetBrowserView(
                Font,
                projectPath,
                LayerMask,
                toolbarOrder,
                rowBackgroundOrder,
                iconBackgroundOrder,
                DialogTextOrder,
                false);
            BrowserView.SetToolbarButtonRenderOrders(DialogTextOrder, DialogTextOrder);
            BrowserView.SetExtensionFilter(SceneAsset.FileExtension);
            BrowserView.AssetActivated += HandleAssetActivated;
            BrowserView.SelectionCleared += HandleSelectionCleared;
            DialogPanelRoot.AddChild(BrowserView.Entity);

            StatusHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            DialogPanelRoot.AddChild(StatusHost);

            StatusText = new TextComponent {
                Font = font,
                Text = string.Empty,
                Color = ThemeManager.Colors.StateWarning,
                Size = new int2(1, Math.Max(1, (int)Math.Ceiling(Math.Max(font.LineHeight, 1f)))),
                RenderOrder2D = DialogTextOrder
            };
            StatusHost.AddComponent(StatusText);

            CancelButtonHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            DialogPanelRoot.AddChild(CancelButtonHost);

            CancelButton = new ButtonComponent("Cancel", CancelButtonSize, font, Hide, 0f);
            CancelButtonHost.AddComponent(CancelButton);
            CancelButton.SetRenderOrders(DialogTextOrder, DialogTextOrder);

            OpenButtonHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            DialogPanelRoot.AddChild(OpenButtonHost);

            OpenButton = new ButtonComponent("Open", OpenButtonSize, font, HandleOpenClicked, 0f);
            OpenButtonHost.AddComponent(OpenButton);
            OpenButton.SetRenderOrders(DialogTextOrder, DialogTextOrder);

            Enabled = false;
            IsInitialized = true;
        }

        /// <summary>
        /// Shows the dialog and prepares its initial directory.
        /// </summary>
        /// <param name="initialRelativeDirectory">Relative directory to navigate to initially.</param>
        public void Show(string initialRelativeDirectory) {
            ResetDialogPositioning();
            SelectedEntry = null;
            LastActivatedTicks = 0;
            BrowserView.ClearSelection();
            StatusText.Text = string.Empty;
            Enabled = true;

            if (!BrowserView.TryNavigateTo(initialRelativeDirectory)) {
                if (!BrowserView.TryNavigateTo(SceneSavePathResolver.DefaultSceneDirectory)) {
                    BrowserView.TryNavigateTo(string.Empty);
                }
            }

            BrowserView.RefreshEntries();
        }

        /// <summary>
        /// Hides the dialog and clears its input-capture blocker.
        /// </summary>
        public void Hide() {
            SelectedEntry = null;
            LastActivatedTicks = 0;
            BrowserView.ClearSelection();
            StatusText.Text = string.Empty;
            ClearDialogBackdrop();
            ResetDialogPositioning();
            Enabled = false;
        }

        /// <summary>
        /// Shows one validation or load error in the dialog footer.
        /// </summary>
        /// <param name="message">Validation or load error to display.</param>
        public void ShowError(string message) {
            StatusText.Text = message ?? string.Empty;
        }

        /// <summary>
        /// Updates dialog sizing and layout to fit the provided window dimensions.
        /// </summary>
        /// <param name="windowWidth">Current window width.</param>
        /// <param name="windowHeight">Current window height.</param>
        public void UpdateLayout(int windowWidth, int windowHeight) {
            if (!IsInitialized) {
                return;
            }

            if (!Enabled) {
                ClearDialogBackdrop();
                return;
            }

            int safeWindowWidth = Math.Max(1, windowWidth);
            int safeWindowHeight = Math.Max(1, windowHeight);
            if (!DialogIsUserPositioned) {
                int maxWidth = Math.Max(MinPanelWidth, safeWindowWidth - PanelPadding * 2);
                int maxHeight = Math.Max(MinPanelHeight, safeWindowHeight - PanelPadding * 2);
                int panelWidth = Math.Min(MaxPanelWidth, Math.Min(maxWidth, safeWindowWidth));
                int panelHeight = Math.Min(MaxPanelHeight, Math.Min(maxHeight, safeWindowHeight));
                SetDialogSize(panelWidth, panelHeight);
            }

            if (!UpdateDialogFrame(windowWidth, windowHeight)) {
                return;
            }

            PanelSize = DialogPanelBackground.Size;
            PanelPosition = DialogPanelPosition;
            LayoutBrowser();
            LayoutStatus();
            LayoutFooter();
        }

        /// <summary>
        /// Raises the open request for the currently selected scene file.
        /// </summary>
        protected override void OnCloseRequested() {
            Hide();
        }

        /// <summary>
        /// Handles the open button by validating the current selection and raising the open request when successful.
        /// </summary>
        void HandleOpenClicked() {
            if (SelectedEntry == null || SelectedEntry.IsDirectory) {
                StatusText.Text = "Select a scene file to open.";
                return;
            }

            if (!string.Equals(SelectedEntry.Extension, SceneAsset.FileExtension, StringComparison.OrdinalIgnoreCase)) {
                StatusText.Text = "Selected file is not a scene.";
                return;
            }

            if (string.IsNullOrWhiteSpace(SelectedEntry.FullPath)) {
                StatusText.Text = "Selected scene path is invalid.";
                return;
            }

            StatusText.Text = string.Empty;
            OpenRequested?.Invoke(SelectedEntry.FullPath);
        }

        /// <summary>
        /// Handles file activation from the browser by selecting the entry for opening.
        /// </summary>
        /// <param name="entry">Activated browser entry.</param>
        void HandleAssetActivated(AssetBrowserEntry entry) {
            if (entry == null || entry.IsDirectory) {
                return;
            }

            long now = Environment.TickCount64;
            bool isDoubleClick = SelectedEntry != null &&
                                 string.Equals(SelectedEntry.FullPath, entry.FullPath, StringComparison.OrdinalIgnoreCase) &&
                                 now - LastActivatedTicks <= RowDoubleClickMs;

            SelectedEntry = entry;
            StatusText.Text = string.Empty;
            LastActivatedTicks = now;

            if (isDoubleClick) {
                HandleOpenClicked();
            }
        }

        /// <summary>
        /// Clears the selected file entry when the browser clears its current selection.
        /// </summary>
        void HandleSelectionCleared() {
            SelectedEntry = null;
            LastActivatedTicks = 0;
            StatusText.Text = string.Empty;
        }

        /// <summary>
        /// Updates browser placement within the dialog panel.
        /// </summary>
        void LayoutBrowser() {
            int browserWidth = Math.Max(0, PanelSize.X - PanelPadding * 2);
            int browserTop = PanelPadding + HeaderHeight + SectionSpacing;
            int footerTop = PanelSize.Y - PanelPadding - FooterHeight;
            int statusTop = footerTop - SectionSpacing - StatusHeight;
            int browserBottom = statusTop - SectionSpacing;
            int browserHeight = Math.Max(120, browserBottom - browserTop);

            BrowserView.Entity.Position = new float3(PanelPadding, browserTop, 0.2f);
            BrowserView.UpdateLayout(browserWidth, browserHeight);
        }

        /// <summary>
        /// Updates the status text placement.
        /// </summary>
        void LayoutStatus() {
            int footerTop = PanelSize.Y - PanelPadding - FooterHeight;
            int statusTop = footerTop - SectionSpacing - StatusHeight;
            int contentWidth = Math.Max(0, PanelSize.X - PanelPadding * 2);

            StatusHost.Position = new float3(PanelPadding, statusTop, 0.2f);
            FontTightMetrics statusMetrics = Font.MeasureTight(StatusText.Text ?? string.Empty);
            StatusText.Size = new int2(contentWidth, (int)Math.Ceiling(Math.Max(statusMetrics.Height, Font.LineHeight)));
        }

        /// <summary>
        /// Updates footer button placement within the dialog panel.
        /// </summary>
        void LayoutFooter() {
            int footerTop = PanelSize.Y - PanelPadding - FooterHeight;
            int openButtonX = PanelSize.X - PanelPadding - OpenButtonSize.X;
            int cancelButtonX = openButtonX - 8 - CancelButtonSize.X;
            int buttonY = footerTop + Math.Max(0, (FooterHeight - OpenButtonSize.Y) / 2);

            CancelButtonHost.Position = new float3(cancelButtonX, buttonY, 0.2f);
            OpenButtonHost.Position = new float3(openButtonX, buttonY, 0.2f);
        }
    }
}
