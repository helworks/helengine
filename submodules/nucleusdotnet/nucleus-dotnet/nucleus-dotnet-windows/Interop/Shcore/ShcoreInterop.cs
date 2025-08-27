using System.Runtime.InteropServices;

namespace Nucleus.Platform.Windows.Interop {
    /// <summary>
    /// Interop functionality for Windows 8.1+
    /// </summary>
    internal static class ShcoreInterop {
        [DllImport("shcore.dll")]
        internal static extern int SetProcessDpiAwareness(ProcessDPIAwareness value);
    }
}
