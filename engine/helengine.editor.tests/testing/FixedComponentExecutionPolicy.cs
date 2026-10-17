namespace helengine.editor.tests.testing {
    /// <summary>
    /// Supplies a deterministic editor decision and records evaluations so tests can distinguish scoped, host and runtime dispatch.
    /// </summary>
    public sealed class FixedComponentExecutionPolicy : IComponentExecutionPolicy {
        /// <summary>
        /// Lifecycle decision returned for every evaluated component.
        /// </summary>
        readonly bool LifecycleEnabled;

        /// <summary>
        /// Creates a policy with a fixed authoring decision.
        /// </summary>
        /// <param name="lifecycleEnabled">Whether evaluated components should execute lifecycle callbacks.</param>
        public FixedComponentExecutionPolicy(bool lifecycleEnabled) {
            LifecycleEnabled = lifecycleEnabled;
        }

        /// <summary>
        /// Gets how many times the execution boundary delegated a lifecycle decision to this policy.
        /// </summary>
        public int EvaluationCount { get; private set; }

        /// <inheritdoc />
        public bool ShouldRunComponentLifecycle(Component component, Entity entity) {
            EvaluationCount++;
            return LifecycleEnabled;
        }
    }
}
