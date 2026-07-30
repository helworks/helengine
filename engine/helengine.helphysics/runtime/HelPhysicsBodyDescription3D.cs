namespace helengine {
    /// <summary>
    /// Carries every authored value required to reserve one box body without inventing simulation state at creation time.
    /// </summary>
    public sealed class HelPhysicsBodyDescription3D {
        /// <summary>
        /// Initializes and validates a complete box body description, deriving inverse mass and box inertia from explicit mass and shape.
        /// </summary>
        /// <param name="shape">Explicit centered box shape value allocated for this body.</param>
        /// <param name="bodyKind">Static, kinematic, or dynamic participation mode.</param>
        /// <param name="position">Explicit world-space center-of-mass position.</param>
        /// <param name="orientation">Explicit normalized world-space orientation.</param>
        /// <param name="linearVelocity">Explicit initial world-space linear velocity.</param>
        /// <param name="angularVelocity">Explicit initial world-space angular velocity.</param>
        /// <param name="mass">Positive dynamic mass or exact zero for static and kinematic bodies.</param>
        /// <param name="material">Explicit validated contact material.</param>
        /// <param name="collisionLayer">Explicit collision layer emitted by this body.</param>
        /// <param name="collisionMask">Explicit collision mask accepted by this body.</param>
        /// <param name="entityBindingId">Explicit engine ownership identifier retained for later scene binding.</param>
        /// <param name="gravityScale">Explicit multiplier applied to world gravity.</param>
        /// <param name="linearDamping">Finite non-negative linear damping coefficient.</param>
        /// <param name="angularDamping">Finite non-negative angular damping coefficient.</param>
        /// <param name="linearSleepThreshold">Finite non-negative linear speed threshold.</param>
        /// <param name="angularSleepThreshold">Finite non-negative angular speed threshold.</param>
        /// <param name="sleepTicks">Positive number of consecutive quiet steps required for sleep.</param>
        /// <param name="isAwake">Explicit initial awake state; only dynamic bodies may begin awake.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when mode, shape, mass, pose, damping, or sleep values are invalid.</exception>
        /// <exception cref="ArgumentException">Thrown when immovable body motion or awake state contradicts its mode.</exception>
        public HelPhysicsBodyDescription3D(
            HelPhysicsBoxShape3D shape,
            BodyKind3D bodyKind,
            PhysicsVector3 position,
            PhysicsQuaternion orientation,
            PhysicsVector3 linearVelocity,
            PhysicsVector3 angularVelocity,
            PhysicsScalar mass,
            HelPhysicsMaterial3D material,
            ushort collisionLayer,
            ushort collisionMask,
            int entityBindingId,
            PhysicsScalar gravityScale,
            PhysicsScalar linearDamping,
            PhysicsScalar angularDamping,
            PhysicsScalar linearSleepThreshold,
            PhysicsScalar angularSleepThreshold,
            ushort sleepTicks,
            bool isAwake) {
            ValidateShape(in shape);
            ValidateBodyKind(bodyKind);
            ValidateOrientation(in orientation);
            ValidateMass(bodyKind, mass);
            ValidateNonNegative(linearDamping, nameof(linearDamping));
            ValidateNonNegative(angularDamping, nameof(angularDamping));
            ValidateNonNegative(linearSleepThreshold, nameof(linearSleepThreshold));
            ValidateNonNegative(angularSleepThreshold, nameof(angularSleepThreshold));
            if (sleepTicks == 0) {
                throw new ArgumentOutOfRangeException(nameof(sleepTicks), "Sleep tick counts must be positive.");
            }

            if (bodyKind == BodyKind3D.Static &&
                (linearVelocity.LengthSquared() != PhysicsScalar.Zero || angularVelocity.LengthSquared() != PhysicsScalar.Zero)) {
                throw new ArgumentException("Static bodies cannot carry authored linear or angular velocity.", nameof(linearVelocity));
            }

            if (bodyKind != BodyKind3D.Dynamic && isAwake) {
                throw new ArgumentException("Only dynamic bodies may begin in the awake simulation set.", nameof(isAwake));
            }

            Shape = shape;
            BodyKind = bodyKind;
            Position = position;
            Orientation = orientation;
            LinearVelocity = linearVelocity;
            AngularVelocity = angularVelocity;
            Mass = mass;
            if (bodyKind == BodyKind3D.Dynamic) {
                InverseMass = PhysicsScalar.One / mass;
            } else {
                InverseMass = PhysicsScalar.Zero;
            }
            LocalInverseInertia = HelPhysicsBoxGeometry3D.ComputeLocalInverseInertia(shape, bodyKind, mass);
            Material = material;
            CollisionLayer = collisionLayer;
            CollisionMask = collisionMask;
            EntityBindingId = entityBindingId;
            GravityScale = gravityScale;
            LinearDamping = linearDamping;
            AngularDamping = angularDamping;
            LinearSleepThreshold = linearSleepThreshold;
            AngularSleepThreshold = angularSleepThreshold;
            LinearSleepThresholdSquared = linearSleepThreshold * linearSleepThreshold;
            AngularSleepThresholdSquared = angularSleepThreshold * angularSleepThreshold;
            SleepTicks = sleepTicks;
            IsAwake = isAwake;
        }

        /// <summary>
        /// Gets the explicit centered box shape allocated for this body.
        /// </summary>
        public HelPhysicsBoxShape3D Shape { get; }

        /// <summary>
        /// Gets the explicit simulation participation mode.
        /// </summary>
        public BodyKind3D BodyKind { get; }

        /// <summary>
        /// Gets the explicit initial world-space center-of-mass position.
        /// </summary>
        public PhysicsVector3 Position { get; }

        /// <summary>
        /// Gets the explicit normalized initial world-space orientation.
        /// </summary>
        public PhysicsQuaternion Orientation { get; }

        /// <summary>
        /// Gets the explicit initial world-space linear velocity.
        /// </summary>
        public PhysicsVector3 LinearVelocity { get; }

        /// <summary>
        /// Gets the explicit initial world-space angular velocity.
        /// </summary>
        public PhysicsVector3 AngularVelocity { get; }

        /// <summary>
        /// Gets the explicit mass supplied by the author.
        /// </summary>
        public PhysicsScalar Mass { get; }

        /// <summary>
        /// Gets reciprocal dynamic mass or zero for non-dynamic modes.
        /// </summary>
        public PhysicsScalar InverseMass { get; }

        /// <summary>
        /// Gets local inverse box inertia derived deterministically from explicit shape, mode, and mass.
        /// </summary>
        public PhysicsMatrix3x3 LocalInverseInertia { get; }

        /// <summary>
        /// Gets the explicit contact response material.
        /// </summary>
        public HelPhysicsMaterial3D Material { get; }

        /// <summary>
        /// Gets the explicit collision layer emitted by the body.
        /// </summary>
        public ushort CollisionLayer { get; }

        /// <summary>
        /// Gets the explicit collision layers accepted by the body.
        /// </summary>
        public ushort CollisionMask { get; }

        /// <summary>
        /// Gets the explicit engine ownership identifier retained for scene binding.
        /// </summary>
        public int EntityBindingId { get; }

        /// <summary>
        /// Gets the explicit multiplier applied to world gravity.
        /// </summary>
        public PhysicsScalar GravityScale { get; }

        /// <summary>
        /// Gets the explicit non-negative linear damping coefficient.
        /// </summary>
        public PhysicsScalar LinearDamping { get; }

        /// <summary>
        /// Gets the explicit non-negative angular damping coefficient.
        /// </summary>
        public PhysicsScalar AngularDamping { get; }

        /// <summary>
        /// Gets the explicit non-negative linear speed threshold.
        /// </summary>
        public PhysicsScalar LinearSleepThreshold { get; }

        /// <summary>
        /// Gets the explicit non-negative angular speed threshold.
        /// </summary>
        public PhysicsScalar AngularSleepThreshold { get; }

        /// <summary>
        /// Gets the precomputed squared linear speed threshold used by hot sleep loops.
        /// </summary>
        public PhysicsScalar LinearSleepThresholdSquared { get; }

        /// <summary>
        /// Gets the precomputed squared angular speed threshold used by hot sleep loops.
        /// </summary>
        public PhysicsScalar AngularSleepThresholdSquared { get; }

        /// <summary>
        /// Gets the positive authored quiet duration required before sleep.
        /// </summary>
        public ushort SleepTicks { get; }

        /// <summary>
        /// Gets the explicit initial dynamic awake state.
        /// </summary>
        public bool IsAwake { get; }

        /// <summary>
        /// Validates that a box value did not bypass its value-type constructor with zero extents.
        /// </summary>
        /// <param name="shape">Shape value to validate.</param>
        static void ValidateShape(in HelPhysicsBoxShape3D shape) {
            if (shape.HalfExtents.X <= PhysicsScalar.Zero ||
                shape.HalfExtents.Y <= PhysicsScalar.Zero ||
                shape.HalfExtents.Z <= PhysicsScalar.Zero) {
                throw new ArgumentOutOfRangeException(nameof(shape), "Body descriptions require a non-degenerate explicit box shape.");
            }
        }

        /// <summary>
        /// Validates one supported body participation mode.
        /// </summary>
        /// <param name="bodyKind">Mode to validate.</param>
        static void ValidateBodyKind(BodyKind3D bodyKind) {
            if (bodyKind != BodyKind3D.Static && bodyKind != BodyKind3D.Kinematic && bodyKind != BodyKind3D.Dynamic) {
                throw new ArgumentOutOfRangeException(nameof(bodyKind), "Body descriptions require a supported body mode.");
            }
        }

        /// <summary>
        /// Validates that an authored orientation is already unit length and can be consumed without silent normalization.
        /// </summary>
        /// <param name="orientation">Quaternion to validate.</param>
        static void ValidateOrientation(in PhysicsQuaternion orientation) {
            double lengthSquared =
                ((double)orientation.X.ToFloat() * orientation.X.ToFloat()) +
                ((double)orientation.Y.ToFloat() * orientation.Y.ToFloat()) +
                ((double)orientation.Z.ToFloat() * orientation.Z.ToFloat()) +
                ((double)orientation.W.ToFloat() * orientation.W.ToFloat());
            if (Math.Abs(lengthSquared - 1d) > 0.0001d) {
                throw new ArgumentOutOfRangeException(nameof(orientation), "Body orientations must be normalized before creation.");
            }
        }

        /// <summary>
        /// Validates positive dynamic mass and exact zero mass for immovable response modes.
        /// </summary>
        /// <param name="bodyKind">Body mode that interprets mass.</param>
        /// <param name="mass">Explicit mass to validate.</param>
        static void ValidateMass(BodyKind3D bodyKind, PhysicsScalar mass) {
            if (bodyKind == BodyKind3D.Dynamic) {
                if (mass <= PhysicsScalar.Zero) {
                    throw new ArgumentOutOfRangeException(nameof(mass), "Dynamic bodies require strictly positive mass.");
                }
            } else if (mass != PhysicsScalar.Zero) {
                throw new ArgumentOutOfRangeException(nameof(mass), "Static and kinematic bodies require exact zero mass and inertia response.");
            }
        }

        /// <summary>
        /// Validates one finite physics scalar that cannot be negative.
        /// </summary>
        /// <param name="value">Scalar to validate.</param>
        /// <param name="parameterName">Constructor parameter name used by diagnostics.</param>
        static void ValidateNonNegative(PhysicsScalar value, string parameterName) {
            if (value < PhysicsScalar.Zero) {
                throw new ArgumentOutOfRangeException(parameterName, "Damping and sleep thresholds must be non-negative.");
            }
        }
    }
}
