namespace helengine {
    /// <summary>
    /// Adapts one binder-owned HelPhysics scene to Core's exact fixed-step and profiler contracts.
    /// </summary>
    public sealed class HelPhysicsSceneRuntime3D : IPhysicsRuntime
#if !HELENGINE_CODEGEN_FEATURE_DISABLED_RUNTIME_PROFILER
        , IPhysicsRuntimeProfilerMetricsProvider
#endif
    {
        /// <summary>
        /// Initializes the Core-facing adapter around one exclusively owned scene binder.
        /// </summary>
        /// <param name="binder">Binder that coordinates entity synchronization and world stepping.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="binder"/> is null.</exception>
        public HelPhysicsSceneRuntime3D(HelPhysicsSceneBinder3D binder) {
            Binder = binder ?? throw new ArgumentNullException(nameof(binder));
        }

        /// <summary>
        /// Gets the binder that owns all scene associations and the underlying fixed-capacity world.
        /// </summary>
        public HelPhysicsSceneBinder3D Binder { get; }

        /// <summary>
        /// Advances entity input, the HelPhysics world, and dynamic entity output by one exact configured step.
        /// </summary>
        /// <param name="stepSeconds">Core scheduler duration that must exactly match the world setting.</param>
        /// <exception cref="InvalidOperationException">Thrown before mutation when Core and HelPhysics use different fixed-step settings.</exception>
        public void Step(double stepSeconds) {
            if (stepSeconds != Binder.World.Settings.FixedStepSeconds) {
                throw new InvalidOperationException("Core and HelPhysics fixed-step settings do not match.");
            }

            Binder.Step();
        }

        /// <summary>
        /// Forwards the reusable runtime-owned physics metrics published by the binder's world.
        /// </summary>
        /// <param name="metrics">Receives the world's current authoritative physics metrics.</param>
        /// <returns>True when the world provides its metrics sample.</returns>
#if !HELENGINE_CODEGEN_FEATURE_DISABLED_RUNTIME_PROFILER
        public bool TryGetRuntimeProfilerMetrics(out RuntimePhysicsProfilerMetrics metrics) {
            return Binder.World.TryGetRuntimeProfilerMetrics(out metrics);
        }
#endif
    }
}
