namespace helengine.editor {
    /// <summary>
    /// Detects whether one generated solution is already open in the IDE.
    /// </summary>
    public interface IEditorIdeSolutionDetector {
        /// <summary>
        /// Returns whether the supplied solution path already appears to be open in the IDE.
        /// </summary>
        /// <param name="solutionPath">Absolute path to the generated solution file.</param>
        /// <returns>True when the solution is already open; otherwise false.</returns>
        bool IsSolutionAlreadyOpen(string solutionPath);
    }
}
