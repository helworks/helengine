using System.Reflection;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies text selection behavior for the runtime text component.
    /// </summary>
    public class TextComponentSelectionTests {
        /// <summary>
        /// Ensures the selection flag exists and starts disabled so existing labels keep their current behavior.
        /// </summary>
        [Fact]
        public void TextComponent_SelectionEnabled_DefaultsToFalse() {
            TextComponent textComponent = new TextComponent();

            Assert.False(GetPropertyValue<bool>(textComponent, "SelectionEnabled"));
        }

        /// <summary>
        /// Ensures dragging across a selectable text component creates a non-empty selection range.
        /// </summary>
        [Fact]
        public void TextComponent_WhenSelectionIsEnabledAndDragged_CreatesASelectionRange() {
            InitializeCore();
            EditorEntity entity = new EditorEntity();
            TextComponent textComponent = new TextComponent {
                Font = CreateFont(),
                Text = "Name"
            };

            SetPropertyValue(textComponent, "SelectionEnabled", true);
            entity.AddComponent(textComponent);

            ((TestInputBackend)Core.Instance.InputSystem.Backend).SetMouseState(new MouseState(
                10,
                10,
                0,
                ButtonState.Pressed,
                ButtonState.Released,
                ButtonState.Released,
                ButtonState.Released,
                ButtonState.Released));
            Core.Instance.Update();

            ((TestInputBackend)Core.Instance.InputSystem.Backend).SetMouseState(new MouseState(
                42,
                10,
                0,
                ButtonState.Pressed,
                ButtonState.Released,
                ButtonState.Released,
                ButtonState.Released,
                ButtonState.Released));
            Core.Instance.Update();

            Assert.True(GetPropertyValue<bool>(textComponent, "HasSelection"));
            Assert.Equal(1, GetPropertyValue<int>(textComponent, "SelectionStart"));
            Assert.Equal(4, GetPropertyValue<int>(textComponent, "SelectionEnd"));
        }

        /// <summary>
        /// Ensures keyboard selection follows the same caret-based model used by text boxes.
        /// </summary>
        [Fact]
        public void TextComponent_WhenSelectionIsEnabled_ShiftArrowExtendsTheSelection() {
            InitializeCore();
            EditorEntity entity = new EditorEntity();
            TextComponent textComponent = new TextComponent {
                Font = CreateFont(),
                Text = "Name"
            };

            SetPropertyValue(textComponent, "SelectionEnabled", true);
            entity.AddComponent(textComponent);

            ((TestInputBackend)Core.Instance.InputSystem.Backend).SetMouseState(new MouseState(
                10,
                10,
                0,
                ButtonState.Pressed,
                ButtonState.Released,
                ButtonState.Released,
                ButtonState.Released,
                ButtonState.Released));
            Core.Instance.Update();

            ((TestInputBackend)Core.Instance.InputSystem.Backend).SetMouseState(new MouseState(
                10,
                10,
                0,
                ButtonState.Released,
                ButtonState.Released,
                ButtonState.Released,
                ButtonState.Released,
                ButtonState.Released));
            Core.Instance.Update();

            ((TestInputBackend)Core.Instance.InputSystem.Backend).SetKeyboardState(new KeyboardState(Keys.RightShift, Keys.Right));
            Core.Instance.Update();

            Assert.True(GetPropertyValue<bool>(textComponent, "HasSelection"));
            Assert.Equal(1, GetPropertyValue<int>(textComponent, "SelectionStart"));
            Assert.Equal(2, GetPropertyValue<int>(textComponent, "SelectionEnd"));
        }

        /// <summary>
        /// Ensures pointer hit testing resolves caret positions relative to the text host instead of raw screen coordinates.
        /// </summary>
        [Fact]
        public void TextComponent_WhenParentHasScreenOffset_DragSelectionUsesLocalTextCoordinates() {
            InitializeCore();
            EditorEntity entity = new EditorEntity {
                Position = new float3(120f, 0f, 0f)
            };
            TextComponent textComponent = new TextComponent {
                Font = CreateFont(),
                Text = "Name"
            };

            SetPropertyValue(textComponent, "SelectionEnabled", true);
            entity.AddComponent(textComponent);

            ((TestInputBackend)Core.Instance.InputSystem.Backend).SetMouseState(new MouseState(
                130,
                10,
                0,
                ButtonState.Pressed,
                ButtonState.Released,
                ButtonState.Released,
                ButtonState.Released,
                ButtonState.Released));
            Core.Instance.Update();

            ((TestInputBackend)Core.Instance.InputSystem.Backend).SetMouseState(new MouseState(
                146,
                10,
                0,
                ButtonState.Pressed,
                ButtonState.Released,
                ButtonState.Released,
                ButtonState.Released,
                ButtonState.Released));
            Core.Instance.Update();

            Assert.True(GetPropertyValue<bool>(textComponent, "HasSelection"));
            Assert.Equal(1, GetPropertyValue<int>(textComponent, "SelectionStart"));
            Assert.Equal(3, GetPropertyValue<int>(textComponent, "SelectionEnd"));
        }

        /// <summary>
        /// Ensures selecting text on later lines uses the pointer Y position to resolve the correct line.
        /// </summary>
        [Fact]
        public void TextComponent_WhenTextContainsLineBreaks_DragSelectionUsesThePointerLine() {
            InitializeCore();
            EditorEntity entity = new EditorEntity();
            TextComponent textComponent = new TextComponent {
                Font = CreateFont(),
                Text = "One\nTwo"
            };

            SetPropertyValue(textComponent, "SelectionEnabled", true);
            entity.AddComponent(textComponent);

            ((TestInputBackend)Core.Instance.InputSystem.Backend).SetMouseState(new MouseState(
                2,
                20,
                0,
                ButtonState.Pressed,
                ButtonState.Released,
                ButtonState.Released,
                ButtonState.Released,
                ButtonState.Released));
            Core.Instance.Update();

            ((TestInputBackend)Core.Instance.InputSystem.Backend).SetMouseState(new MouseState(
                18,
                20,
                0,
                ButtonState.Pressed,
                ButtonState.Released,
                ButtonState.Released,
                ButtonState.Released,
                ButtonState.Released));
            Core.Instance.Update();

            Assert.True(GetPropertyValue<bool>(textComponent, "HasSelection"));
            Assert.Equal(4, GetPropertyValue<int>(textComponent, "SelectionStart"));
            Assert.Equal(6, GetPropertyValue<int>(textComponent, "SelectionEnd"));
        }

        /// <summary>
        /// Initializes the minimal core services required by text interaction tests.
        /// </summary>
        void InitializeCore() {
            Core core = new Core();
            core.Initialize(null, new TestRenderManager2D(), new TestInputBackend());
        }

        /// <summary>
        /// Creates a deterministic font asset for text layout and hit testing.
        /// </summary>
        /// <returns>Font asset with stable glyph metrics.</returns>
        FontAsset CreateFont() {
            Dictionary<char, FontChar> characters = new Dictionary<char, FontChar> {
                ['N'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['O'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['T'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['a'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['n'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['m'] = new FontChar(new float4(0f, 0f, 10f, 12f), 0f, 10f, 0f, 0f),
                ['e'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['o'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['w'] = new FontChar(new float4(0f, 0f, 10f, 12f), 0f, 10f, 0f, 0f)
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

        /// <summary>
        /// Reads one public or non-public property value.
        /// </summary>
        /// <typeparam name="T">Expected property type.</typeparam>
        /// <param name="target">Object that owns the property.</param>
        /// <param name="propertyName">Property name to read.</param>
        /// <returns>Property value cast to the requested type.</returns>
        T GetPropertyValue<T>(object target, string propertyName) {
            PropertyInfo property = FindProperty(target.GetType(), propertyName);
            return Assert.IsAssignableFrom<T>(property.GetValue(target));
        }

        /// <summary>
        /// Writes one public or non-public property value.
        /// </summary>
        /// <param name="target">Object that owns the property.</param>
        /// <param name="propertyName">Property name to write.</param>
        /// <param name="value">Value to assign.</param>
        void SetPropertyValue(object target, string propertyName, object value) {
            PropertyInfo property = FindProperty(target.GetType(), propertyName);
            property.SetValue(target, value);
        }

        /// <summary>
        /// Finds one property by name on the target type or its base types.
        /// </summary>
        /// <param name="type">Type to search.</param>
        /// <param name="propertyName">Property name to locate.</param>
        /// <returns>Matching property metadata.</returns>
        PropertyInfo FindProperty(Type type, string propertyName) {
            PropertyInfo property = type.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null) {
                return property;
            }

            throw new MissingMemberException(type.FullName, propertyName);
        }
    }
}

