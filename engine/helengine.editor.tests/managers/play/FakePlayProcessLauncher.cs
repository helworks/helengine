namespace helengine.editor.tests.managers.play;

/// <summary>
/// Test double for the player process launcher that records every launch request and hands out fake processes.
/// </summary>
public sealed class FakePlayProcessLauncher : IEditorPlayProcessLauncher {
    /// <summary>
    /// Launch requests received, in call order.
    /// </summary>
    public readonly List<EditorPlayLaunchRequest> Requests = new List<EditorPlayLaunchRequest>();

    /// <summary>
    /// Fake processes handed out, in launch order; each one matches the request at the same index.
    /// </summary>
    public readonly List<FakePlayProcess> StartedProcesses = new List<FakePlayProcess>();

    /// <summary>
    /// When true, every started process has already exited before the session service can subscribe to it.
    /// </summary>
    public bool StartAlreadyExited;

    /// <summary>
    /// Exit code used for processes started while <see cref="StartAlreadyExited"/> is true.
    /// </summary>
    public int AlreadyExitedCode;

    /// <summary>
    /// Records the launch request and returns a new fake process with an increasing process identifier, exiting it
    /// immediately when <see cref="StartAlreadyExited"/> is set.
    /// </summary>
    /// <param name="request">Launch request built by the session service.</param>
    /// <returns>The new fake process.</returns>
    public IEditorPlayProcess Start(EditorPlayLaunchRequest request) {
        Requests.Add(request);
        FakePlayProcess process = new FakePlayProcess(1000 + StartedProcesses.Count);
        StartedProcesses.Add(process);
        if (StartAlreadyExited) {
            process.RaiseExit(AlreadyExitedCode);
        }

        return process;
    }
}
