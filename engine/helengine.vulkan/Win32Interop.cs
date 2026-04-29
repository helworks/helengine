using System.Runtime.InteropServices;

namespace helengine.vulkan {
    /// <summary>
    /// Exposes Win32 helpers needed for Vulkan surface creation.
    /// </summary>
    static class Win32Interop {
        /// <summary>
        /// Gets the module handle for the specified module name.
        /// </summary>
        /// <param name="moduleName">Module name or null for the current process.</param>
        /// <returns>Handle to the module.</returns>
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr GetModuleHandle(string moduleName);
    }
}
