using System.Diagnostics;
using System.Text;

namespace helengine.editor {
    /// <summary>
    /// Runs <c>git rev-parse</c> and <c>dotnet publish</c> for the on-demand engine codegen build.
    /// </summary>
    public sealed class DotNetEngineCodegenToolPublisher : IEngineCodegenToolPublisher {
        /// <summary>
        /// Executable name used to read the submodule commit.
        /// </summary>
        const string GitExecutableName = "git";

        /// <summary>
        /// Executable name used to publish the codegen project.
        /// </summary>
        const string DotNetExecutableName = "dotnet";

        /// <summary>
        /// Configuration the on-demand build publishes in.
        /// </summary>
        const string PublishConfiguration = "Release";

        /// <inheritdoc />
        public string ReadCommit(string submoduleRootPath) {
            if (string.IsNullOrWhiteSpace(submoduleRootPath)) {
                throw new ArgumentException("Submodule root path must be provided.", nameof(submoduleRootPath));
            }

            ProcessStartInfo startInfo = new ProcessStartInfo {
                FileName = GitExecutableName,
                WorkingDirectory = submoduleRootPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add("rev-parse");
            startInfo.ArgumentList.Add("HEAD");

            Run(startInfo, out string stdout, out string stderr, out int exitCode);
            if (exitCode != 0) {
                throw new InvalidOperationException($"git rev-parse HEAD failed in '{submoduleRootPath}' with exit code {exitCode}. {stderr.Trim()}");
            }

            return stdout.Trim();
        }

        /// <inheritdoc />
        public void Publish(string codegenProjectPath, string outputDirectoryPath) {
            if (string.IsNullOrWhiteSpace(codegenProjectPath)) {
                throw new ArgumentException("Codegen project path must be provided.", nameof(codegenProjectPath));
            }
            if (string.IsNullOrWhiteSpace(outputDirectoryPath)) {
                throw new ArgumentException("Output directory path must be provided.", nameof(outputDirectoryPath));
            }

            Directory.CreateDirectory(outputDirectoryPath);
            ProcessStartInfo startInfo = new ProcessStartInfo {
                FileName = DotNetExecutableName,
                WorkingDirectory = Path.GetDirectoryName(Path.GetFullPath(codegenProjectPath)) ?? Environment.CurrentDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add("publish");
            startInfo.ArgumentList.Add(codegenProjectPath);
            startInfo.ArgumentList.Add("-c");
            startInfo.ArgumentList.Add(PublishConfiguration);
            startInfo.ArgumentList.Add("-o");
            startInfo.ArgumentList.Add(outputDirectoryPath);
            startInfo.ArgumentList.Add("--nologo");

            Run(startInfo, out string stdout, out string stderr, out int exitCode);
            if (exitCode != 0) {
                string output = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
                throw new InvalidOperationException($"dotnet publish of '{codegenProjectPath}' failed with exit code {exitCode}. {output.Trim()}");
            }
        }

        /// <summary>
        /// Runs one process and drains both streams asynchronously so a chatty child cannot deadlock the pipes.
        /// </summary>
        /// <param name="startInfo">Configured start information with both output streams redirected.</param>
        /// <param name="stdout">Receives the captured standard output.</param>
        /// <param name="stderr">Receives the captured standard error.</param>
        /// <param name="exitCode">Receives the process exit code.</param>
        static void Run(ProcessStartInfo startInfo, out string stdout, out string stderr, out int exitCode) {
            using Process process = Process.Start(startInfo)
                ?? throw new InvalidOperationException($"Failed to launch '{startInfo.FileName}'.");
            StringBuilder stdoutBuilder = new StringBuilder();
            StringBuilder stderrBuilder = new StringBuilder();
            process.OutputDataReceived += (sender, eventArgs) => { if (eventArgs.Data != null) { stdoutBuilder.AppendLine(eventArgs.Data); } };
            process.ErrorDataReceived += (sender, eventArgs) => { if (eventArgs.Data != null) { stderrBuilder.AppendLine(eventArgs.Data); } };
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.WaitForExit();
            stdout = stdoutBuilder.ToString();
            stderr = stderrBuilder.ToString();
            exitCode = process.ExitCode;
        }
    }
}
