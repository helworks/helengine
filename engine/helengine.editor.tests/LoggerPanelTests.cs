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
        /// Initializes the core services required by the logger panel tests.
        /// </summary>
        public LoggerPanelTests() {
            TempRootPath = Path.Combine(Path.GetTempPath(), "helengine-logger-panel-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(TempRootPath);

            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = TempRootPath
            });
            core.Initialize(null, new TestRenderManager2D(), null);
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
            LoggerPanel panel = new LoggerPanel(CreateFont(), metrics) {
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
        /// Creates one logger panel populated with the supplied entry messages.
        /// </summary>
        /// <param name="messages">Messages to enqueue into the logger panel.</param>
        /// <returns>Populated logger panel.</returns>
        LoggerPanel CreatePanelWithEntries(params string[] messages) {
            LoggerPanel panel = new LoggerPanel(CreateFont()) {
                Size = new int2(320, 240)
            };

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
