namespace helengine.editor.tests.managers.play;

/// <summary>
/// Test scheduler that runs a Play preparation synchronously on the calling thread. The preparation still reports its
/// outcome through the main-thread queue, so a test sees exactly the frame-thread hand-off the editor sees, without a
/// second thread: the player launches only when the test drains the queue.
/// </summary>
public sealed class InlinePlayPreparationScheduler : IEditorPlayPreparationScheduler {
    /// <summary>
    /// Gets the number of preparations started.
    /// </summary>
    public int StartCount { get; private set; }

    /// <summary>
    /// Runs the preparation work immediately on the calling thread.
    /// </summary>
    /// <param name="work">Preparation work to run.</param>
    public void Start(Action work) {
        StartCount++;
        work();
    }
}
