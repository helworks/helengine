using System.Reflection;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests.managers.dock {
    /// <summary>
    /// Verifies keyboard-focus traversal and lifetime behavior exposed by the dock layout engine.
    /// </summary>
    public class DockLayoutEngineKeyboardFocusTests : IDisposable {
        /// <summary>
        /// Ensures traversal order follows visible leaf panels and excludes hidden sibling tabs.
        /// </summary>
        [Fact]
        public void GetVisibleDockablesInTraversalOrder_WhenTabsAndSplitsExist_ReturnsVisibleActiveDockablesInLeafOrder() {
            InitializeCore();
            DockLayoutEngine layout = new DockLayoutEngine();
            DockableEntity left = CreateDock("Left");
            DockableEntity top = CreateDock("Top");
            DockableEntity topReplacement = CreateDock("TopReplacement");
            DockableEntity bottom = CreateDock("Bottom");

            layout.DockAsRoot(left);
            layout.DockRelative(top, left, DockInsertDirection.Right);
            layout.DockRelative(bottom, top, DockInsertDirection.Bottom);
            layout.DockRelative(topReplacement, top, DockInsertDirection.Fill);

            IReadOnlyList<DockableEntity> dockables = layout.GetVisibleDockablesInTraversalOrder();

            Assert.Equal(3, dockables.Count);
            Assert.Same(left, dockables[0]);
            Assert.Same(topReplacement, dockables[1]);
            Assert.Same(bottom, dockables[2]);
            Assert.DoesNotContain(top, dockables);
        }

        /// <summary>
        /// Ensures removing one tab from a tabbed panel unregisters the strip's keyboard-focus targets.
        /// </summary>
        [Fact]
        public void DockLayoutEngine_WhenTabbedPanelIsRemoved_DisposesTabFocusTargets() {
            InitializeCore();
            DockLayoutEngine layout = new DockLayoutEngine();
            DockableEntity first = CreateDock("First");
            DockableEntity second = CreateDock("Second");

            layout.DockAsRoot(first);
            layout.DockRelative(second, first, DockInsertDirection.Fill);
            layout.Layout(new int2(800, 600));

            Assert.True(GetRegisteredTargetCount() > 0);

            layout.Remove(second);

            Assert.Equal(0, GetRegisteredTargetCount());
        }

        /// <summary>
        /// Ensures tabbed dock strips stay pinned one pixel below the title-bar top and remain one pixel shorter than the scaled title bar.
        /// </summary>
        [Fact]
        public void Layout_WhenTabbedPanelUsesScaledMetrics_PinsTabStripToOnePixelSeparator() {
            InitializeCore();
            EditorUiMetrics scaledMetrics = new EditorUiMetrics(1.5d);
            DockLayoutEngine layout = new DockLayoutEngine();
            DockableEntity first = CreateDock("First", scaledMetrics);
            DockableEntity second = CreateDock("Second", scaledMetrics);

            layout.DockAsRoot(first);
            layout.DockRelative(second, first, DockInsertDirection.Fill);
            layout.Layout(new int2(800, 600));

            object rootNode = GetPrivateField<object>(layout, "root");
            object tabStrip = GetPrivateField<object>(rootNode, "tabStrip");
            float3 stripPosition = Assert.IsType<float3>(GetPublicPropertyValue(tabStrip, "Position"));
            List<DockTabEntry> tabs = GetPrivateField<List<DockTabEntry>>(tabStrip, "tabs");

            Assert.Equal(first.Position.Y + 1f, stripPosition.Y, 3);
            Assert.Equal(first.TitleBarHeightPixels - 1, tabs[0].Background.Size.Y);
        }

        /// <summary>
        /// Clears shared keyboard-focus state after each test.
        /// </summary>
        public void Dispose() {
            EditorKeyboardFocusService.Reset();
        }

        /// <summary>
        /// Initializes the core services required by dock-layout keyboard-focus tests.
        /// </summary>
        void InitializeCore() {
            Core core = new Core();
            core.Initialize(null, new TestRenderManager2D(), null, new PlatformInfo("test", "test-version"));
            EditorKeyboardFocusService.Reset();
        }

        /// <summary>
        /// Creates a dockable entity with the supplied title.
        /// </summary>
        /// <param name="title">Title shown by the dock.</param>
        /// <returns>Configured dockable entity.</returns>
        DockableEntity CreateDock(string title) {
            return CreateDock(title, EditorUiMetrics.Default);
        }

        /// <summary>
        /// Creates a dockable entity with the supplied title and scaled metrics.
        /// </summary>
        /// <param name="title">Title shown by the dock.</param>
        /// <param name="metrics">Scaled dock metrics applied to the entity.</param>
        /// <returns>Configured dockable entity.</returns>
        DockableEntity CreateDock(string title, EditorUiMetrics metrics) {
            DockableEntity dock = new DockableEntity(CreateFont(), metrics);
            dock.Title = title;
            return dock;
        }

        /// <summary>
        /// Reads the number of currently registered keyboard-focus targets.
        /// </summary>
        /// <returns>Registered focus-target count.</returns>
        int GetRegisteredTargetCount() {
            FieldInfo field = typeof(EditorKeyboardFocusService).GetField("RegisteredTargets", BindingFlags.Static | BindingFlags.NonPublic);
            if (field == null) {
                throw new InvalidOperationException("Expected RegisteredTargets field was not found.");
            }

            object value = field.GetValue(null);
            List<IFocusTarget> targets = Assert.IsType<List<IFocusTarget>>(value);
            return targets.Count;
        }

        /// <summary>
        /// Reads one private instance field and returns its raw value.
        /// </summary>
        /// <typeparam name="T">Requested field value type.</typeparam>
        /// <param name="target">Object containing the field.</param>
        /// <param name="fieldName">Exact field name to read.</param>
        /// <returns>Field value cast to the requested type.</returns>
        T GetPrivateField<T>(object target, string fieldName) {
            if (target == null) {
                throw new ArgumentNullException(nameof(target));
            }

            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null) {
                throw new InvalidOperationException("Expected private field was not found.");
            }

            object value = field.GetValue(target);
            return Assert.IsAssignableFrom<T>(value);
        }

        /// <summary>
        /// Reads one public property value from the provided instance.
        /// </summary>
        /// <param name="target">Object containing the property.</param>
        /// <param name="propertyName">Exact property name to read.</param>
        /// <returns>Raw property value.</returns>
        object GetPublicPropertyValue(object target, string propertyName) {
            if (target == null) {
                throw new ArgumentNullException(nameof(target));
            }

            PropertyInfo property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            if (property == null) {
                throw new InvalidOperationException("Expected property was not found.");
            }

            return property.GetValue(target);
        }

        /// <summary>
        /// Creates a deterministic font asset for dock and tab layout.
        /// </summary>
        /// <returns>Font asset with basic glyph metrics.</returns>
        FontAsset CreateFont() {
            Dictionary<char, FontChar> characters = new Dictionary<char, FontChar> {
                ['B'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['F'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['L'] = new FontChar(new float4(0f, 0f, 7f, 12f), 0f, 7f, 0f, 0f),
                ['R'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['S'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['T'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['a'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['c'] = new FontChar(new float4(0f, 0f, 7f, 12f), 0f, 7f, 0f, 0f),
                ['d'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['e'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['f'] = new FontChar(new float4(0f, 0f, 5f, 12f), 0f, 5f, 0f, 0f),
                ['g'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['h'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['i'] = new FontChar(new float4(0f, 0f, 3f, 12f), 0f, 3f, 0f, 0f),
                ['l'] = new FontChar(new float4(0f, 0f, 4f, 12f), 0f, 4f, 0f, 0f),
                ['m'] = new FontChar(new float4(0f, 0f, 10f, 12f), 0f, 10f, 0f, 0f),
                ['n'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['o'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['p'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['r'] = new FontChar(new float4(0f, 0f, 6f, 12f), 0f, 6f, 0f, 0f),
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
