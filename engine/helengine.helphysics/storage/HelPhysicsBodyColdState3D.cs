namespace helengine {
    /// <summary>
    /// Stores body metadata that simulation hot loops do not need to read while integrating motion.
    /// </summary>
    struct HelPhysicsBodyColdState3D {
        /// <summary>
        /// Stores the separate shape allocation that defines this body's collision geometry.
        /// </summary>
        public HelPhysicsShapeHandle3D ShapeHandle;

        /// <summary>
        /// Stores how the body participates in simulation and collision response.
        /// </summary>
        public BodyKind3D BodyKind;

        /// <summary>
        /// Stores validated contact-response coefficients without requiring a runtime material lookup.
        /// </summary>
        public HelPhysicsMaterial3D Material;

        /// <summary>
        /// Stores the collision layer this body belongs to.
        /// </summary>
        public ushort CollisionLayer;

        /// <summary>
        /// Stores the collision layers this body accepts.
        /// </summary>
        public ushort CollisionMask;

        /// <summary>
        /// Stores the authored entity binding used to connect simulation results back to engine ownership.
        /// </summary>
        public int EntityBindingId;

        /// <summary>
        /// Stores the validated squared linear speed at or below which this body contributes to quiet island duration.
        /// </summary>
        public readonly PhysicsScalar LinearSleepThresholdSquared;

        /// <summary>
        /// Stores the validated squared angular speed at or below which this body contributes to quiet island duration.
        /// </summary>
        public readonly PhysicsScalar AngularSleepThresholdSquared;

        /// <summary>
        /// Stores the positive number of consecutive quiet fixed steps this body requires before its island may sleep.
        /// </summary>
        public readonly ushort SleepTicks;

        /// <summary>
        /// Initializes complete cold metadata while validating the sleep configuration needed by allocation-free island evaluation.
        /// </summary>
        /// <param name="shapeHandle">Separate fixed-pool shape allocation used by this body.</param>
        /// <param name="bodyKind">Simulation and collision participation mode.</param>
        /// <param name="material">Validated contact-response coefficients.</param>
        /// <param name="collisionLayer">Collision layer emitted by this body.</param>
        /// <param name="collisionMask">Collision layers accepted by this body.</param>
        /// <param name="entityBindingId">Authored entity ownership identifier.</param>
        /// <param name="linearSleepThresholdSquared">Finite non-negative squared linear speed threshold.</param>
        /// <param name="angularSleepThresholdSquared">Finite non-negative squared angular speed threshold.</param>
        /// <param name="sleepTicks">Positive consecutive quiet-step requirement.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when a threshold is not finite or non-negative, or when <paramref name="sleepTicks"/> is zero.</exception>
        public HelPhysicsBodyColdState3D(
            HelPhysicsShapeHandle3D shapeHandle,
            BodyKind3D bodyKind,
            HelPhysicsMaterial3D material,
            ushort collisionLayer,
            ushort collisionMask,
            int entityBindingId,
            PhysicsScalar linearSleepThresholdSquared,
            PhysicsScalar angularSleepThresholdSquared,
            ushort sleepTicks) {
            ValidateSleepThreshold(linearSleepThresholdSquared, nameof(linearSleepThresholdSquared));
            ValidateSleepThreshold(angularSleepThresholdSquared, nameof(angularSleepThresholdSquared));
            if (sleepTicks == 0) {
                throw new ArgumentOutOfRangeException(nameof(sleepTicks), "Sleep tick counts must be positive.");
            }

            ShapeHandle = shapeHandle;
            BodyKind = bodyKind;
            Material = material;
            CollisionLayer = collisionLayer;
            CollisionMask = collisionMask;
            EntityBindingId = entityBindingId;
            LinearSleepThresholdSquared = linearSleepThresholdSquared;
            AngularSleepThresholdSquared = angularSleepThresholdSquared;
            SleepTicks = sleepTicks;
        }

        /// <summary>
        /// Validates one squared speed threshold before it enters cold body storage.
        /// </summary>
        /// <param name="thresholdSquared">Squared speed threshold to validate.</param>
        /// <param name="parameterName">Constructor parameter name used by diagnostics.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the threshold is not finite or is negative.</exception>
        static void ValidateSleepThreshold(PhysicsScalar thresholdSquared, string parameterName) {
            double thresholdValue = thresholdSquared.ToFloat();
            if (double.IsNaN(thresholdValue) || double.IsInfinity(thresholdValue) || thresholdValue < 0d) {
                throw new ArgumentOutOfRangeException(parameterName, "Squared sleep thresholds must be finite and non-negative.");
            }
        }
    }
}
