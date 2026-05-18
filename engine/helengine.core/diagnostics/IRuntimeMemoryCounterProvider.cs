namespace helengine {
    /// <summary>
    /// Supplies reusable scalar runtime memory counters without building rich diagnostics object graphs.
    /// </summary>
    public interface IRuntimeMemoryCounterProvider {
        /// <summary>
        /// Captures the current scalar runtime memory counters into the supplied reusable container.
        /// </summary>
        /// <param name="counters">Reusable counter container that should receive the latest values.</param>
        void CaptureMemoryCounters(RuntimeMemoryCounters counters);
    }
}
