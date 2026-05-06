namespace helengine.editor.tests.testing {
    /// <summary>
    /// Minimal project-authored editor command used to verify command discovery from loaded editor assemblies.
    /// </summary>
    internal sealed class TestEditorCommand : IEditorCommand {
        /// <summary>
        /// Gets the number of times the test command has been executed in the current process.
        /// </summary>
        public static int ExecuteCount { get; private set; }

        /// <summary>
        /// Gets the stable test command identifier.
        /// </summary>
        public string CommandId => "menu.regenerate-demo-disc-main-menu";

        /// <summary>
        /// Gets the display label surfaced by the editor command catalog.
        /// </summary>
        public string DisplayName => "Regenerate Demo Disc Main Menu";

        /// <summary>
        /// Executes the test command. The discovery test does not require any behavior here.
        /// </summary>
        /// <param name="context">Editor command context supplied by the command runner.</param>
        public void Execute(IEditorCommandContext context) {
            ExecuteCount++;
        }

        /// <summary>
        /// Resets the process-wide execution count tracked by the test command.
        /// </summary>
        public static void Reset() {
            ExecuteCount = 0;
        }
    }
}
