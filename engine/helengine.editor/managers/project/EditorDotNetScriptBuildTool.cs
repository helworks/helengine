using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace helengine.editor {
    /// <summary>
    /// Builds the generated scripting solution by invoking the local `dotnet` CLI.
    /// </summary>
    public sealed class EditorDotNetScriptBuildTool : IEditorScriptBuildToolWithOutputRoot {
        /// <summary>
        /// Build configuration used for editor-driven script reloads.
        /// </summary>
        const string BuildConfigurationValue = "Debug";

        /// <summary>
        /// CLI executable used for solution builds.
        /// </summary>
        const string DotNetExecutableName = "dotnet";

        /// <summary>
        /// Prefix for short-lived MSBuild property transport files used to preserve arbitrary output-root characters.
        /// </summary>
        const string OutputRootTransportFilePrefix = "helengine-output-root-";

        /// <summary>
        /// Temporary root used for MSBuild's scalar path properties before results are published to the requested root.
        /// </summary>
        const string MsBuildOutputRootDirectoryName = "helengine-msbuild-output-roots";

        /// <summary>
        /// Builds one solution file and returns the captured process outcome.
        /// </summary>
        /// <param name="solutionPath">Absolute path to the generated solution file.</param>
        /// <returns>Build result describing success or failure.</returns>
        public EditorBuildExecutionResult Build(string solutionPath) {
            return Build(solutionPath, string.Empty);
        }

        /// <summary>
        /// Builds one solution with an optional invocation-specific generated-output root.
        /// </summary>
        /// <param name="solutionPath">Absolute path to the generated solution file.</param>
        /// <param name="executionOutputRootPath">Unique generated-output root for this invocation, or empty to use project metadata defaults.</param>
        /// <returns>Structured execution result describing the process outcome.</returns>
        public EditorBuildExecutionResult Build(string solutionPath, string executionOutputRootPath) {
            if (string.IsNullOrWhiteSpace(solutionPath)) {
                throw new ArgumentException("Solution path must be provided.", nameof(solutionPath));
            }
            if (executionOutputRootPath == null) {
                throw new ArgumentNullException(nameof(executionOutputRootPath));
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
            string outputRootTransportFilePath = string.Empty;
            string msbuildOutputRootPath = string.Empty;
            if (!string.IsNullOrWhiteSpace(executionOutputRootPath)) {
                msbuildOutputRootPath = ResolveMsBuildOutputRootPath(executionOutputRootPath);
                outputRootTransportFilePath = CreateOutputRootTransportFile(msbuildOutputRootPath);
                startInfo.ArgumentList.Add("-p:HelengineExecutionOutputRootFile=" + outputRootTransportFilePath);
            }

            try {
                using Process process = Process.Start(startInfo);
                if (process == null) {
                    throw new InvalidOperationException($"Failed to launch '{DotNetExecutableName}'.");
                }

                CaptureProcessOutput(process, out string stdout, out string stderr);

                if (process.ExitCode == 0) {
                    if (!string.IsNullOrWhiteSpace(msbuildOutputRootPath)) {
                        PublishBuildOutputs(msbuildOutputRootPath, executionOutputRootPath);
                    }

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
            } finally {
                if (!string.IsNullOrWhiteSpace(outputRootTransportFilePath)
                    && File.Exists(outputRootTransportFilePath)) {
                    File.Delete(outputRootTransportFilePath);
                }
                if (!string.IsNullOrWhiteSpace(msbuildOutputRootPath)
                    && Directory.Exists(msbuildOutputRootPath)) {
                    Directory.Delete(msbuildOutputRootPath, true);
                }
            }
        }

        /// <summary>
        /// Resolves a filesystem-safe temporary MSBuild root for one exact requested output root.
        /// </summary>
        /// <param name="executionOutputRootPath">Requested output root, which may contain MSBuild separator characters.</param>
        /// <returns>Safe temporary MSBuild output root.</returns>
        static string ResolveMsBuildOutputRootPath(string executionOutputRootPath) {
            byte[] pathHash = SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(executionOutputRootPath)));
            return Path.Combine(
                Path.GetTempPath(),
                MsBuildOutputRootDirectoryName,
                Convert.ToHexString(pathHash).ToLowerInvariant());
        }

        /// <summary>
        /// Creates one temporary MSBuild property file carrying the exact invocation output root.
        /// </summary>
        /// <param name="executionOutputRootPath">Output root to transport to the child process.</param>
        /// <returns>Path to the temporary property file.</returns>
        static string CreateOutputRootTransportFile(string executionOutputRootPath) {
            string fullOutputRootPath = Path.GetFullPath(executionOutputRootPath);
            string transportFilePath = Path.Combine(
                Path.GetTempPath(),
                OutputRootTransportFilePrefix + Guid.NewGuid().ToString("N") + ".props");
            string contents = "<Project>\n"
                + "  <PropertyGroup>\n"
                + "    <HelengineExecutionOutputRoot>"
                + fullOutputRootPath
                + "</HelengineExecutionOutputRoot>\n"
                + "  </PropertyGroup>\n"
                + "</Project>\n";
            File.WriteAllText(transportFilePath, contents, Encoding.UTF8);
            return transportFilePath;
        }

        /// <summary>
        /// Publishes completed MSBuild outputs into the exact invocation root requested by the caller.
        /// </summary>
        /// <param name="msbuildOutputRootPath">Safe temporary MSBuild output root.</param>
        /// <param name="executionOutputRootPath">Exact caller-visible output root.</param>
        static void PublishBuildOutputs(string msbuildOutputRootPath, string executionOutputRootPath) {
            if (!Directory.Exists(msbuildOutputRootPath)) {
                return;
            }

            string destinationRootPath = Path.GetFullPath(executionOutputRootPath);
            Directory.CreateDirectory(destinationRootPath);
            foreach (string sourceDirectoryPath in Directory.GetDirectories(msbuildOutputRootPath, "*", SearchOption.AllDirectories)) {
                string relativeDirectoryPath = Path.GetRelativePath(msbuildOutputRootPath, sourceDirectoryPath);
                Directory.CreateDirectory(Path.Combine(destinationRootPath, relativeDirectoryPath));
            }

            foreach (string sourceFilePath in Directory.GetFiles(msbuildOutputRootPath, "*", SearchOption.AllDirectories)) {
                string relativeFilePath = Path.GetRelativePath(msbuildOutputRootPath, sourceFilePath);
                string destinationFilePath = Path.Combine(destinationRootPath, relativeFilePath);
                string destinationDirectoryPath = Path.GetDirectoryName(destinationFilePath);
                if (!string.IsNullOrWhiteSpace(destinationDirectoryPath)) {
                    Directory.CreateDirectory(destinationDirectoryPath);
                }
                File.Copy(sourceFilePath, destinationFilePath, true);
            }
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

        /// <summary>
        /// Captures the redirected process output streams without risking the deadlock caused by sequential synchronous pipe reads on warning-heavy builds.
        /// </summary>
        /// <param name="process">Running build process whose redirected streams should be drained.</param>
        /// <param name="stdout">Captured standard output text.</param>
        /// <param name="stderr">Captured standard error text.</param>
        void CaptureProcessOutput(Process process, out string stdout, out string stderr) {
            if (process == null) {
                throw new ArgumentNullException(nameof(process));
            }

            StringBuilder stdoutBuilder = new StringBuilder();
            StringBuilder stderrBuilder = new StringBuilder();

            process.OutputDataReceived += (sender, eventArgs) => AppendCapturedLine(stdoutBuilder, eventArgs.Data);
            process.ErrorDataReceived += (sender, eventArgs) => AppendCapturedLine(stderrBuilder, eventArgs.Data);

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.WaitForExit();

            stdout = stdoutBuilder.ToString();
            stderr = stderrBuilder.ToString();
        }

        /// <summary>
        /// Appends one captured process-output line to the supplied buffer while preserving line boundaries.
        /// </summary>
        /// <param name="builder">Destination text buffer.</param>
        /// <param name="line">Captured output line.</param>
        void AppendCapturedLine(StringBuilder builder, string line) {
            if (builder == null) {
                throw new ArgumentNullException(nameof(builder));
            } else if (line == null) {
                return;
            }

            builder.AppendLine(line);
        }
    }
}
