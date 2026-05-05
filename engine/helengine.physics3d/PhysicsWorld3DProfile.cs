namespace helengine {
    /// <summary>
    /// Describes one named 3D physics runtime profile and its default capability limits.
    /// </summary>
    public class PhysicsWorld3DProfile {
        /// <summary>
        /// Initializes a new 3D physics runtime profile.
        /// </summary>
        /// <param name="profileId">Stable identifier for the profile.</param>
        /// <param name="defaultBroadphaseKind">Default dynamic-body broadphase strategy.</param>
        /// <param name="allowStaticMeshCollision">True when cooked static mesh world collision is available.</param>
        /// <param name="allowDynamicBodies">True when solver-driven dynamic bodies are available.</param>
        /// <param name="allowJoints">True when rigid-body joints are available.</param>
        /// <param name="allowContinuousCollisionDetection">True when continuous collision detection is available.</param>
        /// <param name="solverIterations">Default iterative contact-solver passes applied each fixed step.</param>
        public PhysicsWorld3DProfile(
            string profileId,
            BroadphaseKind3D defaultBroadphaseKind,
            bool allowStaticMeshCollision,
            bool allowDynamicBodies,
            bool allowJoints,
            bool allowContinuousCollisionDetection,
            int solverIterations) {
            if (string.IsNullOrWhiteSpace(profileId)) {
                throw new ArgumentException("Profile id must be provided.", nameof(profileId));
            }
            if (solverIterations <= 0) {
                throw new ArgumentOutOfRangeException(nameof(solverIterations), "Solver iteration counts must be greater than zero.");
            }

            ProfileId = profileId;
            DefaultBroadphaseKind = defaultBroadphaseKind;
            AllowStaticMeshCollision = allowStaticMeshCollision;
            AllowDynamicBodies = allowDynamicBodies;
            AllowJoints = allowJoints;
            AllowContinuousCollisionDetection = allowContinuousCollisionDetection;
            SolverIterations = solverIterations;
        }

        /// <summary>
        /// Gets the stable identifier for this runtime profile.
        /// </summary>
        public string ProfileId { get; }

        /// <summary>
        /// Gets the default dynamic-body broadphase strategy for this profile.
        /// </summary>
        public BroadphaseKind3D DefaultBroadphaseKind { get; }

        /// <summary>
        /// Gets a value indicating whether cooked static mesh world collision is available.
        /// </summary>
        public bool AllowStaticMeshCollision { get; }

        /// <summary>
        /// Gets a value indicating whether solver-driven dynamic bodies are available.
        /// </summary>
        public bool AllowDynamicBodies { get; }

        /// <summary>
        /// Gets a value indicating whether rigid-body joints are available.
        /// </summary>
        public bool AllowJoints { get; }

        /// <summary>
        /// Gets a value indicating whether continuous collision detection is available.
        /// </summary>
        public bool AllowContinuousCollisionDetection { get; }

        /// <summary>
        /// Gets the default iterative contact-solver passes applied each fixed step.
        /// </summary>
        public int SolverIterations { get; }

        /// <summary>
        /// Creates the medium console profile that targets PS2, GameCube, and similar hardware.
        /// </summary>
        /// <returns>Configured medium runtime profile.</returns>
        public static PhysicsWorld3DProfile CreateMedium() {
            return new PhysicsWorld3DProfile(
                "medium",
                BroadphaseKind3D.UniformGrid,
                true,
                true,
                false,
                false,
                8);
        }
    }
}
