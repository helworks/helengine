namespace helengine.editor {
    /// <summary>
    /// Builds the generated C# scripting solution for the current game project.
    /// </summary>
    public interface IEditorScriptBuildTool {
        /// <summary>
        /// Builds the supplied solution file and returns the process outcome.
        /// </summary>
        /// <param name="solutionPath">Absolute path to the generated solution file.</param>
        /// <returns>Structured build result describing success or failure.</returns>
        EditorBuildExecutionResult Build(string solutionPath);
    }
}
