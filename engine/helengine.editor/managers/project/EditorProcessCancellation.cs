using System.Diagnostics;
using System.Runtime.InteropServices;

namespace helengine.editor {
    /// <summary>
    /// Cancels the child processes one background operation starts, such as the <c>dotnet build</c> of an editor Play
    /// preparation. Cancelling is a flag plus a kill of the running process tree, never an exception: the operation
    /// polls <see cref="IsCancellationRequested"/> before it starts a process, attaches each process it starts, and a
    /// process attached after cancellation is killed at once and refused. All members are thread-safe.
    /// <para>
    /// On Windows each attached process is put into its own job object created with
    /// <c>JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE</c>. Every process it starts afterwards (MSBuild worker nodes, the compiler
    /// server) joins the job, so <see cref="Cancel"/> kills the whole tree with one <c>TerminateJobObject</c> call:
    /// no process enumeration and no exceptions, unlike <see cref="Process.Kill(bool)"/>, which raises a first-chance
    /// exception for every system process it may not open. <see cref="Detach"/> closes the job, which also ends
    /// processes the build left running, and an editor crash closes it too, so no build process outlives the editor.
    /// </para>
    /// <para>
    /// Race: <see cref="Process.Start()"/> cannot start a process suspended, so the process runs for the moment between
    /// its start and <see cref="TryAttach"/>. A child it starts in that window would not be in the job. The caller
    /// attaches immediately after the start returns, and <c>dotnet build</c> only starts child processes after resolving
    /// the SDK and evaluating the projects, far later than that window; the window is accepted rather than replacing the
    /// process start with a hand-written suspended <c>CreateProcess</c>.
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
        /// Job holding <see cref="RunningProcess"/> and its descendants on Windows, or null.
        /// </summary>
        EditorJobObjectHandle RunningJob;

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
        /// Records a process the operation just started, putting it into a kill-on-close job on Windows, so
        /// <see cref="Cancel"/> can kill its tree. When the operation was already cancelled, the process is killed
        /// instead and false is returned.
        /// </summary>
        /// <param name="process">Started process; attach it immediately after the start.</param>
        /// <returns>True when the process may run; false when it was killed because the operation is cancelled.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the process is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the job cannot be created or the running process cannot join it.</exception>
        public bool TryAttach(Process process) {
            if (process == null) {
                throw new ArgumentNullException(nameof(process));
            }

            lock (Sync) {
                EditorJobObjectHandle job = OperatingSystem.IsWindows() ? CreateJob(process) : null;
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
        /// Forgets the attached process once it exited, before its owner disposes it, and closes its job, which ends any
        /// process the build left behind.
        /// </summary>
        public void Detach() {
            lock (Sync) {
                RunningJob?.Dispose();
                RunningJob = null;
                RunningProcess = null;
            }
        }

        /// <summary>
        /// Creates a kill-on-close job and puts the process into it.
        /// </summary>
        /// <param name="process">Process to contain.</param>
        /// <returns>Job containing the process, or null when the process exited before it could join.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the job cannot be created or configured, or a running process cannot join it.</exception>
        static EditorJobObjectHandle CreateJob(Process process) {
            EditorJobObjectHandle job = EditorJobObjectNativeMethods.CreateJobObjectW(IntPtr.Zero, null);
            if (job.IsInvalid) {
                int error = Marshal.GetLastWin32Error();
                job.Dispose();
                throw new InvalidOperationException($"A job object for the build process could not be created (Win32 error {error}).");
            }

            EditorJobObjectExtendedLimitInformation limits = new EditorJobObjectExtendedLimitInformation();
            limits.BasicLimitInformation.LimitFlags = EditorJobObjectNativeMethods.JobObjectLimitKillOnJobClose;
            if (!EditorJobObjectNativeMethods.SetInformationJobObject(
                    job,
                    EditorJobObjectNativeMethods.JobObjectExtendedLimitInformationClass,
                    ref limits,
                    (uint)Marshal.SizeOf<EditorJobObjectExtendedLimitInformation>())) {
                int error = Marshal.GetLastWin32Error();
                job.Dispose();
                throw new InvalidOperationException($"The build job object could not be set to kill its processes on close (Win32 error {error}).");
            }

            if (!EditorJobObjectNativeMethods.AssignProcessToJobObject(job, process.SafeHandle)) {
                int error = Marshal.GetLastWin32Error();
                job.Dispose();
                if (process.HasExited) {
                    return null;
                }

                throw new InvalidOperationException($"The build process could not join its job object (Win32 error {error}).");
            }

            return job;
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
