namespace helengine {
    /// <summary>
    /// Allows a physics runtime to expose only the profiler counters it can authoritatively own.
    /// </summary>
    public interface IPhysicsRuntimeProfilerMetricsProvider {
        /// <summary>
        /// Attempts to provide the current runtime-owned physics metrics after one core update.
        /// </summary>
        /// <param name="metrics">Current authoritative physics metrics when available.</param>
        /// <returns>True when the runtime supplied metrics; otherwise false.</returns>
        bool TryGetRuntimeProfilerMetrics(out RuntimePhysicsProfilerMetrics metrics);
    }
}
