using System.Runtime.InteropServices;

namespace helengine.editor {
    /// <summary>
    /// Win32 pipe functions that read only the bytes already waiting in a pipe, so a reader never blocks on a pipe whose
    /// write end another process still holds. Every function reports failure through its return value; nothing here throws.
    /// </summary>
    static class EditorPipeNativeMethods {
        /// <summary>
        /// Reports how many bytes wait in a pipe without removing them.
        /// </summary>
        /// <param name="pipe">Read end of the pipe.</param>
        /// <param name="buffer">Receives peeked bytes; zero to peek none.</param>
        /// <param name="bufferSize">Size of <paramref name="buffer"/> in bytes.</param>
        /// <param name="bytesRead">Receives the number of bytes copied to <paramref name="buffer"/>.</param>
        /// <param name="totalBytesAvailable">Receives the number of bytes waiting in the pipe.</param>
        /// <param name="bytesLeftThisMessage">Receives the bytes left in the current message; zero for byte pipes.</param>
        /// <returns>True on success; false once every write end is closed and the pipe is empty, or on error.</returns>
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool PeekNamedPipe(
            SafeHandle pipe,
            IntPtr buffer,
            uint bufferSize,
            IntPtr bytesRead,
            out uint totalBytesAvailable,
            IntPtr bytesLeftThisMessage);

        /// <summary>
        /// Reads bytes from a pipe. Called only for bytes <see cref="PeekNamedPipe"/> reported, so it returns at once.
        /// </summary>
        /// <param name="file">Read end of the pipe.</param>
        /// <param name="buffer">Receives the bytes.</param>
        /// <param name="bytesToRead">Number of bytes to read.</param>
        /// <param name="bytesRead">Receives the number of bytes read.</param>
        /// <param name="overlapped">Must be zero: the pipe is synchronous.</param>
        /// <returns>True on success.</returns>
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ReadFile(SafeHandle file, byte[] buffer, uint bytesToRead, out uint bytesRead, IntPtr overlapped);
    }
}
