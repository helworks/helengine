namespace helengine.editor;

/// <summary>
/// Owns the project-scoped cleanup ledger used by an editor session.
/// </summary>
public sealed class EditorProjectLifecycleCoordinator : IDisposable {
    /// <summary>
    /// The existing ordered cleanup ledger remains the single disposal implementation.
    /// </summary>
    internal EditorSessionConstructionLedger Ledger { get; }

    /// <summary>
    /// Initializes an empty project lifecycle coordinator.
    /// </summary>
    public EditorProjectLifecycleCoordinator() {
        Ledger = new EditorSessionConstructionLedger();
    }

    /// <summary>
    /// Gets whether project ownership has transferred from construction to live use.
    /// </summary>
    public bool HasTransferredOwnership => Ledger.HasTransferredOwnership;

    /// <summary>
    /// Registers a project-owned cleanup action.
    /// </summary>
    /// <param name="cleanup">Cleanup action.</param>
    /// <param name="phase">Dependency-aware cleanup phase.</param>
    internal void Register(Action cleanup, EditorSessionCleanupPhase phase = EditorSessionCleanupPhase.Dispose) {
        Ledger.Register(cleanup, phase);
    }

    /// <summary>
    /// Transfers the registered resources to the live project lifetime.
    /// </summary>
    public void TransferOwnership() {
        Ledger.TransferOwnership();
    }

    /// <summary>
    /// Disposes unresolved project-owned resources using the existing retryable policy.
    /// </summary>
    public void Dispose() {
        Ledger.Dispose();
    }
}
