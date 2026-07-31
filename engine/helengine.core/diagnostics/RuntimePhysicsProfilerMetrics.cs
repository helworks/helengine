#if !HELENGINE_CODEGEN_FEATURE_DISABLED_RUNTIME_PROFILER
namespace helengine {
    /// <summary>
    /// Describes the physics counters a runtime can authoritatively expose for one profiler frame and allows allocation-sensitive runtimes to reuse one owned sample.
    /// Individual counters are marked unavailable when the physics implementation does not own a valid value.
    /// </summary>
    public class RuntimePhysicsProfilerMetrics {
        /// <summary>
        /// Initializes a complete physics metric sample with body, contact, and constraint counts.
        /// </summary>
        /// <param name="bodyCount">Number of currently active physics bodies.</param>
        /// <param name="contactCount">Number of currently active contact points.</param>
        /// <param name="constraintCount">Number of currently active constraints.</param>
        public RuntimePhysicsProfilerMetrics(int bodyCount, int contactCount, int constraintCount) {
            Update(bodyCount, contactCount, constraintCount);
        }

        /// <summary>
        /// Initializes a physics metric sample when the runtime owns a body count but cannot derive contact or constraint counts faithfully.
        /// </summary>
        /// <param name="bodyCount">Number of currently active physics bodies.</param>
        public RuntimePhysicsProfilerMetrics(int bodyCount) {
            ValidateCount(bodyCount, nameof(bodyCount));
            HasBodyCount = true;
            HasContactCount = false;
            HasConstraintCount = false;
            BodyCount = bodyCount;
            ContactCount = 0;
            ConstraintCount = 0;
        }

        /// <summary>
        /// Gets whether <see cref="BodyCount"/> contains a valid runtime-owned value.
        /// </summary>
        public bool HasBodyCount { get; private set; }

        /// <summary>
        /// Gets whether <see cref="ContactCount"/> contains a valid runtime-owned value.
        /// </summary>
        public bool HasContactCount { get; private set; }

        /// <summary>
        /// Gets whether <see cref="ConstraintCount"/> contains a valid runtime-owned value.
        /// </summary>
        public bool HasConstraintCount { get; private set; }

        /// <summary>
        /// Gets the active physics-body count when <see cref="HasBodyCount"/> is true.
        /// </summary>
        public int BodyCount { get; private set; }

        /// <summary>
        /// Gets the active contact-point count when <see cref="HasContactCount"/> is true.
        /// </summary>
        public int ContactCount { get; private set; }

        /// <summary>
        /// Gets the active constraint count when <see cref="HasConstraintCount"/> is true.
        /// </summary>
        public int ConstraintCount { get; private set; }

        /// <summary>
        /// Replaces this runtime-owned sample with a complete validated set of body, contact, and constraint counts without allocating a new object.
        /// </summary>
        /// <param name="bodyCount">Number of currently active physics bodies.</param>
        /// <param name="contactCount">Number of currently active contact points.</param>
        /// <param name="constraintCount">Number of currently active constraints.</param>
        protected void Update(int bodyCount, int contactCount, int constraintCount) {
            ValidateCount(bodyCount, nameof(bodyCount));
            ValidateCount(contactCount, nameof(contactCount));
            ValidateCount(constraintCount, nameof(constraintCount));
            HasBodyCount = true;
            HasContactCount = true;
            HasConstraintCount = true;
            BodyCount = bodyCount;
            ContactCount = contactCount;
            ConstraintCount = constraintCount;
        }

        /// <summary>
        /// Rejects counters that cannot represent a real runtime quantity.
        /// </summary>
        /// <param name="value">Counter value to validate.</param>
        /// <param name="parameterName">Name of the source argument.</param>
        static void ValidateCount(int value, string parameterName) {
            if (value < 0) {
                throw new ArgumentOutOfRangeException(parameterName, "Profiler metric counts cannot be negative.");
            }
        }
    }
}
#endif
