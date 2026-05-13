using helengine.editor;
using Font = System.Drawing.Font;
using FontStyle = System.Drawing.FontStyle;
using GraphicsUnit = System.Drawing.GraphicsUnit;

namespace helengine.editor.app {
    /// <summary>
    /// Resolves one canonical project file path and boots the WinForms editor host.
    /// </summary>
    internal static class Program {
        /// <summary>
        /// Starts the editor application or headless build mode depending on the provided arguments.
        /// </summary>
        /// <param name="args">Command-line arguments provided by the operating system shell.</param>
        [STAThread]
        static int Main(string[] args) {
            if (TryRunEditorCommandMode(args, out int commandExitCode)) {
                return commandExitCode;
            }

            if (TryRunBuildMode(args, out int buildExitCode)) {
                return buildExitCode;
            }

            if (!TryGetProjectPath(args, out var projectPath)) {
                return 1;
            }

            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm(projectPath));
            return 0;
        }

        /// <summary>
        /// Runs the editor's headless build mode when the requested arguments include `--build`.
        /// </summary>
        /// <param name="args">Command-line arguments provided by the operating system shell.</param>
        /// <param name="exitCode">Process exit code produced by the headless build mode.</param>
        /// <returns>True when the arguments requested headless build mode.</returns>
        static bool TryRunBuildMode(string[] args, out int exitCode) {
            exitCode = 0;
            if (!EditorCliArgumentParser.IsBuildModeRequested(args)) {
                return false;
            }

            if (!EditorCliArgumentParser.TryParseBuildOptions(args, out EditorCliBuildOptions options, out string errorMessage)) {
                Console.Error.WriteLine(errorMessage);
                exitCode = 1;
                return true;
            }

            try {
                IReadOnlyList<IAssetImporterRegistration> importers = EditorHostImporterFactory.CreateDefault();
                FontAsset defaultFontAsset = GDIFontProcessor.ImportFont(new Font("Consolas", 12, FontStyle.Regular, GraphicsUnit.Pixel));
                EditorCliBuildRunner runner = new EditorCliBuildRunner(importers, defaultFontAsset);
                EditorBuildExecutionResult result = runner.Run(options);
                if (result.Succeeded) {
                    Console.WriteLine(result.Message);
                    exitCode = 0;
                } else {
                    Console.Error.WriteLine(result.Message);
                    exitCode = 1;
                }
            } catch (Exception exception) {
                Console.Error.WriteLine(exception.ToString());
                exitCode = 1;
            }

            return true;
        }

        /// <summary>
        /// Runs one headless project-authored editor command when the requested arguments include `--editor-command`.
        /// </summary>
        /// <param name="args">Command-line arguments provided by the operating system shell.</param>
        /// <param name="exitCode">Process exit code produced by the headless editor-command mode.</param>
        /// <returns>True when the arguments requested headless editor-command mode.</returns>
        static bool TryRunEditorCommandMode(string[] args, out int exitCode) {
            exitCode = 0;
            if (!EditorCliArgumentParser.IsEditorCommandModeRequested(args)) {
                return false;
            }

            if (!EditorCliArgumentParser.TryParseEditorCommandOptions(args, out EditorCliCommandOptions options, out string errorMessage)) {
                Console.Error.WriteLine(errorMessage);
                exitCode = 1;
                return true;
            }

            try {
                FontAsset defaultFontAsset = GDIFontProcessor.ImportFont(new Font("Consolas", 12, FontStyle.Regular, GraphicsUnit.Pixel));
                EditorCliCommandRunner runner = new EditorCliCommandRunner(defaultFontAsset);
                EditorBuildExecutionResult result = runner.Run(options);
                if (result.Succeeded) {
                    Console.WriteLine(result.Message);
                    exitCode = 0;
                } else {
                    Console.Error.WriteLine(result.Message);
                    exitCode = 1;
                }
            } catch (Exception exception) {
                Console.Error.WriteLine(exception.ToString());
                exitCode = 1;
            }

            return true;
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
