namespace helengine.bepu.tests {
    /// <summary>
    /// Verifies entity transforms and runtime body state remain synchronized.
    /// </summary>
    public sealed class BepuEntitySynchronization3DTests : IDisposable {
        /// <summary>
        /// Initializes the minimal core services required for entity-backed synchronization tests.
        /// </summary>
        public BepuEntitySynchronization3DTests() {
            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = AppContext.BaseDirectory
            });
            core.Initialize(null, null, null, new PlatformInfo("test", "test-version"));
        }

        /// <summary>
        /// Ends one synchronization test lifecycle.
        /// </summary>
        public void Dispose() {
        }

        /// <summary>
        /// Ensures runtime output is written back to the entity transform after a step.
        /// </summary>
        [Fact]
        public void Step_WithDynamicBody_UpdatesEntityPositionFromRuntimeState() {
            Entity entity = new Entity();
            entity.LocalPosition = new float3(0f, 3f, 0f);
            entity.InitComponents();
            entity.AddComponent(new RigidBody3DComponent {
                BodyKind = BodyKind3D.Dynamic,
                UseGravity = true,
                Mass = 1d
            });
            entity.AddComponent(new BoxCollider3DComponent {
                Size = new float3(1f, 1f, 1f)
            });

            BepuPhysicsWorld3D world = BepuPhysicsWorld3D.CreateDefault();
            world.BindScene(new[] { entity });
            world.Step(1d / 60d);

            Assert.True(entity.LocalPosition.Y < 3f);
        }
    }
}
