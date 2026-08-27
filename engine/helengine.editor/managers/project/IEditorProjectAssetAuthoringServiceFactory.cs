namespace helengine.editor {
    /// <summary>
    /// Creates host-configured asset-authoring capabilities for project editor commands.
    /// </summary>
    public interface IEditorProjectAssetAuthoringServiceFactory {
        /// <summary>
        /// Creates one asset-authoring capability for a project root.
        /// </summary>
        /// <param name="projectRootPath">Absolute project root path.</param>
        /// <returns>Host-configured asset-authoring capability.</returns>
        IEditorProjectAssetAuthoringService Create(string projectRootPath);
    }
}
