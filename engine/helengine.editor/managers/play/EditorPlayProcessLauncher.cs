using System.Diagnostics;

namespace helengine.editor {
    /// <summary>
    /// Starts the real desktop player process for editor Play. The player is launched directly (no shell) with its
    /// console streams redirected, and every argument is passed as its own <see cref="ProcessStartInfo.ArgumentList"/>
    /// entry so no command line is ever assembled by string concatenation.
    /// <para>
    /// Right after the process starts, the editor grants it the right to take the foreground, so the player window can
    /// come to the front when it opens. The grant names the started process id rather than <c>ASFW_ANY</c>:
    /// <see cref="Process"/> exposes the id only once the process exists, and granting every process would let any app
    /// steal the foreground until the next input. The player only activates its window after loading its adapter and
    /// creating the window, long after the grant. Windows refuses the grant when the editor is no longer the foreground
    /// app (the user switched apps while Play was cooking); the refusal is logged and the player then opens behind,
    /// flashing in the taskbar. The editor never forces the foreground.
    /// </para>
    /// </summary>
    public sealed class EditorPlayProcessLauncher : IEditorPlayProcessLauncher {
        /// <summary>
        /// Start of the editor log line written when Windows refused to let the started player take the foreground; the
        /// Win32 error code follows it.
        /// </summary>
        public const string ForegroundGrantRefusedPrefix = "[play] the player may not take the foreground (Windows error ";

        /// <summary>
        /// End of the editor log line written when Windows refused to let the started player take the foreground.
        /// </summary>
        public const string ForegroundGrantRefusedSuffix = "): the editor was not the foreground app at launch, so the player window opens behind and flashes in the taskbar.";

        /// <summary>
        /// Queue drained on the editor frame thread; the started process delivers its events through it.
        /// </summary>
        readonly EditorMainThreadActionQueue MainThreadQueue;

        /// <summary>
        /// Grants the started player the right to bring its window to the foreground.
        /// </summary>
        readonly IEditorPlayForegroundPermission ForegroundPermission;

        /// <summary>
        /// Initializes the launcher.
        /// </summary>
        /// <param name="mainThreadQueue">Queue the editor drains every frame, used to deliver player events on the frame thread.</param>
        /// <param name="foregroundPermission">Grants each started player the right to take the foreground.</param>
        /// <exception cref="ArgumentNullException">Thrown when the queue or the foreground permission is null.</exception>
        public EditorPlayProcessLauncher(EditorMainThreadActionQueue mainThreadQueue, IEditorPlayForegroundPermission foregroundPermission) {
            if (mainThreadQueue == null) {
                throw new ArgumentNullException(nameof(mainThreadQueue));
            } else if (foregroundPermission == null) {
                throw new ArgumentNullException(nameof(foregroundPermission));
            }

            MainThreadQueue = mainThreadQueue;
            ForegroundPermission = foregroundPermission;
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
        /// Starts the player described by the request, grants it the right to take the foreground and begins streaming
        /// its console output. A refused grant is logged and does not fail the launch. Exit notification is enabled only
        /// after both console readers are running, so the exit is always delivered after the last line.
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

            if (!ForegroundPermission.TryAllow(process.Id, out int foregroundErrorCode)) {
                Logger.WriteLine(ForegroundGrantRefusedPrefix + foregroundErrorCode + ForegroundGrantRefusedSuffix);
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.EnableRaisingEvents = true;
            return playProcess;
        }
    }
}
