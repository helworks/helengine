namespace helengine.editor {
    /// <summary>
    /// Supplies component descriptors discovered from the currently loaded scripting assembly.
    /// </summary>
    public interface IEditorScriptComponentCatalogProvider {
        /// <summary>
        /// Returns the addable script components currently available for one entity.
        /// </summary>
        /// <param name="entity">Entity that will receive the component if selected.</param>
        /// <returns>Script component descriptors exposed by the latest loaded game assembly.</returns>
        IReadOnlyList<EditorComponentAddDescriptor> GetAvailableScriptComponents(Entity entity);
    }
}
