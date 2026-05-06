namespace helengine.editor.tests.testing {
    /// <summary>
    /// Minimal script build tool used by editor-session menu tests without invoking `dotnet`.
    /// </summary>
    internal sealed class TestScriptBuildTool : IEditorScriptBuildTool {
        /// <summary>
        /// Initializes one fake script build tool with a fixed outcome.
        /// </summary>
        /// <param name="result">Fixed build result returned by the fake tool.</param>
        public TestScriptBuildTool(EditorBuildExecutionResult result) {
            Result = result ?? throw new ArgumentNullException(nameof(result));
        }

        /// <summary>
        /// Gets the fixed result returned by the fake tool.
        /// </summary>
        public EditorBuildExecutionResult Result { get; }

        /// <summary>
        /// Gets the most recent solution path passed into the fake tool.
        /// </summary>
        public string SolutionPath { get; private set; }

        /// <summary>
        /// Records the supplied solution path and returns the fixed test result.
        /// </summary>
        /// <param name="solutionPath">Absolute path to the generated solution file.</param>
        /// <returns>Fixed build result configured for the test.</returns>
        public EditorBuildExecutionResult Build(string solutionPath) {
            SolutionPath = solutionPath;
            return Result;
        }
    }
}
