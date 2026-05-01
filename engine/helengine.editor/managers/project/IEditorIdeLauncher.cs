namespace helengine.editor {
    /// <summary>
    /// Launches one generated solution file in the preferred desktop IDE.
    /// </summary>
    public interface IEditorIdeLauncher {
        /// <summary>
        /// Opens the supplied solution file in the configured IDE.
        /// </summary>
        /// <param name="solutionPath">Absolute path to the generated solution file.</param>
        void OpenSolution(string solutionPath);
    }
}
