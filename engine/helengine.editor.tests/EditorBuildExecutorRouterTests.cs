using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies queued build items are routed to the executor that owns their platform id.
    /// </summary>
    public sealed class EditorBuildExecutorRouterTests {
        /// <summary>
        /// Ensures the router dispatches a PS2 queue item to the PS2 executor and leaves the Windows executor untouched.
        /// </summary>
        [Fact]
        public void Execute_WhenPlatformIsPs2_RoutesToPs2Executor() {
            TestEditorBuildExecutor windowsExecutor = new TestEditorBuildExecutor([
                EditorBuildExecutionResult.Success("Windows build completed.")
            ]);
            TestEditorBuildExecutor ps2Executor = new TestEditorBuildExecutor([
                EditorBuildExecutionResult.Success("PS2 build completed.")
            ]);
            EditorBuildExecutorRouter router = new EditorBuildExecutorRouter(new Dictionary<string, IEditorBuildExecutor>(StringComparer.OrdinalIgnoreCase) {
                { "windows", windowsExecutor },
                { "ps2", ps2Executor }
            });

            EditorBuildExecutionResult result = router.Execute(new EditorBuildQueueItemDocument {
                QueueItemId = "queue-ps2",
                PlatformId = "ps2"
            });

            Assert.True(result.Succeeded);
            Assert.Equal("PS2 build completed.", result.Message);
            Assert.Empty(windowsExecutor.ExecutedQueueItemIds);
            Assert.Single(ps2Executor.ExecutedQueueItemIds);
            Assert.Equal("queue-ps2", ps2Executor.ExecutedQueueItemIds[0]);
        }

        /// <summary>
        /// Ensures the router returns a failure when no executor has been registered for one platform id.
        /// </summary>
        [Fact]
        public void Execute_WhenPlatformIsMissing_ReturnsFailure() {
            EditorBuildExecutorRouter router = new EditorBuildExecutorRouter(new Dictionary<string, IEditorBuildExecutor>(StringComparer.OrdinalIgnoreCase));

            EditorBuildExecutionResult result = router.Execute(new EditorBuildQueueItemDocument {
                QueueItemId = "queue-missing",
                PlatformId = "ps2"
            });

            Assert.False(result.Succeeded);
            Assert.Contains("ps2", result.Message, StringComparison.OrdinalIgnoreCase);
        }
    }
}
