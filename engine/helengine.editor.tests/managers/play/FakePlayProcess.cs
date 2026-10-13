namespace helengine.editor.tests.managers.play;

/// <summary>
/// Test double for a running player process that records kills and lets tests raise output and exit events. It also
/// remembers every handler ever subscribed, so tests can simulate a late event whose delegate was captured before
/// the session service unsubscribed.
/// </summary>
public sealed class FakePlayProcess : IEditorPlayProcess {
    /// <summary>
    /// Every standard-output handler ever subscribed, including ones later removed.
    /// </summary>
    readonly List<Action<string>> EverSubscribedOutputHandlers = new List<Action<string>>();

    /// <summary>
    /// Every standard-error handler ever subscribed, including ones later removed.
    /// </summary>
    readonly List<Action<string>> EverSubscribedErrorHandlers = new List<Action<string>>();

    /// <summary>
    /// Every exit handler ever subscribed, including ones later removed.
    /// </summary>
    readonly List<Action> EverSubscribedExitHandlers = new List<Action>();

    /// <summary>
    /// Currently subscribed standard-output handlers.
    /// </summary>
    Action<string> OutputLineHandlers;

    /// <summary>
    /// Currently subscribed standard-error handlers.
    /// </summary>
    Action<string> ErrorLineHandlers;

    /// <summary>
    /// Currently subscribed exit handlers.
    /// </summary>
    Action ExitedHandlers;

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
    public event Action<string> OutputLine {
        add {
            OutputLineHandlers += value;
            EverSubscribedOutputHandlers.Add(value);
        }
        remove {
            OutputLineHandlers -= value;
        }
    }

    /// <summary>
    /// Raised when a test simulates a standard-error line from the player.
    /// </summary>
    public event Action<string> ErrorLine {
        add {
            ErrorLineHandlers += value;
            EverSubscribedErrorHandlers.Add(value);
        }
        remove {
            ErrorLineHandlers -= value;
        }
    }

    /// <summary>
    /// Raised when a test simulates the player exiting.
    /// </summary>
    public event Action Exited {
        add {
            ExitedHandlers += value;
            EverSubscribedExitHandlers.Add(value);
        }
        remove {
            ExitedHandlers -= value;
        }
    }

    /// <summary>
    /// Gets the identifier of the simulated player process.
    /// </summary>
    public int ProcessId { get; }

    /// <summary>
    /// Gets whether the simulated process has exited, either through an exit simulation or <see cref="Kill"/>.
    /// </summary>
    public bool HasExited { get; private set; }

    /// <summary>
    /// Gets the simulated exit code; only meaningful after <see cref="HasExited"/> becomes true.
    /// </summary>
    public int ExitCode { get; private set; }

    /// <summary>
    /// Gets how many times the session service asked this process to terminate.
    /// </summary>
    public int KillCount { get; private set; }

    /// <summary>
    /// Records the kill request and marks the simulated process as exited without raising <see cref="Exited"/>.
    /// </summary>
    public void Kill() {
        KillCount++;
        HasExited = true;
    }

    /// <summary>
    /// Simulates the player writing one standard-output line to the currently subscribed handlers.
    /// </summary>
    /// <param name="line">Line the player wrote.</param>
    public void RaiseOutputLine(string line) {
        OutputLineHandlers?.Invoke(line);
    }

    /// <summary>
    /// Simulates the player writing one standard-error line to the currently subscribed handlers.
    /// </summary>
    /// <param name="line">Line the player wrote.</param>
    public void RaiseErrorLine(string line) {
        ErrorLineHandlers?.Invoke(line);
    }

    /// <summary>
    /// Simulates the player exiting with the given code: sets <see cref="HasExited"/> and <see cref="ExitCode"/>, then
    /// raises <see cref="Exited"/> to the currently subscribed handlers.
    /// </summary>
    /// <param name="code">Exit code the simulated player returns.</param>
    public void RaiseExit(int code) {
        HasExited = true;
        ExitCode = code;
        ExitedHandlers?.Invoke();
    }

    /// <summary>
    /// Simulates late events delivered through delegates captured before unsubscription: one output line, one error
    /// line and an exit with the given code, each sent to every handler ever subscribed.
    /// </summary>
    /// <param name="line">Line delivered to every output and error handler ever subscribed.</param>
    /// <param name="code">Exit code the simulated player returns.</param>
    public void RaiseStaleEventsToEverySubscriber(string line, int code) {
        HasExited = true;
        ExitCode = code;
        foreach (Action<string> handler in EverSubscribedOutputHandlers) {
            handler(line);
        }

        foreach (Action<string> handler in EverSubscribedErrorHandlers) {
            handler(line);
        }

        foreach (Action handler in EverSubscribedExitHandlers) {
            handler();
        }
    }
}
