namespace helengine {
    /// <summary>
    /// Defines every fixed allocation and solve setting owned for the complete lifetime of one HelPhysics world.
    /// </summary>
    public sealed class HelPhysicsWorldSettings3D {
        /// <summary>
        /// Stores the default number of addressable body slots.
        /// </summary>
        const int DefaultBodyCapacity = 32;

        /// <summary>
        /// Stores the default number of separately allocated box-shape slots.
        /// </summary>
        const int DefaultShapeCapacity = 32;

        /// <summary>
        /// Stores the default maximum number of broadphase candidates in one step.
        /// </summary>
        const int DefaultCandidatePairCapacity = 128;

        /// <summary>
        /// Stores the default power-of-two persistent manifold table size.
        /// </summary>
        const int DefaultManifoldCapacity = 64;

        /// <summary>
        /// Stores the default maximum number of active contact points in one step.
        /// </summary>
        const int DefaultContactPointCapacity = 256;

        /// <summary>
        /// Stores the default maximum number of simultaneously published dynamic islands.
        /// </summary>
        const int DefaultIslandCapacity = 32;

        /// <summary>
        /// Stores the default number of deferred world mutations retained before a step.
        /// </summary>
        const int DefaultDeferredCommandCapacity = 128;

        /// <summary>
        /// Stores the default number of sequential velocity iterations for active contacts.
        /// </summary>
        const int DefaultVelocityIterationCount = 4;

        /// <summary>
        /// Stores the default number of split positional-correction passes.
        /// </summary>
        const int DefaultPenetrationCorrectionPassCount = 1;

        /// <summary>
        /// Stores the exact default fixed step of one twentieth of a second.
        /// </summary>
        const double DefaultFixedStepSeconds = 1d / 20d;

        /// <summary>
        /// Initializes the exact console-first capacity and solve profile with conventional downward gravity.
        /// </summary>
        public HelPhysicsWorldSettings3D()
            : this(
                DefaultBodyCapacity,
                DefaultShapeCapacity,
                DefaultCandidatePairCapacity,
                DefaultManifoldCapacity,
                DefaultContactPointCapacity,
                DefaultIslandCapacity,
                DefaultDeferredCommandCapacity,
                DefaultVelocityIterationCount,
                DefaultPenetrationCorrectionPassCount,
                DefaultFixedStepSeconds,
                new PhysicsVector3(0f, -9.81f, 0f)) {
        }

        /// <summary>
        /// Initializes a completely explicit fixed world profile after validating every allocation and solve boundary.
        /// </summary>
        /// <param name="bodyCapacity">Addressable body-slot count from one through 65,534.</param>
        /// <param name="shapeCapacity">Addressable box-shape slot count from one through 65,534.</param>
        /// <param name="candidatePairCapacity">Positive maximum broadphase candidate count.</param>
        /// <param name="manifoldCapacity">Positive power-of-two persistent manifold count.</param>
        /// <param name="contactPointCapacity">Positive maximum active contact-point count.</param>
        /// <param name="islandCapacity">Positive island count no greater than body capacity.</param>
        /// <param name="deferredCommandCapacity">Positive deferred mutation count.</param>
        /// <param name="velocityIterationCount">Positive sequential velocity iteration count.</param>
        /// <param name="penetrationCorrectionPassCount">Positive split positional-correction pass count.</param>
        /// <param name="fixedStepSeconds">Positive finite public fixed step representable by <see cref="PhysicsScalar"/>.</param>
        /// <param name="gravity">Explicit finite world-space gravitational acceleration.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when a capacity, work count, or fixed step is invalid.</exception>
        public HelPhysicsWorldSettings3D(
            int bodyCapacity,
            int shapeCapacity,
            int candidatePairCapacity,
            int manifoldCapacity,
            int contactPointCapacity,
            int islandCapacity,
            int deferredCommandCapacity,
            int velocityIterationCount,
            int penetrationCorrectionPassCount,
            double fixedStepSeconds,
            PhysicsVector3 gravity) {
            ValidateHandleCapacity(bodyCapacity, nameof(bodyCapacity));
            ValidateHandleCapacity(shapeCapacity, nameof(shapeCapacity));
            ValidatePositiveCapacity(candidatePairCapacity, nameof(candidatePairCapacity));
            ValidateManifoldCapacity(manifoldCapacity);
            ValidatePositiveCapacity(contactPointCapacity, nameof(contactPointCapacity));
            ValidatePositiveCapacity(islandCapacity, nameof(islandCapacity));
            ValidatePositiveCapacity(deferredCommandCapacity, nameof(deferredCommandCapacity));
            ValidatePositiveCapacity(velocityIterationCount, nameof(velocityIterationCount));
            ValidatePositiveCapacity(penetrationCorrectionPassCount, nameof(penetrationCorrectionPassCount));
            if (islandCapacity > bodyCapacity) {
                throw new ArgumentOutOfRangeException(nameof(islandCapacity), "Island capacity cannot exceed fixed body capacity.");
            }

            float scalarStepSeconds = (float)fixedStepSeconds;
            if (double.IsNaN(fixedStepSeconds) ||
                double.IsInfinity(fixedStepSeconds) ||
                fixedStepSeconds <= 0d ||
                float.IsNaN(scalarStepSeconds) ||
                float.IsInfinity(scalarStepSeconds) ||
                scalarStepSeconds <= 0f) {
                throw new ArgumentOutOfRangeException(nameof(fixedStepSeconds), "The fixed physics step must be positive, finite, and representable by the physics scalar.");
            }

            BodyCapacity = bodyCapacity;
            ShapeCapacity = shapeCapacity;
            CandidatePairCapacity = candidatePairCapacity;
            ManifoldCapacity = manifoldCapacity;
            ContactPointCapacity = contactPointCapacity;
            IslandCapacity = islandCapacity;
            DeferredCommandCapacity = deferredCommandCapacity;
            VelocityIterationCount = velocityIterationCount;
            PenetrationCorrectionPassCount = penetrationCorrectionPassCount;
            FixedStepSeconds = fixedStepSeconds;
            Gravity = gravity;
        }

        /// <summary>
        /// Gets the fixed number of body slots allocated by the world.
        /// </summary>
        public int BodyCapacity { get; }

        /// <summary>
        /// Gets the fixed number of independently generated box-shape slots.
        /// </summary>
        public int ShapeCapacity { get; }

        /// <summary>
        /// Gets the maximum number of broadphase candidate pairs one step may publish.
        /// </summary>
        public int CandidatePairCapacity { get; }

        /// <summary>
        /// Gets the power-of-two capacity of the persistent manifold table and active manifold arrays.
        /// </summary>
        public int ManifoldCapacity { get; }

        /// <summary>
        /// Gets the maximum number of active contact points one step may prepare for solving.
        /// </summary>
        public int ContactPointCapacity { get; }

        /// <summary>
        /// Gets the maximum number of dynamic islands one successful build may publish.
        /// </summary>
        public int IslandCapacity { get; }

        /// <summary>
        /// Gets the number of gameplay mutations that may wait for the next fixed step.
        /// </summary>
        public int DeferredCommandCapacity { get; }

        /// <summary>
        /// Gets the configured sequential velocity iteration count for steps with active contacts.
        /// </summary>
        public int VelocityIterationCount { get; }

        /// <summary>
        /// Gets the configured split positional-correction pass count for steps with active contacts.
        /// </summary>
        public int PenetrationCorrectionPassCount { get; }

        /// <summary>
        /// Gets the exact public double value accepted by <see cref="HelPhysicsWorld3D.Step(double)"/>.
        /// </summary>
        public double FixedStepSeconds { get; }

        /// <summary>
        /// Gets the explicit world-space gravitational acceleration applied to awake dynamics.
        /// </summary>
        public PhysicsVector3 Gravity { get; }

        /// <summary>
        /// Validates a body or shape capacity against the reserved invalid handle index.
        /// </summary>
        /// <param name="capacity">Requested handle-addressable slot count.</param>
        /// <param name="parameterName">Constructor parameter name used by diagnostics.</param>
        static void ValidateHandleCapacity(int capacity, string parameterName) {
            if (capacity < 1 || capacity > 65534) {
                throw new ArgumentOutOfRangeException(parameterName, "Body and shape capacities must be between 1 and 65,534 inclusive.");
            }
        }

        /// <summary>
        /// Validates one allocation or work count that must contain at least one fixed slot or pass.
        /// </summary>
        /// <param name="capacity">Requested positive count.</param>
        /// <param name="parameterName">Constructor parameter name used by diagnostics.</param>
        static void ValidatePositiveCapacity(int capacity, string parameterName) {
            if (capacity <= 0) {
                throw new ArgumentOutOfRangeException(parameterName, "Physics capacities and solve counts must be positive.");
            }
        }

        /// <summary>
        /// Validates the manifold table size required by deterministic bit-mask probing.
        /// </summary>
        /// <param name="manifoldCapacity">Requested persistent table size.</param>
        static void ValidateManifoldCapacity(int manifoldCapacity) {
            if (manifoldCapacity <= 0 || (manifoldCapacity & (manifoldCapacity - 1)) != 0) {
                throw new ArgumentOutOfRangeException(nameof(manifoldCapacity), "Manifold capacity must be a positive power of two.");
            }
        }
    }
}
