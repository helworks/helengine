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

    /// <summary>
    /// Builds generated script projects with an invocation-specific compiler-output root.
    /// </summary>
    public interface IEditorScriptBuildToolWithOutputRoot : IEditorScriptBuildTool {
        /// <summary>
        /// Builds the supplied solution while overriding its generated compiler-output root for this invocation.
        /// </summary>
        /// <param name="solutionPath">Absolute path to the generated solution file.</param>
        /// <param name="executionOutputRootPath">Unique output root reserved for this invocation.</param>
        /// <returns>Structured build result describing the process outcome.</returns>
        EditorBuildExecutionResult Build(string solutionPath, string executionOutputRootPath);
    }

    /// <summary>
    /// Builds generated scripts while borrowing the caller's held generated-workspace lease.
    /// </summary>
    public interface IEditorScriptBuildToolWithWorkspaceLease : IEditorScriptBuildToolWithOutputRoot {
        /// <summary>
        /// Builds the supplied solution under an already-held workspace lease.
        /// </summary>
        /// <param name="solutionPath">Absolute path to the generated solution.</param>
        /// <param name="executionOutputRootPath">Unique output root reserved for this invocation, or empty for fallback output.</param>
        /// <param name="workspaceLease">Lease retained from generation through build evaluation.</param>
        /// <returns>Structured build result.</returns>
        EditorBuildExecutionResult Build(
            string solutionPath,
            string executionOutputRootPath,
            EditorGeneratedCodeWorkspaceLease workspaceLease);
    }
}
