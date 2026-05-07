namespace helengine.editor {
    /// <summary>
    /// Polls editor input and forwards keyboard-focus commands into the shared focus service.
    /// </summary>
    public class EditorKeyboardFocusUpdateComponent : UpdateComponent {
        /// <summary>
        /// Callback invoked when the editor-global save shortcut is pressed.
        /// </summary>
        public Action SaveShortcutRequested { get; set; }

        /// <summary>
        /// Routes per-frame input into the shared keyboard-focus service.
        /// </summary>
        public override void Update() {
            InputSystem input = Core.Instance.Input;
            if (input == null) {
                return;
            }

            if (input.WasMouseLeftButtonPressed()) {
                EditorKeyboardFocusService.HandlePointerPressed(input.GetMousePosition(), false);
            } else if (input.WasMouseRightButtonPressed()) {
                EditorKeyboardFocusService.HandlePointerPressed(input.GetMousePosition(), true);
            }

            bool shiftPressed = input.IsKeyDown(Keys.LeftShift) || input.IsKeyDown(Keys.RightShift);
            bool controlPressed = input.IsKeyDown(Keys.LeftControl) || input.IsKeyDown(Keys.RightControl);
            if (input.WasKeyPressed(Keys.Tab)) {
                if (controlPressed) {
                    EditorKeyboardFocusService.HandleCtrlTab(!shiftPressed);
                } else {
                    EditorKeyboardFocusService.HandleTab(!shiftPressed);
                }
            } else if (controlPressed && input.WasKeyPressed(Keys.S)) {
                if (SaveShortcutRequested != null) {
                    SaveShortcutRequested();
                }
            } else if (input.WasKeyPressed(Keys.Enter)) {
                EditorKeyboardFocusService.HandleActivationKey(Keys.Enter);
            } else if (input.WasKeyPressed(Keys.Space)) {
                EditorKeyboardFocusService.HandleActivationKey(Keys.Space);
            } else if (input.WasKeyPressed(Keys.W)) {
                EditorKeyboardFocusService.HandleActivationKey(Keys.W);
            } else if (input.WasKeyPressed(Keys.R)) {
                EditorKeyboardFocusService.HandleActivationKey(Keys.R);
            } else if (input.WasKeyPressed(Keys.S)) {
                EditorKeyboardFocusService.HandleActivationKey(Keys.S);
            } else if (input.WasKeyPressed(Keys.Up)) {
                EditorKeyboardFocusService.HandleActivationKey(Keys.Up);
            } else if (input.WasKeyPressed(Keys.Down)) {
                EditorKeyboardFocusService.HandleActivationKey(Keys.Down);
            } else if (input.WasKeyPressed(Keys.Left)) {
                EditorKeyboardFocusService.HandleActivationKey(Keys.Left);
            } else if (input.WasKeyPressed(Keys.Right)) {
                EditorKeyboardFocusService.HandleActivationKey(Keys.Right);
            }

            EditorKeyboardFocusService.Update();
        }
    }
}


