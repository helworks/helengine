using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;

namespace helengine.editor {
    /// <summary>
    /// Captures the redirected standard output and error of a <c>dotnet build</c> and returns as soon as the process exits.
    /// <para>
    /// It never waits for the output pipes to close. <c>dotnet build</c> starts MSBuild worker nodes that stay alive for
    /// reuse, and on Windows those nodes end up holding the build's standard output and error handles, so the pipes stay
    /// open for the node's whole idle lifetime (about 15 minutes) after the build itself has finished.
    /// <see cref="Process.WaitForExit()"/> with asynchronous line reading waits for exactly that close, and hung every build
    /// that started a fresh node.
    /// </para>
    /// <para>
    /// On Windows the output is instead read only as far as it is already waiting in each pipe, while the process runs and
    /// once more after it exits. Every byte the process wrote before exiting is in its pipe by then, so nothing it wrote is
    /// lost and no read ever blocks. Other operating systems keep line reading until the pipes close: MSBuild gives its
    /// nodes their own pipes there.
    /// </para>
    /// </summary>
    static class EditorBuildProcessOutputCapture {
        /// <summary>
        /// Longest time the capture sleeps while the process runs and neither pipe has output waiting.
        /// </summary>
        const int IdleWaitMilliseconds = 15;

        /// <summary>
        /// Largest number of bytes read from a pipe in one call.
        /// </summary>
        const int ReadChunkSize = 64 * 1024;

        /// <summary>
        /// Win32 <c>ERROR_BROKEN_PIPE</c>: every write end is closed and the pipe is empty.
        /// </summary>
        const int BrokenPipeError = 109;

        /// <summary>
        /// Reads a started process's redirected standard output and error until the process exits.
        /// </summary>
        /// <param name="process">Started process whose standard output and error are redirected and not yet read.</param>
        /// <param name="stdout">Receives everything the process wrote to standard output.</param>
        /// <param name="stderr">Receives everything the process wrote to standard error.</param>
        /// <exception cref="ArgumentNullException">Thrown when the process is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when a pipe that reported waiting bytes cannot be read.</exception>
        public static void Capture(Process process, out string stdout, out string stderr) {
            if (process == null) {
                throw new ArgumentNullException(nameof(process));
            }

            if (OperatingSystem.IsWindows()) {
                CaptureUntilExit(process, out stdout, out stderr);
            } else {
                CaptureUntilStreamsClose(process, out stdout, out stderr);
            }
        }

        /// <summary>
        /// Drains both pipes without blocking while the process runs, then drains them once more after it exited.
        /// </summary>
        /// <param name="process">Started process with redirected standard output and error.</param>
        /// <param name="stdout">Receives the decoded standard output.</param>
        /// <param name="stderr">Receives the decoded standard error.</param>
        static void CaptureUntilExit(Process process, out string stdout, out string stderr) {
            StreamReader outputReader = process.StandardOutput;
            StreamReader errorReader = process.StandardError;
            SafeHandle outputPipe = ResolvePipeHandle(outputReader.BaseStream);
            SafeHandle errorPipe = ResolvePipeHandle(errorReader.BaseStream);
            MemoryStream outputBytes = new MemoryStream();
            MemoryStream errorBytes = new MemoryStream();
            byte[] chunk = new byte[ReadChunkSize];
            bool exited = false;
            while (true) {
                bool readOutput = ReadWaitingBytes(outputPipe, chunk, outputBytes);
                bool readError = ReadWaitingBytes(errorPipe, chunk, errorBytes);
                if (readOutput || readError) {
                    continue;
                } else if (exited) {
                    break;
                }

                // Once this reports the exit, the loop drains each pipe one final time: the process wrote everything
                // before exiting, so a pass that finds both pipes empty has captured all of it.
                exited = process.WaitForExit(IdleWaitMilliseconds);
            }

            stdout = outputReader.CurrentEncoding.GetString(outputBytes.GetBuffer(), 0, (int)outputBytes.Length);
            stderr = errorReader.CurrentEncoding.GetString(errorBytes.GetBuffer(), 0, (int)errorBytes.Length);
        }

        /// <summary>
        /// Reads the bytes waiting in a pipe right now, without blocking.
        /// </summary>
        /// <param name="pipe">Read end of the pipe.</param>
        /// <param name="chunk">Scratch buffer for one read.</param>
        /// <param name="destination">Receives the bytes read.</param>
        /// <returns>True when bytes were read; false when the pipe is empty or every write end is closed.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the pipe cannot be peeked or read.</exception>
        static bool ReadWaitingBytes(SafeHandle pipe, byte[] chunk, MemoryStream destination) {
            if (!EditorPipeNativeMethods.PeekNamedPipe(pipe, IntPtr.Zero, 0, IntPtr.Zero, out uint available, IntPtr.Zero)) {
                int peekError = Marshal.GetLastWin32Error();
                if (peekError == BrokenPipeError) {
                    return false;
                }

                throw new InvalidOperationException($"The build output pipe could not be checked (Win32 error {peekError}).");
            } else if (available == 0) {
                return false;
            }

            uint toRead = Math.Min(available, (uint)chunk.Length);
            if (!EditorPipeNativeMethods.ReadFile(pipe, chunk, toRead, out uint read, IntPtr.Zero)) {
                throw new InvalidOperationException($"The build output pipe could not be read (Win32 error {Marshal.GetLastWin32Error()}).");
            }

            destination.Write(chunk, 0, (int)read);
            return read > 0;
        }

        /// <summary>
        /// Resolves the operating-system handle of a redirected process stream.
        /// </summary>
        /// <param name="stream">Base stream of a redirected standard output or error reader.</param>
        /// <returns>Read end of the stream's pipe.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the stream is not backed by a file or pipe handle.</exception>
        static SafeHandle ResolvePipeHandle(Stream stream) {
            if (stream is FileStream fileStream) {
                return fileStream.SafeFileHandle;
            } else if (stream is PipeStream pipeStream) {
                return pipeStream.SafePipeHandle;
            }

            throw new InvalidOperationException($"Redirected process output of type '{stream.GetType().FullName}' has no pipe handle.");
        }

        /// <summary>
        /// Reads both streams line by line until the process exits and both streams close.
        /// </summary>
        /// <param name="process">Started process with redirected standard output and error.</param>
        /// <param name="stdout">Receives the standard output lines.</param>
        /// <param name="stderr">Receives the standard error lines.</param>
        static void CaptureUntilStreamsClose(Process process, out string stdout, out string stderr) {
            StringBuilder stdoutBuilder = new StringBuilder();
            StringBuilder stderrBuilder = new StringBuilder();
            process.OutputDataReceived += (sender, eventArgs) => AppendCapturedLine(stdoutBuilder, eventArgs.Data);
            process.ErrorDataReceived += (sender, eventArgs) => AppendCapturedLine(stderrBuilder, eventArgs.Data);
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.WaitForExit();
            stdout = stdoutBuilder.ToString();
            stderr = stderrBuilder.ToString();
        }

        /// <summary>
        /// Appends one captured line to its buffer, ignoring the end-of-stream notification.
        /// </summary>
        /// <param name="builder">Destination text buffer.</param>
        /// <param name="line">Captured line, or null at the end of the stream.</param>
        static void AppendCapturedLine(StringBuilder builder, string line) {
            if (line == null) {
                return;
            }

            builder.AppendLine(line);
        }
    }
}
