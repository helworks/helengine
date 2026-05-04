namespace helengine {
    /// <summary>
    /// Polls input for a selectable text component and forwards interaction into its selection logic.
    /// </summary>
    public sealed class TextComponentSelectionUpdateComponent : UpdateComponent {
        /// <summary>
        /// Initializes a new input-forwarding update component for the supplied text component.
        /// </summary>
        /// <param name="textComponent">Text component that owns the selection state.</param>
        public TextComponentSelectionUpdateComponent(TextComponent textComponent) {
            TextComponent = textComponent ?? throw new ArgumentNullException(nameof(textComponent));
        }

        /// <summary>
        /// Gets the text component that owns the selection state.
        /// </summary>
        public TextComponent TextComponent { get; }

        /// <summary>
        /// Polls the current input state and forwards any selection interaction to the owning text component.
        /// </summary>
        public override void Update() {
            TextComponent.UpdateSelectionInput();
        }
    }
}
