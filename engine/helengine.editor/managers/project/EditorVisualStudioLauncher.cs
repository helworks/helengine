using System.Diagnostics;

namespace helengine.editor {
    /// <summary>
    /// Opens generated solutions in Visual Studio when an installation can be located on the host machine.
    /// </summary>
    public sealed class EditorVisualStudioLauncher : IEditorIdeLauncher, IEditorIdeSolutionDetector {
        /// <summary>
        /// Installer utility name used to discover Visual Studio instances.
        /// </summary>
        const string VsWhereExecutableName = "vswhere.exe";

        /// <summary>
        /// Visual Studio installation root folder name used when searching for the 2026 IDE.
        /// </summary>
        const string VisualStudio2026FolderName = "2026";

        /// <summary>
        /// Executable name used by Visual Studio desktop installations.
        /// </summary>
        const string VisualStudioExecutableName = "devenv.exe";

        /// <summary>
        /// Opens one generated solution file using the best Visual Studio launcher that can be resolved locally.
        /// </summary>
        /// <param name="solutionPath">Absolute path to the generated solution file.</param>
        public void OpenSolution(string solutionPath) {
            if (string.IsNullOrWhiteSpace(solutionPath)) {
                throw new ArgumentException("Solution path must be provided.", nameof(solutionPath));
            }

            string fullSolutionPath = Path.GetFullPath(solutionPath);
            if (!File.Exists(fullSolutionPath)) {
                throw new FileNotFoundException($"Solution file '{fullSolutionPath}' does not exist.", fullSolutionPath);
            }

            string launcherPath = TryResolveVisualStudioExecutablePath();
            if (string.IsNullOrWhiteSpace(launcherPath)) {
                throw new InvalidOperationException("Visual Studio could not be located on this machine.");
            }

            StartProcess(launcherPath, Quote(fullSolutionPath), Path.GetDirectoryName(fullSolutionPath));
        }

        /// <summary>
        /// Returns whether one matching Visual Studio window already appears to be hosting the supplied solution.
        /// </summary>
        /// <param name="solutionPath">Absolute path to the generated solution file.</param>
        /// <returns>True when a running Visual Studio instance already looks like it has the same solution open.</returns>
        public bool IsSolutionAlreadyOpen(string solutionPath) {
            if (string.IsNullOrWhiteSpace(solutionPath)) {
                throw new ArgumentException("Solution path must be provided.", nameof(solutionPath));
            }

            string fullSolutionPath = Path.GetFullPath(solutionPath);
            string solutionFileName = Path.GetFileName(fullSolutionPath);
            string solutionNameWithoutExtension = Path.GetFileNameWithoutExtension(fullSolutionPath);
            Process[] processes = Process.GetProcessesByName("devenv");

            for (int processIndex = 0; processIndex < processes.Length; processIndex++) {
                Process process = processes[processIndex];
                string windowTitle = GetProcessWindowTitle(process);
                if (string.IsNullOrWhiteSpace(windowTitle)) {
                    continue;
                }

                if (windowTitle.Contains(solutionFileName, StringComparison.OrdinalIgnoreCase) ||
                    windowTitle.Contains(solutionNameWithoutExtension, StringComparison.OrdinalIgnoreCase)) {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Resolves the best available Visual Studio executable for the local machine.
        /// </summary>
        /// <returns>Absolute `devenv.exe` path when one can be located; otherwise an empty string.</returns>
        string TryResolveVisualStudioExecutablePath() {
            string launcherPath = TryResolveVisualStudioPathFromVsWhere();
            if (!string.IsNullOrWhiteSpace(launcherPath)) {
                return launcherPath;
            }

            launcherPath = TryResolveVisualStudio2026Path();
            if (!string.IsNullOrWhiteSpace(launcherPath)) {
                return launcherPath;
            }

            return TryResolveVisualStudioExecutableFromInstallRoots();
        }

        /// <summary>
        /// Uses `vswhere.exe` to locate a Visual Studio installation and resolve its `devenv.exe`.
        /// </summary>
        /// <returns>Absolute launcher path when found; otherwise an empty string.</returns>
        string TryResolveVisualStudioPathFromVsWhere() {
            string vsWherePath = TryResolveVsWherePath();
            if (string.IsNullOrWhiteSpace(vsWherePath)) {
                return string.Empty;
            }

            string[] arguments = new[] {
                "-latest",
                "-products",
                "*",
                "-requires",
                "Microsoft.Component.MSBuild",
                "-find",
                "Common7\\IDE\\devenv.exe"
            };

            string output = RunProcessAndCaptureStandardOutput(vsWherePath, string.Join(" ", arguments));
            if (string.IsNullOrWhiteSpace(output)) {
                return string.Empty;
            }

            string[] lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < lines.Length; i++) {
                string candidatePath = lines[i].Trim();
                if (File.Exists(candidatePath)) {
                    return candidatePath;
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// Searches the standard Program Files locations for a Visual Studio 2026 `devenv.exe` installation.
        /// </summary>
        /// <returns>Absolute launcher path when found; otherwise an empty string.</returns>
        string TryResolveVisualStudio2026Path() {
            string[] roots = new[] {
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
            };

            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++) {
                string root = roots[rootIndex];
                if (string.IsNullOrWhiteSpace(root)) {
                    continue;
                }

                string visualStudioRoot = Path.Combine(root, "Microsoft Visual Studio", VisualStudio2026FolderName);
                if (!Directory.Exists(visualStudioRoot)) {
                    continue;
                }

                string[] candidatePaths = Directory.GetFiles(visualStudioRoot, VisualStudioExecutableName, SearchOption.AllDirectories);
                for (int candidateIndex = 0; candidateIndex < candidatePaths.Length; candidateIndex++) {
                    string candidatePath = candidatePaths[candidateIndex];
                    if (!candidatePath.Contains(Path.DirectorySeparatorChar + VisualStudio2026FolderName + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
                        !candidatePath.Contains(Path.AltDirectorySeparatorChar + VisualStudio2026FolderName + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) {
                        continue;
                    }

                    return candidatePath;
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// Searches the standard Program Files locations for any Visual Studio `devenv.exe` installation.
        /// </summary>
        /// <returns>Absolute launcher path when found; otherwise an empty string.</returns>
        string TryResolveVisualStudioExecutableFromInstallRoots() {
            string[] roots = new[] {
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
            };

            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++) {
                string root = roots[rootIndex];
                if (string.IsNullOrWhiteSpace(root)) {
                    continue;
                }

                string visualStudioRoot = Path.Combine(root, "Microsoft Visual Studio");
                if (!Directory.Exists(visualStudioRoot)) {
                    continue;
                }

                string[] candidatePaths = Directory.GetFiles(visualStudioRoot, VisualStudioExecutableName, SearchOption.AllDirectories);
                for (int candidateIndex = 0; candidateIndex < candidatePaths.Length; candidateIndex++) {
                    string candidatePath = candidatePaths[candidateIndex];
                    if (File.Exists(candidatePath)) {
                        return candidatePath;
                    }
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// Locates the Visual Studio installer utility on the current machine.
        /// </summary>
        /// <returns>Absolute `vswhere.exe` path when found; otherwise an empty string.</returns>
        string TryResolveVsWherePath() {
            string[] roots = new[] {
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)
            };

            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++) {
                string root = roots[rootIndex];
                if (string.IsNullOrWhiteSpace(root)) {
                    continue;
                }

                string installerRoot = Path.Combine(root, "Microsoft Visual Studio", "Installer");
                if (!Directory.Exists(installerRoot)) {
                    continue;
                }

                string candidatePath = Path.Combine(installerRoot, VsWhereExecutableName);
                if (File.Exists(candidatePath)) {
                    return candidatePath;
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// Starts one local process and throws when it cannot be launched.
        /// </summary>
        /// <param name="fileName">Executable path or command name.</param>
        /// <param name="arguments">Command-line arguments passed to the process.</param>
        /// <param name="workingDirectory">Working directory used by the process.</param>
        void StartProcess(string fileName, string arguments, string workingDirectory) {
            ProcessStartInfo startInfo = new ProcessStartInfo {
                FileName = fileName,
                Arguments = arguments ?? string.Empty,
                WorkingDirectory = string.IsNullOrWhiteSpace(workingDirectory) ? Environment.CurrentDirectory : workingDirectory,
                UseShellExecute = false
            };

            using Process process = Process.Start(startInfo);
            if (process == null) {
                throw new InvalidOperationException($"Failed to launch '{fileName}'.");
            }
        }

        /// <summary>
        /// Executes one process and returns its standard output as text.
        /// </summary>
        /// <param name="fileName">Executable path or command name.</param>
        /// <param name="arguments">Command-line arguments passed to the process.</param>
        /// <returns>Collected standard output text.</returns>
        string RunProcessAndCaptureStandardOutput(string fileName, string arguments) {
            ProcessStartInfo startInfo = new ProcessStartInfo {
                FileName = fileName,
                Arguments = arguments ?? string.Empty,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using Process process = Process.Start(startInfo);
            if (process == null) {
                throw new InvalidOperationException($"Failed to launch '{fileName}'.");
            }

            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0 && !string.IsNullOrWhiteSpace(error)) {
                return string.Empty;
            }

            return output ?? string.Empty;
        }

        /// <summary>
        /// Reads one process window title without letting transient process errors escape.
        /// </summary>
        /// <param name="process">Process to inspect.</param>
        /// <returns>Window title when available; otherwise an empty string.</returns>
        static string GetProcessWindowTitle(Process process) {
            if (process == null) {
                return string.Empty;
            }

            try {
                return process.MainWindowTitle ?? string.Empty;
            } catch {
                return string.Empty;
            }
        }

        /// <summary>
        /// Surrounds one path with double quotes for command-line usage.
        /// </summary>
        /// <param name="path">Path value to quote.</param>
        /// <returns>Quoted path text.</returns>
        static string Quote(string path) {
            return "\"" + path + "\"";
        }
    }
}
