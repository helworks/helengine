namespace helengine.editor {
    /// <summary>
    /// Builds the runtime script modules the desktop player loads for one editor Play session.
    /// </summary>
    public interface IEditorPlayScriptBuilder {
        /// <summary>
        /// Builds the project's gameplay scripts with the given platform's symbols.
        /// </summary>
        /// <param name="platformId">Identifier of the platform whose gameplay symbols the scripts compile against.</param>
        /// <param name="runtimeModulesRootPath">Receives the directory holding the built runtime modules on success.</param>
        /// <returns>Build outcome; the session aborts Play when it did not succeed.</returns>
        EditorBuildExecutionResult Build(string platformId, out string runtimeModulesRootPath);
    }
}
