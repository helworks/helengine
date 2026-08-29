namespace helengine.editor {
    /// <summary>
    /// Polls editor input and forwards keyboard-focus commands into the owning session focus service.
    /// </summary>
    public class EditorKeyboardFocusUpdateComponent : UpdateComponent {
        readonly InputSystem Input;
        readonly EditorSessionInteractionServices InteractionServices;

        public EditorKeyboardFocusUpdateComponent(InputSystem input, EditorSessionInteractionServices interactionServices) {
            Input = input ?? throw new ArgumentNullException(nameof(input));
            InteractionServices = interactionServices ?? throw new ArgumentNullException(nameof(interactionServices));
        }

        /// <summary>
        /// Callback invoked when the editor-global save shortcut is pressed.
        /// </summary>
        public Action SaveShortcutRequested { get; set; }

        /// <summary>
        /// Callback invoked when the editor-global undo shortcut is pressed.
        /// </summary>
        public Action UndoShortcutRequested { get; set; }

        /// <summary>
        /// Callback invoked when the editor-global redo shortcut is pressed.
        /// </summary>
        public Action RedoShortcutRequested { get; set; }

        /// <summary>
        /// Callback invoked when the editor-global delete shortcut is pressed.
        /// </summary>
        public Action DeleteShortcutRequested { get; set; }

        /// <summary>
        /// Callback invoked when the editor-global duplicate shortcut is pressed.
        /// </summary>
        public Action DuplicateShortcutRequested { get; set; }

        /// <summary>
        /// Routes per-frame input into the shared keyboard-focus service.
        /// </summary>
        public override void Update() {
            InputSystem input = Input;

            if (input.WasMouseLeftButtonPressed()) {
                InteractionServices.KeyboardFocus.HandlePointerPressed(input.GetMousePosition(), false);
            } else if (input.WasMouseRightButtonPressed()) {
                InteractionServices.KeyboardFocus.HandlePointerPressed(input.GetMousePosition(), true);
            }

            bool shiftPressed = input.IsKeyDown(Keys.LeftShift) || input.IsKeyDown(Keys.RightShift);
            bool controlPressed = input.IsKeyDown(Keys.LeftControl) || input.IsKeyDown(Keys.RightControl);
            if (input.WasKeyPressed(Keys.Tab)) {
                if (controlPressed) {
                    InteractionServices.KeyboardFocus.HandleCtrlTab(!shiftPressed);
                } else {
                    InteractionServices.KeyboardFocus.HandleTab(!shiftPressed);
                }
            } else if (controlPressed && shiftPressed && input.WasKeyPressed(Keys.Z)) {
                if (RedoShortcutRequested != null) {
                    RedoShortcutRequested();
                }
            } else if (controlPressed && input.WasKeyPressed(Keys.Z)) {
                if (UndoShortcutRequested != null) {
                    UndoShortcutRequested();
                }
            } else if (controlPressed && input.WasKeyPressed(Keys.Y)) {
                if (RedoShortcutRequested != null) {
                    RedoShortcutRequested();
                }
            } else if (controlPressed && input.WasKeyPressed(Keys.S)) {
                if (SaveShortcutRequested != null) {
                    SaveShortcutRequested();
                }
            } else if (controlPressed && input.WasKeyPressed(Keys.D)) {
                if (DuplicateShortcutRequested != null) {
                    DuplicateShortcutRequested();
                }
            } else if (input.WasKeyPressed(Keys.Delete)) {
                if (DeleteShortcutRequested != null) {
                    DeleteShortcutRequested();
                }
            } else if (input.WasKeyPressed(Keys.Enter)) {
                InteractionServices.KeyboardFocus.HandleActivationKey(Keys.Enter);
            } else if (input.WasKeyPressed(Keys.Space)) {
                InteractionServices.KeyboardFocus.HandleActivationKey(Keys.Space);
            } else if (input.WasKeyPressed(Keys.W)) {
                InteractionServices.KeyboardFocus.HandleActivationKey(Keys.W);
            } else if (input.WasKeyPressed(Keys.R)) {
                InteractionServices.KeyboardFocus.HandleActivationKey(Keys.R);
            } else if (input.WasKeyPressed(Keys.S)) {
                InteractionServices.KeyboardFocus.HandleActivationKey(Keys.S);
            } else if (input.WasKeyPressed(Keys.F)) {
                InteractionServices.KeyboardFocus.HandleActivationKey(Keys.F);
            } else if (input.WasKeyPressed(Keys.Up)) {
                InteractionServices.KeyboardFocus.HandleActivationKey(Keys.Up);
            } else if (input.WasKeyPressed(Keys.Down)) {
                InteractionServices.KeyboardFocus.HandleActivationKey(Keys.Down);
            } else if (input.WasKeyPressed(Keys.Left)) {
                InteractionServices.KeyboardFocus.HandleActivationKey(Keys.Left);
            } else if (input.WasKeyPressed(Keys.Right)) {
                InteractionServices.KeyboardFocus.HandleActivationKey(Keys.Right);
            }

            InteractionServices.KeyboardFocus.Update();
        }
    }
}


