using Xunit;

namespace helengine.editor.tests;

/// <summary>
/// Verifies project-scoped ownership and retryable disposal around the session cleanup ledger.
/// </summary>
public sealed class EditorProjectLifecycleCoordinatorTests {
    /// <summary>
    /// Ensures a coordinator disposes registered project resources exactly once.
    /// </summary>
    [Fact]
    public void Dispose_IsIdempotentAndPreservesLedgerOrdering() {
        List<string> disposed = new();
        EditorProjectLifecycleCoordinator coordinator = new();
        coordinator.Register(() => disposed.Add("project"), EditorSessionCleanupPhase.OwnedState);
        coordinator.Register(() => disposed.Add("detach"), EditorSessionCleanupPhase.Detach);
        coordinator.TransferOwnership();

        coordinator.Dispose();
        coordinator.Dispose();

        Assert.True(coordinator.HasTransferredOwnership);
        Assert.Equal(new[] { "detach", "project" }, disposed);
    }

    /// <summary>
    /// Ensures a cleanup failure remains retryable through the coordinator.
    /// </summary>
    [Fact]
    public void Dispose_RetriesFailedResource() {
        int attempts = 0;
        EditorProjectLifecycleCoordinator coordinator = new();
        coordinator.Register(() => {
            attempts++;
            if (attempts == 1) {
                throw new InvalidOperationException("transient cleanup failure");
            }
        });

        Assert.Throws<InvalidOperationException>(() => coordinator.Dispose());
        coordinator.Dispose();

        Assert.Equal(2, attempts);
    }
}
