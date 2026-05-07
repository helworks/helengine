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
        /// Context menu shown for the currently right-clicked row selection.
        /// </summary>
        readonly ContextMenu RowContextMenu;
        /// <summary>
        /// Menu items available for the logger row context menu.
        /// </summary>
        readonly List<ContextMenuItem> RowContextMenuItems;
        /// <summary>
        /// Scroll controller that keeps the focused logger row inside the visible panel body.
        /// </summary>
        readonly ScrollComponent ScrollComponent;
        /// <summary>
        /// Keyboard-focus target that routes logger keyboard commands while the panel is focused.
        /// </summary>
        readonly EditorFocusTarget FocusTarget;

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
        /// Index of the first visible logger row in the panel body.
        /// </summary>
        int FirstVisibleRowIndex;
        /// <summary>
        /// Tracks whether the logger focus target is currently active.
        /// </summary>
        bool IsKeyboardFocused;

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
            FirstVisibleRowIndex = 0;
            RowContextMenuItems = new List<ContextMenuItem> {
                new ContextMenuItem("Copy", HandleCopyContextMenuRequested)
            };
            RowContextMenu = new ContextMenu(font, LayerMask, RenderOrder2D.OverlayBackground, RenderOrder2D.OverlayForeground);
            AddChild(RowContextMenu.Entity);
            ScrollComponent = new ScrollComponent();
            ScrollComponent.Size = new int2(Math.Max(Size.X, MinSize.X), Math.Max(Size.Y, MinSize.Y));
            ScrollComponent.VisibleItemCount = GetVisibleRowCapacity();
            ScrollComponent.ScrollOffsetChanged += HandleScrollOffsetChanged;
            contentRoot.AddComponent(ScrollComponent);

            FocusTarget = new EditorFocusTarget(
                this,
                0,
                true,
                () => Enabled && entries.Count > 0,
                point => ContainsContentPoint(point),
                isFocused => {
                    IsKeyboardFocused = isFocused;
                    LayoutRows();
                },
                CanActivateWithKey,
                HandleActivationKey);
            EditorKeyboardFocusService.RegisterTarget(FocusTarget);

            AddComponent(new LoggerPanelUpdater(this));

            Logger.MessageLogged += HandleMessageLogged;

            isInitialized = true;
        }

        /// <summary>
        /// Detaches the panel from the logger events.
        /// </summary>
        public void Detach() {
            Logger.MessageLogged -= HandleMessageLogged;
            EditorKeyboardFocusService.UnregisterTarget(FocusTarget);
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
                ShiftSelectionStateAfterTrim(removeCount);
            }

            ScrollComponent.ItemCount = entries.Count;
            ScrollComponent.VisibleItemCount = GetVisibleRowCapacity();
            ScrollComponent.ClampScrollOffset();
            FirstVisibleRowIndex = ScrollComponent.ScrollOffset;
            UpdateContentRootPosition();
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
            UpdateContentRootPosition();
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

            ScrollComponent.Size = new int2(Math.Max(Size.X, MinSize.X), Math.Max(Size.Y, MinSize.Y));
            ScrollComponent.ItemCount = entries.Count;
            ScrollComponent.VisibleItemCount = GetVisibleRowCapacity();
            ScrollComponent.ClampScrollOffset();
            FirstVisibleRowIndex = ScrollComponent.ScrollOffset;
            UpdateContentRootPosition();

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

                row.Background.Color = ResolveRowBackgroundColor(i);
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
                EnsureFocusedRowVisible();
                return;
            }

            FocusedRowIndex = rowIndex;
            AnchorRowIndex = rowIndex;

            if (isControlPressed) {
                ToggleRowSelection(rowIndex);
                EnsureFocusedRowVisible();
                EditorKeyboardFocusService.SetFocusedTarget(FocusTarget);
                return;
            }

            SelectSingleRow(rowIndex);
            EnsureFocusedRowVisible();
            EditorKeyboardFocusService.SetFocusedTarget(FocusTarget);
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

        /// <summary>
        /// Selects one row for context-menu interaction and shows the logger row menu.
        /// </summary>
        /// <param name="rowIndex">Row index that was right-clicked.</param>
        /// <param name="localPointerPosition">Pointer position relative to the logger panel.</param>
        void HandleRowRightPressed(int rowIndex, int2 localPointerPosition) {
            if (rowIndex < 0 || rowIndex >= entries.Count) {
                return;
            }

            if (!SelectedRowIndices.Contains(rowIndex)) {
                FocusedRowIndex = rowIndex;
                AnchorRowIndex = rowIndex;
                SelectSingleRow(rowIndex);
            } else {
                FocusedRowIndex = rowIndex;
                AnchorRowIndex = rowIndex;
            }

            RowContextMenu.Show(RowContextMenuItems, localPointerPosition, GetContextMenuHostSize());
            EnsureFocusedRowVisible();
            EditorKeyboardFocusService.SetFocusedTarget(FocusTarget);
        }

        /// <summary>
        /// Copies the currently selected rows to the host clipboard.
        /// </summary>
        void CopySelection() {
            Core.Instance.TextClipboardService.WriteText(BuildSelectedRowsText());
        }

        /// <summary>
        /// Handles activation of the logger row copy context-menu item.
        /// </summary>
        void HandleCopyContextMenuRequested() {
            CopySelection();
        }

        /// <summary>
        /// Builds one text payload from the currently selected rows in visible row order.
        /// </summary>
        /// <returns>Joined logger text payload for the current selection.</returns>
        string BuildSelectedRowsText() {
            if (SelectedRowIndices.Count == 0) {
                if (FocusedRowIndex < 0 || FocusedRowIndex >= entries.Count) {
                    return string.Empty;
                }

                return FormatEntry(entries[FocusedRowIndex]);
            }

            List<string> lines = new List<string>(SelectedRowIndices.Count);
            for (int rowIndex = 0; rowIndex < entries.Count; rowIndex++) {
                if (!SelectedRowIndices.Contains(rowIndex)) {
                    continue;
                }

                lines.Add(FormatEntry(entries[rowIndex]));
            }

            return string.Join(Environment.NewLine, lines);
        }

        /// <summary>
        /// Resolves the host region used to clamp the logger row context menu.
        /// </summary>
        /// <returns>Host size for logger context-menu layout.</returns>
        int2 GetContextMenuHostSize() {
            return new int2(
                Math.Max(Size.X, MinSize.X),
                Math.Max(Size.Y + TitleBarHeightPixels, MinSize.Y + TitleBarHeightPixels));
        }

        /// <summary>
        /// Resolves the background color that should be used for one row index.
        /// </summary>
        /// <param name="rowIndex">Row index whose color should be resolved.</param>
        /// <returns>Background color for the row.</returns>
        byte4 ResolveRowBackgroundColor(int rowIndex) {
            bool isSelected = SelectedRowIndices.Contains(rowIndex);
            bool isFocused = FocusedRowIndex == rowIndex && IsKeyboardFocused;
            if (isSelected && isFocused) {
                return ThemeManager.Colors.AccentPrimary;
            }

            if (isSelected) {
                return ThemeManager.Colors.AccentSecondary;
            }

            if (isFocused) {
                return ThemeManager.Colors.AccentTertiary;
            }

            return rowIndex % 2 == 1
                ? ThemeManager.Colors.SurfaceInput
                : ThemeManager.Colors.SurfacePrimary;
        }

        /// <summary>
        /// Updates logger keyboard interaction for the current frame when the logger focus target is active.
        /// </summary>
        internal void UpdateKeyboardInput() {
            if (!IsKeyboardFocused) {
                return;
            }

            InputSystem input = Core.Instance.Input;
            bool isShiftPressed = input.IsKeyDown(Keys.LeftShift) || input.IsKeyDown(Keys.RightShift);
            bool isControlPressed = input.IsKeyDown(Keys.LeftControl) || input.IsKeyDown(Keys.RightControl);

            if (input.WasKeyPressed(Keys.Down)) {
                MoveFocusBy(1, isControlPressed, isShiftPressed);
            } else if (input.WasKeyPressed(Keys.Up)) {
                MoveFocusBy(-1, isControlPressed, isShiftPressed);
            } else if (isControlPressed && input.WasKeyPressed(Keys.Space)) {
                ToggleFocusedRowSelection();
            } else if (isControlPressed && input.WasKeyPressed(Keys.C)) {
                CopySelection();
            }
        }

        /// <summary>
        /// Updates logger context-menu visibility from right-click input.
        /// </summary>
        internal void UpdateContextMenuInput() {
            InputSystem input = Core.Instance.Input;
            if (!input.WasMouseRightButtonPressed()) {
                return;
            }

            int2 pointer = input.GetMousePosition();
            if (EditorInputCaptureService.IsPointerBlocked(pointer, owner => !ReferenceEquals(owner, this))) {
                return;
            }
            if (!ContainsContentPoint(pointer)) {
                RowContextMenu.Hide();
                return;
            }

            if (!TryGetRowAtScreenPoint(pointer, out int rowIndex)) {
                RowContextMenu.Hide();
                return;
            }

            int2 localPosition = new int2(
                pointer.X - (int)Math.Round(Position.X),
                pointer.Y - (int)Math.Round(Position.Y));
            HandleRowRightPressed(rowIndex, localPosition);
        }

        /// <summary>
        /// Resolves whether the logger focus target should activate for the supplied key.
        /// </summary>
        /// <param name="key">Activation key to evaluate.</param>
        /// <returns>True when the logger target handles the key.</returns>
        bool CanActivateWithKey(Keys key) {
            return false;
        }

        /// <summary>
        /// Routes one activation key from the editor keyboard-focus service into logger keyboard handling.
        /// </summary>
        /// <param name="key">Activation key to process.</param>
        void HandleActivationKey(Keys key) {
        }

        /// <summary>
        /// Moves the focused row by one offset and updates selection according to the supplied modifier semantics.
        /// </summary>
        /// <param name="delta">Relative row movement to apply.</param>
        /// <param name="preserveSelection">True when the existing selection set should remain unchanged.</param>
        /// <param name="extendSelection">True when selection should expand from the current anchor.</param>
        void MoveFocusBy(int delta, bool preserveSelection, bool extendSelection) {
            if (entries.Count == 0) {
                return;
            }

            int nextRowIndex = FocusedRowIndex;
            if (nextRowIndex < 0) {
                nextRowIndex = delta >= 0 ? 0 : entries.Count - 1;
            } else {
                nextRowIndex += delta;
            }

            if (nextRowIndex < 0) {
                nextRowIndex = 0;
            } else if (nextRowIndex >= entries.Count) {
                nextRowIndex = entries.Count - 1;
            }

            FocusedRowIndex = nextRowIndex;
            if (extendSelection) {
                if (AnchorRowIndex < 0) {
                    AnchorRowIndex = nextRowIndex;
                }

                SelectRangeFromAnchor(nextRowIndex);
            } else if (preserveSelection) {
                if (AnchorRowIndex < 0) {
                    AnchorRowIndex = nextRowIndex;
                }
            } else {
                AnchorRowIndex = nextRowIndex;
                SelectSingleRow(nextRowIndex);
            }

            EnsureFocusedRowVisible();
            LayoutRows();
        }

        /// <summary>
        /// Toggles the focused row inside the current selection set.
        /// </summary>
        void ToggleFocusedRowSelection() {
            if (FocusedRowIndex < 0 || FocusedRowIndex >= entries.Count) {
                return;
            }

            AnchorRowIndex = FocusedRowIndex;
            ToggleRowSelection(FocusedRowIndex);
            EnsureFocusedRowVisible();
            LayoutRows();
        }

        /// <summary>
        /// Adjusts the scroll offset so the focused row remains inside the visible window.
        /// </summary>
        void EnsureFocusedRowVisible() {
            if (FocusedRowIndex < 0 || entries.Count == 0) {
                return;
            }

            int visibleRowCapacity = GetVisibleRowCapacity();
            if (FocusedRowIndex < FirstVisibleRowIndex) {
                ScrollComponent.ScrollTo(FocusedRowIndex);
            } else if (FocusedRowIndex >= FirstVisibleRowIndex + visibleRowCapacity) {
                ScrollComponent.ScrollTo(FocusedRowIndex - visibleRowCapacity + 1);
            }
        }

        /// <summary>
        /// Returns the number of full rows visible inside the current logger body viewport.
        /// </summary>
        /// <returns>Visible logger row capacity.</returns>
        int GetVisibleRowCapacity() {
            int rowHeight = GetRowHeightPixels();
            if (rowHeight <= 0) {
                return 1;
            }

            return Math.Max(1, Math.Max(Size.Y, MinSize.Y) / rowHeight);
        }

        /// <summary>
        /// Updates the content root translation from the current scroll offset.
        /// </summary>
        void UpdateContentRootPosition() {
            contentRoot.Position = new float3(0f, TitleBarHeightPixels - (FirstVisibleRowIndex * GetRowHeightPixels()), 0.05f);
        }

        /// <summary>
        /// Returns true when the provided screen point lies inside the logger content body.
        /// </summary>
        /// <param name="point">Screen point to evaluate.</param>
        /// <returns>True when the point lies inside the logger content area.</returns>
        bool ContainsContentPoint(int2 point) {
            int panelX = (int)Math.Round(Position.X);
            int panelY = (int)Math.Round(Position.Y);
            int panelWidth = Math.Max(Size.X, MinSize.X);
            int panelHeight = Math.Max(Size.Y, MinSize.Y);
            return point.X >= panelX &&
                   point.X < panelX + panelWidth &&
                   point.Y >= panelY + TitleBarHeightPixels &&
                   point.Y < panelY + TitleBarHeightPixels + panelHeight;
        }

        /// <summary>
        /// Attempts to resolve one logger row index for the supplied screen-space pointer.
        /// </summary>
        /// <param name="pointer">Pointer position in screen coordinates.</param>
        /// <param name="rowIndex">Resolved row index when one exists.</param>
        /// <returns>True when a visible logger row was found.</returns>
        bool TryGetRowAtScreenPoint(int2 pointer, out int rowIndex) {
            if (!ContainsContentPoint(pointer)) {
                rowIndex = -1;
                return false;
            }

            int localY = pointer.Y - (int)Math.Round(Position.Y) - TitleBarHeightPixels;
            int rowIndexCandidate = FirstVisibleRowIndex + (localY / Math.Max(1, GetRowHeightPixels()));
            if (rowIndexCandidate < 0 || rowIndexCandidate >= entries.Count) {
                rowIndex = -1;
                return false;
            }

            rowIndex = rowIndexCandidate;
            return true;
        }

        /// <summary>
        /// Rebuilds content position after the row scroll offset changes.
        /// </summary>
        /// <param name="scrollComponent">Scroll controller that raised the change.</param>
        /// <param name="scrollOffset">Current scroll offset in item units.</param>
        void HandleScrollOffsetChanged(ScrollComponent scrollComponent, int scrollOffset) {
            FirstVisibleRowIndex = scrollOffset;
            UpdateContentRootPosition();
        }

        /// <summary>
        /// Shifts selection, focus, and anchor state after old rows are trimmed from the front of the logger.
        /// </summary>
        /// <param name="removeCount">Number of rows removed from the front of the logger.</param>
        void ShiftSelectionStateAfterTrim(int removeCount) {
            if (removeCount <= 0) {
                return;
            }

            HashSet<int> shiftedSelection = new HashSet<int>();
            foreach (int selectedRowIndex in SelectedRowIndices) {
                int shiftedRowIndex = selectedRowIndex - removeCount;
                if (shiftedRowIndex >= 0) {
                    shiftedSelection.Add(shiftedRowIndex);
                }
            }

            SelectedRowIndices.Clear();
            foreach (int shiftedRowIndex in shiftedSelection) {
                SelectedRowIndices.Add(shiftedRowIndex);
            }

            FocusedRowIndex -= removeCount;
            AnchorRowIndex -= removeCount;
            FirstVisibleRowIndex = Math.Max(0, FirstVisibleRowIndex - removeCount);

            if (entries.Count == 0) {
                FocusedRowIndex = -1;
                AnchorRowIndex = -1;
                SelectedRowIndices.Clear();
                FirstVisibleRowIndex = 0;
                return;
            }

            int lastRemainingIndex = entries.Count - 1;
            if (FocusedRowIndex < 0) {
                FocusedRowIndex = 0;
            } else if (FocusedRowIndex > lastRemainingIndex) {
                FocusedRowIndex = lastRemainingIndex;
            }

            if (AnchorRowIndex < 0) {
                AnchorRowIndex = 0;
            } else if (AnchorRowIndex > lastRemainingIndex) {
                AnchorRowIndex = lastRemainingIndex;
            }
        }
    }
}
