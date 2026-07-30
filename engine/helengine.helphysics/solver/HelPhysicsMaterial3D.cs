namespace helengine {
    /// <summary>
    /// Stores validated contact-response coefficients directly on cold body state without requiring a runtime material registry.
    /// </summary>
    public readonly struct HelPhysicsMaterial3D {
        /// <summary>
        /// Stores the coefficient used to decide whether a contact can completely cancel tangential motion.
        /// </summary>
        public readonly PhysicsScalar StaticFriction;

        /// <summary>
        /// Stores the coefficient used to limit sliding friction after the static friction cone is exceeded.
        /// </summary>
        public readonly PhysicsScalar DynamicFriction;

        /// <summary>
        /// Stores the zero-through-one coefficient used to retain impact speed along the contact normal.
        /// </summary>
        public readonly PhysicsScalar Restitution;

        /// <summary>
        /// Initializes one material after validating its friction and restitution coefficient domains.
        /// </summary>
        /// <param name="staticFriction">Non-negative static friction coefficient.</param>
        /// <param name="dynamicFriction">Non-negative dynamic friction coefficient.</param>
        /// <param name="restitution">Restitution coefficient from zero through one.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when friction is negative or restitution lies outside zero through one.</exception>
        public HelPhysicsMaterial3D(
            PhysicsScalar staticFriction,
            PhysicsScalar dynamicFriction,
            PhysicsScalar restitution) {
            if (staticFriction < PhysicsScalar.Zero) {
                throw new ArgumentOutOfRangeException(nameof(staticFriction), "Static friction must be greater than or equal to zero.");
            }

            if (dynamicFriction < PhysicsScalar.Zero) {
                throw new ArgumentOutOfRangeException(nameof(dynamicFriction), "Dynamic friction must be greater than or equal to zero.");
            }

            if (restitution < PhysicsScalar.Zero || restitution > PhysicsScalar.One) {
                throw new ArgumentOutOfRangeException(nameof(restitution), "Restitution must be between zero and one inclusive.");
            }

            StaticFriction = staticFriction;
            DynamicFriction = dynamicFriction;
            Restitution = restitution;
        }
    }
}
