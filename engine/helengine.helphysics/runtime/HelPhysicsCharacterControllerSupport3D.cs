namespace helengine {
    /// <summary>
    /// Describes one walkable support sample returned by a HelPhysics character query.
    /// </summary>
    public readonly struct HelPhysicsCharacterControllerSupport3D {
        /// <summary>
        /// Initializes one support sample.
        /// </summary>
        /// <param name="isValid">Whether a support surface was found.</param>
        /// <param name="height">World-space top height of the support surface.</param>
        /// <param name="velocity">World-space support velocity inherited by a grounded controller.</param>
        public HelPhysicsCharacterControllerSupport3D(bool isValid, float height, float3 velocity)
            : this(isValid, height, velocity, new float3(0f, 1f, 0f)) {
        }

        /// <summary>
        /// Initializes one support sample with its world-space contact normal.
        /// </summary>
        /// <param name="isValid">Whether a support surface was found.</param>
        /// <param name="height">World-space top height of the support surface.</param>
        /// <param name="velocity">World-space support velocity inherited by a grounded controller.</param>
        /// <param name="normal">World-space unit support normal.</param>
        public HelPhysicsCharacterControllerSupport3D(bool isValid, float height, float3 velocity, float3 normal) {
            IsValid = isValid;
            Height = height;
            Velocity = velocity;
            Normal = normal;
        }
        /// <summary>
        /// Gets whether the query found a support surface beneath the controller footprint.
        /// </summary>
        public bool IsValid { get; }

        /// <summary>
        /// Gets the world-space top height of the support surface.
        /// </summary>
        public float Height { get; }

        /// <summary>
        /// Gets the world-space support velocity inherited while grounded.
        /// </summary>
        public float3 Velocity { get; }
        /// <summary>
        /// Gets the world-space support normal used for walkable-slope filtering.
        /// </summary>
        public float3 Normal { get; }
    }
}
