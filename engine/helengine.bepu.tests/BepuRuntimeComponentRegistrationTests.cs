namespace helengine.bepu.tests {
    /// <summary>
    /// Verifies the BEPU-backed runtime registration path lazily attaches the expected physics runtime.
    /// </summary>
    public sealed class BepuRuntimeComponentRegistrationTests {
        /// <summary>
        /// Ensures registration defers BEPU-backed runtime attachment until one physics scene is loaded.
        /// </summary>
        [Fact]
        public void Register_WhenCalled_DoesNotAttachBepuPhysicsRuntimeUntilPhysicsSceneLoads() {
            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = AppContext.BaseDirectory
            });
            core.Initialize(null, null, null, new PlatformInfo("test", "test-version"));

            BepuRuntimeComponentRegistration.Register(core);

            Assert.Null(core.PhysicsRuntime);
        }

        /// <summary>
        /// Ensures one non-physics scene keeps the runtime detached after lazy registration.
        /// </summary>
        [Fact]
        public void HandleLoadedScene_WhenSceneHasNoPhysics_DoesNotAttachRuntime() {
            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = AppContext.BaseDirectory
            });
            core.Initialize(null, null, null, new PlatformInfo("test", "test-version"));

            BepuRuntimeComponentRegistration.Register(core);
            BepuRuntimeComponentRegistration.HandleLoadedScene(core, [CreateNonPhysicsEntity()]);

            Assert.Null(core.PhysicsRuntime);
        }

        /// <summary>
        /// Ensures one physics scene lazily creates and attaches the default BEPU-backed world.
        /// </summary>
        [Fact]
        public void HandleLoadedScene_WhenSceneHasPhysics_AttachesDefaultSolveScheduleWorld() {
            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = AppContext.BaseDirectory
            });
            core.Initialize(null, null, null, new PlatformInfo("test", "test-version"));

            BepuRuntimeComponentRegistration.Register(core);
            BepuRuntimeComponentRegistration.HandleLoadedScene(core, [CreateStaticBoxPhysicsEntity()]);

            BepuPhysicsWorld3D world = Assert.IsType<BepuPhysicsWorld3D>(core.PhysicsRuntime);
            Assert.Equal(4, world.SolveVelocityIterationCount);
            Assert.Equal(1, world.SolveSubstepCount);
            Assert.Equal(1, world.RegisteredBodyCount);
        }

        /// <summary>
        /// Ensures one non-physics scene detaches the lazy runtime after one physics scene was previously loaded.
        /// </summary>
        [Fact]
        public void HandleLoadedScene_WhenPhysicsSceneIsFollowedByNonPhysicsScene_DetachesRuntime() {
            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = AppContext.BaseDirectory
            });
            core.Initialize(null, null, null, new PlatformInfo("test", "test-version"));

            BepuRuntimeComponentRegistration.Register(core);
            BepuRuntimeComponentRegistration.HandleLoadedScene(core, [CreateStaticBoxPhysicsEntity()]);
            Assert.IsType<BepuPhysicsWorld3D>(core.PhysicsRuntime);

            BepuRuntimeComponentRegistration.HandleLoadedScene(core, [CreateNonPhysicsEntity()]);

            Assert.Null(core.PhysicsRuntime);
        }

        /// <summary>
        /// Creates one root entity without any authored physics components.
        /// </summary>
        /// <returns>Entity that should not require physics runtime attachment.</returns>
        static Entity CreateNonPhysicsEntity() {
            Entity entity = new Entity();
            entity.InitComponents();
            return entity;
        }

        /// <summary>
        /// Creates one static box body that requires the BEPU-backed runtime to bind the scene.
        /// </summary>
        /// <returns>Entity that should trigger lazy physics runtime attachment.</returns>
        static Entity CreateStaticBoxPhysicsEntity() {
            Entity entity = new Entity();
            entity.InitComponents();
            entity.AddComponent(new RigidBody3DComponent {
                BodyKind = BodyKind3D.Static,
                UseGravity = false
            });
            entity.AddComponent(new BoxCollider3DComponent {
                Size = new float3(1f, 1f, 1f)
            });
            return entity;
        }
    }
}
