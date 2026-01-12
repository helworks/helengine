namespace helengine.editor {
    /// <summary>
    /// Dockable panel intended to show editable properties for the current selection.
    /// </summary>
    public class PropertiesPanel : DockableEntity {
        /// <summary>
        /// Padding applied to the content area.
        /// </summary>
        const int ContentPadding = 8;
        /// <summary>
        /// Spacing between stacked text lines.
        /// </summary>
        const int LineSpacing = 6;

        /// <summary>
        /// Font used for property text.
        /// </summary>
        readonly FontAsset font;
        /// <summary>
        /// Render order for property text.
        /// </summary>
        readonly byte textOrder;
        /// <summary>
        /// Root entity hosting property text lines.
        /// </summary>
        readonly EditorEntity contentRoot;
        /// <summary>
        /// Hosts for each text line.
        /// </summary>
        readonly List<EditorEntity> lineHosts;
        /// <summary>
        /// Text components for each line.
        /// </summary>
        readonly List<TextComponent> lineTexts;
        /// <summary>
        /// Header text line.
        /// </summary>
        readonly TextComponent headerText;
        /// <summary>
        /// Asset path text line.
        /// </summary>
        readonly TextComponent pathText;
        /// <summary>
        /// Importer identifier text line.
        /// </summary>
        readonly TextComponent importerText;
        /// <summary>
        /// Source checksum text line.
        /// </summary>
        readonly TextComponent checksumText;
        /// <summary>
        /// Asset identifier text line.
        /// </summary>
        readonly TextComponent assetIdText;
        /// <summary>
        /// Status or error message line.
        /// </summary>
        readonly TextComponent statusText;
        /// <summary>
        /// View that renders asset import settings controls.
        /// </summary>
        readonly AssetImportSettingsView importSettingsView;
        /// <summary>
        /// Currently selected asset entry, if any.
        /// </summary>
        AssetBrowserEntry currentEntry;
        /// <summary>
        /// Tracks whether the panel finished initialization.
        /// </summary>
        bool isInitialized;

        /// <summary>
        /// Raised when the user applies a pending import setting change.
        /// </summary>
        public event Action<AssetBrowserEntry, string> ImportSettingsApplyRequested;

        /// <summary>
        /// Initializes a new properties panel with the provided font.
        /// </summary>
        /// <param name="font">Font used for the title bar.</param>
        public PropertiesPanel(FontAsset font) : base(font) {
            if (font == null) {
                throw new ArgumentNullException(nameof(font));
            }

            this.font = font;
            Title = "Property Manager";
            MinSize = new int2(220, 160);

            textOrder = Core.Instance.ObjectManager.GetRenderOrderForLayer2D(2);

            contentRoot = new EditorEntity();
            contentRoot.LayerMask = LayerMask;
            contentRoot.Position = new float3(0, TitleBarHeight, 0.05f);
            AddChild(contentRoot);

            lineHosts = new List<EditorEntity>(6);
            lineTexts = new List<TextComponent>(6);

            headerText = AddLine();
            pathText = AddLine();
            importerText = AddLine();
            checksumText = AddLine();
            assetIdText = AddLine();
            statusText = AddLine();

            importSettingsView = new AssetImportSettingsView(font, LayerMask);
            importSettingsView.ApplyRequested += HandleImportSettingsApplyRequested;
            contentRoot.AddChild(importSettingsView.Root);

            ShowEmpty();
            isInitialized = true;
        }

        /// <summary>
        /// Shows import settings for the specified asset entry.
        /// </summary>
        /// <param name="entry">Selected asset entry.</param>
        /// <param name="settings">Import settings to display.</param>
        /// <param name="importerIds">Registered importer identifiers.</param>
        public void ShowImportSettings(AssetBrowserEntry entry, AssetImportSettings settings, IReadOnlyList<string> importerIds) {
            if (entry == null) {
                throw new ArgumentNullException(nameof(entry));
            }

            if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            }

            if (importerIds == null) {
                throw new ArgumentNullException(nameof(importerIds));
            }

            currentEntry = entry;
            headerText.Text = string.Empty;
            pathText.Text = string.Empty;
            importerText.Text = string.Empty;
            checksumText.Text = string.Empty;
            assetIdText.Text = string.Empty;
            statusText.Text = string.Empty;

            importSettingsView.Show(importerIds, settings.ImporterId);
            LayoutLines();
        }

        /// <summary>
        /// Shows an error message for an asset when import settings cannot be resolved.
        /// </summary>
        /// <param name="entry">Selected asset entry.</param>
        /// <param name="message">Error message to display.</param>
        public void ShowImportError(AssetBrowserEntry entry, string message) {
            if (entry == null) {
                throw new ArgumentNullException(nameof(entry));
            }

            if (string.IsNullOrWhiteSpace(message)) {
                throw new ArgumentException("Message must be provided.", nameof(message));
            }

            headerText.Text = "Properties";
            pathText.Text = $"Asset: {BuildAssetLabel(entry)}";
            importerText.Text = string.Empty;
            checksumText.Text = string.Empty;
            assetIdText.Text = string.Empty;
            statusText.Text = $"Status: {message}";

            currentEntry = null;
            importSettingsView.Hide();
            LayoutLines();
        }

        /// <summary>
        /// Resets the panel to its empty selection state.
        /// </summary>
        public void ShowEmpty() {
            headerText.Text = string.Empty;
            pathText.Text = string.Empty;
            importerText.Text = string.Empty;
            checksumText.Text = string.Empty;
            assetIdText.Text = string.Empty;
            statusText.Text = string.Empty;

            currentEntry = null;
            importSettingsView.Hide();
            LayoutLines();
        }

        /// <summary>
        /// Handles layout updates when the dockable size changes.
        /// </summary>
        protected override void OnSizeChanged() {
            base.OnSizeChanged();
            if (!isInitialized) {
                return;
            }

            LayoutLines();
        }

        /// <summary>
        /// Creates a new line host with a text component.
        /// </summary>
        /// <returns>The created text component.</returns>
        TextComponent AddLine() {
            var host = new EditorEntity();
            host.LayerMask = LayerMask;
            host.Position = float3.Zero;
            contentRoot.AddChild(host);

            var text = new TextComponent();
            text.Font = font;
            text.Text = string.Empty;
            text.Color = ThemeManager.Colors.InputForegroundPrimary;
            text.Size = new int2(1, 1);
            text.RenderOrder2D = textOrder;
            host.AddComponent(text);

            lineHosts.Add(host);
            lineTexts.Add(text);
            return text;
        }

        /// <summary>
        /// Updates line positions and sizes based on the current text content.
        /// </summary>
        void LayoutLines() {
            int rowWidth = Math.Max(Size.X, MinSize.X);
            int maxWidth = Math.Max(0, rowWidth - ContentPadding * 2);
            float lineHeight = (float)Math.Max((double)font.LineHeight, 1.0);

            float offsetY = 0f;
            for (int i = 0; i < lineTexts.Count; i++) {
                TextComponent text = lineTexts[i];
                EditorEntity host = lineHosts[i];
                if (string.IsNullOrWhiteSpace(text.Text)) {
                    host.Enabled = false;
                    continue;
                }

                host.Enabled = true;
                host.Position = new float3(ContentPadding, (float)Math.Round(offsetY), 0.2f);
                text.Size = new int2(maxWidth, (int)Math.Ceiling(lineHeight));
                offsetY += lineHeight + LineSpacing;
            }

            if (importSettingsView.IsVisible) {
                int viewTop = (int)Math.Round(offsetY);
                importSettingsView.UpdateLayout(ContentPadding, viewTop, maxWidth);
            }
        }

        /// <summary>
        /// Forwards apply requests from the import settings view.
        /// </summary>
        /// <param name="importerId">Pending importer identifier to apply.</param>
        void HandleImportSettingsApplyRequested(string importerId) {
            if (string.IsNullOrWhiteSpace(importerId)) {
                throw new ArgumentException("Importer id must be provided.", nameof(importerId));
            }

            if (currentEntry == null) {
                throw new InvalidOperationException("No asset is currently selected.");
            }

            if (ImportSettingsApplyRequested != null) {
                ImportSettingsApplyRequested(currentEntry, importerId);
            }
        }

        /// <summary>
        /// Builds a display label for an asset entry.
        /// </summary>
        /// <param name="entry">Asset entry to describe.</param>
        /// <returns>Display label for the asset path.</returns>
        string BuildAssetLabel(AssetBrowserEntry entry) {
            if (entry == null) {
                throw new ArgumentNullException(nameof(entry));
            }

            if (!string.IsNullOrWhiteSpace(entry.RelativePath)) {
                return entry.RelativePath;
            }

            return entry.Name;
        }
    }
}
