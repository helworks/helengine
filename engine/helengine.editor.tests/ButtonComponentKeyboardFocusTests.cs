using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies keyboard-focus behavior for button controls.
    /// </summary>
    public class ButtonComponentKeyboardFocusTests {
        /// <summary>
        /// Ensures focused buttons activate from Enter and Space.
        /// </summary>
        [Fact]
        public void ButtonComponent_WhenFocused_ActivatesFromEnterAndSpace() {
            InitializeCore();
            TestFocusGroup focusGroup = new TestFocusGroup(null, 0, 0, 0, 200, 60);
            EditorEntity entity = new EditorEntity();
            int activationCount = 0;
            ButtonComponent button = new ButtonComponent("Run", new int2(96, 28), CreateFont(), () => activationCount++);
            button.FocusGroup = focusGroup.FocusGroup;
            entity.AddComponent(button);
            IFocusTarget focusTarget = button;

            focusTarget.SetTargetFocused(true);
            focusTarget.ActivateFromKey(Keys.Enter);
            focusTarget.ActivateFromKey(Keys.Space);

            Assert.True(button.IsKeyboardFocused);
            Assert.Equal(2, activationCount);
        }

        /// <summary>
        /// Ensures button removal clears its local keyboard-focus visual state.
        /// </summary>
        [Fact]
        public void ButtonComponent_ComponentRemoved_ClearsItsKeyboardFocusState() {
            InitializeCore();
            TestFocusGroup focusGroup = new TestFocusGroup(null, 0, 0, 0, 200, 60);
            EditorEntity entity = new EditorEntity();
            ButtonComponent button = new ButtonComponent("Run", new int2(96, 28), CreateFont(), null);
            button.FocusGroup = focusGroup.FocusGroup;
            entity.AddComponent(button);
            IFocusTarget focusTarget = button;

            focusTarget.SetTargetFocused(true);
            Assert.True(button.IsKeyboardFocused);

            button.ComponentRemoved(entity);

            Assert.False(button.IsKeyboardFocused);
        }

        /// <summary>
        /// Initializes the core services required by keyboard-focus tests.
        /// </summary>
        void InitializeCore() {
            Core core = new Core();
            core.Initialize(null, new TestRenderManager2D(), null);
        }

        /// <summary>
        /// Creates a deterministic font asset for button layout tests.
        /// </summary>
        /// <returns>Font asset with basic glyph metrics.</returns>
        FontAsset CreateFont() {
            Dictionary<char, FontChar> characters = new Dictionary<char, FontChar> {
                ['R'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['u'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['n'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f)
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
