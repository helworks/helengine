using System.Reflection;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies keyboard-focus behavior for text boxes.
    /// </summary>
    public class TextBoxComponentKeyboardFocusTests {
        /// <summary>
        /// Ensures text-box focus reuses the existing text-entry focus semantics and does not treat Space as activation.
        /// </summary>
        [Fact]
        public void TextBoxComponent_SetTargetFocused_UsesExistingTextFocusSemanticsWithoutSpaceActivation() {
            InitializeCore();
            TestFocusGroup focusGroup = new TestFocusGroup(null, 0, 0, 0, 240, 40);
            EditorEntity entity = new EditorEntity();
            TextBoxComponent textBox = new TextBoxComponent(new int2(180, 28), CreateFont(), "Name");
            int submitCount = 0;
            textBox.FocusGroup = focusGroup;
            textBox.Submitted += submitted => submitCount++;
            entity.AddComponent(textBox);
            IFocusTarget focusTarget = textBox;

            focusTarget.SetTargetFocused(true);

            Assert.True(textBox.IsFocused);
            Assert.False(focusTarget.CanActivateWithKey(Keys.Space));

            focusTarget.ActivateFromKey(Keys.Enter);

            Assert.Equal(1, submitCount);
            Assert.False(textBox.IsFocused);
        }

        /// <summary>
        /// Ensures text-box removal clears both the static text-focus reference and the local focus state.
        /// </summary>
        [Fact]
        public void TextBoxComponent_ComponentRemoved_ClearsStaticTextFocusAndItsKeyboardState() {
            InitializeCore();
            TestFocusGroup focusGroup = new TestFocusGroup(null, 0, 0, 0, 240, 40);
            EditorEntity entity = new EditorEntity();
            TextBoxComponent textBox = new TextBoxComponent(new int2(180, 28), CreateFont(), "Name");
            textBox.FocusGroup = focusGroup;
            entity.AddComponent(textBox);
            IFocusTarget focusTarget = textBox;

            focusTarget.SetTargetFocused(true);

            textBox.ComponentRemoved(entity);

            Assert.Null(GetFocusedTextBox());
            Assert.False(textBox.IsFocused);
        }

        /// <summary>
        /// Initializes the core services required by keyboard-focus tests.
        /// </summary>
        void InitializeCore() {
            Core core = new Core();
            core.Initialize(null, new TestRenderManager2D(), new TestInputManager());
        }

        /// <summary>
        /// Reads the static focused text-box field.
        /// </summary>
        /// <returns>Currently focused text box, or null when none are focused.</returns>
        TextBoxComponent GetFocusedTextBox() {
            FieldInfo field = typeof(TextBoxComponent).GetField("focusedTextBox", BindingFlags.Static | BindingFlags.NonPublic);
            object value = field.GetValue(null);
            if (value == null) {
                return null;
            }

            return Assert.IsType<TextBoxComponent>(value);
        }

        /// <summary>
        /// Creates a deterministic font asset for text-box layout tests.
        /// </summary>
        /// <returns>Font asset with basic glyph metrics.</returns>
        FontAsset CreateFont() {
            Dictionary<char, FontChar> characters = new Dictionary<char, FontChar> {
                ['N'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['a'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['m'] = new FontChar(new float4(0f, 0f, 10f, 12f), 0f, 10f, 0f, 0f),
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
