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
        readonly FontAsset font;
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
        /// Synchronizes access to pending entries.
        /// </summary>
        readonly object syncRoot;

        /// <summary>
        /// Tracks whether the panel finished initialization.
        /// </summary>
        bool isInitialized;

        /// <summary>
        /// Initializes a new logger panel with the provided font.
        /// </summary>
        /// <param name="font">Font used for log text.</param>
        public LoggerPanel(FontAsset font) : base(font) {
            this.font = font;
            Title = "Logger";
            MinSize = new int2(260, 160);

            rowBackgroundOrder = Core.Instance.ObjectManager.GetRenderOrderForLayer2D(1);
            textOrder = Core.Instance.ObjectManager.GetRenderOrderForLayer2D(2);

            contentRoot = new EditorEntity();
            contentRoot.LayerMask = LayerMask;
            contentRoot.Position = new float3(0, TitleBarHeight, 0.05f);
            AddChild(contentRoot);

            entries = new List<LogEntry>(MaxEntries);
            pendingEntries = new List<LogEntry>(32);
            stagedEntries = new List<LogEntry>(32);
            rows = new List<LoggerPanelRow>(32);
            syncRoot = new object();

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

            var labelHost = new EditorEntity();
            labelHost.LayerMask = LayerMask;
            labelHost.Position = new float3(RowPadding, 2, 0.2f);
            rowEntity.AddChild(labelHost);

            var text = new TextComponent();
            text.Font = font;
            text.Text = string.Empty;
            text.Color = ThemeManager.Colors.InputForegroundPrimary;
            text.Size = new int2(100, RowHeight);
            text.RenderOrder2D = textOrder;
            labelHost.AddComponent(text);

            contentRoot.AddChild(rowEntity);

            return new LoggerPanelRow(rowEntity, background, labelHost, text);
        }

        /// <summary>
        /// Updates row positions, sizes, and text for the current entries.
        /// </summary>
        void LayoutRows() {
            EnsureRowCount(entries.Count);

            int rowWidth = Math.Max(Size.X, MinSize.X);
            float lineHeight = (float)Math.Max(font.LineHeight, 1.0);
            float verticalOffset = (float)Math.Round((RowHeight - lineHeight) * 0.5, MidpointRounding.AwayFromZero);

            for (int i = 0; i < rows.Count; i++) {
                LoggerPanelRow row = rows[i];
                if (i >= entries.Count) {
                    row.Entity.Enabled = false;
                    continue;
                }

                LogEntry entry = entries[i];
                row.Entity.Enabled = true;
                row.Entity.Position = new float3(0, i * RowHeight, 0.1f);
                row.Background.Size = new int2(rowWidth, RowHeight);

                bool alternate = i % 2 == 1;
                row.Background.Color = alternate ? ThemeManager.Colors.SurfaceInput : ThemeManager.Colors.SurfacePrimary;

                row.LabelHost.Position = new float3(RowPadding, verticalOffset, 0.2f);
                row.Label.Size = new int2(Math.Max(0, rowWidth - (RowPadding * 2)), (int)Math.Ceiling(lineHeight));
                row.Label.Color = ResolveTextColor(entry.Level);
                row.Label.Text = FormatEntry(entry);
            }
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
    }
}
