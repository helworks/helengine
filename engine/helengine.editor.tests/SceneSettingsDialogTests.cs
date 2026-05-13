using System.Reflection;
using helengine.editor;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies scene-settings dialog field wiring and presentation state.
    /// </summary>
    public class SceneSettingsDialogTests {
        /// <summary>
        /// Ensures both scene-canvas textboxes use the modal dialog render orders so they remain visible above the shared panel chrome.
        /// </summary>
        [Fact]
        public void Constructor_WhenDialogIsCreated_ConfiguresCanvasFieldsWithModalRenderOrders() {
            InitializeCore();
            SceneSettingsDialog dialog = new SceneSettingsDialog(CreateFont(), EditorUiMetrics.Default);

            byte dialogPanelOrder = GetNonPublicProperty<byte>(dialog, "DialogPanelOrder");
            byte dialogTextOrder = GetNonPublicProperty<byte>(dialog, "DialogTextOrder");
            TextBoxComponent canvasWidthField = GetNonPublicField<TextBoxComponent>(dialog, "CanvasWidthField");
            TextBoxComponent canvasHeightField = GetNonPublicField<TextBoxComponent>(dialog, "CanvasHeightField");

            AssertTextBoxRenderOrders(canvasWidthField, dialogPanelOrder, dialogTextOrder);
            AssertTextBoxRenderOrders(canvasHeightField, dialogPanelOrder, dialogTextOrder);
        }

        /// <summary>
        /// Initializes the shared core services required by editor-entity dialog tests.
        /// </summary>
        void InitializeCore() {
            Core core = new Core();
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null, new PlatformInfo("test", "test-version"));
        }

        /// <summary>
        /// Reads one non-public instance field and casts it to the requested type.
        /// </summary>
        /// <typeparam name="T">Expected field type.</typeparam>
        /// <param name="target">Object that owns the field.</param>
        /// <param name="fieldName">Name of the field to read.</param>
        /// <returns>Field value cast to the requested type.</returns>
        T GetNonPublicField<T>(object target, string fieldName) {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            return Assert.IsType<T>(field.GetValue(target));
        }

        /// <summary>
        /// Reads one inherited non-public property and casts it to the requested type.
        /// </summary>
        /// <typeparam name="T">Expected property type.</typeparam>
        /// <param name="target">Object that owns the property.</param>
        /// <param name="propertyName">Name of the property to read.</param>
        /// <returns>Property value cast to the requested type.</returns>
        T GetNonPublicProperty<T>(object target, string propertyName) {
            PropertyInfo property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.NonPublic);
            return Assert.IsType<T>(property.GetValue(target));
        }

        /// <summary>
        /// Ensures one textbox background and text layers match the owning modal dialog shell.
        /// </summary>
        /// <param name="textBox">Textbox that should use modal render orders.</param>
        /// <param name="expectedBackgroundOrder">Expected render order for the textbox background.</param>
        /// <param name="expectedTextOrder">Expected render order for the textbox text.</param>
        void AssertTextBoxRenderOrders(TextBoxComponent textBox, byte expectedBackgroundOrder, byte expectedTextOrder) {
            RoundedRectComponent backgroundSprite = GetNonPublicField<RoundedRectComponent>(textBox, "backgroundSprite");
            TextComponent textComponent = GetNonPublicField<TextComponent>(textBox, "textComponent");

            Assert.Equal(expectedBackgroundOrder, backgroundSprite.RenderOrder2D);
            Assert.Equal(expectedTextOrder, textComponent.RenderOrder2D);
        }

        /// <summary>
        /// Creates a minimal font asset suitable for dialog and text-box layout in unit tests.
        /// </summary>
        /// <returns>Font asset with basic glyph metrics.</returns>
        FontAsset CreateFont() {
            Dictionary<char, FontChar> characters = new Dictionary<char, FontChar> {
                ['0'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['1'] = new FontChar(new float4(0f, 0f, 6f, 12f), 0f, 6f, 0f, 0f),
                ['2'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['7'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['8'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['C'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['a'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['n'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['v'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['s'] = new FontChar(new float4(0f, 0f, 7f, 12f), 0f, 7f, 0f, 0f),
                ['W'] = new FontChar(new float4(0f, 0f, 12f, 12f), 0f, 12f, 0f, 0f),
                ['i'] = new FontChar(new float4(0f, 0f, 4f, 12f), 0f, 4f, 0f, 0f),
                ['d'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['t'] = new FontChar(new float4(0f, 0f, 5f, 12f), 0f, 5f, 0f, 0f),
                ['h'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['H'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['e'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['g'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f)
            };

            return new FontAsset(
                new FontInfo("Dialog", 14, 4f),
                new TestRuntimeTexture {
                    Width = 64,
                    Height = 64
                },
                characters,
                14f,
                64,
                64);
        }
    }
}
