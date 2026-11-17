namespace helengine {
    /// <summary>
    /// Helper update component that forwards updates to its owning text box.
    /// </summary>
    [RunInEditor]
    class TextBoxUpdateComponent : UpdateComponent {
        /// <summary>Borrowed owner of this input/update forwarding component.</summary>
        TextBoxComponent TextBox;

        /// <summary>
        /// Creates a forwarding update component for the given text box.
        /// </summary>
        /// <param name="textBox">Text box to drive.</param>
        public TextBoxUpdateComponent(TextBoxComponent textBox) {
            this.TextBox = textBox;
        }

        /// <summary>
        /// Forwards update calls to the text box.
        /// </summary>
        public override void Update() {
            TextBox.Update();

            if (!TextBox.IsFocused) {
                return;
            }

            InputSystem input = OwnerCore.Input;
            if (!input.WasPointerPrimaryPressed()) {
                return;
            }

            int pointerX = input.GetPointerX();
            int pointerY = input.GetPointerY();
            if (!TextBox.ContainsScreenPoint(pointerX, pointerY)) {
                TextBox.IsFocused = false;
            }
        }
    }
}
