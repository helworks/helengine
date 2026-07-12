using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies editor-global keyboard shortcuts are routed through the keyboard-focus update component without colliding with focus activation keys.
    /// </summary>
    public sealed class EditorKeyboardFocusUpdateComponentTests : IDisposable {
        /// <summary>
        /// Test input backend used to simulate keyboard transitions.
        /// </summary>
        readonly TestInputBackend InputBackend;

        /// <summary>
        /// Initializes one keyboard-focus update component test fixture with a live core and deterministic input backend.
        /// </summary>
        public EditorKeyboardFocusUpdateComponentTests() {
            Core core = new Core();
            InputBackend = new TestInputBackend();
            core.Initialize(null, new TestRenderManager2D(), InputBackend, new PlatformInfo("test", "test-version"));
        }

        /// <summary>
        /// Clears shared keyboard-focus state after each test run.
        /// </summary>
        public void Dispose() {
            EditorKeyboardFocusService.Reset();
        }

        /// <summary>
        /// Ensures pressing Ctrl+Z invokes the undo shortcut callback.
        /// </summary>
        [Fact]
        public void Update_when_ctrl_z_is_pressed_invokes_the_undo_callback() {
            EditorKeyboardFocusUpdateComponent component = new EditorKeyboardFocusUpdateComponent();
            int undoCount = 0;
            int redoCount = 0;
            component.UndoShortcutRequested = () => undoCount++;
            component.RedoShortcutRequested = () => redoCount++;

            AdvanceToNeutralFrame();
            InputBackend.SetKeyboardState(new KeyboardState(Keys.LeftControl, Keys.Z));
            InputBackend.EarlyUpdate();

            component.Update();

            Assert.Equal(1, undoCount);
            Assert.Equal(0, redoCount);
        }

        /// <summary>
        /// Ensures pressing Ctrl+Y invokes the redo shortcut callback.
        /// </summary>
        [Fact]
        public void Update_when_ctrl_y_is_pressed_invokes_the_redo_callback() {
            EditorKeyboardFocusUpdateComponent component = new EditorKeyboardFocusUpdateComponent();
            int undoCount = 0;
            int redoCount = 0;
            component.UndoShortcutRequested = () => undoCount++;
            component.RedoShortcutRequested = () => redoCount++;

            AdvanceToNeutralFrame();
            InputBackend.SetKeyboardState(new KeyboardState(Keys.LeftControl, Keys.Y));
            InputBackend.EarlyUpdate();

            component.Update();

            Assert.Equal(0, undoCount);
            Assert.Equal(1, redoCount);
        }

        /// <summary>
        /// Ensures pressing Ctrl+Shift+Z invokes the redo shortcut callback instead of falling through to undo.
        /// </summary>
        [Fact]
        public void Update_when_ctrl_shift_z_is_pressed_invokes_the_redo_callback() {
            EditorKeyboardFocusUpdateComponent component = new EditorKeyboardFocusUpdateComponent();
            int undoCount = 0;
            int redoCount = 0;
            component.UndoShortcutRequested = () => undoCount++;
            component.RedoShortcutRequested = () => redoCount++;

            AdvanceToNeutralFrame();
            InputBackend.SetKeyboardState(new KeyboardState(Keys.LeftControl, Keys.LeftShift, Keys.Z));
            InputBackend.EarlyUpdate();

            component.Update();

            Assert.Equal(0, undoCount);
            Assert.Equal(1, redoCount);
        }

        /// <summary>
        /// Advances the input system through one neutral frame so the next key state is observed as a press transition.
        /// </summary>
        void AdvanceToNeutralFrame() {
            InputBackend.SetKeyboardState(new KeyboardState());
            InputBackend.EarlyUpdate();
            InputBackend.Update();
        }
    }
}
