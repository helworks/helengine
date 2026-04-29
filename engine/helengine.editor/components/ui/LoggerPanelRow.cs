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
        public LoggerPanelRow(EditorEntity entity, SpriteComponent background, EditorEntity labelHost, TextComponent label) {
            Entity = entity;
            Background = background;
            LabelHost = labelHost;
            Label = label;
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
    }
}
