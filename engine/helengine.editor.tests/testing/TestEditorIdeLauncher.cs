namespace helengine.editor.tests.testing {
    /// <summary>
    /// Minimal IDE launcher used by generated-solution tests without opening a real IDE.
    /// </summary>
    internal sealed class TestEditorIdeLauncher : IEditorIdeLauncher {
        /// <summary>
        /// Gets the most recent solution path passed into the fake launcher.
        /// </summary>
        public string SolutionPath { get; private set; }

        /// <summary>
        /// Records the supplied solution path without opening an IDE.
        /// </summary>
        /// <param name="solutionPath">Absolute path to the generated solution file.</param>
        public void OpenSolution(string solutionPath) {
            SolutionPath = solutionPath;
        }
    }
}
