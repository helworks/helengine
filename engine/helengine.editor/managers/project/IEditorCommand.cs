namespace helengine.editor {
    /// <summary>
    /// Defines one project-authored editor command discovered from an editor module assembly.
    /// </summary>
    public interface IEditorCommand {
        /// <summary>
        /// Gets the stable command identifier.
        /// </summary>
        string CommandId { get; }

        /// <summary>
        /// Gets the display name surfaced by the editor command catalog.
        /// </summary>
        string DisplayName { get; }

        /// <summary>
        /// Executes the command using the supplied editor-safe context.
        /// </summary>
        /// <param name="context">Editor-safe context used by the command.</param>
        void Execute(IEditorCommandContext context);
    }
}
