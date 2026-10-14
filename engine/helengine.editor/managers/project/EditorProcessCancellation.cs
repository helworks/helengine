using System.Diagnostics;
using System.Runtime.InteropServices;

namespace helengine.editor {
    /// <summary>
    /// Cancels the child processes one background operation starts, such as the <c>dotnet build</c> of an editor Play
    /// preparation. Cancelling is a flag plus a kill of the running process tree, never an exception: the operation
    /// polls <see cref="IsCancellationRequested"/> before it starts a process, attaches each process it starts, and a
    /// process attached after cancellation is killed at once and refused. All members are thread-safe.
    /// <para>
    /// On Windows each attached process gets its own job object, and every process it starts afterwards joins it, so
    /// <see cref="Cancel"/> kills the whole tree with one <c>TerminateJobObject</c> call: no process enumeration and no
    /// exceptions, unlike <see cref="Process.Kill(bool)"/>, which raises a first-chance exception for every system process
    /// it may not open.
    /// </para>
    /// <para>
    /// With an <see cref="EditorSessionProcessJob"/> (the editor's Play builds) the process first joins the session job
    /// and then its own job, which Windows nests inside the session job. The per-build job does not kill on close:
    /// <see cref="Detach"/> after a build that finished leaves the MSBuild nodes and compiler server it started warm for
    /// the next Play, and the session job ends them with the editor. <see cref="Cancel"/> terminates only the per-build
    /// job: the cancelled <c>dotnet</c> and the processes it started; nodes still warm from earlier builds live in their
    /// own builds' jobs and survive. Without a session job the per-build job kills on close, so nothing outlives
    /// <see cref="Detach"/>.
    /// </para>
    /// <para>
    /// Race: <see cref="Process.Start()"/> cannot start a process suspended, so the process runs for the moment between
    /// its start and <see cref="TryAttach(Process, EditorSessionProcessJob)"/>. A child it starts in that window would not
    /// be in the jobs. The caller attaches immediately after the start returns, and <c>dotnet build</c> only starts
    /// child processes after resolving the SDK and evaluating the projects, far later than that window.
    /// </para>
    /// </summary>
    public sealed class EditorProcessCancellation {
        /// <summary>
        /// Exit code given to processes terminated by cancellation.
        /// </summary>
        const uint CancelledExitCode = 1;

        /// <summary>
        /// Guards the flag, the attached process and its job, so a job is never terminated while it is being closed.
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
        /// Per-build job holding <see cref="RunningProcess"/> and its descendants on Windows, or null.
        /// </summary>
        EditorJobObjectHandle RunningJob;

        /// <summary>
        /// Gets or sets a Win32 error the next per-build job assignment reports instead of assigning, so tests can
        /// exercise the failure path; zero leaves assignment alone.
        /// </summary>
        internal int SimulatedAssignErrorForTests { get; set; }

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
        /// Cancels the operation and kills the process it is running, with every process it started, when one is
        /// running. It never blocks: terminating a job returns at once.
        /// </summary>
        public void Cancel() {
            lock (Sync) {
                CancellationRequested = true;
                KillTree(RunningProcess, RunningJob);
            }
        }

        /// <summary>
        /// Records a process the operation just started, in a per-build job that kills on close. Used where no session
        /// job keeps build nodes warm.
        /// </summary>
        /// <param name="process">Started process; attach it immediately after the start.</param>
        /// <returns>True when the process may run; false when it was killed because the operation is cancelled.</returns>
        public bool TryAttach(Process process) {
            return TryAttach(process, null);
        }

        /// <summary>
        /// Records a process the operation just started, so <see cref="Cancel"/> can kill its tree. On Windows the process
        /// joins the session job (when one is given) and then its own per-build job. When the operation was already
        /// cancelled, the process is killed instead and false is returned. When a job cannot be created or joined, the
        /// process is killed and waited for before the error is raised, so a build is never left running unmanaged.
        /// </summary>
        /// <param name="process">Started process; attach it immediately after the start.</param>
        /// <param name="sessionJob">Session job that keeps build nodes warm, or null.</param>
        /// <returns>True when the process may run; false when it was killed because the operation is cancelled.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the process is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown, after the process was killed, when it could not join its jobs.</exception>
        public bool TryAttach(Process process, EditorSessionProcessJob sessionJob) {
            if (process == null) {
                throw new ArgumentNullException(nameof(process));
            }

            lock (Sync) {
                EditorJobObjectHandle job = null;
                if (OperatingSystem.IsWindows()) {
                    int error = TryContain(process, sessionJob, out job);
                    if (error != 0) {
                        KillUnmanaged(process);
                        throw new InvalidOperationException($"The build process could not join its job object (Win32 error {error}); it was stopped.");
                    }
                }

                if (CancellationRequested) {
                    KillTree(process, job);
                    job?.Dispose();
                    return false;
                }

                RunningProcess = process;
                RunningJob = job;
                return true;
            }
        }

        /// <summary>
        /// Forgets the attached process once it exited, before its owner disposes it, and closes its per-build job.
        /// Without a session job that ends any process the build left behind; with one, those processes stay warm.
        /// </summary>
        public void Detach() {
            lock (Sync) {
                RunningJob?.Dispose();
                RunningJob = null;
                RunningProcess = null;
            }
        }

        /// <summary>
        /// Puts the process into the session job (when given) and then into a new per-build job, which Windows nests
        /// inside the session job.
        /// </summary>
        /// <param name="process">Process to contain.</param>
        /// <param name="sessionJob">Session job, or null.</param>
        /// <param name="job">Receives the per-build job, or null when the process exited before it could join.</param>
        /// <returns>Zero on success, otherwise the Win32 error.</returns>
        int TryContain(Process process, EditorSessionProcessJob sessionJob, out EditorJobObjectHandle job) {
            job = null;
            if (sessionJob != null) {
                int sessionError = sessionJob.TryAssign(process);
                if (sessionError != 0) {
                    return process.HasExited ? 0 : sessionError;
                }
            }

            int createError = EditorJobObjectFactory.TryCreate(sessionJob == null, out EditorJobObjectHandle created);
            if (createError != 0) {
                return createError;
            }

            int assignError = SimulatedAssignErrorForTests;
            if (assignError == 0 && !EditorJobObjectNativeMethods.AssignProcessToJobObject(created, process.SafeHandle)) {
                assignError = Marshal.GetLastWin32Error();
            }

            if (assignError != 0) {
                created.Dispose();
                return process.HasExited ? 0 : assignError;
            }

            job = created;
            return 0;
        }

        /// <summary>
        /// Stops a process that could not be contained and waits for it, so it never keeps running unmanaged. Only the
        /// process itself is terminated; that uses no process enumeration and raises no exception for a running process.
        /// </summary>
        /// <param name="process">Process to stop.</param>
        static void KillUnmanaged(Process process) {
            if (!process.HasExited) {
                process.Kill();
            }

            process.WaitForExit();
        }

        /// <summary>
        /// Kills a process and every process it started. With a job this is one <c>TerminateJobObject</c>; without one
        /// (other operating systems, or a process that exited before it could join) the process tree is killed directly
        /// when it is still running.
        /// </summary>
        /// <param name="process">Process to kill, or null.</param>
        /// <param name="job">Job containing the process, or null.</param>
        /// <exception cref="InvalidOperationException">Thrown when terminating the job fails while the process still runs.</exception>
        static void KillTree(Process process, EditorJobObjectHandle job) {
            if (job != null) {
                if (!EditorJobObjectNativeMethods.TerminateJobObject(job, CancelledExitCode)) {
                    int error = Marshal.GetLastWin32Error();
                    if (process != null && !process.HasExited) {
                        throw new InvalidOperationException($"The build process tree could not be terminated (Win32 error {error}).");
                    }
                }

                return;
            }

            if (process == null || process.HasExited || OperatingSystem.IsWindows()) {
                return;
            }

            process.Kill(true);
        }
    }
}
