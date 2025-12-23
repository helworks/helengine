namespace helengine.editor {
    /// <summary>
    /// Displays a dockable browser that mirrors the project assets folder.
    /// </summary>
    public class AssetBrowserPanel : DockableEntity {
        /// <summary>
        /// Height of each row in the asset list.
        /// </summary>
        public const int RowHeight = 24;

        /// <summary>
        /// Height of the toolbar area above the list.
        /// </summary>
        public const int ToolbarHeight = 28;

        /// <summary>
        /// Size of the square icon in each row.
        /// </summary>
        const int IconSize = 18;

        /// <summary>
        /// Padding between the left edge and the row icon.
        /// </summary>
        const int IconPadding = 8;

        /// <summary>
        /// Spacing between the icon and the row label.
        /// </summary>
        const int LabelPadding = 8;

        /// <summary>
        /// Left and right padding for toolbar elements.
        /// </summary>
        const int ToolbarPadding = 8;

        /// <summary>
        /// Spacing between toolbar items.
        /// </summary>
        const int ToolbarSpacing = 8;

        /// <summary>
        /// Fixed size for the toolbar up button.
        /// </summary>
        static readonly int2 UpButtonSize = new int2(46, 20);

        /// <summary>
        /// Font used to render toolbar and row labels.
        /// </summary>
        FontAsset font;

        /// <summary>
        /// Asset manager used to supply browsing data and classifications.
        /// </summary>
        EditorAssetManager assetManager;

        /// <summary>
        /// Root entity hosting toolbar and row content.
        /// </summary>
        EditorEntity contentRoot;

        /// <summary>
        /// Toolbar host entity.
        /// </summary>
        EditorEntity toolbarRoot;

        /// <summary>
        /// Toolbar background sprite.
        /// </summary>
        SpriteComponent toolbarBackground;

        /// <summary>
        /// Entity hosting the current path text.
        /// </summary>
        EditorEntity pathTextHost;

        /// <summary>
        /// Text component that shows the current path.
        /// </summary>
        TextComponent pathText;

        /// <summary>
        /// Entity hosting the up navigation button.
        /// </summary>
        EditorEntity upButtonHost;

        /// <summary>
        /// Button component used to navigate up the folder tree.
        /// </summary>
        ButtonComponent upButton;

        /// <summary>
        /// Current entries displayed in the list.
        /// </summary>
        List<AssetBrowserEntry> entries;

        /// <summary>
        /// Pool of row visuals used to display entries.
        /// </summary>
        List<AssetBrowserRow> rows;

        /// <summary>
        /// Gets or sets a value indicating whether the panel has completed initialization.
        /// </summary>
        bool isInitialized;

        /// <summary>
        /// Initializes a new asset browser panel for the provided project path.
        /// </summary>
        /// <param name="font">Font used for labels.</param>
        /// <param name="projectPath">Path to the project root.</param>
        public AssetBrowserPanel(FontAsset font, string projectPath) : base(font) {
            this.font = font;
            Title = "Assets";
            MinSize = new int2(260, 180);

            assetManager = new EditorAssetManager(projectPath);

            contentRoot = new EditorEntity();
            contentRoot.LayerMask = LayerMask;
            contentRoot.Position = new float3(0, TitleBarHeight, 0.05f);
            AddChild(contentRoot);

            BuildToolbar();

            entries = new List<AssetBrowserEntry>(64);
            rows = new List<AssetBrowserRow>(32);

            isInitialized = true;
            RefreshEntries();
        }

        /// <summary>
        /// Refreshes the asset list from disk and updates layout.
        /// </summary>
        public void RefreshEntries() {
            assetManager.LoadEntries(entries);
            UpdatePathText();
            LayoutToolbar();
            LayoutRows();
        }

        /// <summary>
        /// Handles layout updates when the dockable size changes.
        /// </summary>
        protected override void OnSizeChanged() {
            base.OnSizeChanged();
            if (!isInitialized) {
                return;
            }

            LayoutToolbar();
            LayoutRows();
        }

        /// <summary>
        /// Builds the toolbar UI elements.
        /// </summary>
        void BuildToolbar() {
            toolbarRoot = new EditorEntity();
            toolbarRoot.LayerMask = LayerMask;
            toolbarRoot.Position = float3.Zero;
            contentRoot.AddChild(toolbarRoot);

            toolbarBackground = new SpriteComponent();
            toolbarBackground.Texture = TextureUtils.PixelTexture;
            toolbarBackground.Color = ThemeManager.Colors.SurfacePrimary;
            toolbarBackground.RenderOrder2D = 1;
            toolbarRoot.AddComponent(toolbarBackground);

            upButtonHost = new EditorEntity();
            upButtonHost.LayerMask = LayerMask;
            upButtonHost.Position = float3.Zero;
            toolbarRoot.AddChild(upButtonHost);

            upButton = new ButtonComponent("Up", UpButtonSize, font, NavigateUp);
            upButtonHost.AddComponent(upButton);

            pathTextHost = new EditorEntity();
            pathTextHost.LayerMask = LayerMask;
            pathTextHost.Position = float3.Zero;
            toolbarRoot.AddChild(pathTextHost);

            float lineHeight = MathF.Max(font.LineHeight, 1f);
            pathText = new TextComponent();
            pathText.Font = font;
            pathText.Text = string.Empty;
            pathText.Color = ThemeManager.Colors.InputForegroundPrimary;
            pathText.Size = new int2(1, (int)MathF.Ceiling(lineHeight));
            pathText.RenderOrder2D = 3;
            pathTextHost.AddComponent(pathText);
        }

        /// <summary>
        /// Updates toolbar positions and sizing based on the current panel size.
        /// </summary>
        void LayoutToolbar() {
            int rowWidth = Math.Max(Size.X, MinSize.X);
            toolbarBackground.Size = new int2(rowWidth, ToolbarHeight);

            float buttonY = MathF.Round((ToolbarHeight - UpButtonSize.Y) * 0.5f);
            upButtonHost.Position = new float3(ToolbarPadding, buttonY, 0.2f);

            float pathX = ToolbarPadding + UpButtonSize.X + ToolbarSpacing;
            var pathMetrics = font.MeasureTight(pathText.Text);
            float pathY = GetTextTopOffset(ToolbarHeight, pathMetrics);
            pathTextHost.Position = new float3(pathX, pathY, 0.2f);

            int pathWidth = Math.Max(0, rowWidth - (int)pathX - ToolbarPadding);
            pathText.Size = new int2(pathWidth, (int)MathF.Ceiling(pathMetrics.Height));
        }

        /// <summary>
        /// Updates the visible path label based on the current folder.
        /// </summary>
        void UpdatePathText() {
            pathText.Text = assetManager.GetDisplayPath();
        }

        /// <summary>
        /// Navigates to a child folder by relative path.
        /// </summary>
        /// <param name="relativePath">Relative path to navigate into.</param>
        void NavigateTo(string relativePath) {
            if (assetManager.TryNavigateTo(relativePath)) {
                RefreshEntries();
            }
        }

        /// <summary>
        /// Navigates to the parent folder if available.
        /// </summary>
        void NavigateUp() {
            if (assetManager.TryNavigateUp()) {
                RefreshEntries();
            }
        }

        /// <summary>
        /// Ensures the row pool can display the provided number of entries.
        /// </summary>
        /// <param name="count">Number of rows required.</param>
        void EnsureRowCount(int count) {
            bool created = false;
            for (int i = rows.Count; i < count; i++) {
                rows.Add(CreateRow());
                created = true;
            }
            if (created) {
                RefreshRenderOrderBias();
            }
        }

        /// <summary>
        /// Creates a new row element with background, icon, label, and interaction.
        /// </summary>
        /// <returns>New row container.</returns>
        AssetBrowserRow CreateRow() {
            var rowEntity = new EditorEntity();
            rowEntity.LayerMask = LayerMask;
            rowEntity.Position = float3.Zero;

            var background = new SpriteComponent();
            background.Texture = TextureUtils.PixelTexture;
            background.Color = ThemeManager.Colors.SurfacePrimary;
            background.RenderOrder2D = 2;
            rowEntity.AddComponent(background);

            var iconHost = new EditorEntity();
            iconHost.LayerMask = LayerMask;
            iconHost.Position = new float3(IconPadding, 0, 0.2f);
            rowEntity.AddChild(iconHost);

            var iconBackground = new SpriteComponent();
            iconBackground.Texture = TextureUtils.PixelTexture;
            iconBackground.Color = ThemeManager.Colors.AccentSecondary;
            iconBackground.RenderOrder2D = 3;
            iconHost.AddComponent(iconBackground);

            var iconTextHost = new EditorEntity();
            iconTextHost.LayerMask = LayerMask;
            iconTextHost.Position = new float3(0, 0, 0.1f);
            iconHost.AddChild(iconTextHost);

            var iconText = new TextComponent();
            iconText.Font = font;
            iconText.Text = string.Empty;
            iconText.Color = ThemeManager.Colors.TextOnAccent;
            iconText.Size = new int2(1, 1);
            iconText.RenderOrder2D = 4;
            iconTextHost.AddComponent(iconText);

            var labelHost = new EditorEntity();
            labelHost.LayerMask = LayerMask;
            labelHost.Position = new float3(IconPadding + IconSize + LabelPadding, 0, 0.2f);
            rowEntity.AddChild(labelHost);

            var label = new TextComponent();
            label.Font = font;
            label.Text = string.Empty;
            label.Color = ThemeManager.Colors.InputForegroundPrimary;
            label.Size = new int2(100, RowHeight);
            label.RenderOrder2D = 4;
            labelHost.AddComponent(label);

            var interactable = new InteractableComponent();
            interactable.Size = new int2(Size.X, RowHeight);
            rowEntity.AddComponent(interactable);

            var row = new AssetBrowserRow(rowEntity, background, iconBackground, iconText, label, interactable);
            interactable.CursorEvent += (pos, delta, state) => HandleRowCursor(row, state);
            contentRoot.AddChild(rowEntity);
            return row;
        }

        /// <summary>
        /// Lays out rows based on the current entry list and panel size.
        /// </summary>
        void LayoutRows() {
            EnsureRowCount(entries.Count);

            int rowWidth = Math.Max(Size.X, MinSize.X);

            for (int i = 0; i < rows.Count; i++) {
                var row = rows[i];
                if (i >= entries.Count) {
                    row.Entity.Enabled = false;
                    row.Entry = null;
                    row.IsHovering = false;
                    row.IsPressed = false;
                    UpdateRowBackground(row, ThemeManager.Colors.SurfacePrimary);
                    continue;
                }

                var entry = entries[i];
                row.Entity.Enabled = true;
                row.Entry = entry;
                row.Entity.Position = new float3(0, ToolbarHeight + i * RowHeight, 0.1f);

                bool alternate = i % 2 == 1;
                byte4 baseColor = alternate ? ThemeManager.Colors.SurfaceInput : ThemeManager.Colors.SurfacePrimary;
                row.BaseColor = baseColor;
                UpdateRowBackground(row, baseColor);

                row.Background.Size = new int2(rowWidth, RowHeight);
                row.Interactable.Size = new int2(rowWidth, RowHeight);

                float iconY = MathF.Round((RowHeight - IconSize) * 0.5f);
                if (row.IconBackground.Parent != null) {
                    row.IconBackground.Parent.Position = new float3(IconPadding, iconY, 0.2f);
                }
                row.IconBackground.Size = new int2(IconSize, IconSize);

                GetIconForEntry(entry, out var iconColor, out var iconLabel, out var iconTextColor);
                row.IconBackground.Color = iconColor;
                row.IconText.Text = iconLabel;
                row.IconText.Color = iconTextColor;

                var iconMetrics = font.MeasureTight(iconLabel);
                float iconTextX = MathF.Round((IconSize - iconMetrics.Width) * 0.5f);
                float iconTextY = GetTextTopOffset(IconSize, iconMetrics);
                if (row.IconText.Parent != null) {
                    row.IconText.Parent.Position = new float3(iconTextX, iconTextY, 0.1f);
                }
                row.IconText.Size = new int2((int)MathF.Ceiling(iconMetrics.Width), (int)MathF.Ceiling(iconMetrics.Height));

                float labelX = IconPadding + IconSize + LabelPadding;
                string labelText = entry.IsDirectory ? $"{entry.Name}/" : entry.Name;
                var labelMetrics = font.MeasureTight(labelText);
                float labelY = GetTextTopOffset(RowHeight, labelMetrics);
                if (row.Label.Parent != null) {
                    row.Label.Parent.Position = new float3(labelX, labelY, 0.2f);
                }
                row.Label.Text = labelText;
                row.Label.Color = ThemeManager.Colors.InputForegroundPrimary;
                row.Label.Size = new int2(Math.Max(0, rowWidth - (int)labelX - LabelPadding), (int)MathF.Ceiling(labelMetrics.Height));
            }
        }

        /// <summary>
        /// Computes the vertical offset needed to center text using tight metrics.
        /// </summary>
        /// <param name="containerHeight">Height of the container in pixels.</param>
        /// <param name="metrics">Tight font metrics for the text.</param>
        /// <returns>Top offset to position the line.</returns>
        float GetTextTopOffset(float containerHeight, FontTightMetrics metrics) {
            return MathF.Round((containerHeight - metrics.Height) * 0.5f - metrics.MinTop);
        }

        /// <summary>
        /// Handles pointer interactions on a row and triggers navigation.
        /// </summary>
        /// <param name="row">Row receiving the interaction.</param>
        /// <param name="state">Pointer interaction state.</param>
        void HandleRowCursor(AssetBrowserRow row, PointerInteraction state) {
            switch (state) {
                case PointerInteraction.Hover:
                    row.IsHovering = true;
                    break;
                case PointerInteraction.Press:
                    row.IsPressed = true;
                    break;
                case PointerInteraction.Release:
                    bool shouldActivate = row.IsPressed && row.IsHovering;
                    row.IsPressed = false;
                    if (shouldActivate && row.Entry != null && row.Entry.IsDirectory) {
                        NavigateTo(row.Entry.RelativePath);
                    }
                    break;
                case PointerInteraction.Leave:
                    row.IsHovering = false;
                    row.IsPressed = false;
                    break;
                default:
                    break;
            }

            UpdateRowBackground(row, row.BaseColor);
        }

        /// <summary>
        /// Updates a row background color based on hover and press state.
        /// </summary>
        /// <param name="row">Row to update.</param>
        /// <param name="baseColor">Base color for the row.</param>
        void UpdateRowBackground(AssetBrowserRow row, byte4 baseColor) {
            if (row.IsPressed) {
                row.Background.Color = ThemeManager.Colors.AccentPrimary;
                return;
            }

            if (row.IsHovering) {
                row.Background.Color = ThemeManager.Colors.AccentSecondary;
                return;
            }

            row.Background.Color = baseColor;
        }

        /// <summary>
        /// Determines the icon style for a given entry.
        /// </summary>
        /// <param name="entry">Entry to classify.</param>
        /// <param name="color">Output icon background color.</param>
        /// <param name="label">Output icon label.</param>
        /// <param name="textColor">Output icon text color.</param>
        void GetIconForEntry(AssetBrowserEntry entry, out byte4 color, out string label, out byte4 textColor) {
            switch (assetManager.GetEntryKind(entry)) {
                case AssetEntryKind.Directory:
                    color = ThemeManager.Colors.AccentSecondary;
                    label = "DIR";
                    textColor = ThemeManager.Colors.TextOnAccent;
                    return;
                case AssetEntryKind.Image:
                    color = ThemeManager.Colors.StateSuccess;
                    label = "IMG";
                    textColor = ThemeManager.Colors.TextOnAccent;
                    return;
                case AssetEntryKind.Model:
                    color = ThemeManager.Colors.StateWarning;
                    label = "3D";
                    textColor = ThemeManager.Colors.TextOnAccent;
                    return;
                case AssetEntryKind.Audio:
                    color = ThemeManager.Colors.AccentPrimary;
                    label = "SND";
                    textColor = ThemeManager.Colors.TextOnAccent;
                    return;
                case AssetEntryKind.Script:
                    color = ThemeManager.Colors.AccentTertiary;
                    label = "SCR";
                    textColor = ThemeManager.Colors.TextOnAccent;
                    return;
                case AssetEntryKind.Config:
                    color = ThemeManager.Colors.AccentQuaternary;
                    label = "CFG";
                    textColor = ThemeManager.Colors.TextOnAccent;
                    return;
                case AssetEntryKind.Unknown:
                    color = ThemeManager.Colors.AccentSecondary;
                    label = "UNK";
                    textColor = ThemeManager.Colors.TextOnAccent;
                    return;
                default:
                    color = ThemeManager.Colors.SurfaceInput;
                    label = "FIL";
                    textColor = ThemeManager.Colors.InputForegroundPrimary;
                    return;
            }
        }
    }
}
