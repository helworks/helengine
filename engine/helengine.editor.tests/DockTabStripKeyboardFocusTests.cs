using System.Reflection;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies keyboard-focus behavior for dock tab strips.
    /// </summary>
    public class DockTabStripKeyboardFocusTests : IDisposable {
        /// <summary>
        /// Ensures visible tabs create persistent focus targets that are rebound to the active dock group.
        /// </summary>
        [Fact]
        public void DockTabStrip_WhenUpdated_CreatesPersistentFocusTargetsForVisibleTabs() {
            InitializeCore();
            DockTabStrip strip = new DockTabStrip(CreateFont(), HandleTabSelected);
            DockableEntity first = CreateDock("First");
            DockableEntity second = CreateDock("Second");

            strip.UpdateTabs(
                new[] { first, second },
                0,
                float3.Zero,
                640,
                0b1000000000000000);

            List<DockTabEntry> tabs = GetPrivateField<List<DockTabEntry>>(strip, "tabs");
            EditorFocusTarget firstTarget = tabs[0].FocusTarget;
            EditorFocusTarget secondTarget = tabs[1].FocusTarget;

            Assert.NotNull(firstTarget);
            Assert.NotNull(secondTarget);

            strip.UpdateTabs(
                new[] { first, second },
                1,
                new float3(16f, 8f, 0f),
                640,
                0b1000000000000000);

            tabs = GetPrivateField<List<DockTabEntry>>(strip, "tabs");

            Assert.Same(firstTarget, tabs[0].FocusTarget);
            Assert.Same(secondTarget, tabs[1].FocusTarget);
            Assert.Same(second, tabs[0].FocusTarget.FocusGroup);
            Assert.Same(second, tabs[1].FocusTarget.FocusGroup);
        }

        /// <summary>
        /// Ensures Enter on a focused tab activates the same tab-selection path used by pointer release.
        /// </summary>
        [Fact]
        public void DockTabStrip_WhenEnterIsPressedOnFocusedTab_SelectsThatTab() {
            InitializeCore();
            int selectedIndex = -1;
            DockTabStrip strip = new DockTabStrip(CreateFont(), index => selectedIndex = index);
            DockableEntity first = CreateDock("First");
            DockableEntity second = CreateDock("Second");

            strip.UpdateTabs(
                new[] { first, second },
                0,
                float3.Zero,
                640,
                0b1000000000000000);

            List<DockTabEntry> tabs = GetPrivateField<List<DockTabEntry>>(strip, "tabs");

            tabs[1].FocusTarget.SetTargetFocused(true);
            tabs[1].FocusTarget.ActivateFromKey(Keys.Enter);

            Assert.True(tabs[1].IsKeyboardFocused);
            Assert.Equal(1, selectedIndex);
        }

        /// <summary>
        /// Clears shared keyboard-focus state after each test.
        /// </summary>
        public void Dispose() {
            EditorKeyboardFocusService.Reset();
        }

        /// <summary>
        /// Initializes the core services required for dock-tab keyboard-focus tests.
        /// </summary>
        void InitializeCore() {
            Core core = new Core();
            core.Initialize(null, new TestRenderManager2D(), null);
            EditorKeyboardFocusService.Reset();
        }

        /// <summary>
        /// Creates a dockable entity with the supplied title.
        /// </summary>
        /// <param name="title">Title shown by the dock.</param>
        /// <returns>Configured dockable entity.</returns>
        DockableEntity CreateDock(string title) {
            DockableEntity dock = new DockableEntity(CreateFont());
            dock.Title = title;
            return dock;
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
        /// Ignores tab-selection callbacks when the current test does not inspect them directly.
        /// </summary>
        /// <param name="index">Index selected by the strip.</param>
        void HandleTabSelected(int index) {
        }

        /// <summary>
        /// Creates a deterministic font asset for dock-tab layout tests.
        /// </summary>
        /// <returns>Font asset used by the tab strip created in this test.</returns>
        FontAsset CreateFont() {
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
                16f,
                64,
                64);
        }
    }
}
