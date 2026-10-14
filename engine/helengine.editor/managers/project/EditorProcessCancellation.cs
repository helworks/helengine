using System.Diagnostics;

namespace helengine.editor {
    /// <summary>
    /// Cancels the child processes one background operation starts, such as the <c>dotnet build</c> of an editor Play
    /// preparation. Cancelling is a flag plus a kill of the running process tree, never an exception: the operation
    /// polls <see cref="IsCancellationRequested"/> before it starts a process, attaches each process it starts, and a
    /// process attached after cancellation is refused so the caller kills it at once. All members are thread-safe.
    /// </summary>
    public sealed class EditorProcessCancellation {
        /// <summary>
        /// Guards the flag and the attached process, so a process is never killed while it is being disposed.
        /// </summary>
        readonly object Sync = new object();

        /// <summary>
        /// Set once the operation is cancelled.
        /// </summary>
        bool CancellationRequested;

        /// <summary>
        /// Process the operation is running now, or null.
        /// </summary>
        Process RunningProcess;

        /// <summary>
        /// Gets whether the operation was cancelled.
        /// </summary>
        public bool IsCancellationRequested {
            get {
                lock (Sync) {
                    return CancellationRequested;
                }
            }
        }

        /// <summary>
        /// Cancels the operation and kills the process it is running, with every child process, when one is running.
        /// </summary>
        public void Cancel() {
            lock (Sync) {
                CancellationRequested = true;
                KillTree(RunningProcess);
            }
        }

        /// <summary>
        /// Records a process the operation just started, so <see cref="Cancel"/> can kill it. When the operation was
        /// already cancelled, the process is killed instead and false is returned.
        /// </summary>
        /// <param name="process">Started process.</param>
        /// <returns>True when the process may run; false when it was killed because the operation is cancelled.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the process is null.</exception>
        public bool TryAttach(Process process) {
            if (process == null) {
                throw new ArgumentNullException(nameof(process));
            }

            lock (Sync) {
                if (CancellationRequested) {
                    KillTree(process);
                    return false;
                }

                RunningProcess = process;
                return true;
            }
        }

        /// <summary>
        /// Forgets the attached process once it exited, before its owner disposes it.
        /// </summary>
        public void Detach() {
            lock (Sync) {
                RunningProcess = null;
            }
        }

        /// <summary>
        /// Kills a process and its children when it is still running. The exit check makes the normal case of a process
        /// that already finished free of exceptions.
        /// </summary>
        /// <param name="process">Process to kill, or null.</param>
        static void KillTree(Process process) {
            if (process == null || process.HasExited) {
                return;
            }

            process.Kill(true);
        }
    }
}
