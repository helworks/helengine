namespace helengine.editor {
    /// <summary>
    /// Defines one editor-only provider that contributes menu-strip items from a project-authored editor module.
    /// </summary>
    public interface IEditorMenuItemProvider {
        /// <summary>
        /// Returns the contributed menu items surfaced by the provider.
        /// </summary>
        /// <returns>Contributed menu item descriptors.</returns>
        IReadOnlyList<EditorMenuItemDescriptor> GetMenuItems();
    }
}
