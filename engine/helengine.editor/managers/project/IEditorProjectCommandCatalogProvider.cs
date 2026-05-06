namespace helengine.editor {
    /// <summary>
    /// Exposes project-authored editor commands discovered from loaded editor module assemblies.
    /// </summary>
    public interface IEditorProjectCommandCatalogProvider {
        /// <summary>
        /// Returns the currently available project-authored editor commands.
        /// </summary>
        /// <returns>Discovered editor command descriptors.</returns>
        IReadOnlyList<EditorProjectCommandDescriptor> GetAvailableEditorCommands();
    }
}
