using System.Runtime.InteropServices;

namespace helengine.editor {
    /// <summary>
    /// Mirror of the Win32 <c>JOBOBJECT_BASIC_LIMIT_INFORMATION</c> structure; only <see cref="LimitFlags"/> is set.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    struct EditorJobObjectBasicLimitInformation {
        /// <summary>Per-process user-mode time limit; unused.</summary>
        public long PerProcessUserTimeLimit;

        /// <summary>Per-job user-mode time limit; unused.</summary>
        public long PerJobUserTimeLimit;

        /// <summary>Limit flags, such as <c>JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE</c>.</summary>
        public uint LimitFlags;

        /// <summary>Minimum working set size; unused.</summary>
        public UIntPtr MinimumWorkingSetSize;

        /// <summary>Maximum working set size; unused.</summary>
        public UIntPtr MaximumWorkingSetSize;

        /// <summary>Active process limit; unused.</summary>
        public uint ActiveProcessLimit;

        /// <summary>Processor affinity; unused.</summary>
        public UIntPtr Affinity;

        /// <summary>Priority class; unused.</summary>
        public uint PriorityClass;

        /// <summary>Scheduling class; unused.</summary>
        public uint SchedulingClass;
    }
}
