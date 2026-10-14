using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace helengine.editor {
    /// <summary>
    /// Win32 job-object functions used to kill a child process tree without enumerating the system's processes. Every
    /// function reports failure through its return value; nothing here throws.
    /// </summary>
    static class EditorJobObjectNativeMethods {
        /// <summary>
        /// <c>JobObjectExtendedLimitInformation</c> information class for <see cref="SetInformationJobObject"/>.
        /// </summary>
        public const int JobObjectExtendedLimitInformationClass = 9;

        /// <summary>
        /// <c>JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE</c>: closing the last handle to the job terminates every process in it.
        /// </summary>
        public const uint JobObjectLimitKillOnJobClose = 0x00002000;

        /// <summary>
        /// Creates an unnamed job object.
        /// </summary>
        /// <param name="jobAttributes">Security attributes; zero for the defaults.</param>
        /// <param name="name">Job name; null for an unnamed job.</param>
        /// <returns>Job handle; invalid when the call failed.</returns>
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern EditorJobObjectHandle CreateJobObjectW(IntPtr jobAttributes, string name);

        /// <summary>
        /// Sets the extended limits of a job.
        /// </summary>
        /// <param name="job">Job to configure.</param>
        /// <param name="informationClass">Must be <see cref="JobObjectExtendedLimitInformationClass"/>.</param>
        /// <param name="information">Extended limit information.</param>
        /// <param name="informationLength">Size of <paramref name="information"/> in bytes.</param>
        /// <returns>True on success.</returns>
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetInformationJobObject(EditorJobObjectHandle job, int informationClass, ref EditorJobObjectExtendedLimitInformation information, uint informationLength);

        /// <summary>
        /// Puts a process, and every process it starts afterwards, into a job.
        /// </summary>
        /// <param name="job">Job that receives the process.</param>
        /// <param name="process">Process handle.</param>
        /// <returns>True on success.</returns>
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool AssignProcessToJobObject(EditorJobObjectHandle job, SafeProcessHandle process);

        /// <summary>
        /// Terminates every process in a job.
        /// </summary>
        /// <param name="job">Job to terminate.</param>
        /// <param name="exitCode">Exit code given to the terminated processes.</param>
        /// <returns>True on success.</returns>
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool TerminateJobObject(EditorJobObjectHandle job, uint exitCode);

        /// <summary>
        /// Closes a kernel handle.
        /// </summary>
        /// <param name="handle">Handle to close.</param>
        /// <returns>True on success.</returns>
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CloseHandle(IntPtr handle);
    }
}
