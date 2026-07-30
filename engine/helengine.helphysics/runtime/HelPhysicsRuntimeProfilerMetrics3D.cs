namespace helengine {
    /// <summary>
    /// Owns the HelPhysics profiler sample and exposes its protected reusable update path only inside the runtime assembly.
    /// </summary>
    sealed class HelPhysicsRuntimeProfilerMetrics3D : RuntimePhysicsProfilerMetrics {
        /// <summary>
        /// Initializes one complete zero-valued sample that the world reuses for its entire lifetime.
        /// </summary>
        public HelPhysicsRuntimeProfilerMetrics3D()
            : base(0, 0, 0) {
        }

        /// <summary>
        /// Publishes one completed HelPhysics step into the inherited immutable-to-callers profiler view.
        /// </summary>
        /// <param name="bodyCount">Number of active world bodies.</param>
        /// <param name="contactCount">Number of current active contact points.</param>
        /// <param name="constraintCount">Number of current active manifold constraints.</param>
        internal void Publish(int bodyCount, int contactCount, int constraintCount) {
            Update(bodyCount, contactCount, constraintCount);
        }
    }
}
