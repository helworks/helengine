namespace helengine.editor {
    /// <summary>
    /// Captures one requested headless editor-command invocation.
    /// </summary>
    public sealed class EditorCliCommandOptions {
        /// <summary>
        /// Initializes one headless editor-command request.
        /// </summary>
        /// <param name="projectPath">Project directory or canonical project file path.</param>
        /// <param name="commandId">Stable project-authored editor command identifier.</param>
        public EditorCliCommandOptions(string projectPath, string commandId) {
            ProjectPath = projectPath ?? throw new ArgumentNullException(nameof(projectPath));
            CommandId = commandId ?? throw new ArgumentNullException(nameof(commandId));
        }

        /// <summary>
        /// Gets the project directory or canonical project file path.
        /// </summary>
        public string ProjectPath { get; }

        /// <summary>
        /// Gets the stable project-authored editor command identifier.
        /// </summary>
        public string CommandId { get; }
    }
}
