using helengine.editor;

namespace helengine.editor.app {
    /// <summary>
    /// Resolves one canonical project file path and boots the WinForms editor host.
    /// </summary>
    internal static class Program {
        /// <summary>
        /// Starts the editor application when one valid project argument is supplied.
        /// </summary>
        /// <param name="args">Command-line arguments provided by the operating system shell.</param>
        [STAThread]
        static void Main(string[] args) {
            if (!TryGetProjectPath(args, out var projectPath)) {
                return;
            }

            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm(projectPath));
        }

        /// <summary>
        /// Resolves one incoming project argument to the validated canonical `.heproj` path expected by the editor.
        /// </summary>
        /// <param name="args">Command-line arguments that may contain a project path.</param>
        /// <param name="projectPath">Resolved canonical project file path when one valid argument is supplied.</param>
        /// <returns><c>true</c> when one valid project path argument exists; otherwise <c>false</c>.</returns>
        static bool TryGetProjectPath(string[] args, out string projectPath) {
            projectPath = string.Empty;
            try {
                EditorStartupProjectPathResolver resolver = new EditorStartupProjectPathResolver();
                projectPath = resolver.Resolve(args);
                return true;
            } catch (InvalidOperationException exception) {
                ShowStartupError(exception.Message);
            }

            return false;
        }

        /// <summary>
        /// Shows one blocking startup error dialog when the editor cannot resolve a valid project path.
        /// </summary>
        /// <param name="message">Human-readable startup error message.</param>
        static void ShowStartupError(string message) {
            MessageBox.Show(
                message,
                "helengine",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}
