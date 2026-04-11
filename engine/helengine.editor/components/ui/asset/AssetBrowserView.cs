namespace helengine.editor {
    /// <summary>
    /// Shared asset browser view that renders the toolbar and list for asset navigation.
    /// </summary>
    public class AssetBrowserView {
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
        static readonly int2 UpButtonSize = new int2(64, 22);

        /// <summary>
        /// Font used for toolbar and row labels.
        /// </summary>
        readonly FontAsset Font;
        /// <summary>
        /// Asset-browser data source that merges filesystem and generated entries.
        /// </summary>
        readonly AssetBrowserDataSource DataSource;
        /// <summary>
        /// Root entity hosting toolbar and list content.
        /// </summary>
        readonly EditorEntity Root;
        /// <summary>
        /// Toolbar host entity.
        /// </summary>
        readonly EditorEntity ToolbarRoot;
        /// <summary>
        /// Toolbar background sprite.
        /// </summary>
        readonly SpriteComponent ToolbarBackground;
        /// <summary>
        /// Entity hosting the current path text.
        /// </summary>
        readonly EditorEntity PathTextHost;
        /// <summary>
        /// Text component that shows the current path.
        /// </summary>
        readonly TextComponent PathText;
        /// <summary>
        /// Entity hosting the up navigation button.
        /// </summary>
        readonly EditorEntity UpButtonHost;
        /// <summary>
        /// Button component used to navigate up the folder tree.
        /// </summary>
        readonly ButtonComponent UpButton;
        /// <summary>
        /// Root entity hosting the list rows.
        /// </summary>
        readonly EditorEntity ListRoot;
        /// <summary>
        /// Host entity for list background hit testing.
        /// </summary>
        readonly EditorEntity ListHitHost;
        /// <summary>
        /// Interactable used to clear selection when clicking empty space.
        /// </summary>
        readonly InteractableComponent ListHitInteractable;
        /// <summary>
        /// Current entries displayed in the list.
        /// </summary>
        readonly List<AssetBrowserEntry> Entries;
        /// <summary>
        /// Pool of row visuals used to display entries.
        /// </summary>
        readonly List<AssetBrowserRow> Rows;
        /// <summary>
        /// Render order used for toolbar backgrounds.
        /// </summary>
        readonly byte ToolbarOrder;
        /// <summary>
        /// Render order used for row backgrounds.
        /// </summary>
        readonly byte RowBackgroundOrder;
        /// <summary>
        /// Render order used for icon backgrounds.
        /// </summary>
        readonly byte IconBackgroundOrder;
        /// <summary>
        /// Render order used for text labels.
        /// </summary>
        readonly byte TextOrder;
        /// <summary>
        /// Cached size of the view.
        /// </summary>
        int2 Size;
        /// <summary>
        /// Gets or sets a value indicating whether the view has completed initialization.
        /// </summary>
        bool IsInitialized;
        /// <summary>
        /// Optional extension filter for assets.
        /// </summary>
        string ExtensionFilter;

        /// <summary>
        /// Raised when a file entry is activated by the user.
        /// </summary>
        public event Action<AssetBrowserEntry> AssetActivated;
        /// <summary>
        /// Raised when the list background is clicked to clear selection.
        /// </summary>
        public event Action SelectionCleared;

        /// <summary>
        /// Initializes a new asset browser view.
        /// </summary>
        /// <param name="font">Font used for labels.</param>
        /// <param name="projectPath">Path to the project root.</param>
        /// <param name="layerMask">Layer mask for all entities in the view.</param>
        /// <param name="toolbarOrder">Render order for toolbar backgrounds.</param>
        /// <param name="rowBackgroundOrder">Render order for row backgrounds.</param>
        /// <param name="iconBackgroundOrder">Render order for icon backgrounds.</param>
        /// <param name="textOrder">Render order for text labels.</param>
        /// <param name="includeGeneratedEntries">True to include generated-provider roots and entries.</param>
        public AssetBrowserView(
            FontAsset font,
            string projectPath,
            ushort layerMask,
            byte toolbarOrder,
            byte rowBackgroundOrder,
            byte iconBackgroundOrder,
            byte textOrder,
            bool includeGeneratedEntries = true) {
            if (font == null) {
                throw new ArgumentNullException(nameof(font));
            }
            if (string.IsNullOrWhiteSpace(projectPath)) {
                throw new ArgumentException("Project path must be provided.", nameof(projectPath));
            }

            Font = font;
            DataSource = new AssetBrowserDataSource(projectPath, includeGeneratedEntries);
            ToolbarOrder = toolbarOrder;
            RowBackgroundOrder = rowBackgroundOrder;
            IconBackgroundOrder = iconBackgroundOrder;
            TextOrder = textOrder;

            Root = new EditorEntity {
                LayerMask = layerMask,
                Position = float3.Zero
            };

            ToolbarRoot = new EditorEntity {
                LayerMask = layerMask,
                Position = float3.Zero
            };
            Root.AddChild(ToolbarRoot);

            ToolbarBackground = new SpriteComponent {
                Texture = TextureUtils.PixelTexture,
                Color = ThemeManager.Colors.SurfacePrimary,
                RenderOrder2D = ToolbarOrder
            };
            ToolbarRoot.AddComponent(ToolbarBackground);

            UpButtonHost = new EditorEntity {
                LayerMask = layerMask,
                Position = float3.Zero
            };
            ToolbarRoot.AddChild(UpButtonHost);

            UpButton = new ButtonComponent("Up", UpButtonSize, font, NavigateUp, 0f);
            UpButtonHost.AddComponent(UpButton);

            PathTextHost = new EditorEntity {
                LayerMask = layerMask,
                Position = float3.Zero
            };
            ToolbarRoot.AddChild(PathTextHost);

            float lineHeight = MathF.Max(font.LineHeight, 1f);
            PathText = new TextComponent {
                Font = font,
                Text = string.Empty,
                Color = ThemeManager.Colors.InputForegroundPrimary,
                Size = new int2(1, (int)MathF.Ceiling(lineHeight)),
                RenderOrder2D = TextOrder
            };
            PathTextHost.AddComponent(PathText);

            ListHitHost = new EditorEntity {
                LayerMask = layerMask,
                Position = float3.Zero
            };
            Root.AddChild(ListHitHost);

            ListHitInteractable = new InteractableComponent {
                Size = new int2(0, 0)
            };
            ListHitInteractable.CursorEvent += HandleListHitCursor;
            ListHitHost.AddComponent(ListHitInteractable);

            ListRoot = new EditorEntity {
                LayerMask = layerMask,
                Position = float3.Zero
            };
            Root.AddChild(ListRoot);

            Entries = new List<AssetBrowserEntry>(64);
            Rows = new List<AssetBrowserRow>(32);
            Size = new int2(1, 1);

            IsInitialized = true;
            RefreshEntries();
        }

        /// <summary>
        /// Gets the root entity for attaching the view to a parent.
        /// </summary>
        public EditorEntity Entity => Root;

        /// <summary>
        /// Gets the absolute path of the current folder displayed by the view.
        /// </summary>
        public string CurrentDirectoryPath => DataSource.CurrentDirectoryPath;

        /// <summary>
        /// Gets a value indicating whether the current directory accepts filesystem creation commands.
        /// </summary>
        public bool CanCreateFileSystemEntries => DataSource.CanCreateFileSystemEntries;

        /// <summary>
        /// Overrides the toolbar button render orders for modal or overlay contexts.
        /// </summary>
        /// <param name="backgroundOrder">Render order used for the button background.</param>
        /// <param name="textOrder">Render order used for the button label.</param>
        public void SetToolbarButtonRenderOrders(byte backgroundOrder, byte textOrder) {
            UpButton.SetRenderOrders(backgroundOrder, textOrder);
        }

        /// <summary>
        /// Sets an extension filter without refreshing entries.
        /// </summary>
        /// <param name="extensionFilter">Extension filter to apply.</param>
        public void SetExtensionFilter(string extensionFilter) {
            ExtensionFilter = NormalizeExtensionFilter(extensionFilter);
        }

        /// <summary>
        /// Clears the extension filter without refreshing entries.
        /// </summary>
        public void ClearExtensionFilter() {
            ExtensionFilter = string.Empty;
        }

        /// <summary>
        /// Updates layout to fit the provided size.
        /// </summary>
        /// <param name="width">View width in pixels.</param>
        /// <param name="height">View height in pixels.</param>
        public void UpdateLayout(int width, int height) {
            if (!IsInitialized) {
                return;
            }

            int safeWidth = Math.Max(1, width);
            int safeHeight = Math.Max(1, height);
            Size = new int2(safeWidth, safeHeight);
            LayoutToolbar();
            LayoutRows();
        }

        /// <summary>
        /// Refreshes the asset list from disk and updates layout.
        /// </summary>
        public void RefreshEntries() {
            DataSource.LoadEntries(Entries);
            ApplyExtensionFilter();
            UpdatePathText();
            LayoutToolbar();
            LayoutRows();
        }

        /// <summary>
        /// Navigates to a specific relative path and refreshes the visible entries when successful.
        /// </summary>
        /// <param name="relativePath">Relative path to navigate into.</param>
        /// <returns>True when the target directory exists.</returns>
        public bool TryNavigateTo(string relativePath) {
            if (!DataSource.TryNavigateTo(relativePath)) {
                return false;
            }

            RefreshEntries();
            return true;
        }

        /// <summary>
        /// Updates the visible path label based on the current folder.
        /// </summary>
        void UpdatePathText() {
            PathText.Text = DataSource.GetDisplayPath();
        }

        /// <summary>
        /// Navigates to a child folder by relative path.
        /// </summary>
        /// <param name="relativePath">Relative path to navigate into.</param>
        void NavigateTo(string relativePath) {
            if (DataSource.TryNavigateTo(relativePath)) {
                RefreshEntries();
            }
        }

        /// <summary>
        /// Navigates to the parent folder if available.
        /// </summary>
        void NavigateUp() {
            if (DataSource.TryNavigateUp()) {
                RefreshEntries();
            }
        }

        /// <summary>
        /// Ensures the row pool can display the provided number of entries.
        /// </summary>
        /// <param name="count">Number of rows required.</param>
        void EnsureRowCount(int count) {
            for (int i = Rows.Count; i < count; i++) {
                Rows.Add(CreateRow());
            }
        }

        /// <summary>
        /// Creates a new row element with background, icon, label, and interaction.
        /// </summary>
        /// <returns>New row container.</returns>
        AssetBrowserRow CreateRow() {
            var rowEntity = new EditorEntity {
                LayerMask = Root.LayerMask,
                Position = float3.Zero
            };

            var background = new SpriteComponent {
                Texture = TextureUtils.PixelTexture,
                Color = ThemeManager.Colors.SurfacePrimary,
                RenderOrder2D = RowBackgroundOrder
            };
            rowEntity.AddComponent(background);

            var iconHost = new EditorEntity {
                LayerMask = Root.LayerMask,
                Position = new float3(IconPadding, 0, 0.2f)
            };
            rowEntity.AddChild(iconHost);

            var iconBackground = new SpriteComponent {
                Texture = TextureUtils.PixelTexture,
                Color = ThemeManager.Colors.AccentSecondary,
                RenderOrder2D = IconBackgroundOrder
            };
            iconHost.AddComponent(iconBackground);

            var iconTextHost = new EditorEntity {
                LayerMask = Root.LayerMask,
                Position = new float3(0, 0, 0.1f)
            };
            iconHost.AddChild(iconTextHost);

            var iconText = new TextComponent {
                Font = Font,
                Text = string.Empty,
                Color = ThemeManager.Colors.TextOnAccent,
                Size = new int2(1, 1),
                RenderOrder2D = TextOrder
            };
            iconTextHost.AddComponent(iconText);

            var labelHost = new EditorEntity {
                LayerMask = Root.LayerMask,
                Position = new float3(IconPadding + IconSize + LabelPadding, 0, 0.2f)
            };
            rowEntity.AddChild(labelHost);

            var label = new TextComponent {
                Font = Font,
                Text = string.Empty,
                Color = ThemeManager.Colors.InputForegroundPrimary,
                Size = new int2(100, RowHeight),
                RenderOrder2D = TextOrder
            };
            labelHost.AddComponent(label);

            var interactable = new InteractableComponent {
                Size = new int2(Size.X, RowHeight)
            };
            rowEntity.AddComponent(interactable);

            var row = new AssetBrowserRow(rowEntity, background, iconBackground, iconText, label, interactable);
            interactable.CursorEvent += (pos, delta, state) => HandleRowCursor(row, state);
            ListRoot.AddChild(rowEntity);
            return row;
        }

        /// <summary>
        /// Updates toolbar positions and sizing based on the current view size.
        /// </summary>
        void LayoutToolbar() {
            int rowWidth = Math.Max(1, Size.X);
            ToolbarBackground.Size = new int2(rowWidth, ToolbarHeight);

            float buttonY = MathF.Round((ToolbarHeight - UpButtonSize.Y) * 0.5f);
            UpButtonHost.Position = new float3(ToolbarPadding, buttonY, 0.2f);

            float pathX = ToolbarPadding + UpButtonSize.X + ToolbarSpacing;
            var pathMetrics = Font.MeasureTight(PathText.Text);
            float pathY = GetTextTopOffset(ToolbarHeight, pathMetrics);
            PathTextHost.Position = new float3(pathX, pathY, 0.2f);

            int pathWidth = Math.Max(0, rowWidth - (int)pathX - ToolbarPadding);
            PathText.Size = new int2(pathWidth, (int)MathF.Ceiling(pathMetrics.Height));
        }

        /// <summary>
        /// Lays out rows based on the current entry list and view size.
        /// </summary>
        void LayoutRows() {
            EnsureRowCount(Entries.Count);

            int rowWidth = Math.Max(1, Size.X);

            for (int i = 0; i < Rows.Count; i++) {
                var row = Rows[i];
                if (i >= Entries.Count) {
                    row.Entity.Enabled = false;
                    row.Entry = null;
                    row.IsHovering = false;
                    row.IsPressed = false;
                    UpdateRowBackground(row, ThemeManager.Colors.SurfacePrimary);
                    continue;
                }

                var entry = Entries[i];
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

                var iconMetrics = Font.MeasureTight(iconLabel);
                float iconTextX = MathF.Round((IconSize - iconMetrics.Width) * 0.5f);
                float iconTextY = GetTextTopOffset(IconSize, iconMetrics);
                if (row.IconText.Parent != null) {
                    row.IconText.Parent.Position = new float3(iconTextX, iconTextY, 0.1f);
                }
                row.IconText.Size = new int2((int)MathF.Ceiling(iconMetrics.Width), (int)MathF.Ceiling(iconMetrics.Height));

                float labelX = IconPadding + IconSize + LabelPadding;
                string labelText = entry.IsDirectory ? $"{entry.Name}/" : entry.Name;
                var labelMetrics = Font.MeasureTight(labelText);
                float labelY = GetTextTopOffset(RowHeight, labelMetrics);
                if (row.Label.Parent != null) {
                    row.Label.Parent.Position = new float3(labelX, labelY, 0.2f);
                }
                row.Label.Text = labelText;
                row.Label.Color = ThemeManager.Colors.InputForegroundPrimary;
                row.Label.Size = new int2(Math.Max(0, rowWidth - (int)labelX - LabelPadding), (int)MathF.Ceiling(labelMetrics.Height));
            }

            int listHeight = Math.Max(0, Size.Y - ToolbarHeight);
            ListHitHost.Position = new float3(0f, ToolbarHeight, 0.05f);
            ListHitInteractable.Size = new int2(rowWidth, listHeight);
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
        /// Handles pointer interactions on a row and triggers navigation or activation.
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
                    if (shouldActivate && row.Entry != null) {
                        if (row.Entry.IsDirectory) {
                            NavigateTo(row.Entry.RelativePath);
                        } else {
                            NotifyAssetActivated(row.Entry);
                        }
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
        /// Handles cursor interactions on the list background to clear selection.
        /// </summary>
        /// <param name="relPos">Relative pointer position.</param>
        /// <param name="delta">Pointer delta.</param>
        /// <param name="state">Pointer interaction state.</param>
        void HandleListHitCursor(int2 relPos, int2 delta, PointerInteraction state) {
            if (state == PointerInteraction.Release) {
                NotifySelectionCleared();
            }
        }

        /// <summary>
        /// Notifies listeners that a file entry was activated.
        /// </summary>
        /// <param name="entry">Selected file entry.</param>
        void NotifyAssetActivated(AssetBrowserEntry entry) {
            if (entry == null) {
                throw new ArgumentNullException(nameof(entry));
            }

            AssetActivated?.Invoke(entry);
        }

        /// <summary>
        /// Notifies listeners that the current selection was cleared.
        /// </summary>
        void NotifySelectionCleared() {
            if (SelectionCleared != null) {
                SelectionCleared();
            }
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
            switch (entry.EntryKind) {
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
                case AssetEntryKind.Material:
                    color = ThemeManager.Colors.AccentQuaternary;
                    label = "MAT";
                    textColor = ThemeManager.Colors.TextOnAccent;
                    return;
                case AssetEntryKind.Scene:
                    color = ThemeManager.Colors.AccentPrimary;
                    label = "SCN";
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

        /// <summary>
        /// Removes entries that do not match the active extension filter.
        /// </summary>
        void ApplyExtensionFilter() {
            if (string.IsNullOrWhiteSpace(ExtensionFilter)) {
                return;
            }

            for (int i = Entries.Count - 1; i >= 0; i--) {
                AssetBrowserEntry entry = Entries[i];
                if (entry == null) {
                    Entries.RemoveAt(i);
                    continue;
                }

                if (entry.IsDirectory) {
                    continue;
                }

                if (!DoesEntryMatchExtensionFilter(entry)) {
                    Entries.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// Determines whether one entry matches the active extension filter, including virtual generated entries.
        /// </summary>
        /// <param name="entry">Entry to validate against the current filter.</param>
        /// <returns>True when the entry should remain visible.</returns>
        bool DoesEntryMatchExtensionFilter(AssetBrowserEntry entry) {
            if (entry == null) {
                return false;
            }

            if (string.Equals(entry.Extension, ExtensionFilter, StringComparison.OrdinalIgnoreCase)) {
                return true;
            }

            if (!entry.IsGenerated) {
                return false;
            }

            string generatedExtension = GetGeneratedEntryExtension(entry.EntryKind);
            if (string.IsNullOrWhiteSpace(generatedExtension)) {
                return false;
            }

            return string.Equals(generatedExtension, ExtensionFilter, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Resolves the logical extension used to filter one generated entry kind.
        /// </summary>
        /// <param name="entryKind">Generated entry kind to classify.</param>
        /// <returns>Logical extension used by pickers, or an empty string when none applies.</returns>
        string GetGeneratedEntryExtension(AssetEntryKind entryKind) {
            switch (entryKind) {
                case AssetEntryKind.Material:
                    return EditorFileTemplateRegistry.MaterialExtension;
                case AssetEntryKind.Scene:
                    return SceneAsset.FileExtension;
                default:
                    return string.Empty;
            }
        }

        /// <summary>
        /// Normalizes an extension filter to ensure a leading dot.
        /// </summary>
        /// <param name="extensionFilter">Extension filter to normalize.</param>
        /// <returns>Normalized extension filter.</returns>
        string NormalizeExtensionFilter(string extensionFilter) {
            if (string.IsNullOrWhiteSpace(extensionFilter)) {
                return string.Empty;
            }

            string trimmed = extensionFilter.Trim();
            if (trimmed.StartsWith(".")) {
                return trimmed;
            }

            return "." + trimmed;
        }
    }
}
