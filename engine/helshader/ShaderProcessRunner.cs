using System.Diagnostics;

namespace helshader {
    /// <summary>
    /// Executes external shader tool processes.
    /// </summary>
    public class ShaderProcessRunner {
        /// <summary>
        /// Runs a process and captures standard output and error output.
        /// </summary>
        /// <param name="fileName">Executable path.</param>
        /// <param name="arguments">Command line arguments.</param>
        /// <param name="workingDirectory">Working directory for the process.</param>
        /// <returns>Process result data.</returns>
        public ShaderProcessResult Run(string fileName, string arguments, string workingDirectory) {
            if (string.IsNullOrWhiteSpace(fileName)) {
                throw new ArgumentException("Executable path must be provided.", nameof(fileName));
            }

            string safeArguments = arguments;
            if (safeArguments == null) {
                safeArguments = string.Empty;
            }

            string safeWorkingDirectory = workingDirectory;
            if (safeWorkingDirectory == null) {
                safeWorkingDirectory = string.Empty;
            }

            ProcessStartInfo startInfo = new ProcessStartInfo {
                FileName = fileName,
                Arguments = safeArguments,
                WorkingDirectory = safeWorkingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (Process process = Process.Start(startInfo)) {
                if (process == null) {
                    throw new InvalidOperationException($"Failed to start process '{fileName}'.");
                }

                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();
                return new ShaderProcessResult(process.ExitCode, output, error);
            }
        }
    }
}
