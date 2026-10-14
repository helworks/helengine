using System.Runtime.InteropServices;

namespace helengine.editor {
    /// <summary>
    /// Mirror of the Win32 <c>JOBOBJECT_EXTENDED_LIMIT_INFORMATION</c> structure passed to
    /// <see cref="EditorJobObjectNativeMethods.SetInformationJobObject"/>.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    struct EditorJobObjectExtendedLimitInformation {
        /// <summary>Basic limits, which carry the limit flags.</summary>
        public EditorJobObjectBasicLimitInformation BasicLimitInformation;

        /// <summary>I/O counters; unused.</summary>
        public EditorJobObjectIoCounters IoInfo;

        /// <summary>Per-process memory limit; unused.</summary>
        public UIntPtr ProcessMemoryLimit;

        /// <summary>Per-job memory limit; unused.</summary>
        public UIntPtr JobMemoryLimit;

        /// <summary>Peak process memory; output only.</summary>
        public UIntPtr PeakProcessMemoryUsed;

        /// <summary>Peak job memory; output only.</summary>
        public UIntPtr PeakJobMemoryUsed;
    }
}
