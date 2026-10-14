using System.Diagnostics;
using System.Runtime.InteropServices;

namespace helengine.editor {
    /// <summary>
    /// One Win32 job object for every Play script build of an editor session, created on the first build with
    /// <c>JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE</c> and closed when the editor shuts down. Each build's <c>dotnet</c>
    /// process joins it, and so does everything that process starts, including the MSBuild worker nodes and the compiler
    /// server that stay warm between builds. They keep running between Plays and end with the session (or when the editor
    /// crashes, because the kernel closes the handle). A build's own cancellable tree is a nested job inside this one;
    /// see <see cref="EditorProcessCancellation"/>. Thread-safe; nothing on the normal path throws.
    /// </summary>
    public sealed class EditorSessionProcessJob : IDisposable {
        /// <summary>
        /// Error reported for a build that tries to join after the session ended (Win32 <c>ERROR_INVALID_HANDLE</c>).
        /// </summary>
        internal const int SessionEndedError = 6;

        /// <summary>
        /// Guards lazy creation and disposal.
        /// </summary>
        readonly object Sync = new object();

        /// <summary>
        /// Session job, or null before the first build and after disposal.
        /// </summary>
        EditorJobObjectHandle Job;

        /// <summary>
        /// Set once the session ended; no process may join afterwards.
        /// </summary>
        bool IsDisposed;

        /// <summary>
        /// Gets whether the job was created, which happens with the first build that joins it.
        /// </summary>
        public bool IsCreated {
            get {
                lock (Sync) {
                    return Job != null;
                }
            }
        }

        /// <summary>
        /// Puts a started process into the session job, creating the job first when this is the first build.
        /// </summary>
        /// <param name="process">Started build process.</param>
        /// <returns>Zero on success, otherwise the Win32 error that prevented creating the job or joining it, or
        /// <see cref="SessionEndedError"/> when the session already ended.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the process is null.</exception>
        internal int TryAssign(Process process) {
            if (process == null) {
                throw new ArgumentNullException(nameof(process));
            }

            lock (Sync) {
                if (IsDisposed) {
                    return SessionEndedError;
                }

                if (Job == null) {
                    int createError = EditorJobObjectFactory.TryCreate(true, out EditorJobObjectHandle created);
                    if (createError != 0) {
                        return createError;
                    }

                    Job = created;
                }

                return EditorJobObjectNativeMethods.AssignProcessToJobObject(Job, process.SafeHandle)
                    ? 0
                    : Marshal.GetLastWin32Error();
            }
        }

        /// <summary>
        /// Ends the session: closing the job terminates every process still in it, the warm build nodes included.
        /// </summary>
        public void Dispose() {
            lock (Sync) {
                IsDisposed = true;
                Job?.Dispose();
                Job = null;
            }
        }
    }
}
