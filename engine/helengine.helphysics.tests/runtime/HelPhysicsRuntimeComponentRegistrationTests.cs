using System.Reflection;

namespace helengine {
    /// <summary>
    /// Verifies HelPhysics runtime registration follows authored scene composition and owns lifecycle transitions safely.
    /// </summary>
    [Collection("HelPhysicsSceneBindingCoreTests")]
    public sealed class HelPhysicsRuntimeComponentRegistrationTests {
        /// <summary>Verifies the HelPhysics assembly exposes its generated runtime module contract.</summary>
        [Fact]
        public void HelPhysicsAssembly_DeclaresGeneratedRuntimeModuleManifest() {
            GeneratedRuntimeModuleManifestAttribute manifest = Assert.Single(
                typeof(HelPhysicsRuntimeComponentRegistration)
                    .Assembly
                    .GetCustomAttributes<GeneratedRuntimeModuleManifestAttribute>());

            Assert.Equal("helphysics-runtime-module", manifest.ModuleId);
            Assert.Equal(typeof(HelPhysicsRuntimeComponentRegistration), manifest.RegistrationType);
            Assert.Equal(nameof(HelPhysicsRuntimeComponentRegistration.Register), manifest.RegistrationMethodName);
            Assert.Contains(typeof(RigidBody3DComponent), manifest.ActivationTypes);
            Assert.Contains(typeof(BoxCollider3DComponent), manifest.ActivationTypes);
            Assert.Contains(typeof(SphereCollider3DComponent), manifest.ActivationTypes);
            Assert.Contains(typeof(CharacterController3DComponent), manifest.ActivationTypes);
            Assert.Contains(typeof(SceneEntityTriggerObserverComponent), manifest.ActivationTypes);
        }

        /// <summary>Verifies registration is lazy and leaves a physics-free core detached.</summary>
        [Fact]
        public void Register_AndLoadPhysicsFreeScene_LeavesRuntimeDetached() {
            using Core core = CreateInitializedCore();

            HelPhysicsRuntimeComponentRegistration.Register(core);
            HelPhysicsRuntimeComponentRegistration.HandleLoadedScene(core, [CreateNonPhysicsEntity(core)]);

            Assert.Null(core.PhysicsRuntime);
        }

        /// <summary>
        /// Verifies a scene-bound trigger observer runs through the real core update loop after binding.
        /// </summary>
        [Fact]
        public void BindLoadedScene_BeforeHierarchyInitialization_AttachesRuntimeBeforeTriggerObserverUpdate() {
            using Core core = CreateInitializedCore();
            Entity root = HelPhysicsTestSceneFactory3D.CreateEntity(core, float3.Zero);
            Entity trigger = HelPhysicsTestSceneFactory3D.CreateEntity(core, float3.Zero);
            trigger.AddComponent(new RigidBody3DComponent { BodyKind = BodyKind3D.Static });
            trigger.AddComponent(new SphereCollider3DComponent { Radius = 0.5f, IsTrigger = true });
            trigger.AddComponent(new SceneEntityRuntimeIdComponent { SceneEntityId = 100u });
            SceneEntityTriggerObserverComponent observer = new SceneEntityTriggerObserverComponent {
                TargetEntityReference = new SceneEntityReference { EntityId = 200u }
            };
            trigger.AddComponent(observer);

            Entity target = HelPhysicsTestSceneFactory3D.CreateEntity(core, float3.Zero);
            target.AddComponent(new RigidBody3DComponent { BodyKind = BodyKind3D.Dynamic });
            target.AddComponent(new SphereCollider3DComponent { Radius = 0.25f });
            target.AddComponent(new SceneEntityRuntimeIdComponent { SceneEntityId = 200u });
            root.AddChild(trigger);
            root.AddChild(target);

            HelPhysicsRuntimeComponentRegistration.Register(core);
            HelPhysicsRuntimeComponentRegistration.HandleLoadedScene(core, [root]);
            Assert.IsType<HelPhysicsRuntime3D>(core.PhysicsRuntime);

            root.InitializeHierarchy();
            double stepSeconds = core.PhysicsScheduler.StepSeconds;
            core.Update(stepSeconds);
            Assert.False(observer.GetIsTriggered());
            core.Update(stepSeconds);

            Assert.True(observer.GetIsTriggered());
            Assert.Equal(1, core.LastPhysicsStepCount);
        }
        /// <summary>Verifies repeated registration reuses one callback pair instead of subscribing duplicates.</summary>
        [Fact]
        public void Register_Repeatedly_ReusesOneRegistrationStateAndCallbackPair() {
            using Core core = CreateInitializedCore();

            HelPhysicsRuntimeComponentRegistration.Register(core);
            object firstState = GetRegistrationState(core);
            Delegate firstLoadedHandler = GetHandler(core.SceneManager, "SceneLoaded");
            Delegate firstUnloadingHandler = GetHandler(core.SceneManager, "SceneUnloading");

            HelPhysicsRuntimeComponentRegistration.Register(core);
            object secondState = GetRegistrationState(core);

            Assert.Single(firstLoadedHandler.GetInvocationList());
            Assert.Single(firstUnloadingHandler.GetInvocationList());
            Assert.Same(firstState, secondState);
            Assert.Same(firstLoadedHandler, GetHandler(core.SceneManager, "SceneLoaded"));
            Assert.Same(firstUnloadingHandler, GetHandler(core.SceneManager, "SceneUnloading"));
        }

        /// <summary>Verifies malformed physics composition fails before the runtime is attached and releases the failed adapter.</summary>
        [Fact]
        public void HandleLoadedScene_WithMalformedPhysicsComposition_LeavesRuntimeDetached() {
            using Core core = CreateInitializedCore();
            HelPhysicsRuntimeComponentRegistration.Register(core);
            Entity malformedEntity = HelPhysicsTestSceneFactory3D.CreateInvalidPhysicsEntity(core, "collider-without-body");

            Assert.Throws<InvalidOperationException>(() =>
                HelPhysicsRuntimeComponentRegistration.HandleLoadedScene(core, [malformedEntity]));

            Assert.Null(core.PhysicsRuntime);
            object state = GetRegistrationState(core);
            Assert.Null(GetField(state, "RuntimeWorld"));
        }

        /// <summary>Verifies a loaded physics scene can unload and a later physics scene receives a fresh attached runtime.</summary>
        [Fact]
        public void HandleLoadedAndUnloadingScene_ReplacesRuntimeAcrossSceneTransitions() {
            using Core core = CreateInitializedCore();
            HelPhysicsRuntimeComponentRegistration.Register(core);
            Entity firstEntity = HelPhysicsTestSceneFactory3D.CreateBoxEntity(core, float3.Zero, float3.One, BodyKind3D.Static);
            Entity secondEntity = HelPhysicsTestSceneFactory3D.CreateBoxEntity(core, float3.One, float3.One, BodyKind3D.Static);

            HelPhysicsRuntimeComponentRegistration.HandleLoadedScene(core, [firstEntity]);
            HelPhysicsRuntime3D firstRuntime = Assert.IsType<HelPhysicsRuntime3D>(core.PhysicsRuntime);

            HelPhysicsRuntimeComponentRegistration.HandleUnloadingScene(core, [firstEntity]);
            Assert.Null(core.PhysicsRuntime);
            Assert.Null(firstRuntime.Binder);
            Assert.Null(firstRuntime.World);

            HelPhysicsRuntimeComponentRegistration.HandleLoadedScene(core, [secondEntity]);
            HelPhysicsRuntime3D secondRuntime = Assert.IsType<HelPhysicsRuntime3D>(core.PhysicsRuntime);
            Assert.NotSame(firstRuntime, secondRuntime);
        }

        /// <summary>Creates one initialized core for lifecycle tests.</summary>
        /// <returns>An initialized core owned by the caller.</returns>
        static Core CreateInitializedCore() {
            Core core = new Core(new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(AppContext.BaseDirectory),
                SceneCatalog = new RuntimeSceneCatalog(Array.Empty<RuntimeSceneCatalogEntry>())
            });
            core.Initialize(null, null, null, new PlatformInfo("test", "test-version"));
            return core;
        }

        /// <summary>Creates one entity without authored physics components.</summary>
        /// <param name="core">Core that owns the entity.</param>
        /// <returns>A physics-free entity.</returns>
        static Entity CreateNonPhysicsEntity(Core core) {
            Entity entity = new Entity(core);
            entity.InitComponents();
            return entity;
        }

        /// <summary>Reads the internal Core registration state for callback identity assertions.</summary>
        /// <param name="core">Core whose state should be read.</param>
        /// <returns>The currently stored registration state.</returns>
        static object GetRegistrationState(Core core) {
            FieldInfo field = typeof(Core).GetField(
                "PhysicsRuntimeRegistrationState",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field);
            object state = field.GetValue(core);
            Assert.NotNull(state);
            return state;
        }

        /// <summary>Reads one internal state field for lifecycle assertions.</summary>
        /// <param name="state">Registration state under inspection.</param>
        /// <param name="fieldName">Name of the state field.</param>
        /// <returns>The field value, or null when it is unset.</returns>
        static object GetField(object state, string fieldName) {
            FieldInfo field = state.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field);
            return field.GetValue(state);
        }

        /// <summary>Reads one callback delegate from internal state.</summary>
        /// <param name="state">Registration state under inspection.</param>
        /// <param name="fieldName">Callback field name.</param>
        /// <returns>The callback delegate.</returns>
        static Delegate GetHandler(object state, string fieldName) {
            return Assert.IsAssignableFrom<Delegate>(GetField(state, fieldName));
        }
    }
}
