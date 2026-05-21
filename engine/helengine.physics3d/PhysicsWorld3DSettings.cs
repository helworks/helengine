namespace helengine {
    /// <summary>
    /// Stores one world instance's effective 3D physics configuration resolved from profile defaults and local overrides.
    /// </summary>
    public class PhysicsWorld3DSettings {
        /// <summary>
        /// Initializes a new 3D physics world settings record.
        /// </summary>
        /// <param name="profile">Runtime profile that constrains this world.</param>
        /// <param name="broadphaseKind">Broadphase strategy used for dynamic-body candidate generation.</param>
        /// <param name="solverIterations">Iterative contact-solver passes applied each fixed step.</param>
        public PhysicsWorld3DSettings(PhysicsWorld3DProfile profile, BroadphaseKind3D broadphaseKind, int solverIterations) {
            Profile = profile ?? throw new ArgumentNullException(nameof(profile));
            if (solverIterations <= 0) {
                throw new ArgumentOutOfRangeException(nameof(solverIterations), "Solver iteration counts must be greater than zero.");
            }

            BroadphaseKind = broadphaseKind;
            SolverIterations = solverIterations;
        }

        /// <summary>
        /// Gets the runtime profile that constrains this world.
        /// </summary>
        public PhysicsWorld3DProfile Profile { get; }

        /// <summary>
        /// Gets the dynamic-body broadphase strategy used by this world instance.
        /// </summary>
        public BroadphaseKind3D BroadphaseKind { get; }

        /// <summary>
        /// Gets the iterative contact-solver passes applied each fixed step.
        /// </summary>
        public int SolverIterations { get; }

        /// <summary>
        /// Creates settings from the defaults exposed by the supplied profile.
        /// </summary>
        /// <param name="profile">Runtime profile that constrains this world.</param>
        /// <returns>Settings initialized from the profile defaults.</returns>
        public static PhysicsWorld3DSettings CreateDefault(PhysicsWorld3DProfile profile) {
            if (profile == null) {
                throw new ArgumentNullException(nameof(profile));
            }

            return new PhysicsWorld3DSettings(profile, profile.DefaultBroadphaseKind, profile.SolverIterations);
        }
    }
}
