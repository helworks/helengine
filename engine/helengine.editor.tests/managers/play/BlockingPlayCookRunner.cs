namespace helengine.editor.tests.managers.play;

/// <summary>
/// Test double for the Play cook step that blocks until the test releases it, recording the thread it ran on. It lets a
/// test observe the editor while a cook is in progress, and prove where the cook ran. Waiting uses wait handles only, so
/// it raises no exception of its own.
/// </summary>
public sealed class BlockingPlayCookRunner : IEditorPlayCookRunner, IDisposable {
    /// <summary>
    /// Signaled once the cook has started.
    /// </summary>
    readonly ManualResetEventSlim StartedSignal = new ManualResetEventSlim(false);

    /// <summary>
    /// Signaled by the test to let the cook finish.
    /// </summary>
    readonly ManualResetEventSlim ReleaseSignal = new ManualResetEventSlim(false);

    /// <summary>
    /// Managed id of the thread the cook ran on; zero until it starts.
    /// </summary>
    int CookThreadIdValue;

    /// <summary>
    /// Initializes the fake with the result the cook returns once released.
    /// </summary>
    /// <param name="result">Cook outcome returned to the session service.</param>
    public BlockingPlayCookRunner(EditorPlayCookResult result) {
        Result = result ?? throw new ArgumentNullException(nameof(result));
    }

    /// <summary>
    /// Gets the cook outcome returned once released.
    /// </summary>
    public EditorPlayCookResult Result { get; }

    /// <summary>
    /// Gets the managed id of the thread the cook ran on, or zero before it started.
    /// </summary>
    public int CookThreadId => Volatile.Read(ref CookThreadIdValue);

    /// <summary>
    /// Records the cook thread, signals that the cook started and waits until the test releases it.
    /// </summary>
    /// <param name="platformId">Platform the session service asked to cook.</param>
    /// <returns>The scripted cook outcome.</returns>
    public EditorPlayCookResult Cook(string platformId) {
        Volatile.Write(ref CookThreadIdValue, Environment.CurrentManagedThreadId);
        StartedSignal.Set();
        ReleaseSignal.Wait();
        return Result;
    }

    /// <summary>
    /// Waits until the cook has started.
    /// </summary>
    /// <param name="timeout">Longest time to wait.</param>
    /// <returns>True when the cook started within the timeout.</returns>
    public bool WaitUntilStarted(TimeSpan timeout) {
        return StartedSignal.Wait(timeout);
    }

    /// <summary>
    /// Lets the blocked cook finish.
    /// </summary>
    public void Release() {
        ReleaseSignal.Set();
    }

    /// <summary>
    /// Releases a still-blocked cook so a failed test never leaves its preparation thread waiting. The wait handles are
    /// left to the finalizer because the preparation thread may still be using them.
    /// </summary>
    public void Dispose() {
        ReleaseSignal.Set();
    }
}
