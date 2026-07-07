namespace helengine.physics3d.tests {
    /// <summary>
    /// Verifies fixed-step host scheduling preserves BEPU scene progression after intermittent long frames.
    /// </summary>
    [Collection(Physics3DTestCollection.Name)]
    public sealed class PhysicsWorld3DFixedStepSchedulingTests {
        /// <summary>
        /// Initializes the minimal core services required for entity-backed physics tests.
        /// </summary>
        public PhysicsWorld3DFixedStepSchedulingTests() {
            Core core = new Core(new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(AppContext.BaseDirectory)
            });
            core.Initialize(null, null, null, new PlatformInfo("test", "test-version"));
        }

        /// <summary>
        /// Ensures one temporary host-frame hitch does not permanently leave the authored city box tower behind direct BEPU stepping.
        /// </summary>
        [Fact]
        public void Update_WithCityEightBoxTowerAndTemporaryHostHitch_CatchesBackUpToDirectBepuStepping() {
            Entity[] directEntities = CreateCityEightBoxTowerEntities();
            Entity[] coreDrivenEntities = CreateCityEightBoxTowerEntities();
            PhysicsWorld3D directWorld = PhysicsWorld3D.CreateMediumDefault();
            PhysicsWorld3D coreDrivenWorld = PhysicsWorld3D.CreateMediumDefault();
            directWorld.BindScene(directEntities);
            coreDrivenWorld.BindScene(coreDrivenEntities);

            for (int stepIndex = 0; stepIndex < 120; stepIndex++) {
                directWorld.Step(1.0d / 60.0d);
            }

            Core core = CreateCoreWithAttachedPhysicsRuntime(coreDrivenWorld, 8);
            try {
                core.Update(0.5d);
                for (int updateIndex = 0; updateIndex < 90; updateIndex++) {
                    core.Update(1.0d / 60.0d);
                }

                Assert.InRange(core.PhysicsScheduler.AccumulatedSeconds, 0d, (1.0d / 60.0d) - 0.0000001d);
                AssertTowerMatchesDirectStepping(directEntities, coreDrivenEntities, 0.05f);
            } finally {
                core.Dispose();
            }
        }

        /// <summary>
        /// Creates one initialized core instance attached to the supplied BEPU world.
        /// </summary>
        /// <param name="world">Physics world that should receive fixed-step updates.</param>
        /// <param name="physicsMaxStepsPerUpdate">Maximum number of fixed steps allowed per core update.</param>
        /// <returns>Initialized core instance with the physics runtime attached.</returns>
        static Core CreateCoreWithAttachedPhysicsRuntime(PhysicsWorld3D world, int physicsMaxStepsPerUpdate) {
            if (world == null) {
                throw new ArgumentNullException(nameof(world));
            }

            Core core = new Core(new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(AppContext.BaseDirectory),
                PhysicsFixedStepSeconds = 1.0d / 60.0d,
                PhysicsMaxStepsPerUpdate = physicsMaxStepsPerUpdate
            });
            core.Initialize(null, null, null, new PlatformInfo("test", "test-version"));
            core.AttachPhysicsRuntime(world);
            return core;
        }

        /// <summary>
        /// Creates the authored city-style collapsing box tower used to reproduce visible fixed-step lag.
        /// </summary>
        /// <returns>Initialized root entities containing one static ground body and eight dynamic boxes.</returns>
        static Entity[] CreateCityEightBoxTowerEntities() {
            Entity groundEntity = CreateEntity(new float3(0f, -0.5f, 0f));
            groundEntity.AddComponent(new RigidBody3DComponent {
                BodyKind = BodyKind3D.Static,
                UseGravity = false
            });
            groundEntity.AddComponent(new BoxCollider3DComponent {
                Size = new float3(18f, 1f, 18f),
                StaticFriction = 1d,
                DynamicFriction = 1d
            });

            Entity[] boxes = new[] {
                CreateDynamicBoxEntity(new float3(0f, 1f, 0f)),
                CreateDynamicBoxEntity(new float3(0.9f, 3f, 0f)),
                CreateDynamicBoxEntity(new float3(-0.45f, 5f, 0f)),
                CreateDynamicBoxEntity(new float3(0.45f, 7f, 0f)),
                CreateDynamicBoxEntity(new float3(-0.25f, 9f, 0f)),
                CreateDynamicBoxEntity(new float3(0.25f, 11f, 0f)),
                CreateDynamicBoxEntity(new float3(-0.1f, 13f, 0f)),
                CreateDynamicBoxEntity(new float3(0.1f, 15f, 0f))
            };

            for (int index = 0; index < boxes.Length; index++) {
                SetBoxFriction(boxes[index], 1d, 1d);
            }

            Entity[] rootEntities = new Entity[boxes.Length + 1];
            rootEntities[0] = groundEntity;
            for (int index = 0; index < boxes.Length; index++) {
                rootEntities[index + 1] = boxes[index];
            }

            return rootEntities;
        }

        /// <summary>
        /// Ensures every dynamic box remains close to the direct fixed-step reference result.
        /// </summary>
        /// <param name="expectedEntities">Entities advanced by direct BEPU stepping.</param>
        /// <param name="actualEntities">Entities advanced through the core scheduler.</param>
        /// <param name="maximumDistance">Maximum tolerated position error in world units.</param>
        static void AssertTowerMatchesDirectStepping(Entity[] expectedEntities, Entity[] actualEntities, float maximumDistance) {
            if (expectedEntities == null) {
                throw new ArgumentNullException(nameof(expectedEntities));
            }
            if (actualEntities == null) {
                throw new ArgumentNullException(nameof(actualEntities));
            }

            double maximumDistanceSquared = maximumDistance * maximumDistance;
            for (int index = 1; index < expectedEntities.Length; index++) {
                float3 expectedPosition = expectedEntities[index].LocalPosition;
                float3 actualPosition = actualEntities[index].LocalPosition;
                double positionErrorSquared =
                    ((double)expectedPosition.X - actualPosition.X) * ((double)expectedPosition.X - actualPosition.X) +
                    ((double)expectedPosition.Y - actualPosition.Y) * ((double)expectedPosition.Y - actualPosition.Y) +
                    ((double)expectedPosition.Z - actualPosition.Z) * ((double)expectedPosition.Z - actualPosition.Z);
                Assert.True(
                    positionErrorSquared <= maximumDistanceSquared,
                    $"Expected box {index} to stay near the direct BEPU reference after hitch recovery, but expected {expectedPosition}, actual {actualPosition}, squared error {positionErrorSquared}.");
            }
        }

        /// <summary>
        /// Creates one initialized entity suitable for scene-free physics tests.
        /// </summary>
        /// <param name="localPosition">Initial local position.</param>
        /// <returns>Initialized entity.</returns>
        static Entity CreateEntity(float3 localPosition) {
            Entity entity = new Entity {
                LocalPosition = localPosition,
                LocalScale = float3.One,
                LocalOrientation = float4.Identity
            };
            entity.InitComponents();
            entity.InitChildren();
            return entity;
        }

        /// <summary>
        /// Creates one initialized dynamic unit box suitable for stack-settling physics tests.
        /// </summary>
        /// <param name="localPosition">Initial local position.</param>
        /// <returns>Initialized dynamic box entity.</returns>
        static Entity CreateDynamicBoxEntity(float3 localPosition) {
            Entity entity = CreateEntity(localPosition);
            entity.AddComponent(new RigidBody3DComponent {
                BodyKind = BodyKind3D.Dynamic,
                UseGravity = true,
                Mass = 1d
            });
            entity.AddComponent(new BoxCollider3DComponent {
                Size = new float3(1f, 1f, 1f)
            });
            return entity;
        }

        /// <summary>
        /// Applies explicit friction values to a dynamic box test entity so scene-specific material behavior is not hidden behind defaults.
        /// </summary>
        /// <param name="entity">Entity containing the box collider.</param>
        /// <param name="staticFriction">Static friction coefficient to assign.</param>
        /// <param name="dynamicFriction">Dynamic friction coefficient to assign.</param>
        static void SetBoxFriction(Entity entity, double staticFriction, double dynamicFriction) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }

            for (int index = 0; index < entity.Components.Count; index++) {
                if (entity.Components[index] is BoxCollider3DComponent collider) {
                    collider.StaticFriction = staticFriction;
                    collider.DynamicFriction = dynamicFriction;
                    return;
                }
            }

            throw new InvalidOperationException("Entity does not contain a box collider component.");
        }
    }
}

