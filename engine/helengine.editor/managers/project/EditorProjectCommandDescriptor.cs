namespace helengine.editor {
    /// <summary>
    /// Describes one project-authored editor command discovered from a loaded editor assembly.
    /// </summary>
    public sealed class EditorProjectCommandDescriptor {
        /// <summary>
        /// Initializes one editor command descriptor.
        /// </summary>
        /// <param name="commandId">Stable command identifier.</param>
        /// <param name="displayName">Display name surfaced by the editor catalog.</param>
        /// <param name="commandType">Concrete command type discovered from the loaded assembly.</param>
        /// <param name="moduleId">Stable module identifier that owns the command.</param>
        public EditorProjectCommandDescriptor(string commandId, string displayName, Type commandType, string moduleId) {
            if (string.IsNullOrWhiteSpace(commandId)) {
                throw new ArgumentException("Command id must be provided.", nameof(commandId));
            }
            if (string.IsNullOrWhiteSpace(displayName)) {
                throw new ArgumentException("Display name must be provided.", nameof(displayName));
            }
            if (commandType == null) {
                throw new ArgumentNullException(nameof(commandType));
            }
            if (string.IsNullOrWhiteSpace(moduleId)) {
                throw new ArgumentException("Module id must be provided.", nameof(moduleId));
            }

            CommandId = commandId;
            DisplayName = displayName;
            CommandType = commandType;
            ModuleId = moduleId;
        }

        /// <summary>
        /// Gets the stable command identifier.
        /// </summary>
        public string CommandId { get; }

        /// <summary>
        /// Gets the display name surfaced by the editor command catalog.
        /// </summary>
        public string DisplayName { get; }

        /// <summary>
        /// Gets the concrete command type discovered from the loaded assembly.
        /// </summary>
        public Type CommandType { get; }

        /// <summary>
        /// Gets the stable module identifier that owns the command.
        /// </summary>
        public string ModuleId { get; }
    }
}
