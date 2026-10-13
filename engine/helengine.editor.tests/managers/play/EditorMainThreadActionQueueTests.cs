using Xunit;

namespace helengine.editor.tests.managers.play;

/// <summary>
/// Verifies that the frame-thread action queue defers background work until the frame thread drains it.
/// </summary>
public sealed class EditorMainThreadActionQueueTests {
    /// <summary>
    /// Ensures actions enqueued from a background thread do not run there, and run in order on the draining thread.
    /// </summary>
    [Fact]
    public void ProcessPending_RunsBackgroundEnqueuedActionsInOrderOnDrainingThread() {
        EditorMainThreadActionQueue queue = new EditorMainThreadActionQueue();
        List<int> executionOrder = new List<int>();
        List<int> executionThreadIds = new List<int>();
        Thread producer = new Thread(() => {
            for (int index = 0; index < 3; index++) {
                int capturedIndex = index;
                queue.Enqueue(() => {
                    executionOrder.Add(capturedIndex);
                    executionThreadIds.Add(Environment.CurrentManagedThreadId);
                });
            }
        });
        producer.Start();
        producer.Join();

        Assert.Empty(executionOrder);

        queue.ProcessPending();

        Assert.Equal(new[] { 0, 1, 2 }, executionOrder);
        Assert.All(executionThreadIds, threadId => Assert.Equal(Environment.CurrentManagedThreadId, threadId));
    }

    /// <summary>
    /// Ensures an action enqueued while draining runs within the same drain.
    /// </summary>
    [Fact]
    public void ProcessPending_RunsActionsEnqueuedDuringTheDrain() {
        EditorMainThreadActionQueue queue = new EditorMainThreadActionQueue();
        List<string> executed = new List<string>();
        queue.Enqueue(() => {
            executed.Add("first");
            queue.Enqueue(() => executed.Add("second"));
        });

        queue.ProcessPending();

        Assert.Equal(new[] { "first", "second" }, executed);
    }

    /// <summary>
    /// Ensures a null action is rejected.
    /// </summary>
    [Fact]
    public void Enqueue_WhenActionIsNull_Throws() {
        EditorMainThreadActionQueue queue = new EditorMainThreadActionQueue();

        Assert.Throws<ArgumentNullException>(() => queue.Enqueue(null));
    }
}
