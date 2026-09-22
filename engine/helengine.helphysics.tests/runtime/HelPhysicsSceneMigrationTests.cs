namespace helengine {
    /// <summary>Verifies scene-facing sphere, trigger, runtime replacement, and terminal cleanup seams.</summary>
    [Collection("HelPhysicsSceneBindingCoreTests")]
    public sealed class HelPhysicsSceneMigrationTests : IDisposable {
        /// <summary>Engine core shared by the scene migration fixtures.</summary>
        readonly Core CoreValue;

        /// <summary>Initializes the engine core used by scene fixtures.</summary>
        public HelPhysicsSceneMigrationTests() {
            CoreValue = new Core(new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(AppContext.BaseDirectory)
            });
            CoreValue.Initialize(null, null, null, new PlatformInfo("test", "test-version"));
        }

        /// <summary>Verifies scaled sphere translation uses the largest absolute world scale.</summary>
        [Fact]
        public void BindHierarchy_WithScaledSphereCollider_StoresEffectiveRadiusAndSphereShape() {
            Entity entity = HelPhysicsTestSceneFactory3D.CreateEntity(CoreValue, float3.Zero);
            entity.LocalScale = new float3(2f, 3f, 4f);
            RigidBody3DComponent rigidBody = new RigidBody3DComponent { BodyKind = BodyKind3D.Dynamic };
            SphereCollider3DComponent collider = new SphereCollider3DComponent { Radius = 0.5f };
            entity.AddComponent(rigidBody);
            entity.AddComponent(collider);
            HelPhysicsSceneBinder3D binder = HelPhysicsRuntimeFactory3D.Create(CreateSettings(1, 2));
            binder.BindHierarchy(entity);
            HelPhysicsEntityBinding3D binding = Assert.Single(binder.Bindings);
            Assert.Equal(HelPhysicsShapeKind3D.Sphere, binding.Description.ShapeKind);
            Assert.Equal(2f, binding.Description.SphereShape.Radius.ToFloat());
            Assert.True(binding.Description.LocalInverseInertia.Row0.X > PhysicsScalar.Zero);
            Assert.Same(collider, binding.SphereCollider);
        }

        /// <summary>Verifies translated trigger events remain stable through Stay, Exit, and an empty frame.</summary>
        [Fact]
        public void TriggerEvents_WithMultipleObservers_TranslateStableEntitiesThroughExitAndEmptyFrame() {
            Entity root = HelPhysicsTestSceneFactory3D.CreateEntity(CoreValue, float3.Zero);
            Entity trigger = CreateSphereEntity(new float3(0f, 0f, 0f), BodyKind3D.Static, true, 0.5f, 100u);
            Entity target = CreateSphereEntity(new float3(0f, 0f, 0f), BodyKind3D.Dynamic, false, 0.25f, 200u);
            trigger.AddComponent(new SceneEntityTriggerObserverComponent { TargetEntityReference = new SceneEntityReference { EntityId = 200u } });
            trigger.AddComponent(new SceneEntityTriggerObserverComponent { TargetEntityReference = new SceneEntityReference { EntityId = 200u } });
            root.AddChild(trigger);
            root.AddChild(target);
            HelPhysicsRuntime3D runtime = HelPhysicsRuntimeFactory3D.CreateDefault();
            runtime.BindScene(new[] { root });
            runtime.Step(runtime.ConfiguredFixedStepSeconds);
            IReadOnlyList<TriggerEvent3D> firstRead = runtime.TriggerEvents;
            IReadOnlyList<TriggerEvent3D> secondRead = runtime.TriggerEvents;
            Assert.Same(firstRead, secondRead);
            Assert.Equal(1, runtime.Binder.World.TriggerEventCount);
            Assert.True(firstRead.Count > 0, $"events={firstRead.Count} lowlevel={runtime.Binder.World.TriggerEventCount} candidates={runtime.Binder.World.LastStepMetrics.CandidatePairCount} bindings={runtime.RegisteredBodyCount}");
            TriggerEvent3D enter = Assert.Single(firstRead);
            Assert.Equal(TriggerEventKind3D.Enter, enter.Kind);
            Assert.Same(trigger, enter.TriggerEntity);
            Assert.Same(target, enter.OtherEntity);

            runtime.Step(runtime.ConfiguredFixedStepSeconds);
            TriggerEvent3D stay = Assert.Single(runtime.TriggerEvents);
            Assert.Equal(TriggerEventKind3D.Stay, stay.Kind);

            target.Dispose();
            runtime.Step(runtime.ConfiguredFixedStepSeconds);
            TriggerEvent3D exit = Assert.Single(runtime.TriggerEvents);
            Assert.Equal(TriggerEventKind3D.Exit, exit.Kind);
            Assert.Same(trigger, exit.TriggerEntity);
            Assert.Same(target, exit.OtherEntity);

            runtime.Step(runtime.ConfiguredFixedStepSeconds);
            Assert.Empty(runtime.TriggerEvents);
            runtime.Dispose();
        }

        /// <summary>Verifies successful same-hierarchy rebind replaces the old binder transactionally.</summary>
        [Fact]
        public void RuntimeBindScene_WithSameHierarchy_ReplacesPriorBinderTransactionally() {
            Entity root = HelPhysicsTestSceneFactory3D.CreateEntity(CoreValue, float3.Zero);
            root.AddChild(HelPhysicsTestSceneFactory3D.CreateBoxEntity(CoreValue, float3.Zero, float3.One, BodyKind3D.Dynamic));
            HelPhysicsRuntime3D runtime = HelPhysicsRuntimeFactory3D.CreateDefault();
            runtime.BindScene(new[] { root });
            Assert.NotNull(runtime.Binder);
            HelPhysicsSceneBinder3D previous = runtime.Binder;
            HelPhysicsEntityBinding3D previousBinding = Assert.Single(previous.Bindings);
            runtime.BindScene(new[] { root });
            Assert.NotSame(previous, runtime.Binder);
            Assert.Equal(1, runtime.RegisteredBodyCount);
            Assert.False(previousBinding.IsValid);
            runtime.Dispose();
        }

        /// <summary>Verifies a failed replacement leaves the prior runtime and binding intact.</summary>
        [Fact]
        public void RuntimeBindScene_WhenReplacementFails_PreservesPriorRuntime() {
            Entity validRoot = HelPhysicsTestSceneFactory3D.CreateEntity(CoreValue, float3.Zero);
            validRoot.AddChild(HelPhysicsTestSceneFactory3D.CreateBoxEntity(CoreValue, float3.Zero, float3.One, BodyKind3D.Dynamic));
            Entity invalid = HelPhysicsTestSceneFactory3D.CreateInvalidPhysicsEntity(CoreValue, "collider-without-body");
            HelPhysicsRuntime3D runtime = HelPhysicsRuntimeFactory3D.CreateDefault();
            runtime.BindScene(new[] { validRoot });
            Assert.NotNull(runtime.Binder);
            HelPhysicsSceneBinder3D previous = runtime.Binder;
            HelPhysicsEntityBinding3D previousBinding = Assert.Single(previous.Bindings);
            Assert.Throws<InvalidOperationException>(() => runtime.BindScene(new[] { validRoot, invalid }));
            Assert.Same(previous, runtime.Binder);
            Assert.Same(previousBinding, Assert.Single(previous.Bindings));
            Assert.True(previousBinding.IsValid);
            runtime.Dispose();
        }

        /// <summary>Verifies terminal disposal releases bindings and bodies after the world is faulted.</summary>
        [Fact]
        public void RuntimeDispose_WhenWorldFaulted_InvalidatesBindingsAndReleasesBodies() {
            Entity root = HelPhysicsTestSceneFactory3D.CreateEntity(CoreValue, float3.Zero);
            root.AddChild(HelPhysicsTestSceneFactory3D.CreateBoxEntity(CoreValue, float3.Zero, float3.One, BodyKind3D.Dynamic));
            HelPhysicsRuntime3D runtime = HelPhysicsRuntimeFactory3D.CreateDefault();
            runtime.BindScene(new[] { root });
            Assert.NotNull(runtime.Binder);
            HelPhysicsSceneBinder3D binder = runtime.Binder;
            HelPhysicsEntityBinding3D binding = Assert.Single(binder.Bindings);
            HelPhysicsBodyHandle3D handle = binding.BodyHandle;
            HelPhysicsWorld3D world = binder.World;
            world.GetType().GetProperty(nameof(HelPhysicsWorld3D.IsFaulted)).SetValue(world, true);
            runtime.Dispose();
            Assert.Null(runtime.Binder);
            Assert.False(binding.IsValid);
            Assert.Throws<InvalidOperationException>(() => world.GetBodySnapshot(handle));
            Assert.Throws<InvalidOperationException>(() => runtime.Step(runtime.ConfiguredFixedStepSeconds));
        }

        /// <summary>Releases the fixture core and all resources it owns.</summary>
        public void Dispose() {
            CoreValue.Dispose();
        }

        Entity CreateSphereEntity(float3 position, BodyKind3D bodyKind, bool isTrigger, float radius, uint sceneEntityId) {
            Entity entity = HelPhysicsTestSceneFactory3D.CreateEntity(CoreValue, position);
            entity.AddComponent(new RigidBody3DComponent { BodyKind = bodyKind });
            entity.AddComponent(new SphereCollider3DComponent { Radius = radius, IsTrigger = isTrigger });
            entity.AddComponent(new SceneEntityRuntimeIdComponent { SceneEntityId = sceneEntityId });
            return entity;
        }

        static HelPhysicsWorldSettings3D CreateSettings(int bodyCapacity, int commandCapacity) {
            return new HelPhysicsWorldSettings3D(bodyCapacity, bodyCapacity, bodyCapacity, bodyCapacity, bodyCapacity * 4, bodyCapacity, commandCapacity, 2, 1, 0.05d, PhysicsVector3.Zero);
        }
    }
}
