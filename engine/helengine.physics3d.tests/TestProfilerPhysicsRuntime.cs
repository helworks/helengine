namespace helengine.physics3d.tests {
    /// <summary>
    /// Supplies deterministic physics profiler metrics to core runtime metric tests without depending on one concrete physics implementation.
    /// </summary>
    sealed class TestProfilerPhysicsRuntime : IPhysicsRuntime, IPhysicsRuntimeProfilerMetricsProvider {
        /// <summary>
        /// Initializes the test runtime with one fully available physics metric sample.
        /// </summary>
        /// <param name="bodyCount">Number of active physics bodies to report.</param>
        /// <param name="contactCount">Number of active contacts to report.</param>
        /// <param name="constraintCount">Number of active constraints to report.</param>
        public TestProfilerPhysicsRuntime(int bodyCount, int contactCount, int constraintCount) {
            Metrics = new RuntimePhysicsProfilerMetrics(bodyCount, contactCount, constraintCount);
        }

        /// <summary>
        /// Stores the metric sample returned to the core after each fixed-step update.
        /// </summary>
        RuntimePhysicsProfilerMetrics Metrics { get; set; }

        /// <summary>
        /// Records one fixed-step call without changing the configured metric sample.
        /// </summary>
        /// <param name="stepSeconds">Fixed simulation duration supplied by the core.</param>
        public void Step(double stepSeconds) {
        }

        /// <summary>
        /// Reports the configured sample as a valid physics metric set.
        /// </summary>
        /// <param name="metrics">Configured metric sample.</param>
        /// <returns>True while metrics remain configured.</returns>
        public bool TryGetRuntimeProfilerMetrics(out RuntimePhysicsProfilerMetrics metrics) {
            metrics = Metrics;
            return Metrics != null;
        }

        /// <summary>
        /// Removes the test metric sample so the core must mark the next frame's physics metrics unavailable.
        /// </summary>
        public void SetMetricsUnavailable() {
            Metrics = null;
        }
    }
}
