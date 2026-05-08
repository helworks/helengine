namespace helengine.editor {
    /// <summary>
    /// Bundles row entities used to display a single log entry.
    /// </summary>
    public sealed class LoggerPanelRow {
        /// <summary>
        /// Initializes a new logger row.
        /// </summary>
        /// <param name="entity">Root entity for the row.</param>
        /// <param name="background">Background sprite component.</param>
        /// <param name="labelHost">Entity hosting the text label.</param>
        /// <param name="label">Text component for the log message.</param>
        /// <param name="interactable">Interactable region used for row pointer input.</param>
        public LoggerPanelRow(EditorEntity entity, SpriteComponent background, EditorEntity labelHost, TextComponent label, InteractableComponent interactable) {
            Entity = entity;
            Background = background;
            LabelHost = labelHost;
            Label = label;
            Interactable = interactable;
            RowIndex = -1;
        }

        /// <summary>
        /// Gets the root entity for the row.
        /// </summary>
        public EditorEntity Entity { get; }

        /// <summary>
        /// Gets the background sprite component.
        /// </summary>
        public SpriteComponent Background { get; }

        /// <summary>
        /// Gets the entity hosting the label text.
        /// </summary>
        public EditorEntity LabelHost { get; }

        /// <summary>
        /// Gets the text component for the log message.
        /// </summary>
        public TextComponent Label { get; }

        /// <summary>
        /// Gets the interactable region used for pointer input.
        /// </summary>
        public InteractableComponent Interactable { get; }

        /// <summary>
        /// Gets or sets the current entry index displayed by this row.
        /// </summary>
        public int RowIndex { get; set; }
    }
}
