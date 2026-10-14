using System.Runtime.InteropServices;

namespace helengine.editor {
    /// <summary>
    /// Mirror of the Win32 <c>IO_COUNTERS</c> structure embedded in the extended job limits; never read.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    struct EditorJobObjectIoCounters {
        /// <summary>Read operation count.</summary>
        public ulong ReadOperationCount;

        /// <summary>Write operation count.</summary>
        public ulong WriteOperationCount;

        /// <summary>Other operation count.</summary>
        public ulong OtherOperationCount;

        /// <summary>Bytes read.</summary>
        public ulong ReadTransferCount;

        /// <summary>Bytes written.</summary>
        public ulong WriteTransferCount;

        /// <summary>Other bytes transferred.</summary>
        public ulong OtherTransferCount;
    }
}
