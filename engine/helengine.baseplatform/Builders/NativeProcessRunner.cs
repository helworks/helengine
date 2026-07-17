using System.Diagnostics;
using System.Text;

namespace helengine.baseplatform.Builders;

/// <summary>
/// Runs native build processes with live line streaming and deterministic process completion.
/// </summary>
public sealed class NativeProcessRunner {
    /// <summary>
    /// Runs one native process, forwards both output streams immediately, and waits for its real exit.
    /// </summary>
    /// <param name="startInfo">Configured process start information.</param>
    /// <param name="cancellationToken">Cancellation token that terminates the process tree when requested.</param>
    /// <param name="outputHandler">Optional handler that receives each output line and its stream designation.</param>
    /// <returns>The process exit code and all output lines received before completion.</returns>
    public NativeProcessRunResult Run(
        ProcessStartInfo startInfo,
        CancellationToken cancellationToken,
        Action<string, bool> outputHandler = null) {
        if (startInfo == null) {
            throw new ArgumentNullException(nameof(startInfo));
        }

        cancellationToken.ThrowIfCancellationRequested();
        startInfo.UseShellExecute = false;
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("The native process could not be started.");
        StringBuilder standardOutputBuilder = new();
        StringBuilder standardErrorBuilder = new();
        object outputLock = new();
        using CancellationTokenRegistration cancellationRegistration = cancellationToken.Register(() => TerminateProcessTree(process));

        process.OutputDataReceived += (_, eventArgs) => CaptureOutputLine(
            eventArgs.Data,
            false,
            standardOutputBuilder,
            outputHandler,
            outputLock);
        process.ErrorDataReceived += (_, eventArgs) => CaptureOutputLine(
            eventArgs.Data,
            true,
            standardErrorBuilder,
            outputHandler,
            outputLock);
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.WaitForExit();
        cancellationToken.ThrowIfCancellationRequested();

        return new NativeProcessRunResult(
            process.ExitCode,
            standardOutputBuilder.ToString(),
            standardErrorBuilder.ToString());
    }

    /// <summary>
    /// Captures one complete output line and forwards it through the configured sink or console.
    /// </summary>
    /// <param name="line">Output line received from the process.</param>
    /// <param name="isError">Whether the line came from standard error.</param>
    /// <param name="streamBuilder">Builder for the selected output stream.</param>
    /// <param name="outputHandler">Optional custom output sink.</param>
    /// <param name="outputLock">Lock shared by both process output callbacks.</param>
    static void CaptureOutputLine(
        string line,
        bool isError,
        StringBuilder streamBuilder,
        Action<string, bool> outputHandler,
        object outputLock) {
        if (line == null) {
            return;
        }

        lock (outputLock) {
            streamBuilder.AppendLine(line);
            if (outputHandler == null) {
                if (isError) {
                    Console.Error.WriteLine(line);
                } else {
                    Console.WriteLine(line);
                }
            } else {
                outputHandler(line, isError);
            }
        }
    }

    /// <summary>
    /// Terminates a running process tree when its build cancellation token is signaled.
    /// </summary>
    /// <param name="process">Process tree to terminate.</param>
    static void TerminateProcessTree(Process process) {
        try {
            process.Kill(entireProcessTree: true);
        } catch (InvalidOperationException) {
            // The process exited between cancellation and termination.
        }
    }
}
