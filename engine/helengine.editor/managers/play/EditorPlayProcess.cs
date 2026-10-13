using System.Diagnostics;

namespace helengine.editor {
    /// <summary>
    /// Wraps one running desktop player <see cref="Process"/>. The operating system reports console lines and the exit
    /// on thread-pool threads; this wrapper never raises its events there. It queues them on the editor's
    /// <see cref="EditorMainThreadActionQueue"/>, so <see cref="OutputLine"/>, <see cref="ErrorLine"/> and
    /// <see cref="Exited"/> are raised on the editor frame thread, in arrival order, with the exit always last.
    /// </summary>
    public sealed class EditorPlayProcess : IEditorPlayProcess {
        /// <summary>
        /// Underlying operating-system process.
        /// </summary>
        readonly Process Process;

        /// <summary>
        /// Queue drained on the editor frame thread that delivers every event of this process.
        /// </summary>
        readonly EditorMainThreadActionQueue MainThreadQueue;

        /// <summary>
        /// True once the exit has been delivered on the frame thread; only the frame thread reads or writes it.
        /// </summary>
        bool ExitDelivered;

        /// <summary>
        /// Wraps a process that has not been started yet and subscribes to its console and exit notifications.
        /// </summary>
        /// <param name="process">Configured, not yet started process with redirected console streams.</param>
        /// <param name="mainThreadQueue">Queue the editor drains every frame.</param>
        /// <exception cref="ArgumentNullException">Thrown when the process or queue is null.</exception>
        public EditorPlayProcess(Process process, EditorMainThreadActionQueue mainThreadQueue) {
            Process = process ?? throw new ArgumentNullException(nameof(process));
            MainThreadQueue = mainThreadQueue ?? throw new ArgumentNullException(nameof(mainThreadQueue));
            Process.OutputDataReceived += HandleOutputDataReceived;
            Process.ErrorDataReceived += HandleErrorDataReceived;
            Process.Exited += HandleProcessExited;
        }

        /// <summary>
        /// Raised on the editor frame thread for each line the player writes to standard output.
        /// </summary>
        public event Action<string> OutputLine;

        /// <summary>
        /// Raised on the editor frame thread for each line the player writes to standard error.
        /// </summary>
        public event Action<string> ErrorLine;

        /// <summary>
        /// Raised once on the editor frame thread after the player exited and every console line was delivered.
        /// </summary>
        public event Action Exited;

        /// <summary>
        /// Gets the operating-system identifier of the player process.
        /// </summary>
        public int ProcessId => Process.Id;

        /// <summary>
        /// Gets whether the player's exit has been delivered on the editor frame thread. The value turns true together
        /// with <see cref="Exited"/>, never earlier, so the editor sees every console line before it observes the exit.
        /// </summary>
        public bool HasExited => ExitDelivered;

        /// <summary>
        /// Gets the player's exit code; only valid once <see cref="HasExited"/> is true.
        /// </summary>
        public int ExitCode => Process.ExitCode;

        /// <summary>
        /// Terminates the player and every process it started. Does nothing when the player has already exited.
        /// </summary>
        public void Kill() {
            if (Process.HasExited) {
                return;
            }

            Process.Kill(entireProcessTree: true);
        }

        /// <summary>
        /// Queues one standard-output line for delivery on the frame thread; the end-of-stream marker is ignored.
        /// </summary>
        /// <param name="sender">Process that produced the line.</param>
        /// <param name="args">Received line, or null at end of stream.</param>
        void HandleOutputDataReceived(object sender, DataReceivedEventArgs args) {
            if (args.Data == null) {
                return;
            }

            string line = args.Data;
            MainThreadQueue.Enqueue(() => RaiseOutputLine(line));
        }

        /// <summary>
        /// Queues one standard-error line for delivery on the frame thread; the end-of-stream marker is ignored.
        /// </summary>
        /// <param name="sender">Process that produced the line.</param>
        /// <param name="args">Received line, or null at end of stream.</param>
        void HandleErrorDataReceived(object sender, DataReceivedEventArgs args) {
            if (args.Data == null) {
                return;
            }

            string line = args.Data;
            MainThreadQueue.Enqueue(() => RaiseErrorLine(line));
        }

        /// <summary>
        /// Waits until both console readers reached end of stream, then queues the exit, so the exit is delivered after
        /// every line the player wrote.
        /// </summary>
        /// <param name="sender">Process that exited.</param>
        /// <param name="args">Unused event arguments.</param>
        void HandleProcessExited(object sender, EventArgs args) {
            Process.WaitForExit();
            MainThreadQueue.Enqueue(RaiseExited);
        }

        /// <summary>
        /// Raises <see cref="OutputLine"/> on the frame thread.
        /// </summary>
        /// <param name="line">Line the player wrote to standard output.</param>
        void RaiseOutputLine(string line) {
            OutputLine?.Invoke(line);
        }

        /// <summary>
        /// Raises <see cref="ErrorLine"/> on the frame thread.
        /// </summary>
        /// <param name="line">Line the player wrote to standard error.</param>
        void RaiseErrorLine(string line) {
            ErrorLine?.Invoke(line);
        }

        /// <summary>
        /// Marks the exit as delivered and raises <see cref="Exited"/> on the frame thread.
        /// </summary>
        void RaiseExited() {
            ExitDelivered = true;
            Exited?.Invoke();
        }
    }
}
