using System.Diagnostics;

namespace helengine.editor {
    /// <summary>
    /// Wraps one running desktop player <see cref="Process"/>. The operating system reports console lines and the exit
    /// on thread-pool threads; this wrapper never raises its events there. It queues them on the editor's
    /// <see cref="EditorMainThreadActionQueue"/>, so <see cref="OutputLine"/>, <see cref="ErrorLine"/> and
    /// <see cref="Exited"/> are raised on the editor frame thread, in arrival order, with the exit after the console
    /// lines. Once the exit is delivered the process identity and exit code are captured and the process is disposed.
    /// </summary>
    public sealed class EditorPlayProcess : IEditorPlayProcess {
        /// <summary>
        /// Longest time the exit notification waits for the console streams to close. A child process that inherited
        /// the player's console handles can keep them open after the player itself exited; the exit is delivered anyway.
        /// </summary>
        static readonly TimeSpan ConsoleCloseTimeout = TimeSpan.FromSeconds(3);

        /// <summary>
        /// Underlying operating-system process; disposed once the exit has been delivered.
        /// </summary>
        readonly Process Process;

        /// <summary>
        /// Queue drained on the editor frame thread that delivers every event of this process.
        /// </summary>
        readonly EditorMainThreadActionQueue MainThreadQueue;

        /// <summary>
        /// Signalled when standard output reaches end of stream.
        /// </summary>
        readonly ManualResetEventSlim OutputClosed;

        /// <summary>
        /// Signalled when standard error reaches end of stream.
        /// </summary>
        readonly ManualResetEventSlim ErrorClosed;

        /// <summary>
        /// True once the exit has been delivered on the frame thread; only the frame thread reads or writes it.
        /// </summary>
        bool ExitDelivered;

        /// <summary>
        /// Process identifier captured when the exit was delivered, before the process was disposed.
        /// </summary>
        int DeliveredProcessId;

        /// <summary>
        /// Exit code captured when the exit was delivered, before the process was disposed.
        /// </summary>
        int DeliveredExitCode;

        /// <summary>
        /// Wraps a process that has not been started yet and subscribes to its console and exit notifications.
        /// </summary>
        /// <param name="process">Configured, not yet started process with redirected console streams.</param>
        /// <param name="mainThreadQueue">Queue the editor drains every frame.</param>
        /// <exception cref="ArgumentNullException">Thrown when the process or queue is null.</exception>
        public EditorPlayProcess(Process process, EditorMainThreadActionQueue mainThreadQueue) {
            Process = process ?? throw new ArgumentNullException(nameof(process));
            MainThreadQueue = mainThreadQueue ?? throw new ArgumentNullException(nameof(mainThreadQueue));
            OutputClosed = new ManualResetEventSlim(false);
            ErrorClosed = new ManualResetEventSlim(false);
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
        /// Raised once on the editor frame thread after the player exited and its console lines were delivered.
        /// </summary>
        public event Action Exited;

        /// <summary>
        /// Gets the operating-system identifier of the player process; captured at exit so it stays readable afterwards.
        /// </summary>
        public int ProcessId => ExitDelivered ? DeliveredProcessId : Process.Id;

        /// <summary>
        /// Gets whether the player's exit has been delivered on the editor frame thread. The value turns true together
        /// with <see cref="Exited"/>, never earlier, so the editor sees the console lines before it observes the exit.
        /// </summary>
        public bool HasExited => ExitDelivered;

        /// <summary>
        /// Gets the player's exit code captured when the exit was delivered.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown before <see cref="HasExited"/> is true.</exception>
        public int ExitCode {
            get {
                if (!ExitDelivered) {
                    throw new InvalidOperationException("The player exit code is only available once its exit was delivered.");
                }

                return DeliveredExitCode;
            }
        }

        /// <summary>
        /// Terminates the player and every process it started. Does nothing when the player has already exited.
        /// </summary>
        public void Kill() {
            if (ExitDelivered || Process.HasExited) {
                return;
            }

            Process.Kill(entireProcessTree: true);
        }

        /// <summary>
        /// Queues one standard-output line for delivery on the frame thread; end of stream signals <see cref="OutputClosed"/>.
        /// </summary>
        /// <param name="sender">Process that produced the line.</param>
        /// <param name="args">Received line, or null at end of stream.</param>
        void HandleOutputDataReceived(object sender, DataReceivedEventArgs args) {
            if (args.Data == null) {
                OutputClosed.Set();
                return;
            }

            string line = args.Data;
            MainThreadQueue.Enqueue(() => RaiseOutputLine(line));
        }

        /// <summary>
        /// Queues one standard-error line for delivery on the frame thread; end of stream signals <see cref="ErrorClosed"/>.
        /// </summary>
        /// <param name="sender">Process that produced the line.</param>
        /// <param name="args">Received line, or null at end of stream.</param>
        void HandleErrorDataReceived(object sender, DataReceivedEventArgs args) {
            if (args.Data == null) {
                ErrorClosed.Set();
                return;
            }

            string line = args.Data;
            MainThreadQueue.Enqueue(() => RaiseErrorLine(line));
        }

        /// <summary>
        /// Waits, bounded by <see cref="ConsoleCloseTimeout"/>, for both console streams to close, then queues the exit
        /// so it is normally delivered after every line the player wrote.
        /// </summary>
        /// <param name="sender">Process that exited.</param>
        /// <param name="args">Unused event arguments.</param>
        void HandleProcessExited(object sender, EventArgs args) {
            Stopwatch stopwatch = Stopwatch.StartNew();
            OutputClosed.Wait(ConsoleCloseTimeout);
            TimeSpan remaining = ConsoleCloseTimeout - stopwatch.Elapsed;
            ErrorClosed.Wait(remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero);
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
        /// Captures the identity and exit code, disposes the process, marks the exit delivered and raises
        /// <see cref="Exited"/> on the frame thread.
        /// </summary>
        void RaiseExited() {
            DeliveredProcessId = Process.Id;
            DeliveredExitCode = Process.ExitCode;
            Process.OutputDataReceived -= HandleOutputDataReceived;
            Process.ErrorDataReceived -= HandleErrorDataReceived;
            Process.Exited -= HandleProcessExited;
            Process.Dispose();
            ExitDelivered = true;
            Exited?.Invoke();
        }
    }
}
