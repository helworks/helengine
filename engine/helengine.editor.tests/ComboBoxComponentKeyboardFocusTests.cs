using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies keyboard-focus behavior for combo boxes.
    /// </summary>
    public class ComboBoxComponentKeyboardFocusTests {
        /// <summary>
        /// Ensures focused combo boxes toggle the main drop-down from Enter and Space.
        /// </summary>
        [Fact]
        public void ComboBoxComponent_WhenFocused_EnterAndSpaceToggleTheMainDropdown() {
            InitializeCore();
            TestFocusGroup focusGroup = new TestFocusGroup(null, 0, 0, 0, 240, 40);
            EditorEntity entity = new EditorEntity();
            ComboBoxComponent comboBox = new ComboBoxComponent(new int2(180, 28), CreateFont(), new[] { "One", "Two" }, 0);
            comboBox.FocusGroup = focusGroup.FocusGroup;
            entity.AddComponent(comboBox);
            IFocusTarget focusTarget = comboBox;

            Assert.False(comboBox.IsOpen);

            focusTarget.ActivateFromKey(Keys.Enter);
            Assert.True(comboBox.IsOpen);

            focusTarget.ActivateFromKey(Keys.Space);
            Assert.False(comboBox.IsOpen);
        }

        /// <summary>
        /// Ensures combo-box removal clears its local keyboard-focus visual state.
        /// </summary>
        [Fact]
        public void ComboBoxComponent_ComponentRemoved_ClearsItsKeyboardFocusState() {
            InitializeCore();
            TestFocusGroup focusGroup = new TestFocusGroup(null, 0, 0, 0, 240, 40);
            EditorEntity entity = new EditorEntity();
            ComboBoxComponent comboBox = new ComboBoxComponent(new int2(180, 28), CreateFont(), new[] { "One", "Two" }, 0);
            comboBox.FocusGroup = focusGroup.FocusGroup;
            entity.AddComponent(comboBox);
            IFocusTarget focusTarget = comboBox;

            focusTarget.SetTargetFocused(true);
            Assert.True(comboBox.IsKeyboardFocused);

            comboBox.ComponentRemoved(entity);

            Assert.False(comboBox.IsKeyboardFocused);
        }

        /// <summary>
        /// Initializes the core services required by keyboard-focus tests.
        /// </summary>
        void InitializeCore() {
            Core core = new Core();
            core.Initialize(null, new TestRenderManager2D(), new TestInputBackend());
        }

        /// <summary>
        /// Creates a deterministic font asset for combo-box layout tests.
        /// </summary>
        /// <returns>Font asset with basic glyph metrics.</returns>
        FontAsset CreateFont() {
            Dictionary<char, FontChar> characters = new Dictionary<char, FontChar> {
                ['O'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['T'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['e'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['n'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
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
    }
}

