using System.Runtime.InteropServices;

namespace helengine.editor {
    /// <summary>
    /// Creates Win32 job objects, reporting failures as Win32 error codes rather than exceptions.
    /// </summary>
    static class EditorJobObjectFactory {
        /// <summary>
        /// Creates an unnamed job object, optionally set to terminate its processes when its last handle closes.
        /// </summary>
        /// <param name="killOnClose">Whether closing the job terminates every process still in it.</param>
        /// <param name="job">Receives the job, or null on failure.</param>
        /// <returns>Zero on success, otherwise the Win32 error.</returns>
        public static int TryCreate(bool killOnClose, out EditorJobObjectHandle job) {
            job = EditorJobObjectNativeMethods.CreateJobObjectW(IntPtr.Zero, null);
            if (job.IsInvalid) {
                int error = Marshal.GetLastWin32Error();
                job.Dispose();
                job = null;
                return error;
            }

            if (!killOnClose) {
                return 0;
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
                job = null;
                return error;
            }

            return 0;
        }
    }
}
