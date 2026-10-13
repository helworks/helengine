using System.Reflection;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies logger panel layout behavior that depends on shared dock metrics.
    /// </summary>
    public class LoggerPanelTests : IDisposable {
        /// <summary>
        /// Temporary content root used by the logger panel tests.
        /// </summary>
        readonly string TempRootPath;
        /// <summary>
        /// Deterministic input backend used by logger panel keyboard tests.
        /// </summary>
        readonly TestInputBackend InputBackend;
        /// <summary>
        /// Clipboard service explicitly bound to each logger panel created by this fixture.
        /// </summary>
        readonly TestTextClipboardService ClipboardService;

        /// <summary>
        /// Initializes the core services required by the logger panel tests.
        /// </summary>
        public LoggerPanelTests() {
            TempRootPath = Path.Combine(Path.GetTempPath(), "helengine-logger-panel-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(TempRootPath);

            Core core = new Core(new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(TempRootPath)
            });
            InputBackend = new TestInputBackend();
            ClipboardService = new TestTextClipboardService();
            core.Initialize(null, new TestRenderManager2D(), InputBackend, new PlatformInfo("test", "test-version"));
            core.SetTextClipboardService(ClipboardService);
        }

        /// <summary>
        /// Deletes temporary content after each test.
        /// </summary>
        public void Dispose() {
            if (Directory.Exists(TempRootPath)) {
                Directory.Delete(TempRootPath, true);
            }
        }

        /// <summary>
        /// Ensures scaled dock metrics move the logger content below the scaled title bar and enlarge row chrome.
        /// </summary>
        [Fact]
        public void FlushPendingEntries_WithScaledMetrics_UsesScaledTitleBarOffsetAndRowHeight() {
            EditorUiMetrics metrics = new EditorUiMetrics(1.5d);
            LoggerPanel panel = new LoggerPanel(Core.Instance, new helengine.editor.EditorSessionInteractionServices(), CreateFont(), metrics) {
                Size = new int2(320, 240)
            };

            try {
                Logger.WriteLine("Scaled logger entry");
                panel.FlushPendingEntries();

                EditorEntity contentRoot = GetPrivateField<EditorEntity>(panel, "contentRoot");
                List<LoggerPanelRow> rows = GetPrivateField<List<LoggerPanelRow>>(panel, "rows");
                LoggerPanelRow row = Assert.Single(rows);

                Assert.Equal(30f, contentRoot.Position.Y);
                Assert.Equal(new int2(390, 33), row.Background.Size);
                Assert.Equal(12f, row.LabelHost.Position.X);
            } finally {
                panel.Detach();
            }
        }

        /// <summary>
        /// Ensures the logger rounds visible rows up when the viewport has enough remaining space to show part of the next row.
        /// </summary>
        [Fact]
        public void FlushPendingEntries_WhenViewportHasPartialRemainingRow_RoundsVisibleRowsUp() {
            LoggerPanel panel = CreatePanelWithEntries("row-0", "row-1", "row-2", "row-3", "row-4", "row-5", "row-6", "row-7");
            panel.MinSize = new int2(0, 0);
            panel.Size = new int2(320, (LoggerPanel.RowHeight * 3) + (LoggerPanel.RowHeight / 2));

            try {
                panel.FlushPendingEntries();

                ScrollComponent scrollComponent = GetPrivateField<EditorScrollComponent>(panel, "ScrollComponent");

                Assert.Equal(4, scrollComponent.VisibleItemCount);
            } finally {
                panel.Detach();
            }
        }

        /// <summary>
        /// Ensures the logger's scroll hit region stays anchored to the fixed viewport while its content moves.
        /// </summary>
        [Fact]
        public void ScrollComponent_WhenLoggerContentScrolls_KeepsHitRegionAnchoredToViewport() {
            LoggerPanel panel = CreatePanelWithEntries("row-0", "row-1", "row-2", "row-3", "row-4");
            panel.MinSize = new int2(0, 0);
            panel.Size = new int2(320, LoggerPanel.RowHeight);

            try {
                ScrollComponent scrollComponent = GetPrivateField<EditorScrollComponent>(panel, "ScrollComponent");
                float4 initialClipRect = scrollComponent.GetClipRect();
                int pointerX = (int)initialClipRect.X + 1;
                int pointerY = (int)initialClipRect.Y + 1;

                Assert.True(scrollComponent.ContainsScreenPoint(pointerX, pointerY));
                Assert.True(scrollComponent.ScrollTo(1));

                float4 scrolledClipRect = scrollComponent.GetClipRect();

                Assert.Equal(initialClipRect.X, scrolledClipRect.X);
                Assert.Equal(initialClipRect.Y, scrolledClipRect.Y);
                Assert.True(scrollComponent.ContainsScreenPoint(pointerX, pointerY));
            } finally {
                panel.Detach();
            }
        }

        /// <summary>
        /// Ensures a plain row click focuses and selects only the clicked row.
        /// </summary>
        [Fact]
        public void HandleRowPressed_WhenPlainClickOccurs_SelectsOnlyThatRow() {
            LoggerPanel panel = CreatePanelWithEntries("row-0", "row-1", "row-2", "row-3");

            try {
                InvokePrivate(panel, "HandleRowPressed", 3, false, false);

                Assert.Equal(3, GetPrivateField<int>(panel, "FocusedRowIndex"));
                Assert.Equal(3, GetPrivateField<int>(panel, "AnchorRowIndex"));
                Assert.Equal(new[] { 3 }, GetPrivateField<HashSet<int>>(panel, "SelectedRowIndices").OrderBy(value => value));
            } finally {
                panel.Detach();
            }
        }

        /// <summary>
        /// Ensures clicking a live logger row through its interactable selects that row.
        /// </summary>
        [Fact]
        public void LoggerRowInteractable_WhenClicked_SelectsTheClickedRow() {
            LoggerPanel panel = CreatePanelWithEntries("row-0", "row-1", "row-2", "row-3");

            try {
                List<LoggerPanelRow> rows = GetPrivateField<List<LoggerPanelRow>>(panel, "rows");

                rows[2].Interactable.OnCursor(new int2(4, 4), new int2(0, 0), PointerInteraction.Press);
                rows[2].Interactable.OnCursor(new int2(4, 4), new int2(0, 0), PointerInteraction.Release);

                Assert.Equal(2, GetPrivateField<int>(panel, "FocusedRowIndex"));
                Assert.Equal(2, GetPrivateField<int>(panel, "AnchorRowIndex"));
                Assert.Equal(new[] { 2 }, GetPrivateField<HashSet<int>>(panel, "SelectedRowIndices").OrderBy(value => value));
            } finally {
                panel.Detach();
            }
        }

        /// <summary>
        /// Ensures a control-click toggles the clicked row while preserving the existing selection set.
        /// </summary>
        [Fact]
        public void HandleRowPressed_WhenControlClickOccurs_TogglesThatRowInsideSelection() {
            LoggerPanel panel = CreatePanelWithEntries("row-0", "row-1", "row-2", "row-3");

            try {
                InvokePrivate(panel, "HandleRowPressed", 1, false, false);
                InvokePrivate(panel, "HandleRowPressed", 3, true, false);

                Assert.Equal(3, GetPrivateField<int>(panel, "FocusedRowIndex"));
                Assert.Equal(3, GetPrivateField<int>(panel, "AnchorRowIndex"));
                Assert.Equal(new[] { 1, 3 }, GetPrivateField<HashSet<int>>(panel, "SelectedRowIndices").OrderBy(value => value));

                InvokePrivate(panel, "HandleRowPressed", 1, true, false);

                Assert.Equal(1, GetPrivateField<int>(panel, "FocusedRowIndex"));
                Assert.Equal(1, GetPrivateField<int>(panel, "AnchorRowIndex"));
                Assert.Equal(new[] { 3 }, GetPrivateField<HashSet<int>>(panel, "SelectedRowIndices").OrderBy(value => value));
            } finally {
                panel.Detach();
            }
        }

        /// <summary>
        /// Ensures a shift-click expands selection to the inclusive range from the current anchor.
        /// </summary>
        [Fact]
        public void HandleRowPressed_WhenShiftClickOccurs_SelectsInclusiveRangeFromAnchor() {
            LoggerPanel panel = CreatePanelWithEntries("row-0", "row-1", "row-2", "row-3", "row-4");

            try {
                InvokePrivate(panel, "HandleRowPressed", 1, false, false);
                InvokePrivate(panel, "HandleRowPressed", 4, false, true);

                Assert.Equal(4, GetPrivateField<int>(panel, "FocusedRowIndex"));
                Assert.Equal(1, GetPrivateField<int>(panel, "AnchorRowIndex"));
                Assert.Equal(new[] { 1, 2, 3, 4 }, GetPrivateField<HashSet<int>>(panel, "SelectedRowIndices").OrderBy(value => value));
            } finally {
                panel.Detach();
            }
        }

        /// <summary>
        /// Ensures the shared copy path writes all selected rows to the clipboard, oldest (bottom) row first.
        /// </summary>
        [Fact]
        public void CopySelection_WhenMultipleRowsSelected_WritesJoinedVisibleRowsToClipboard() {
            LoggerPanel panel = CreatePanelWithEntries("row-0", "row-1", "row-2", "row-3");
            TestTextClipboardService clipboardService = new TestTextClipboardService();
            Core.Instance.SetTextClipboardService(clipboardService);
            panel.SetInputServices(Core.Instance.Input, clipboardService);

            try {
                InvokePrivate(panel, "HandleRowPressed", 1, false, false);
                InvokePrivate(panel, "HandleRowPressed", 3, true, false);

                List<LoggerPanelRow> rows = GetPrivateField<List<LoggerPanelRow>>(panel, "rows");
                string expected = string.Join(
                    Environment.NewLine,
                    rows[3].Label.Text,
                    rows[1].Label.Text);

                InvokePrivate(panel, "CopySelection");

                Assert.Equal(expected, clipboardService.ReadText());
            } finally {
                panel.Detach();
            }
        }

        /// <summary>
        /// Ensures right-clicking an unselected row selects it and opens the copy context menu.
        /// </summary>
        [Fact]
        public void HandleRowRightPressed_WhenClickedRowIsUnselected_SelectsThatRowAndShowsCopyMenu() {
            LoggerPanel panel = CreatePanelWithEntries("row-0", "row-1", "row-2");

            try {
                InvokePrivate(panel, "HandleRowPressed", 0, false, false);
                InvokePrivate(panel, "HandleRowRightPressed", 2, new int2(24, 48));

                ContextMenu contextMenu = GetPrivateField<ContextMenu>(panel, "RowContextMenu");

                Assert.Equal(2, GetPrivateField<int>(panel, "FocusedRowIndex"));
                Assert.Equal(2, GetPrivateField<int>(panel, "AnchorRowIndex"));
                Assert.Equal(new[] { 2 }, GetPrivateField<HashSet<int>>(panel, "SelectedRowIndices").OrderBy(value => value));
                Assert.True(contextMenu.IsVisible);
            } finally {
                panel.Detach();
            }
        }

        /// <summary>
        /// Ensures the context-menu copy action reuses the same payload as the shared copy path.
        /// </summary>
        [Fact]
        public void HandleCopyContextMenuRequested_WhenInvoked_UsesTheSameClipboardPayloadAsKeyboardCopy() {
            LoggerPanel panel = CreatePanelWithEntries("row-0", "row-1", "row-2", "row-3");
            TestTextClipboardService clipboardService = new TestTextClipboardService();
            Core.Instance.SetTextClipboardService(clipboardService);
            panel.SetInputServices(Core.Instance.Input, clipboardService);

            try {
                InvokePrivate(panel, "HandleRowPressed", 1, false, false);
                InvokePrivate(panel, "HandleRowPressed", 3, true, false);

                List<LoggerPanelRow> rows = GetPrivateField<List<LoggerPanelRow>>(panel, "rows");
                string expected = string.Join(
                    Environment.NewLine,
                    rows[3].Label.Text,
                    rows[1].Label.Text);

                InvokePrivate(panel, "HandleRowRightPressed", 3, new int2(24, 72));
                InvokePrivate(panel, "HandleCopyContextMenuRequested");

                Assert.Equal(expected, clipboardService.ReadText());
            } finally {
                panel.Detach();
            }
        }

        /// <summary>
        /// Ensures pressing Down moves focus and collapses selection to the next row.
        /// </summary>
        [Fact]
        public void UpdateKeyboardInput_WhenDownIsPressed_SelectsOnlyTheNextFocusedRow() {
            LoggerPanel panel = CreatePanelWithEntries("row-0", "row-1", "row-2", "row-3");

            try {
                InvokePrivate(panel, "HandleRowPressed", 1, false, false);

                AdvanceKeyboardFrame(new KeyboardState(Keys.Down));
                InvokePrivate(panel, "UpdateKeyboardInput");

                Assert.Equal(2, GetPrivateField<int>(panel, "FocusedRowIndex"));
                Assert.Equal(2, GetPrivateField<int>(panel, "AnchorRowIndex"));
                Assert.Equal(new[] { 2 }, GetPrivateField<HashSet<int>>(panel, "SelectedRowIndices").OrderBy(value => value));
            } finally {
                panel.Detach();
            }
        }

        /// <summary>
        /// Ensures Shift+Down extends the selected range from the current anchor.
        /// </summary>
        [Fact]
        public void UpdateKeyboardInput_WhenShiftDownIsPressed_ExtendsSelectionFromAnchor() {
            LoggerPanel panel = CreatePanelWithEntries("row-0", "row-1", "row-2", "row-3");

            try {
                InvokePrivate(panel, "HandleRowPressed", 1, false, false);

                AdvanceKeyboardFrame(new KeyboardState(Keys.LeftShift, Keys.Down));
                InvokePrivate(panel, "UpdateKeyboardInput");

                Assert.Equal(2, GetPrivateField<int>(panel, "FocusedRowIndex"));
                Assert.Equal(1, GetPrivateField<int>(panel, "AnchorRowIndex"));
                Assert.Equal(new[] { 1, 2 }, GetPrivateField<HashSet<int>>(panel, "SelectedRowIndices").OrderBy(value => value));
            } finally {
                panel.Detach();
            }
        }

        /// <summary>
        /// Ensures Control+Down moves focus while leaving the existing multi-selection unchanged.
        /// </summary>
        [Fact]
        public void UpdateKeyboardInput_WhenControlDownIsPressed_MovesFocusWithoutClearingSelection() {
            LoggerPanel panel = CreatePanelWithEntries("row-0", "row-1", "row-2", "row-3");

            try {
                InvokePrivate(panel, "HandleRowPressed", 1, false, false);
                InvokePrivate(panel, "HandleRowPressed", 3, true, false);

                AdvanceKeyboardFrame(new KeyboardState(Keys.LeftControl, Keys.Down));
                InvokePrivate(panel, "UpdateKeyboardInput");

                Assert.Equal(3, GetPrivateField<int>(panel, "FocusedRowIndex"));
                Assert.Equal(3, GetPrivateField<int>(panel, "AnchorRowIndex"));
                Assert.Equal(new[] { 1, 3 }, GetPrivateField<HashSet<int>>(panel, "SelectedRowIndices").OrderBy(value => value));
            } finally {
                panel.Detach();
            }
        }

        /// <summary>
        /// Ensures Control+Space toggles the focused row inside the selection set.
        /// </summary>
        [Fact]
        public void UpdateKeyboardInput_WhenControlSpaceIsPressed_TogglesTheFocusedRow() {
            LoggerPanel panel = CreatePanelWithEntries("row-0", "row-1", "row-2", "row-3");

            try {
                InvokePrivate(panel, "HandleRowPressed", 1, false, false);
                InvokePrivate(panel, "HandleRowPressed", 3, true, false);

                AdvanceKeyboardFrame(new KeyboardState(Keys.LeftControl, Keys.Space));
                InvokePrivate(panel, "UpdateKeyboardInput");

                Assert.Equal(3, GetPrivateField<int>(panel, "FocusedRowIndex"));
                Assert.Equal(3, GetPrivateField<int>(panel, "AnchorRowIndex"));
                Assert.Equal(new[] { 1 }, GetPrivateField<HashSet<int>>(panel, "SelectedRowIndices").OrderBy(value => value));
            } finally {
                panel.Detach();
            }
        }

        /// <summary>
        /// Ensures Control+C copies the focused row when no explicit selection remains.
        /// </summary>
        [Fact]
        public void UpdateKeyboardInput_WhenControlCIsPressedWithNoSelection_CopiesTheFocusedRow() {
            LoggerPanel panel = CreatePanelWithEntries("row-0", "row-1", "row-2", "row-3");
            TestTextClipboardService clipboardService = new TestTextClipboardService();
            Core.Instance.SetTextClipboardService(clipboardService);
            panel.SetInputServices(Core.Instance.Input, clipboardService);

            try {
                InvokePrivate(panel, "HandleRowPressed", 2, false, false);
                GetPrivateField<HashSet<int>>(panel, "SelectedRowIndices").Clear();
                List<LoggerPanelRow> rows = GetPrivateField<List<LoggerPanelRow>>(panel, "rows");

                AdvanceKeyboardFrame(new KeyboardState(Keys.LeftControl, Keys.C));
                InvokePrivate(panel, "UpdateKeyboardInput");

                Assert.Equal(rows[2].Label.Text, clipboardService.ReadText());
            } finally {
                panel.Detach();
            }
        }

        /// <summary>
        /// Ensures focused-row scrolling advances when keyboard focus moves past the visible logger viewport.
        /// </summary>
        [Fact]
        public void EnsureFocusedRowVisible_WhenFocusMovesPastVisibleWindow_AdjustsScrollOffset() {
            LoggerPanel panel = CreatePanelWithEntries("row-0", "row-1", "row-2", "row-3", "row-4", "row-5", "row-6");
            panel.MinSize = new int2(0, 0);
            panel.Size = new int2(320, 44);

            try {
                InvokePrivate(panel, "HandleRowPressed", 0, false, false);

                AdvanceKeyboardFrame(new KeyboardState(Keys.Down));
                InvokePrivate(panel, "UpdateKeyboardInput");
                AdvanceKeyboardFrame(new KeyboardState());
                AdvanceKeyboardFrame(new KeyboardState(Keys.Down));
                InvokePrivate(panel, "UpdateKeyboardInput");
                AdvanceKeyboardFrame(new KeyboardState());
                AdvanceKeyboardFrame(new KeyboardState(Keys.Down));
                InvokePrivate(panel, "UpdateKeyboardInput");

                Assert.Equal(3, GetPrivateField<int>(panel, "FocusedRowIndex"));
                Assert.True(GetPrivateField<int>(panel, "FirstVisibleRowIndex") > 0);
            } finally {
                panel.Detach();
            }
        }

        /// <summary>
        /// Ensures selected rows use the selected background tint during layout.
        /// </summary>
        [Fact]
        public void LayoutRows_WhenRowIsSelected_UsesSelectedBackgroundTint() {
            LoggerPanel panel = CreatePanelWithEntries("row-0", "row-1", "row-2");

            try {
                InvokePrivate(panel, "HandleRowPressed", 1, false, false);
                SetPrivateField(panel, "IsKeyboardFocused", false);
                InvokePrivate(panel, "LayoutRows");

                List<LoggerPanelRow> rows = GetPrivateField<List<LoggerPanelRow>>(panel, "rows");
                Assert.Equal(ThemeManager.Colors.AccentSecondary, rows[1].Background.Color);
            } finally {
                panel.Detach();
            }
        }

        /// <summary>
        /// Ensures the focused selected row uses a stronger focused tint than selection alone.
        /// </summary>
        [Fact]
        public void LayoutRows_WhenRowIsFocusedAndSelected_UsesFocusedSelectedTint() {
            LoggerPanel panel = CreatePanelWithEntries("row-0", "row-1", "row-2");

            try {
                InvokePrivate(panel, "HandleRowPressed", 1, false, false);
                SetPrivateField(panel, "IsKeyboardFocused", true);
                InvokePrivate(panel, "LayoutRows");

                List<LoggerPanelRow> rows = GetPrivateField<List<LoggerPanelRow>>(panel, "rows");
                Assert.Equal(ThemeManager.Colors.AccentPrimary, rows[1].Background.Color);
            } finally {
                panel.Detach();
            }
        }

        /// <summary>
        /// Ensures the logger lists entries newest-first so the most recent entry is the first visible row.
        /// </summary>
        [Fact]
        public void FlushPendingEntries_AfterLoggingAThenB_ShowsBInTheFirstVisibleRow() {
            LoggerPanel panel = CreatePanelWithEntries("entry-a", "entry-b");

            try {
                List<LoggerPanelRow> rows = GetPrivateField<List<LoggerPanelRow>>(panel, "rows");

                Assert.Equal(0, GetPrivateField<int>(panel, "FirstVisibleRowIndex"));
                Assert.True(rows[0].Entity.Position.Y < rows[1].Entity.Position.Y);
                Assert.EndsWith("entry-b", rows[0].Label.Text);
                Assert.EndsWith("entry-a", rows[1].Label.Text);
            } finally {
                panel.Detach();
            }
        }

        /// <summary>
        /// Ensures a view resting at the top stays pinned to the newest entry when more entries arrive.
        /// </summary>
        [Fact]
        public void AppendEntry_WhenViewIsAtTheTop_KeepsTheNewestEntryAsTheFirstVisibleRow() {
            LoggerPanel panel = CreatePanelWithEntries("row-0", "row-1", "row-2", "row-3", "row-4", "row-5", "row-6");
            panel.MinSize = new int2(0, 0);
            panel.Size = new int2(320, LoggerPanel.RowHeight * 2);

            try {
                Logger.WriteError("newest-error");
                panel.FlushPendingEntries();

                List<LoggerPanelRow> rows = GetPrivateField<List<LoggerPanelRow>>(panel, "rows");
                Assert.Equal(0, GetPrivateField<int>(panel, "FirstVisibleRowIndex"));
                Assert.EndsWith("newest-error", rows[0].Label.Text);
                Assert.True(rows[0].Entity.Enabled);
            } finally {
                panel.Detach();
            }
        }

        /// <summary>
        /// Ensures a view scrolled down to older entries keeps showing the same entry when a new entry arrives.
        /// </summary>
        [Fact]
        public void AppendEntry_WhenViewIsScrolledDown_KeepsTheSameEntryInView() {
            LoggerPanel panel = CreatePanelWithEntries("row-0", "row-1", "row-2", "row-3", "row-4", "row-5", "row-6");
            panel.MinSize = new int2(0, 0);
            panel.Size = new int2(320, LoggerPanel.RowHeight * 2);

            try {
                ScrollComponent scrollComponent = GetPrivateField<EditorScrollComponent>(panel, "ScrollComponent");
                Assert.True(scrollComponent.ScrollTo(2));
                List<LoggerPanelRow> rows = GetPrivateField<List<LoggerPanelRow>>(panel, "rows");
                string firstVisibleTextBefore = rows[2].Label.Text;

                Logger.WriteLine("late-entry");
                panel.FlushPendingEntries();

                int firstVisibleRowIndex = GetPrivateField<int>(panel, "FirstVisibleRowIndex");
                Assert.Equal(3, firstVisibleRowIndex);
                Assert.Equal(3, scrollComponent.ScrollOffset);
                Assert.Equal(firstVisibleTextBefore, rows[firstVisibleRowIndex].Label.Text);
                Assert.EndsWith("late-entry", rows[0].Label.Text);
            } finally {
                panel.Detach();
            }
        }

        /// <summary>
        /// Ensures one flushed batch of several entries scrolls a scrolled-down view by the batch size so the
        /// anchored entry stays the first visible row.
        /// </summary>
        [Fact]
        public void FlushPendingEntries_WhenBatchArrivesWhileScrolledDown_KeepsTheAnchoredEntryAsTheFirstVisibleRow() {
            LoggerPanel panel = CreatePanelWithEntries("row-0", "row-1", "row-2", "row-3", "row-4", "row-5", "row-6");
            panel.MinSize = new int2(0, 0);
            panel.Size = new int2(320, LoggerPanel.RowHeight * 2);

            try {
                ScrollComponent scrollComponent = GetPrivateField<EditorScrollComponent>(panel, "ScrollComponent");
                Assert.True(scrollComponent.ScrollTo(2));
                List<LoggerPanelRow> rows = GetPrivateField<List<LoggerPanelRow>>(panel, "rows");
                string anchoredTextBefore = rows[2].Label.Text;

                Logger.WriteLine("batch-0");
                Logger.WriteLine("batch-1");
                Logger.WriteLine("batch-2");
                panel.FlushPendingEntries();

                Assert.Equal(5, scrollComponent.ScrollOffset);
                Assert.Equal(5, GetPrivateField<int>(panel, "FirstVisibleRowIndex"));
                Assert.Equal(anchoredTextBefore, rows[5].Label.Text);
                Assert.True(rows[5].Entity.Enabled);
                Assert.EndsWith("batch-2", rows[0].Label.Text);
            } finally {
                panel.Detach();
            }
        }

        /// <summary>
        /// Ensures copying a multi-row selection writes entries chronologically (oldest first) even though the
        /// rows display newest-first.
        /// </summary>
        [Fact]
        public void CopySelection_WhenRowsDisplayNewestFirst_WritesSelectedEntriesInChronologicalOrder() {
            LoggerPanel panel = CreatePanelWithEntries("first-logged", "second-logged", "third-logged");

            try {
                InvokePrivate(panel, "HandleRowPressed", 0, false, false);
                InvokePrivate(panel, "HandleRowPressed", 2, false, true);

                InvokePrivate(panel, "CopySelection");

                string[] lines = ClipboardService.ReadText().Split(Environment.NewLine);
                Assert.Equal(3, lines.Length);
                Assert.EndsWith("first-logged", lines[0]);
                Assert.EndsWith("second-logged", lines[1]);
                Assert.EndsWith("third-logged", lines[2]);
            } finally {
                panel.Detach();
            }
        }

        /// <summary>
        /// Ensures an in-progress row press is cancelled when a new entry arrives, so a stale release cannot act on
        /// the different content now shown under the pointer.
        /// </summary>
        [Fact]
        public void FlushPendingEntries_WhenRowIsPressed_CancelsThePress() {
            LoggerPanel panel = CreatePanelWithEntries("row-0", "row-1", "row-2");

            try {
                List<LoggerPanelRow> rows = GetPrivateField<List<LoggerPanelRow>>(panel, "rows");
                rows[1].Interactable.OnCursor(new int2(4, 4), new int2(0, 0), PointerInteraction.Press);
                Assert.Equal(1, GetPrivateField<int>(panel, "PressedRowIndex"));

                Logger.WriteLine("interrupting-entry");
                panel.FlushPendingEntries();
                rows[1].Interactable.OnCursor(new int2(4, 4), new int2(0, 0), PointerInteraction.Release);

                Assert.Equal(-1, GetPrivateField<int>(panel, "PressedRowIndex"));
                Assert.Empty(GetPrivateField<HashSet<int>>(panel, "SelectedRowIndices"));
            } finally {
                panel.Detach();
            }
        }

        /// <summary>
        /// Ensures new entries shift selection, focus, and anchor so they keep tracking the same log entries.
        /// </summary>
        [Fact]
        public void AppendEntry_WhenNewEntryArrives_ShiftsSelectionFocusAndAnchorToTrackTheSameEntries() {
            LoggerPanel panel = CreatePanelWithEntries("row-0", "row-1", "row-2", "row-3");

            try {
                InvokePrivate(panel, "HandleRowPressed", 1, false, false);
                InvokePrivate(panel, "HandleRowPressed", 3, true, false);

                InvokePrivate(panel, "AppendEntry", new LogEntry(LogLevel.Info, "fresh", 0d));

                Assert.Equal(4, GetPrivateField<int>(panel, "FocusedRowIndex"));
                Assert.Equal(4, GetPrivateField<int>(panel, "AnchorRowIndex"));
                Assert.Equal(new[] { 2, 4 }, GetPrivateField<HashSet<int>>(panel, "SelectedRowIndices").OrderBy(value => value));
            } finally {
                panel.Detach();
            }
        }

        /// <summary>
        /// Ensures trimming the oldest entries from the bottom drops their selection and keeps surviving rows tracked.
        /// </summary>
        [Fact]
        public void AppendEntry_WhenOldestRowsAreTrimmed_DropsTrimmedSelectionAndKeepsSurvivingRowsTracked() {
            LoggerPanel panel = CreatePanelWithEntries("row-0", "row-1", "row-2", "row-3");

            try {
                // Newest-first display order is row-3, row-2, row-1, row-0; select row-3 (index 0) and row-0 (index 3).
                InvokePrivate(panel, "HandleRowPressed", 3, false, false);
                InvokePrivate(panel, "HandleRowPressed", 0, true, false);

                for (int entryIndex = 0; entryIndex < LoggerPanel.MaxEntries - 2; entryIndex++) {
                    InvokePrivate(panel, "AppendEntry", new LogEntry(LogLevel.Info, $"extra-{entryIndex}", entryIndex));
                }

                // row-0 and row-1 were trimmed; row-3 shifted from index 0 to index 254.
                Assert.Equal(254, GetPrivateField<int>(panel, "FocusedRowIndex"));
                Assert.Equal(254, GetPrivateField<int>(panel, "AnchorRowIndex"));
                Assert.Equal(new[] { 254 }, GetPrivateField<HashSet<int>>(panel, "SelectedRowIndices").OrderBy(value => value));
            } finally {
                panel.Detach();
            }
        }

        /// <summary>
        /// Ensures focus clamps to the nearest remaining (oldest) row when trimming removes the previously focused row.
        /// </summary>
        [Fact]
        public void AppendEntry_WhenTrimRemovesFocusedRow_ClampsFocusToTheNearestRemainingRow() {
            LoggerPanel panel = CreatePanelWithEntries("row-0", "row-1");

            try {
                // Newest-first display order is row-1, row-0; focus the oldest entry.
                InvokePrivate(panel, "HandleRowPressed", 1, false, false);

                for (int entryIndex = 0; entryIndex < LoggerPanel.MaxEntries - 1; entryIndex++) {
                    InvokePrivate(panel, "AppendEntry", new LogEntry(LogLevel.Info, $"trim-{entryIndex}", entryIndex));
                }

                Assert.Equal(LoggerPanel.MaxEntries - 1, GetPrivateField<int>(panel, "FocusedRowIndex"));
                Assert.Equal(LoggerPanel.MaxEntries - 1, GetPrivateField<int>(panel, "AnchorRowIndex"));
                Assert.Empty(GetPrivateField<HashSet<int>>(panel, "SelectedRowIndices"));
            } finally {
                panel.Detach();
            }
        }

        /// <summary>
        /// Reads one non-public instance field and casts it to the requested type.
        /// </summary>
        /// <typeparam name="T">Expected field type.</typeparam>
        /// <param name="target">Object that owns the field.</param>
        /// <param name="fieldName">Name of the field to read.</param>
        /// <returns>Field value cast to the requested type.</returns>
        T GetPrivateField<T>(object target, string fieldName) {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            return Assert.IsType<T>(field.GetValue(target));
        }

        /// <summary>
        /// Invokes one non-public instance method with the supplied arguments.
        /// </summary>
        /// <param name="target">Object that owns the method.</param>
        /// <param name="methodName">Name of the method to invoke.</param>
        /// <param name="arguments">Arguments passed to the invoked method.</param>
        static void InvokePrivate(object target, string methodName, params object[] arguments) {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(method);
            method.Invoke(target, arguments);
        }

        /// <summary>
        /// Writes one non-public instance field.
        /// </summary>
        /// <param name="target">Object that owns the field.</param>
        /// <param name="fieldName">Name of the field to write.</param>
        /// <param name="value">Value to assign.</param>
        static void SetPrivateField(object target, string fieldName, object value) {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field);
            field.SetValue(target, value);
        }

        /// <summary>
        /// Advances one keyboard frame through the active deterministic input backend.
        /// </summary>
        /// <param name="keyboardState">Keyboard state to expose for the frame.</param>
        void AdvanceKeyboardFrame(KeyboardState keyboardState) {
            InputBackend.SetKeyboardState(keyboardState);
            InputBackend.EarlyUpdate();
            InputBackend.Update();
        }

        /// <summary>
        /// Creates one logger panel populated with the supplied entry messages.
        /// </summary>
        /// <param name="messages">Messages to enqueue into the logger panel.</param>
        /// <returns>Populated logger panel.</returns>
        LoggerPanel CreatePanelWithEntries(params string[] messages) {
            LoggerPanel panel = new LoggerPanel(Core.Instance, new helengine.editor.EditorSessionInteractionServices(), CreateFont()) {
                Size = new int2(320, 240)
            };
            panel.SetInputServices(Core.Instance.Input, ClipboardService);

            for (int messageIndex = 0; messageIndex < messages.Length; messageIndex++) {
                Logger.WriteLine(messages[messageIndex]);
            }

            panel.FlushPendingEntries();
            return panel;
        }

        /// <summary>
        /// Creates a small font asset that can satisfy dock-row layout requirements.
        /// </summary>
        /// <returns>Font asset with basic glyph metrics for the current test.</returns>
        FontAsset CreateFont() {
            Dictionary<char, FontChar> characters = new Dictionary<char, FontChar> {
                ['S'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['c'] = new FontChar(new float4(0f, 0f, 7f, 12f), 0f, 7f, 0f, 0f),
                ['a'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['l'] = new FontChar(new float4(0f, 0f, 4f, 12f), 0f, 4f, 0f, 0f),
                ['e'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['d'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['g'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['r'] = new FontChar(new float4(0f, 0f, 6f, 12f), 0f, 6f, 0f, 0f),
                ['o'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['y'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['n'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['t'] = new FontChar(new float4(0f, 0f, 5f, 12f), 0f, 5f, 0f, 0f),
                ['i'] = new FontChar(new float4(0f, 0f, 3f, 12f), 0f, 3f, 0f, 0f)
            };

            return new FontAsset(
                new FontInfo("Test", 16, 4f),
                new TestRuntimeTexture {
                    Width = 64,
                    Height = 64
                },
                characters,
                16f,
                64,
                64);
        }
    }
}

