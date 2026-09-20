using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies editor-global keyboard shortcuts are routed through the keyboard-focus update component without colliding with focus activation keys.
    /// </summary>
    public sealed class EditorKeyboardFocusUpdateComponentTests : IDisposable {
        readonly helengine.editor.EditorSessionInteractionServices InteractionServices = new helengine.editor.EditorSessionInteractionServices();
        /// <summary>
        /// Test input backend used to simulate keyboard transitions.
        /// </summary>
        readonly TestInputBackend InputBackend;

        /// <summary>
        /// Initializes one keyboard-focus update component test fixture with a live core and deterministic input backend.
        /// </summary>
        public EditorKeyboardFocusUpdateComponentTests() {
            Core core = new Core(new CoreInitializationOptions { ContentStreamSource = new FakeContentStreamSource() });
            InputBackend = new TestInputBackend();
            core.Initialize(null, new TestRenderManager2D(), InputBackend, new PlatformInfo("test", "test-version"));
        }

        /// <summary>
        /// Clears shared keyboard-focus state after each test run.
        /// </summary>
        public void Dispose() {
        }

        /// <summary>
        /// Ensures pressing Ctrl+Z invokes the undo shortcut callback.
        /// </summary>
        [Fact]
        public void Update_when_ctrl_z_is_pressed_invokes_the_undo_callback() {
            EditorKeyboardFocusUpdateComponent component = new EditorKeyboardFocusUpdateComponent(Core.Instance.Input, InteractionServices);
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
        /// Ensures the component actually executes through the real object-manager update loop once its owning entity
        /// hierarchy is initialized, guarding against silently-dead shortcut components that only direct calls would exercise.
        /// </summary>
        [Fact]
        public void Update_loop_runs_the_component_when_the_owning_entity_hierarchy_is_initialized() {
            EditorKeyboardFocusUpdateComponent component = new EditorKeyboardFocusUpdateComponent(Core.Instance.Input, InteractionServices);
            int undoCount = 0;
            component.UndoShortcutRequested = () => undoCount++;
            EditorEntity ownerEntity = new EditorEntity(Core.Instance, new helengine.editor.EditorSessionInteractionServices()) {
                InternalEntity = true,
                Enabled = true
            };
            ownerEntity.AddComponent(component);
            ownerEntity.InitializeHierarchy();

            AdvanceToNeutralFrame();
            InputBackend.SetKeyboardState(new KeyboardState(Keys.LeftControl, Keys.Z));
            InputBackend.EarlyUpdate();
            Core.Instance.ObjectManager.Update();

            Assert.Equal(1, undoCount);
        }

        /// <summary>
        /// Ensures pressing Ctrl+Y invokes the redo shortcut callback.
        /// </summary>
        [Fact]
        public void Update_when_ctrl_y_is_pressed_invokes_the_redo_callback() {
            EditorKeyboardFocusUpdateComponent component = new EditorKeyboardFocusUpdateComponent(Core.Instance.Input, InteractionServices);
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
            EditorKeyboardFocusUpdateComponent component = new EditorKeyboardFocusUpdateComponent(Core.Instance.Input, InteractionServices);
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
        /// <summary>
        /// Dialog and panel text boxes are not registered with the focus service, so the viewport can stay the focused
        /// target while the user types. Gizmo keys and the Delete shortcut must not fire in that state, and must fire
        /// again once the text box loses focus.
        /// </summary>
        [Fact]
        public void Update_when_an_unregistered_text_box_is_being_edited_keeps_activation_keys_and_delete_away_from_the_focused_target() {
            EditorKeyboardFocusUpdateComponent component = new EditorKeyboardFocusUpdateComponent(Core.Instance.Input, InteractionServices);
            List<Keys> activatedKeys = new List<Keys>();
            int deleteCount = 0;
            component.DeleteShortcutRequested = () => deleteCount++;
            TestFocusGroup focusGroup = new TestFocusGroup(null, 0, 0, 0, 640, 480);
            EditorFocusTarget viewportTarget = new EditorFocusTarget(
                focusGroup.FocusGroup,
                0,
                true,
                () => true,
                point => true,
                focused => { },
                key => true,
                key => activatedKeys.Add(key));
            InteractionServices.KeyboardFocus.RegisterTarget(viewportTarget);
            InteractionServices.KeyboardFocus.SetFocusedTarget(viewportTarget);
            EditorEntity entity = new EditorEntity(Core.Instance, InteractionServices);
            TextBoxComponent textBox = new TextBoxComponent(new int2(180, 28), CreateFont(), "Name");
            entity.AddComponent(textBox);
            entity.InitializeHierarchy();

            try {
                textBox.IsFocused = true;

                PressKey(Keys.W);
                component.Update();
                PressKey(Keys.Delete);
                component.Update();

                Assert.Empty(activatedKeys);
                Assert.Equal(0, deleteCount);

                textBox.IsFocused = false;

                PressKey(Keys.W);
                component.Update();

                Assert.Equal(new[] { Keys.W }, activatedKeys);
            } finally {
                textBox.IsFocused = false;
                InteractionServices.KeyboardFocus.UnregisterTarget(viewportTarget);
            }
        }

        void PressKey(Keys key) {
            AdvanceToNeutralFrame();
            InputBackend.SetKeyboardState(new KeyboardState(key));
            InputBackend.EarlyUpdate();
        }

        static FontAsset CreateFont() {
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

        void AdvanceToNeutralFrame() {
            InputBackend.SetKeyboardState(new KeyboardState());
            InputBackend.EarlyUpdate();
            InputBackend.Update();
        }
    }
}
