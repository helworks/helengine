namespace helengine.physics3d.tests {
    /// <summary>
    /// Verifies the hosted 3D physics world satisfies the scene-binding contract the core requires of every attached physics runtime.
    /// </summary>
    [Collection(Physics3DTestCollection.Name)]
    public sealed class PhysicsWorld3DCoreAttachmentTests : IDisposable {
        /// <summary>
        /// Core instance that owns the entities created by these tests.
        /// </summary>
        readonly Core CoreValue;

        /// <summary>
        /// Initializes the minimal core services required for entity-backed physics tests.
        /// </summary>
        public PhysicsWorld3DCoreAttachmentTests() {
            CoreValue = new Core(new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(AppContext.BaseDirectory),
                PhysicsFixedStepSeconds = 1d / 60d,
                PhysicsMaxStepsPerUpdate = 8
            });
            CoreValue.Initialize(null, null, null, new PlatformInfo("test", "test-version"));
        }

        /// <summary>
        /// Releases the core owned by this fixture.
        /// </summary>
        public void Dispose() {
            CoreValue.Dispose();
        }

        /// <summary>
        /// Ensures the shipped hosted physics world is still accepted by the core after the attach-time scene-binding requirement,
        /// so the helengine.physics3d backend keeps working for every host that attaches it.
        /// </summary>
        [Fact]
        public void AttachPhysicsRuntime_WithHostedPhysicsWorld_AttachesTheWorld() {
            PhysicsWorld3D world = PhysicsWorld3D.CreateMediumDefault();

            CoreValue.AttachPhysicsRuntime(world);

            Assert.Same(world, CoreValue.PhysicsRuntime);
            Assert.True(CoreValue.PhysicsRuntime is ISceneBindablePhysicsRuntime);
        }

        /// <summary>
        /// Ensures the hosted world reports every body registered by the most recent scene binding, covering rigid bodies,
        /// character controllers and cooked static meshes, and that rebinding replaces rather than accumulates the count.
        /// </summary>
        [Fact]
        public void RegisteredBodyCount_AfterBindingScene_ReportsEveryRegisteredBody() {
            PhysicsWorld3D world = PhysicsWorld3D.CreateMediumDefault();
            Entity[] rootEntities = new Entity[3];
            rootEntities[0] = CreateStaticGroundEntity();
            rootEntities[1] = CreateDynamicBoxEntity(new float3(0f, 2f, 0f));
            rootEntities[2] = CreateDynamicBoxEntity(new float3(0f, 4f, 0f));

            Assert.Equal(0, world.RegisteredBodyCount);

            world.BindScene(rootEntities);
            Assert.Equal(3, world.RegisteredBodyCount);

            Entity[] smallerScene = new Entity[1];
            smallerScene[0] = CreateStaticGroundEntity();
            world.BindScene(smallerScene);
            Assert.Equal(1, world.RegisteredBodyCount);
        }

        /// <summary>
        /// Creates one initialized entity suitable for scene-free physics tests.
        /// </summary>
        /// <param name="localPosition">Initial local position.</param>
        /// <returns>Initialized entity.</returns>
        Entity CreateEntity(float3 localPosition) {
            Entity entity = new Entity(CoreValue) {
                LocalPosition = localPosition,
                LocalScale = float3.One,
                LocalOrientation = float4.Identity
            };
            entity.InitComponents();
            entity.InitChildren();
            return entity;
        }

        /// <summary>
        /// Creates one initialized static ground body.
        /// </summary>
        /// <returns>Initialized static box entity.</returns>
        Entity CreateStaticGroundEntity() {
            Entity entity = CreateEntity(new float3(0f, -0.5f, 0f));
            entity.AddComponent(new RigidBody3DComponent {
                BodyKind = BodyKind3D.Static,
                UseGravity = false
            });
            entity.AddComponent(new BoxCollider3DComponent {
                Size = new float3(18f, 1f, 18f)
            });
            return entity;
        }

        /// <summary>
        /// Creates one initialized dynamic unit box.
        /// </summary>
        /// <param name="localPosition">Initial local position.</param>
        /// <returns>Initialized dynamic box entity.</returns>
        Entity CreateDynamicBoxEntity(float3 localPosition) {
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
    }
}
