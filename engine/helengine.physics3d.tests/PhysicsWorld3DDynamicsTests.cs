namespace helengine.physics3d.tests {
    /// <summary>
    /// Verifies the active cube-only 3D physics world behavior.
    /// </summary>
    [Collection(Physics3DTestCollection.Name)]
    public sealed class PhysicsWorld3DDynamicsTests : IDisposable {
        /// <summary>
        /// Initializes the minimal core services required for entity-backed physics tests.
        /// </summary>
        public PhysicsWorld3DDynamicsTests() {
            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = AppContext.BaseDirectory
            });
            core.Initialize(null, null, null, new PlatformInfo("test", "test-version"));
        }

        /// <summary>
        /// Leaves the active core singleton attached after each test.
        /// </summary>
        public void Dispose() {
        }

        /// <summary>
        /// Ensures unsupported collider types fail during binding instead of silently entering the cube runtime.
        /// </summary>
        [Fact]
        public void BindScene_WithUnsupportedSphereCollider_ThrowsNotSupportedException() {
            Entity sphere = CreateEntity(new float3(0f, 2f, 0f));
            sphere.AddComponent(new RigidBody3DComponent());
            sphere.AddComponent(new SphereCollider3DComponent());
            PhysicsWorld3D world = PhysicsWorld3D.CreateMediumDefault();

            Assert.Throws<NotSupportedException>(() => world.BindScene(new[] { sphere }));
        }

        /// <summary>
        /// Ensures a dynamic cube falls onto a static cube ground and settles without residual vertical drift.
        /// </summary>
        [Fact]
        public void Step_WithDynamicBoxAboveStaticGround_FallsAndResolvesGroundContact() {
            Entity ground = CreateBoxEntity(new float3(0f, 0f, 0f), new float3(4f, 1f, 4f), BodyKind3D.Static);
            Entity cube = CreateBoxEntity(new float3(0f, 3f, 0f), new float3(1f, 1f, 1f), BodyKind3D.Dynamic);
            PhysicsWorld3D world = PhysicsWorld3D.CreateMediumDefault();
            world.BindScene(new[] { cube, ground });

            StepWorld(world, 180);

            Assert.InRange(cube.LocalPosition.Y, 0.99f, 1.02f);
            Assert.InRange(cube.Components.OfType<RigidBody3DComponent>().Single().LinearVelocity.Y, -0.0001f, 0.0001f);
        }

        /// <summary>
        /// Ensures a fast cube crossing the ground is projected back onto the cube surface in the same step.
        /// </summary>
        [Fact]
        public void Step_WithFastBoxCrossingGroundContact_ResolvesContactInSameStep() {
            Entity ground = CreateBoxEntity(new float3(0f, 0f, 0f), new float3(4f, 1f, 4f), BodyKind3D.Static);
            Entity cube = CreateBoxEntity(new float3(0f, 1.25f, 0f), new float3(1f, 1f, 1f), BodyKind3D.Dynamic);
            cube.Components.OfType<RigidBody3DComponent>().Single().LinearVelocity = new float3(0f, -30f, 0f);
            PhysicsWorld3D world = PhysicsWorld3D.CreateMediumDefault();
            world.BindScene(new[] { cube, ground });

            world.Step(1d / 60d);

            Assert.InRange(cube.LocalPosition.Y, 0.99f, 1.02f);
            Assert.InRange(cube.Components.OfType<RigidBody3DComponent>().Single().LinearVelocity.Y, -0.0001f, 0.0001f);
        }

        /// <summary>
        /// Ensures an offset upper cube loading a grounded cube does not launch the lower cube upward.
        /// </summary>
        [Fact]
        public void Step_WithOffsetUpperBoxLoadingGroundedBox_DoesNotLaunchLowerBoxUpward() {
            Entity ground = CreateBoxEntity(new float3(0f, 0f, 0f), new float3(6f, 1f, 6f), BodyKind3D.Static);
            Entity lower = CreateBoxEntity(new float3(0f, 1f, 0f), new float3(1f, 1f, 1f), BodyKind3D.Dynamic);
            Entity upper = CreateBoxEntity(new float3(0.45f, 2.02f, 0.1f), new float3(1f, 1f, 1f), BodyKind3D.Dynamic);
            PhysicsWorld3D world = PhysicsWorld3D.CreateMediumDefault();
            world.BindScene(new[] { lower, upper, ground });

            StepWorld(world, 180);

            Assert.InRange(lower.LocalPosition.Y, 0.99f, 1.03f);
            Assert.InRange(lower.Components.OfType<RigidBody3DComponent>().Single().LinearVelocity.Y, -0.0001f, 0.0001f);
            Assert.True(upper.LocalPosition.Y > lower.LocalPosition.Y);
        }

        /// <summary>
        /// Ensures collision layers and masks still remove cube pairs from contact resolution.
        /// </summary>
        [Fact]
        public void Step_WithNonMatchingCollisionMasks_IgnoresOverlappingPair() {
            Entity first = CreateBoxEntity(new float3(-0.2f, 0f, 0f), new float3(1f, 1f, 1f), BodyKind3D.Dynamic);
            Entity second = CreateBoxEntity(new float3(0.2f, 0f, 0f), new float3(1f, 1f, 1f), BodyKind3D.Dynamic);
            first.Components.OfType<BoxCollider3DComponent>().Single().CollisionLayer = 0b0001;
            first.Components.OfType<BoxCollider3DComponent>().Single().CollisionMask = 0b0001;
            second.Components.OfType<BoxCollider3DComponent>().Single().CollisionLayer = 0b0010;
            second.Components.OfType<BoxCollider3DComponent>().Single().CollisionMask = 0b0010;
            PhysicsWorld3D world = PhysicsWorld3D.CreateMediumDefault();
            world.BindScene(new[] { first, second });

            world.Step(1d / 60d);

            Assert.InRange(first.LocalPosition.X, -0.21f, -0.19f);
            Assert.InRange(second.LocalPosition.X, 0.19f, 0.21f);
        }

        /// <summary>
        /// Advances the supplied world by a fixed number of frames.
        /// </summary>
        /// <param name="world">Physics world to advance.</param>
        /// <param name="stepCount">Number of fixed steps to execute.</param>
        static void StepWorld(PhysicsWorld3D world, int stepCount) {
            for (int index = 0; index < stepCount; index++) {
                world.Step(1d / 60d);
            }
        }

        /// <summary>
        /// Creates a box entity with rigid body and collider components.
        /// </summary>
        /// <param name="position">Initial entity local position.</param>
        /// <param name="size">Full authored box size.</param>
        /// <param name="bodyKind">Rigid body simulation kind.</param>
        /// <returns>Initialized entity.</returns>
        static Entity CreateBoxEntity(float3 position, float3 size, BodyKind3D bodyKind) {
            Entity entity = CreateEntity(position);
            entity.AddComponent(new RigidBody3DComponent {
                BodyKind = bodyKind
            });
            entity.AddComponent(new BoxCollider3DComponent {
                Size = size
            });
            return entity;
        }

        /// <summary>
        /// Creates one initialized entity at the supplied local position.
        /// </summary>
        /// <param name="position">Initial local position.</param>
        /// <returns>Initialized entity.</returns>
        static Entity CreateEntity(float3 position) {
            Entity entity = new Entity();
            entity.InitComponents();
            entity.InitChildren();
            entity.LocalPosition = position;
            return entity;
        }
    }
}
