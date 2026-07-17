namespace helengine.baseplatform.Builders;

/// <summary>
/// Describes the deterministic result of one completed native build process.
/// </summary>
public sealed class NativeProcessRunResult {
    /// <summary>
    /// Creates a process result with the exit code and captured output streams.
    /// </summary>
    /// <param name="exitCode">Exit code returned by the native process.</param>
    /// <param name="standardOutput">Lines received from standard output.</param>
    /// <param name="standardError">Lines received from standard error.</param>
    public NativeProcessRunResult(int exitCode, string standardOutput, string standardError) {
        if (standardOutput == null) {
            throw new ArgumentNullException(nameof(standardOutput));
        } else if (standardError == null) {
            throw new ArgumentNullException(nameof(standardError));
        }

        ExitCode = exitCode;
        StandardOutput = standardOutput;
        StandardError = standardError;
    }

    /// <summary>
    /// Gets the exit code returned by the native process.
    /// </summary>
    public int ExitCode { get; }

    /// <summary>
    /// Gets the standard-output lines captured while the process was running.
    /// </summary>
    public string StandardOutput { get; }

    /// <summary>
    /// Gets the standard-error lines captured while the process was running.
    /// </summary>
    public string StandardError { get; }
}
