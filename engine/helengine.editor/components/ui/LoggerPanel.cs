namespace helengine.editor {
    /// <summary>
    /// Displays log messages produced by the engine logger.
    /// </summary>
    public class LoggerPanel : DockableEntity {
        /// <summary>
        /// Height of each log row in pixels.
        /// </summary>
        public const int RowHeight = 22;

        /// <summary>
        /// Maximum number of log entries retained for display.
        /// </summary>
        public const int MaxEntries = 256;

        /// <summary>
        /// Horizontal padding for log text.
        /// </summary>
        const int RowPadding = 8;

        /// <summary>
        /// Font used to render log text.
        /// </summary>
        FontAsset font;
        /// <summary>
        /// Render order used for row backgrounds.
        /// </summary>
        readonly byte rowBackgroundOrder;
        /// <summary>
        /// Render order used for row text.
        /// </summary>
        readonly byte textOrder;

        /// <summary>
        /// Root entity hosting log rows.
        /// </summary>
        readonly EditorEntity contentRoot;

        /// <summary>
        /// Log entries displayed by the panel.
        /// </summary>
        readonly List<LogEntry> entries;

        /// <summary>
        /// Pending log entries queued from the logger event.
        /// </summary>
        readonly List<LogEntry> pendingEntries;

        /// <summary>
        /// Scratch list used to drain pending entries without extra allocations.
        /// </summary>
        readonly List<LogEntry> stagedEntries;

        /// <summary>
        /// Pool of row visuals used to display log entries.
        /// </summary>
        readonly List<LoggerPanelRow> rows;
        /// <summary>
        /// Tracks the currently selected logger row indices.
        /// </summary>
        readonly HashSet<int> SelectedRowIndices;

        /// <summary>
        /// Synchronizes access to pending entries.
        /// </summary>
        readonly object syncRoot;

        /// <summary>
        /// Tracks whether the panel finished initialization.
        /// </summary>
        bool isInitialized;
        /// <summary>
        /// Index of the currently focused logger row.
        /// </summary>
        int FocusedRowIndex;
        /// <summary>
        /// Index of the current multi-selection anchor row.
        /// </summary>
        int AnchorRowIndex;

        /// <summary>
        /// Initializes a new logger panel with the provided font.
        /// </summary>
        /// <param name="font">Font used for log text.</param>
        public LoggerPanel(FontAsset font) : this(font, EditorUiMetrics.Default) {
        }

        /// <summary>
        /// Initializes a new logger panel with the provided font and shared metrics source.
        /// </summary>
        /// <param name="font">Font used for log text.</param>
        /// <param name="metrics">Scaled editor UI metrics used to size the dock chrome and rows.</param>
        public LoggerPanel(FontAsset font, EditorUiMetrics metrics) : base(font, metrics) {
            this.font = font;
            Title = "Logger";
            MinSize = new int2(metrics.ScalePixels(260), metrics.ScalePixels(160));

            rowBackgroundOrder = RenderOrder2D.PanelSurface;
            textOrder = RenderOrder2D.PanelForeground;

            contentRoot = new EditorEntity();
            contentRoot.LayerMask = LayerMask;
            contentRoot.Position = new float3(0, TitleBarHeightPixels, 0.05f);
            AddChild(contentRoot);

            entries = new List<LogEntry>(MaxEntries);
            pendingEntries = new List<LogEntry>(32);
            stagedEntries = new List<LogEntry>(32);
            rows = new List<LoggerPanelRow>(32);
            SelectedRowIndices = new HashSet<int>();
            syncRoot = new object();
            FocusedRowIndex = -1;
            AnchorRowIndex = -1;

            AddComponent(new LoggerPanelUpdater(this));

            Logger.MessageLogged += HandleMessageLogged;

            isInitialized = true;
        }

        /// <summary>
        /// Detaches the panel from the logger events.
        /// </summary>
        public void Detach() {
            Logger.MessageLogged -= HandleMessageLogged;
        }

        /// <summary>
        /// Flushes pending log entries into the visible list.
        /// </summary>
        public void FlushPendingEntries() {
            lock (syncRoot) {
                if (pendingEntries.Count == 0) {
                    return;
                }

                stagedEntries.Clear();
                stagedEntries.AddRange(pendingEntries);
                pendingEntries.Clear();
            }

            for (int i = 0; i < stagedEntries.Count; i++) {
                AppendEntry(stagedEntries[i]);
            }
            stagedEntries.Clear();

            LayoutRows();
        }

        /// <summary>
        /// Reapplies scaled dock metrics after one live UI scale change.
        /// </summary>
        /// <param name="font">Updated dock title and row font.</param>
        /// <param name="metrics">Updated scaled editor UI metrics.</param>
        public override void ApplyUiMetrics(FontAsset font, EditorUiMetrics metrics) {
            if (font == null) {
                throw new ArgumentNullException(nameof(font));
            }

            this.font = font;
            base.ApplyUiMetrics(font, metrics);

            for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++) {
                rows[rowIndex].Label.Font = font;
            }
        }

        /// <summary>
        /// Handles new log messages by enqueueing them for display.
        /// </summary>
        /// <param name="entry">Log entry that was recorded.</param>
        void HandleMessageLogged(LogEntry entry) {
            lock (syncRoot) {
                pendingEntries.Add(entry);
            }
        }

        /// <summary>
        /// Adds a log entry to the visible list and trims excess entries.
        /// </summary>
        /// <param name="entry">Log entry to add.</param>
        void AppendEntry(LogEntry entry) {
            entries.Add(entry);
            if (entries.Count > MaxEntries) {
                int removeCount = entries.Count - MaxEntries;
                entries.RemoveRange(0, removeCount);
            }
        }

        /// <summary>
        /// Handles layout updates when the dockable size changes.
        /// </summary>
        protected override void OnSizeChanged() {
            base.OnSizeChanged();
            if (!isInitialized) {
                return;
            }

            LayoutRows();
        }

        /// <summary>
        /// Updates scaled logger content offsets after the shared dock chrome metrics change.
        /// </summary>
        protected override void HandleUiMetricsApplied() {
            MinSize = new int2(UiMetrics.ScalePixels(260), UiMetrics.ScalePixels(160));
            contentRoot.Position = new float3(0f, TitleBarHeightPixels, 0.05f);
        }

        /// <summary>
        /// Ensures enough rows exist to display all current entries.
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
        /// Creates a new row entity for a log entry.
        /// </summary>
        /// <returns>Row elements container.</returns>
        LoggerPanelRow CreateRow() {
            var rowEntity = new EditorEntity();
            rowEntity.LayerMask = LayerMask;
            rowEntity.Position = float3.Zero;

            var background = new SpriteComponent();
            background.Texture = TextureUtils.PixelTexture;
            background.Color = ThemeManager.Colors.SurfacePrimary;
            background.RenderOrder2D = rowBackgroundOrder;
            rowEntity.AddComponent(background);

            var interactable = new InteractableComponent();
            interactable.Size = new int2(Math.Max(Size.X, MinSize.X), GetRowHeightPixels());
            rowEntity.AddComponent(interactable);

            var labelHost = new EditorEntity();
            labelHost.LayerMask = LayerMask;
            labelHost.Position = new float3(GetRowPaddingPixels(), 2, 0.2f);
            rowEntity.AddChild(labelHost);

            var text = new TextComponent();
            text.Font = font;
            text.Text = string.Empty;
            text.Color = ThemeManager.Colors.InputForegroundPrimary;
            text.Size = new int2(100, GetRowHeightPixels());
            text.RenderOrder2D = textOrder;
            labelHost.AddComponent(text);

            contentRoot.AddChild(rowEntity);

            return new LoggerPanelRow(rowEntity, background, labelHost, text, interactable);
        }

        /// <summary>
        /// Updates row positions, sizes, and text for the current entries.
        /// </summary>
        void LayoutRows() {
            EnsureRowCount(entries.Count);

            int rowWidth = Math.Max(Size.X, MinSize.X);
            float lineHeight = (float)Math.Max(font.LineHeight, 1.0);
            float verticalOffset = (float)Math.Round((GetRowHeightPixels() - lineHeight) * 0.5, MidpointRounding.AwayFromZero);

            for (int i = 0; i < rows.Count; i++) {
                LoggerPanelRow row = rows[i];
                if (i >= entries.Count) {
                    row.Entity.Enabled = false;
                    continue;
                }

                LogEntry entry = entries[i];
                row.Entity.Enabled = true;
                row.Entity.Position = new float3(0, i * GetRowHeightPixels(), 0.1f);
                row.Background.Size = new int2(rowWidth, GetRowHeightPixels());
                row.Interactable.Size = new int2(rowWidth, GetRowHeightPixels());

                bool alternate = i % 2 == 1;
                row.Background.Color = alternate ? ThemeManager.Colors.SurfaceInput : ThemeManager.Colors.SurfacePrimary;

                row.LabelHost.Position = new float3(GetRowPaddingPixels(), verticalOffset, 0.2f);
                row.Label.Size = new int2(Math.Max(0, rowWidth - (GetRowPaddingPixels() * 2)), (int)Math.Ceiling(lineHeight));
                row.Label.Color = ResolveTextColor(entry.Level);
                row.Label.Text = FormatEntry(entry);
            }
        }

        /// <summary>
        /// Gets the scaled row height used by the logger rows.
        /// </summary>
        /// <returns>Scaled logger row height in pixels.</returns>
        int GetRowHeightPixels() {
            return UiMetrics.ScalePixels(RowHeight);
        }

        /// <summary>
        /// Gets the scaled horizontal padding used by log text.
        /// </summary>
        /// <returns>Scaled log-row padding in pixels.</returns>
        int GetRowPaddingPixels() {
            return UiMetrics.ScalePixels(RowPadding);
        }

        /// <summary>
        /// Formats a log entry for display.
        /// </summary>
        /// <param name="entry">Entry to format.</param>
        /// <returns>Formatted display text.</returns>
        string FormatEntry(LogEntry entry) {
            string timeText = entry.Timestamp.ToLocalTime().ToString("HH:mm:ss");
            return $"[{timeText}] {entry.Level}: {entry.Message}";
        }

        /// <summary>
        /// Resolves the display color for a given log level.
        /// </summary>
        /// <param name="level">Log severity level.</param>
        /// <returns>Color to apply to the text.</returns>
        byte4 ResolveTextColor(LogLevel level) {
            if (level == LogLevel.Warning) {
                return ThemeManager.Colors.StateWarning;
            }

            if (level == LogLevel.Error) {
                return ThemeManager.Colors.StateDanger;
            }

            return ThemeManager.Colors.InputForegroundPrimary;
        }

        /// <summary>
        /// Applies row selection changes for one pressed logger row.
        /// </summary>
        /// <param name="rowIndex">Row index that was pressed.</param>
        /// <param name="isControlPressed">True when the control modifier is active.</param>
        /// <param name="isShiftPressed">True when the shift modifier is active.</param>
        void HandleRowPressed(int rowIndex, bool isControlPressed, bool isShiftPressed) {
            if (rowIndex < 0 || rowIndex >= entries.Count) {
                return;
            }

            if (isShiftPressed && AnchorRowIndex >= 0) {
                FocusedRowIndex = rowIndex;
                SelectRangeFromAnchor(rowIndex);
                return;
            }

            FocusedRowIndex = rowIndex;
            AnchorRowIndex = rowIndex;

            if (isControlPressed) {
                ToggleRowSelection(rowIndex);
                return;
            }

            SelectSingleRow(rowIndex);
        }

        /// <summary>
        /// Replaces the current selection with one row.
        /// </summary>
        /// <param name="rowIndex">Row index that should remain selected.</param>
        void SelectSingleRow(int rowIndex) {
            SelectedRowIndices.Clear();
            SelectedRowIndices.Add(rowIndex);
        }

        /// <summary>
        /// Toggles one row inside the current selection set.
        /// </summary>
        /// <param name="rowIndex">Row index to toggle.</param>
        void ToggleRowSelection(int rowIndex) {
            if (!SelectedRowIndices.Add(rowIndex)) {
                SelectedRowIndices.Remove(rowIndex);
            }
        }

        /// <summary>
        /// Selects the inclusive range between the current anchor and the supplied row.
        /// </summary>
        /// <param name="rowIndex">Target row that closes the selected range.</param>
        void SelectRangeFromAnchor(int rowIndex) {
            SelectedRowIndices.Clear();

            int rangeStart = Math.Min(AnchorRowIndex, rowIndex);
            int rangeEnd = Math.Max(AnchorRowIndex, rowIndex);
            for (int selectedRowIndex = rangeStart; selectedRowIndex <= rangeEnd; selectedRowIndex++) {
                SelectedRowIndices.Add(selectedRowIndex);
            }
        }
    }
}
