namespace helengine.bepu.tests {
    /// <summary>
    /// Verifies the BEPU-backed runtime registration path lazily attaches the expected physics runtime.
    /// </summary>
    public sealed class BepuRuntimeComponentRegistrationTests {
        /// <summary>
        /// Ensures the cached registration state returned from the core-owned slot is borrowed by native callers.
        /// </summary>
        [Fact]
        public void GetRegistrationState_ReturnValue_IsDeclaredBorrowed() {
            System.Reflection.MethodInfo method = typeof(BepuRuntimeComponentRegistration).GetMethod(
                "GetRegistrationState",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);

            Assert.NotNull(method);
            Assert.NotEmpty(method.GetCustomAttributes(typeof(NativeBorrowedReturnAttribute), false));
        }

        /// <summary>
        /// Ensures registration defers BEPU-backed runtime attachment until one physics scene is loaded.
        /// </summary>
        [Fact]
        public void Register_WhenCalled_DoesNotAttachBepuPhysicsRuntimeUntilPhysicsSceneLoads() {
            Core core = new Core(new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(AppContext.BaseDirectory)
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
                ContentStreamSource = new HostFileSystemContentStreamSource(AppContext.BaseDirectory)
            });
            core.Initialize(null, null, null, new PlatformInfo("test", "test-version"));

            BepuRuntimeComponentRegistration.Register(core);
            BepuRuntimeComponentRegistration.HandleLoadedScene(core, [CreateNonPhysicsEntity(core)]);

            Assert.Null(core.PhysicsRuntime);
        }

        /// <summary>
        /// Ensures one physics scene lazily creates and attaches the default BEPU-backed world.
        /// </summary>
        [Fact]
    public void HandleLoadedScene_WhenSceneHasPhysics_AttachesDefaultSolveScheduleWorld() {
            Core core = new Core(new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(AppContext.BaseDirectory)
            });
            core.Initialize(null, null, null, new PlatformInfo("test", "test-version"));

            BepuRuntimeComponentRegistration.Register(core);
            BepuRuntimeComponentRegistration.HandleLoadedScene(core, [CreateStaticBoxPhysicsEntity(core)]);

            BepuPhysicsWorld3D world = Assert.IsType<BepuPhysicsWorld3D>(core.PhysicsRuntime);
            Assert.Equal(4, world.SolveVelocityIterationCount);
            Assert.Equal(1, world.SolveSubstepCount);
            Assert.Equal(1, world.RegisteredBodyCount);
        Assert.Equal("AfterBepuSceneBinding", core.LastSceneTransitionStage);
    }

    /// <summary>
    /// Ensures an explicit runtime registration solve schedule is preserved when the first physics scene loads.
    /// </summary>
    [Fact]
    public void HandleLoadedScene_WhenRegistrationSpecifiesSolveSchedule_AttachesConfiguredWorld() {
        Core core = new Core(new CoreInitializationOptions {
            ContentStreamSource = new HostFileSystemContentStreamSource(AppContext.BaseDirectory)
        });
        core.Initialize(null, null, null, new PlatformInfo("test", "test-version"));

        BepuRuntimeComponentRegistration.Register(core, 1, 1);
        BepuRuntimeComponentRegistration.HandleLoadedScene(core, [CreateStaticBoxPhysicsEntity(core)]);

        BepuPhysicsWorld3D world = Assert.IsType<BepuPhysicsWorld3D>(core.PhysicsRuntime);
        Assert.Equal(1, world.SolveVelocityIterationCount);
        Assert.Equal(1, world.SolveSubstepCount);
    }

    /// <summary>
    /// Ensures one stepped BEPU world exposes non-negative profiler timing samples and its awake-body count.
    /// </summary>
    [Fact]
    public void Step_WhenPhysicsWorldAdvances_RecordsProfilerBreakdown() {
        BepuPhysicsWorld3D world = BepuPhysicsWorld3D.CreateWithSolveSchedule(1, 1);

        world.Step(1.0d / 20.0d);

        Assert.True(world.LastTimestepMilliseconds >= 0d);
        Assert.True(world.LastEntitySynchronizationMilliseconds >= 0d);
        Assert.True(world.LastTriggerCollectionMilliseconds >= 0d);
        Assert.Equal(0, world.AwakeDynamicBodyCount);
    }

        /// <summary>
        /// Ensures one non-physics scene detaches the lazy runtime after one physics scene was previously loaded.
        /// </summary>
        [Fact]
        public void HandleLoadedScene_WhenPhysicsSceneIsFollowedByNonPhysicsScene_DetachesRuntime() {
            Core core = new Core(new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(AppContext.BaseDirectory)
            });
            core.Initialize(null, null, null, new PlatformInfo("test", "test-version"));

            BepuRuntimeComponentRegistration.Register(core);
        BepuRuntimeComponentRegistration.HandleLoadedScene(core, [CreateStaticBoxPhysicsEntity(core)]);
            Assert.IsType<BepuPhysicsWorld3D>(core.PhysicsRuntime);

        BepuRuntimeComponentRegistration.HandleLoadedScene(core, [CreateNonPhysicsEntity(core)]);

            Assert.Null(core.PhysicsRuntime);
        }

        /// <summary>
        /// Ensures a BEPU world detaches before a physics scene releases its entity hierarchy.
        /// </summary>
        [Fact]
        public void HandleUnloadingScene_WhenSceneHasPhysics_DetachesRuntimeBeforeEntityDisposal() {
            Core core = new Core(new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(AppContext.BaseDirectory)
            });
            core.Initialize(null, null, null, new PlatformInfo("test", "test-version"));

            BepuRuntimeComponentRegistration.Register(core);
            Entity physicsEntity = CreateStaticBoxPhysicsEntity(core);
            BepuRuntimeComponentRegistration.HandleLoadedScene(core, [physicsEntity]);
            Assert.IsType<BepuPhysicsWorld3D>(core.PhysicsRuntime);

            BepuRuntimeComponentRegistration.HandleUnloadingScene(core, [physicsEntity]);

            Assert.Null(core.PhysicsRuntime);
        }

        /// <summary>
        /// Ensures simultaneous runtime cores keep independent BEPU registration state even after the second core becomes ambient.
        /// </summary>
        [Fact]
        public void HandleLoadedScene_WithTwoCores_KeepsWorldsAndCallbacksBoundToTheirOwningCore() {
            Core firstCore = CreateInitializedCore();
            Core secondCore = CreateInitializedCore();
            try {
                BepuRuntimeComponentRegistration.Register(firstCore);
                BepuRuntimeComponentRegistration.Register(secondCore);

                Entity firstEntity = CreateStaticBoxPhysicsEntity(firstCore);
                Entity secondEntity = CreateStaticBoxPhysicsEntity(secondCore);
                BepuRuntimeComponentRegistration.HandleLoadedScene(firstCore, [firstEntity]);
                BepuRuntimeComponentRegistration.HandleLoadedScene(secondCore, [secondEntity]);

                BepuPhysicsWorld3D firstWorld = Assert.IsType<BepuPhysicsWorld3D>(firstCore.PhysicsRuntime);
                BepuPhysicsWorld3D secondWorld = Assert.IsType<BepuPhysicsWorld3D>(secondCore.PhysicsRuntime);
                Assert.NotSame(firstWorld, secondWorld);

                secondCore.Dispose();
                BepuRuntimeComponentRegistration.HandleLoadedScene(firstCore, [firstEntity]);

                Assert.Same(firstWorld, firstCore.PhysicsRuntime);
                Assert.Equal(1, firstWorld.RegisteredBodyCount);
            } finally {
                firstCore.Dispose();
                secondCore.Dispose();
            }
        }

        static Core CreateInitializedCore() {
            Core core = new Core(new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(AppContext.BaseDirectory)
            });
            core.Initialize(null, null, null, new PlatformInfo("test", "test-version"));
            return core;
        }

        /// <summary>
        /// Creates one root entity without any authored physics components.
        /// </summary>
        /// <returns>Entity that should not require physics runtime attachment.</returns>
        static Entity CreateNonPhysicsEntity(Core ownerCore) {
            Entity entity = new Entity(ownerCore);
            entity.InitComponents();
            return entity;
        }

        /// <summary>
        /// Creates one static box body that requires the BEPU-backed runtime to bind the scene.
        /// </summary>
        /// <returns>Entity that should trigger lazy physics runtime attachment.</returns>
        static Entity CreateStaticBoxPhysicsEntity(Core ownerCore) {
            Entity entity = new Entity(ownerCore);
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

