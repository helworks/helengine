using System.Diagnostics;

namespace helengine.editor {
    /// <summary>
    /// Starts the real desktop player process for editor Play. The player is launched directly (no shell) with its
    /// console streams redirected, and every argument is passed as its own <see cref="ProcessStartInfo.ArgumentList"/>
    /// entry so no command line is ever assembled by string concatenation.
    /// </summary>
    public sealed class EditorPlayProcessLauncher : IEditorPlayProcessLauncher {
        /// <summary>
        /// Queue drained on the editor frame thread; the started process delivers its events through it.
        /// </summary>
        readonly EditorMainThreadActionQueue MainThreadQueue;

        /// <summary>
        /// Initializes the launcher.
        /// </summary>
        /// <param name="mainThreadQueue">Queue the editor drains every frame, used to deliver player events on the frame thread.</param>
        /// <exception cref="ArgumentNullException">Thrown when the queue is null.</exception>
        public EditorPlayProcessLauncher(EditorMainThreadActionQueue mainThreadQueue) {
            MainThreadQueue = mainThreadQueue ?? throw new ArgumentNullException(nameof(mainThreadQueue));
        }

        /// <summary>
        /// Builds the process start description for one launch request: the executable started directly from its own
        /// directory, standard output and error redirected, and the request arguments copied one-to-one into the
        /// argument list.
        /// </summary>
        /// <param name="request">Executable path and ordered arguments.</param>
        /// <returns>Start description for <see cref="Process.Start()"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the request is null.</exception>
        public static ProcessStartInfo CreateStartInfo(EditorPlayLaunchRequest request) {
            if (request == null) {
                throw new ArgumentNullException(nameof(request));
            }

            ProcessStartInfo startInfo = new ProcessStartInfo {
                FileName = request.PlayerExecutablePath,
                WorkingDirectory = Path.GetDirectoryName(request.PlayerExecutablePath),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            for (int index = 0; index < request.Arguments.Count; index++) {
                startInfo.ArgumentList.Add(request.Arguments[index]);
            }

            return startInfo;
        }

        /// <summary>
        /// Starts the player described by the request and begins streaming its console output. Exit notification is
        /// enabled only after both console readers are running, so the exit is always delivered after the last line.
        /// </summary>
        /// <param name="request">Executable path and ordered command-line arguments.</param>
        /// <returns>Handle to the started player process.</returns>
        /// <exception cref="FileNotFoundException">Thrown when the player executable does not exist.</exception>
        public IEditorPlayProcess Start(EditorPlayLaunchRequest request) {
            ProcessStartInfo startInfo = CreateStartInfo(request);
            if (!File.Exists(startInfo.FileName)) {
                throw new FileNotFoundException(
                    $"The desktop player executable '{startInfo.FileName}' does not exist. Build helengine.editor.app, which copies helengine.player beside the editor.",
                    startInfo.FileName);
            }

            Process process = new Process {
                StartInfo = startInfo
            };
            EditorPlayProcess playProcess = new EditorPlayProcess(process, MainThreadQueue);
            try {
                process.Start();
            } catch {
                process.Dispose();
                throw;
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.EnableRaisingEvents = true;
            return playProcess;
        }
    }
}
