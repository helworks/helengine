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
        /// <param name="compileSkipped">
        /// Receives true when the existing modules were already up to date and no compile ran; false when a compile ran
        /// or the build failed. The Play startup summary tags the script stage "(skipped)" from it.
        /// </param>
        /// <returns>Build outcome; the session aborts Play when it did not succeed.</returns>
        EditorBuildExecutionResult Build(string platformId, out string runtimeModulesRootPath, out bool compileSkipped);
    }
}
