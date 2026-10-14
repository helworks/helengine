using Microsoft.Win32.SafeHandles;

namespace helengine.editor {
    /// <summary>
    /// Owns a Win32 job-object handle. Releasing it closes the job; with <c>JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE</c> set
    /// that terminates every process still in the job, including after an editor crash.
    /// </summary>
    sealed class EditorJobObjectHandle : SafeHandleZeroOrMinusOneIsInvalid {
        /// <summary>
        /// Initializes an empty handle the marshaller fills.
        /// </summary>
        public EditorJobObjectHandle() : base(true) {
        }

        /// <summary>
        /// Closes the job handle.
        /// </summary>
        /// <returns>True when the handle was closed.</returns>
        protected override bool ReleaseHandle() {
            return EditorJobObjectNativeMethods.CloseHandle(handle);
        }
    }
}
