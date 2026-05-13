using System.Reflection;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies tab-strip visuals use the explicit panel render-order stack.
    /// </summary>
    public class DockTabStripTests : IDisposable {
        /// <summary>
        /// Ensures tab backgrounds and labels use the explicit interactive panel band.
        /// </summary>
        [Fact]
        public void UpdateTabs_UsesExplicitInteractivePanelRenderOrders() {
            InitializeCore();

            FontAsset font = CreateFont();
            DockTabStrip strip = new DockTabStrip(font, HandleTabSelected);
            DockableEntity first = new DockableEntity(font);
            DockableEntity second = new DockableEntity(font);

            first.Title = "First";
            second.Title = "Second";

            strip.UpdateTabs(
                new[] { first, second },
                0,
                float3.Zero,
                640,
                0b1000000000000000);

            List<DockTabEntry> tabs = GetPrivateField<List<DockTabEntry>>(strip, "tabs");

            Assert.Equal(2, tabs.Count);
            Assert.Equal(RenderOrder2D.PanelInteractive, tabs[0].Background.RenderOrder2D);
            Assert.True(tabs[0].Label.RenderOrder2D > tabs[0].Background.RenderOrder2D);
        }

        /// <summary>
        /// Ensures a tab strip reuses the active dock's updated font and stays exactly one pixel shorter than the scaled title bar after one live UI scale change.
        /// </summary>
        [Fact]
        public void UpdateTabs_WhenDockMetricsChange_UsesUpdatedFontAndTitleBarRelativeTabHeight() {
            InitializeCore();

            FontAsset initialFont = CreateFont(16f);
            FontAsset scaledFont = CreateFont(24f);
            EditorUiMetrics initialMetrics = new EditorUiMetrics(1d);
            EditorUiMetrics scaledMetrics = new EditorUiMetrics(1.5d);
            DockTabStrip strip = new DockTabStrip(initialFont, initialMetrics, HandleTabSelected);
            DockableEntity first = new DockableEntity(initialFont, initialMetrics);
            DockableEntity second = new DockableEntity(initialFont, initialMetrics);

            first.Title = "First";
            second.Title = "Second";

            strip.UpdateTabs(
                new[] { first, second },
                0,
                float3.Zero,
                640,
                0b1000000000000000);

            first.ApplyUiMetrics(scaledFont, scaledMetrics);
            second.ApplyUiMetrics(scaledFont, scaledMetrics);
            strip.UpdateTabs(
                new[] { first, second },
                0,
                float3.Zero,
                640,
                0b1000000000000000);

            List<DockTabEntry> tabs = GetPrivateField<List<DockTabEntry>>(strip, "tabs");

            Assert.Same(scaledFont, tabs[0].Label.Font);
            Assert.Equal(scaledMetrics.DockTitleBarHeight - 1, tabs[0].Background.Size.Y);
        }

        /// <summary>
        /// Clears shared keyboard-focus state after each test.
        /// </summary>
        public void Dispose() {
            EditorKeyboardFocusService.Reset();
        }

        /// <summary>
        /// Initializes the core services required for dock-tab tests.
        /// </summary>
        void InitializeCore() {
            Core core = new Core();
            core.Initialize(null, new TestRenderManager2D(), null, new PlatformInfo("test", "test-version"));
            EditorKeyboardFocusService.Reset();
        }

        /// <summary>
        /// Creates a deterministic font asset for dock-tab layout tests.
        /// </summary>
        /// <returns>Font asset used by the tab strip created in this test.</returns>
        FontAsset CreateFont() {
            return CreateFont(16f);
        }

        /// <summary>
        /// Creates a deterministic font asset for dock-tab layout tests using the provided line height.
        /// </summary>
        /// <param name="lineHeight">Line height exposed by the created font.</param>
        /// <returns>Font asset used by the tab strip created in this test.</returns>
        FontAsset CreateFont(float lineHeight) {
            Dictionary<char, FontChar> characters = new Dictionary<char, FontChar> {
                ['F'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['S'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['c'] = new FontChar(new float4(0f, 0f, 7f, 12f), 0f, 7f, 0f, 0f),
                ['d'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['e'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['i'] = new FontChar(new float4(0f, 0f, 3f, 12f), 0f, 3f, 0f, 0f),
                ['n'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['o'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['r'] = new FontChar(new float4(0f, 0f, 6f, 12f), 0f, 6f, 0f, 0f),
                ['s'] = new FontChar(new float4(0f, 0f, 7f, 12f), 0f, 7f, 0f, 0f),
                ['t'] = new FontChar(new float4(0f, 0f, 5f, 12f), 0f, 5f, 0f, 0f)
            };

            return new FontAsset(
                new FontInfo("Test", 16, 4f),
                new TestRuntimeTexture {
                    Width = 64,
                    Height = 64
                },
                characters,
                lineHeight,
                64,
                64);
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
            if (field == null) {
                throw new InvalidOperationException("Expected private field was not found.");
            }

            object value = field.GetValue(target);
            return Assert.IsType<T>(value);
        }

        /// <summary>
        /// Ignores tab-selection callbacks because this test only verifies render-order assignment.
        /// </summary>
        /// <param name="index">Index of the tab selected by the strip.</param>
        void HandleTabSelected(int index) {
        }
    }
}
