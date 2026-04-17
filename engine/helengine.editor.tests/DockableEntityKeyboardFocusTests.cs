using System.Reflection;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies keyboard-focus visuals for dock surfaces.
    /// </summary>
    public class DockableEntityKeyboardFocusTests {
        /// <summary>
        /// Ensures activating a dock focus group shows the accent outline even while the dock remains docked.
        /// </summary>
        [Fact]
        public void SetGroupActive_WhenTrue_ShowsTheDockOutline() {
            InitializeCore();
            DockLayoutEngine layout = new DockLayoutEngine();
            DockableEntity dock = new DockableEntity(CreateFont());
            layout.DockAsRoot(dock);

            RoundedRectComponent panelOutline = GetPrivateField<RoundedRectComponent>(dock, "panelOutline");

            Assert.Equal(0f, panelOutline.BorderThickness);

            dock.SetGroupActive(true);

            Assert.Equal(1f, panelOutline.BorderThickness);
            Assert.Equal(ThemeManager.Colors.AccentPrimary, panelOutline.BorderColor);

            dock.SetGroupActive(false);

            Assert.Equal(0f, panelOutline.BorderThickness);
        }

        /// <summary>
        /// Initializes the core services required by dock keyboard-focus tests.
        /// </summary>
        void InitializeCore() {
            Core core = new Core();
            core.Initialize(null, new TestRenderManager2D(), null);
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
                ['k'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f)
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
