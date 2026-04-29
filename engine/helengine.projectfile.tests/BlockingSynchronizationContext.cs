namespace helengine.projectfile.tests;

/// <summary>
/// Drops posted callbacks so tests can detect async code that incorrectly captures one UI synchronization context.
/// </summary>
public sealed class BlockingSynchronizationContext : SynchronizationContext {
    /// <summary>
    /// Drops posted callbacks to simulate one UI thread without a running message pump.
    /// </summary>
    /// <param name="d">Callback posted by asynchronous continuations.</param>
    /// <param name="state">Opaque callback state.</param>
    public override void Post(SendOrPostCallback d, object state) {
    }

    /// <summary>
    /// Executes synchronous callbacks inline because only posted continuations matter for this deadlock scenario.
    /// </summary>
    /// <param name="d">Callback to execute.</param>
    /// <param name="state">Opaque callback state.</param>
    public override void Send(SendOrPostCallback d, object state) {
        d(state);
    }
}
