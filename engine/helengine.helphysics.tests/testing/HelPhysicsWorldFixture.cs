namespace helengine {
    /// <summary>
    /// Builds the canonical console-first ground and four-box stack used by world behavior and allocation tests.
    /// </summary>
    public sealed class HelPhysicsWorldFixture {
        /// <summary>
        /// Stores the exact twenty-hertz fixed step configured by the fixture world.
        /// </summary>
        public const double StepSeconds = 1d / 20d;

        /// <summary>
        /// Stores the explicitly authored five-tick quiet duration, equal to one quarter second at twenty hertz.
        /// </summary>
        public const ushort SleepTicks = 5;

        /// <summary>
        /// Initializes a fixture from its world and the handles returned for every reserved body.
        /// </summary>
        /// <param name="world">World containing the pending fixture bodies.</param>
        /// <param name="ground">Pending static ground handle.</param>
        /// <param name="dynamicBoxes">Four pending dynamic unit-box handles ordered from bottom to top.</param>
        HelPhysicsWorldFixture(
            HelPhysicsWorld3D world,
            HelPhysicsBodyHandle3D ground,
            HelPhysicsBodyHandle3D[] dynamicBoxes) {
            World = world;
            Ground = ground;
            DynamicBoxes = dynamicBoxes;
        }

        /// <summary>
        /// Gets the deterministic fixed-capacity world owned by this fixture.
        /// </summary>
        public HelPhysicsWorld3D World { get; }

        /// <summary>
        /// Gets the static ground body reserved before the first fixed step.
        /// </summary>
        public HelPhysicsBodyHandle3D Ground { get; }

        /// <summary>
        /// Gets the four dynamic unit boxes in authored bottom-to-top order.
        /// </summary>
        public HelPhysicsBodyHandle3D[] DynamicBoxes { get; }

        /// <summary>
        /// Creates one static ground and four exactly face-touching unit boxes with explicit aggressive sleep settings.
        /// </summary>
        /// <returns>A fixture whose deferred body creations become active at the beginning of its first step.</returns>
        public static HelPhysicsWorldFixture CreateFourBoxStack() {
            HelPhysicsWorldSettings3D settings = new HelPhysicsWorldSettings3D(
                32,
                32,
                128,
                64,
                256,
                32,
                128,
                4,
                1,
                StepSeconds,
                new PhysicsVector3(0f, -9.81f, 0f));
            HelPhysicsWorld3D world = new HelPhysicsWorld3D(settings);
            HelPhysicsBodyHandle3D ground = world.CreateBody(CreateGroundDescription());
            HelPhysicsBodyHandle3D[] dynamicBoxes = new HelPhysicsBodyHandle3D[4];
            for (int boxIndex = 0; boxIndex < dynamicBoxes.Length; boxIndex++) {
                dynamicBoxes[boxIndex] = world.CreateBody(CreateDynamicUnitBoxDescription(
                    new PhysicsVector3(0f, 0.5f + boxIndex, 0f),
                    boxIndex + 1,
                    true));
            }

            return new HelPhysicsWorldFixture(world, ground, dynamicBoxes);
        }

        /// <summary>
        /// Creates the explicit static ground description shared by stack tests.
        /// </summary>
        /// <returns>A ten-by-one-by-ten immovable box whose top face lies at world Y zero.</returns>
        public static HelPhysicsBodyDescription3D CreateGroundDescription() {
            return new HelPhysicsBodyDescription3D(
                new HelPhysicsBoxShape3D(new PhysicsVector3(5f, 0.5f, 5f)),
                BodyKind3D.Static,
                new PhysicsVector3(0f, -0.5f, 0f),
                PhysicsQuaternion.Identity,
                PhysicsVector3.Zero,
                PhysicsVector3.Zero,
                PhysicsScalar.Zero,
                CreateStackMaterial(),
                1,
                ushort.MaxValue,
                0,
                PhysicsScalar.Zero,
                PhysicsScalar.Zero,
                PhysicsScalar.Zero,
                PhysicsScalar.FromFloat(0.2f),
                PhysicsScalar.FromFloat(0.2f),
                SleepTicks,
                false);
        }

        /// <summary>
        /// Creates an explicitly configured unit-mass dynamic unit box for world tests.
        /// </summary>
        /// <param name="position">Authored world-space box center.</param>
        /// <param name="entityBindingId">Stable test ownership identifier stored in cold state.</param>
        /// <param name="isAwake">Whether the new dynamic begins eligible for integration.</param>
        /// <returns>A complete body description with no inferred authoring values.</returns>
        public static HelPhysicsBodyDescription3D CreateDynamicUnitBoxDescription(
            PhysicsVector3 position,
            int entityBindingId,
            bool isAwake) {
            return new HelPhysicsBodyDescription3D(
                new HelPhysicsBoxShape3D(new PhysicsVector3(0.5f, 0.5f, 0.5f)),
                BodyKind3D.Dynamic,
                position,
                PhysicsQuaternion.Identity,
                PhysicsVector3.Zero,
                PhysicsVector3.Zero,
                PhysicsScalar.One,
                CreateStackMaterial(),
                1,
                ushort.MaxValue,
                entityBindingId,
                PhysicsScalar.One,
                PhysicsScalar.FromFloat(0.1f),
                PhysicsScalar.FromFloat(0.1f),
                PhysicsScalar.FromFloat(0.2f),
                PhysicsScalar.FromFloat(0.2f),
                SleepTicks,
                isAwake);
        }

        /// <summary>
        /// Creates the explicitly authored non-bouncing material used by the ground and all stack boxes.
        /// </summary>
        /// <returns>A stable high-friction material with zero restitution.</returns>
        public static HelPhysicsMaterial3D CreateStackMaterial() {
            return new HelPhysicsMaterial3D(
                PhysicsScalar.FromFloat(0.8f),
                PhysicsScalar.FromFloat(0.6f),
                PhysicsScalar.Zero);
        }
    }
}
