namespace helengine.editor {
    /// <summary>
    /// Describes a file template used to create new editor assets.
    /// </summary>
    public class EditorFileTemplate {
        /// <summary>
        /// Initializes a new file template definition.
        /// </summary>
        /// <param name="label">Label shown in the UI.</param>
        /// <param name="defaultName">Default base name used for new files.</param>
        /// <param name="extension">File extension including the leading dot.</param>
        /// <param name="kind">Template behavior classification.</param>
        /// <param name="defaultContents">Default contents written to the file.</param>
        public EditorFileTemplate(
            string label,
            string defaultName,
            string extension,
            EditorFileTemplateKind kind,
            string defaultContents) {
            if (string.IsNullOrWhiteSpace(label)) {
                throw new ArgumentException("Template label must be provided.", nameof(label));
            }
            if (string.IsNullOrWhiteSpace(defaultName)) {
                throw new ArgumentException("Template default name must be provided.", nameof(defaultName));
            }
            if (string.IsNullOrWhiteSpace(extension)) {
                throw new ArgumentException("Template extension must be provided.", nameof(extension));
            }
            if (!extension.StartsWith(".", StringComparison.Ordinal)) {
                throw new ArgumentException("Template extension must begin with a dot.", nameof(extension));
            }

            Label = label;
            DefaultName = defaultName;
            Extension = extension;
            Kind = kind;
            DefaultContents = defaultContents ?? string.Empty;
        }

        /// <summary>
        /// Gets the label shown in menus.
        /// </summary>
        public string Label { get; }

        /// <summary>
        /// Gets the default base name used for new files.
        /// </summary>
        public string DefaultName { get; }

        /// <summary>
        /// Gets the file extension including the leading dot.
        /// </summary>
        public string Extension { get; }

        /// <summary>
        /// Gets the template classification used during creation.
        /// </summary>
        public EditorFileTemplateKind Kind { get; }

        /// <summary>
        /// Gets the default contents written to the file.
        /// </summary>
        public string DefaultContents { get; }
    }
}
