using System.Diagnostics;
using System.Text;

namespace helengine.editor {
    /// <summary>
    /// Builds the generated scripting solution by invoking the local `dotnet` CLI.
    /// </summary>
    public sealed class EditorDotNetScriptBuildTool : IEditorScriptBuildTool {
        /// <summary>
        /// Build configuration used for editor-driven script reloads.
        /// </summary>
        const string BuildConfigurationValue = "Debug";

        /// <summary>
        /// CLI executable used for solution builds.
        /// </summary>
        const string DotNetExecutableName = "dotnet";

        /// <summary>
        /// Builds one solution file and returns the captured process outcome.
        /// </summary>
        /// <param name="solutionPath">Absolute path to the generated solution file.</param>
        /// <returns>Build result describing success or failure.</returns>
        public EditorBuildExecutionResult Build(string solutionPath) {
            if (string.IsNullOrWhiteSpace(solutionPath)) {
                throw new ArgumentException("Solution path must be provided.", nameof(solutionPath));
            }

            string workingDirectory = Path.GetDirectoryName(Path.GetFullPath(solutionPath));
            if (string.IsNullOrWhiteSpace(workingDirectory)) {
                workingDirectory = Environment.CurrentDirectory;
            }

            ProcessStartInfo startInfo = new ProcessStartInfo {
                FileName = DotNetExecutableName,
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add("build");
            startInfo.ArgumentList.Add(solutionPath);
            startInfo.ArgumentList.Add("--configuration");
            startInfo.ArgumentList.Add(BuildConfigurationValue);
            startInfo.ArgumentList.Add("--nologo");

            using Process process = Process.Start(startInfo);
            if (process == null) {
                throw new InvalidOperationException($"Failed to launch '{DotNetExecutableName}'.");
            }

            string stdout = process.StandardOutput.ReadToEnd();
            string stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode == 0) {
                return EditorBuildExecutionResult.Success($"Script build completed: {solutionPath}");
            }

            StringBuilder messageBuilder = new StringBuilder();
            messageBuilder.Append(DotNetExecutableName);
            messageBuilder.Append(" build failed with exit code ");
            messageBuilder.Append(process.ExitCode);
            messageBuilder.Append('.');

            string output = ChooseFailureOutput(stdout, stderr);
            if (!string.IsNullOrWhiteSpace(output)) {
                messageBuilder.Append(' ');
                messageBuilder.Append(output.Trim());
            }

            return EditorBuildExecutionResult.Failure(messageBuilder.ToString());
        }

        /// <summary>
        /// Chooses the most useful captured output for a failed `dotnet` build.
        /// </summary>
        /// <param name="stdout">Captured standard output.</param>
        /// <param name="stderr">Captured standard error.</param>
        /// <returns>Preferred failure output text.</returns>
        string ChooseFailureOutput(string stdout, string stderr) {
            if (!string.IsNullOrWhiteSpace(stderr)) {
                return stderr;
            }

            return stdout ?? string.Empty;
        }
    }
}
