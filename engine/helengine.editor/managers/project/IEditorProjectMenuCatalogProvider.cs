namespace helengine.editor {
    /// <summary>
    /// Exposes project-authored editor menu contributions discovered from loaded editor module assemblies.
    /// </summary>
    public interface IEditorProjectMenuCatalogProvider {
        /// <summary>
        /// Returns the currently available contributed editor menu items.
        /// </summary>
        /// <returns>Discovered contributed editor menu items.</returns>
        IReadOnlyList<EditorMenuItemDescriptor> GetAvailableEditorMenuItems();
    }
}
