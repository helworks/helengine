using System.Reflection;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies per-panel title-bar menu behavior on dockable entities.
    /// </summary>
    public sealed class DockableEntityPanelMenuTests {
        /// <summary>
        /// Ensures the dock constructor creates one panel menu button inside the title bar.
        /// </summary>
        [Fact]
        public void Constructor_CreatesPanelMenuButtonInsideTitleBar() {
            InitializeCore();
            DockableEntity dock = new DockableEntity(CreateFont());

            EditorEntity panelMenuButtonEntity = GetPrivateField<EditorEntity>(dock, "PanelMenuButtonEntity");

            Assert.NotNull(panelMenuButtonEntity);
        }

        /// <summary>
        /// Ensures activating the Close panel-menu action raises the close request event.
        /// </summary>
        [Fact]
        public void ActivatePanelMenuActionForTest_WhenCloseIsRequested_RaisesCloseRequested() {
            InitializeCore();
            DockableEntity dock = new DockableEntity(CreateFont());
            bool raised = false;
            dock.CloseRequested += () => raised = true;

            dock.ActivatePanelMenuActionForTest(DockableEntityPanelMenuAction.Close);

            Assert.True(raised);
        }

        /// <summary>
        /// Initializes the core services required by dockable panel-menu tests.
        /// </summary>
        void InitializeCore() {
            Core core = new Core();
            core.Initialize(null, new TestRenderManager2D(), null, new PlatformInfo("test", "test-version"));
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
        /// Creates a deterministic font asset for dock title layout.
        /// </summary>
        /// <returns>Font asset with basic glyph metrics.</returns>
        FontAsset CreateFont() {
            Dictionary<char, FontChar> characters = new Dictionary<char, FontChar> {
                ['D'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['o'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['c'] = new FontChar(new float4(0f, 0f, 7f, 12f), 0f, 7f, 0f, 0f),
                ['k'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['C'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['l'] = new FontChar(new float4(0f, 0f, 4f, 12f), 0f, 4f, 0f, 0f),
                ['s'] = new FontChar(new float4(0f, 0f, 7f, 12f), 0f, 7f, 0f, 0f),
                ['e'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f)
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
