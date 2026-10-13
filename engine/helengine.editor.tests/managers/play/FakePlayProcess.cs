namespace helengine.editor.tests.managers.play;

/// <summary>
/// Test double for a running player process that records kills and lets tests raise output and exit events.
/// </summary>
public sealed class FakePlayProcess : IEditorPlayProcess {
    /// <summary>
    /// Initializes one fake player process with the given process identifier.
    /// </summary>
    /// <param name="processId">Identifier reported through <see cref="ProcessId"/>.</param>
    public FakePlayProcess(int processId) {
        ProcessId = processId;
    }

    /// <summary>
    /// Raised when a test simulates a standard-output line from the player.
    /// </summary>
    public event Action<string> OutputLine;

    /// <summary>
    /// Raised when a test simulates a standard-error line from the player.
    /// </summary>
    public event Action<string> ErrorLine;

    /// <summary>
    /// Raised when a test simulates the player exiting.
    /// </summary>
    public event Action Exited;

    /// <summary>
    /// Gets the identifier of the simulated player process.
    /// </summary>
    public int ProcessId { get; }

    /// <summary>
    /// Gets whether the simulated process has exited, either through <see cref="RaiseExit"/> or <see cref="Kill"/>.
    /// </summary>
    public bool HasExited { get; set; }

    /// <summary>
    /// Gets the simulated exit code; only meaningful after <see cref="HasExited"/> becomes true.
    /// </summary>
    public int ExitCode { get; set; }

    /// <summary>
    /// Gets how many times the session service asked this process to terminate.
    /// </summary>
    public int KillCount { get; set; }

    /// <summary>
    /// Records the kill request and marks the simulated process as exited without raising <see cref="Exited"/>.
    /// </summary>
    public void Kill() {
        KillCount++;
        HasExited = true;
    }

    /// <summary>
    /// Simulates the player writing one standard-output line.
    /// </summary>
    /// <param name="line">Line the player wrote.</param>
    public void RaiseOutputLine(string line) {
        OutputLine?.Invoke(line);
    }

    /// <summary>
    /// Simulates the player writing one standard-error line.
    /// </summary>
    /// <param name="line">Line the player wrote.</param>
    public void RaiseErrorLine(string line) {
        ErrorLine?.Invoke(line);
    }

    /// <summary>
    /// Simulates the player exiting with the given code: sets <see cref="HasExited"/> and <see cref="ExitCode"/>, then raises <see cref="Exited"/>.
    /// </summary>
    /// <param name="code">Exit code the simulated player returns.</param>
    public void RaiseExit(int code) {
        HasExited = true;
        ExitCode = code;
        Exited?.Invoke();
    }
}
