namespace helengine.helphysics.tests {
    /// <summary>
    /// Verifies configured HelPhysics registration owns scene binding and Core runtime attachment without a backend fallback.
    /// </summary>
    [Collection("HelPhysicsSceneBindingCoreTests")]
    public sealed class HelPhysicsRuntimeComponentRegistrationTests {
        /// <summary>
        /// Initializes the minimal engine Core required by direct binder fixtures before each registration test.
        /// </summary>
        public HelPhysicsRuntimeComponentRegistrationTests() {
            Core core = new Core(new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(AppContext.BaseDirectory)
            });
            core.Initialize(null, null, null, new PlatformInfo("test", "test-version"));
        }

        /// <summary>
        /// Ensures registration subscribes lifecycle handling without attaching a runtime before a physics scene exists.
        /// </summary>
        [Fact]
        public void RegisterConfigured_BeforeSceneLoad_DoesNotAttachRuntime() {
            HelPhysicsWorldSettings3D settings = new HelPhysicsWorldSettings3D();
            Core core = CreateCore(settings.FixedStepSeconds);

            HelPhysicsRuntimeComponentRegistration.RegisterConfigured(core, settings);

            Assert.Null(core.PhysicsRuntime);
        }

        /// <summary>
        /// Ensures a compatible scene receives one fresh binder containing its static ground and four dynamic cubes.
        /// </summary>
        [Fact]
        public void HandleLoadedScene_WithStackedCubeHierarchy_AttachesFiveBindingRuntime() {
            HelPhysicsWorldSettings3D settings = new HelPhysicsWorldSettings3D();
            Core core = CreateCore(settings.FixedStepSeconds);
            Entity root = HelPhysicsTestSceneFactory3D.CreateNestedGroundAndFourBoxScene();
            HelPhysicsRuntimeComponentRegistration.RegisterConfigured(core, settings);

            HelPhysicsRuntimeComponentRegistration.HandleLoadedScene(core, [root]);

            HelPhysicsSceneRuntime3D runtime = Assert.IsType<HelPhysicsSceneRuntime3D>(core.PhysicsRuntime);
            Assert.Equal(5, runtime.Binder.Bindings.Count);
            Assert.Same(settings, runtime.Binder.World.Settings);
            Assert.Equal("AfterHelPhysicsSceneBinding", core.LastSceneTransitionStage);
        }

        /// <summary>
        /// Ensures every scene receives a newly constructed binder rather than retaining solver state from a prior scene.
        /// </summary>
        [Fact]
        public void HandleLoadedScene_WithConsecutivePhysicsScenes_ReplacesRuntimeAndBinder() {
            HelPhysicsWorldSettings3D settings = new HelPhysicsWorldSettings3D();
            Core core = CreateCore(settings.FixedStepSeconds);
            HelPhysicsRuntimeComponentRegistration.RegisterConfigured(core, settings);
            HelPhysicsRuntimeComponentRegistration.HandleLoadedScene(
                core,
                [HelPhysicsTestSceneFactory3D.CreateNestedGroundAndFourBoxScene()]);
            HelPhysicsSceneRuntime3D firstRuntime = Assert.IsType<HelPhysicsSceneRuntime3D>(core.PhysicsRuntime);

            HelPhysicsRuntimeComponentRegistration.HandleLoadedScene(
                core,
                [HelPhysicsTestSceneFactory3D.CreateBoxEntity(float3.Zero, float3.One, BodyKind3D.Static)]);

            HelPhysicsSceneRuntime3D secondRuntime = Assert.IsType<HelPhysicsSceneRuntime3D>(core.PhysicsRuntime);
            Assert.NotSame(firstRuntime, secondRuntime);
            Assert.NotSame(firstRuntime.Binder, secondRuntime.Binder);
            Assert.Single(secondRuntime.Binder.Bindings);
        }

        /// <summary>
        /// Ensures all roots are validated before the first valid root can reserve a body or receive lifecycle state.
        /// </summary>
        [Fact]
        public void HandleLoadedScene_WithInvalidLaterRoot_RejectsWithoutPartialSceneBinding() {
            HelPhysicsWorldSettings3D settings = new HelPhysicsWorldSettings3D();
            Core core = CreateCore(settings.FixedStepSeconds);
            Entity validRoot = HelPhysicsTestSceneFactory3D.CreateBoxEntity(
                float3.Zero,
                float3.One,
                BodyKind3D.Static);
            Entity invalidRoot = HelPhysicsTestSceneFactory3D.CreateInvalidPhysicsEntity("static-mesh");
            HelPhysicsRuntimeComponentRegistration.RegisterConfigured(core, settings);

            Assert.Throws<InvalidOperationException>(() =>
                HelPhysicsRuntimeComponentRegistration.HandleLoadedScene(core, [validRoot, invalidRoot]));

            Assert.Null(core.PhysicsRuntime);
            Assert.DoesNotContain(validRoot.Components, component => component is HelPhysicsEntityBindingLifecycle3D);
        }

        /// <summary>
        /// Ensures binding uses the exact root references captured during preflight instead of rereading a changing caller-owned list.
        /// </summary>
        [Fact]
        public void BindHierarchies_WithChangingRootList_BindsOnlyValidatedRootSnapshot() {
            Entity firstRoot = HelPhysicsTestSceneFactory3D.CreateBoxEntity(
                float3.Zero,
                float3.One,
                BodyKind3D.Static);
            Entity secondRoot = HelPhysicsTestSceneFactory3D.CreateBoxEntity(
                new float3(2f, 0f, 0f),
                float3.One,
                BodyKind3D.Static);
            Entity firstReplacement = HelPhysicsTestSceneFactory3D.CreateBoxEntity(
                new float3(4f, 0f, 0f),
                float3.One,
                BodyKind3D.Static);
            Entity secondReplacement = HelPhysicsTestSceneFactory3D.CreateBoxEntity(
                new float3(6f, 0f, 0f),
                float3.One,
                BodyKind3D.Static);
            ChangingRootEntityList roots = new ChangingRootEntityList(
                [firstRoot, secondRoot],
                [firstReplacement, secondReplacement]);
            HelPhysicsSceneBinder3D binder = HelPhysicsRuntimeFactory3D.Create(new HelPhysicsWorldSettings3D());

            binder.BindHierarchies(roots);

            Assert.Equal(2, binder.Bindings.Count);
            Assert.Contains(binder.Bindings, binding => ReferenceEquals(binding.Entity, firstRoot));
            Assert.Contains(binder.Bindings, binding => ReferenceEquals(binding.Entity, secondRoot));
            Assert.DoesNotContain(binder.Bindings, binding => ReferenceEquals(binding.Entity, firstReplacement));
            Assert.DoesNotContain(binder.Bindings, binding => ReferenceEquals(binding.Entity, secondReplacement));
        }

        /// <summary>
        /// Ensures a later commit-time translation failure removes every body, binding, and lifecycle created earlier in the transaction.
        /// </summary>
        [Fact]
        public void BindHierarchies_WhenLaterCommitFails_RollsBackEarlierBindingState() {
            Entity firstRoot = HelPhysicsTestSceneFactory3D.CreateBoxEntity(
                float3.Zero,
                float3.One,
                BodyKind3D.Static);
            ThrowingSecondPositionReadEntity secondRoot = new ThrowingSecondPositionReadEntity();
            secondRoot.InitComponents();
            secondRoot.InitChildren();
            secondRoot.AddComponent(new RigidBody3DComponent {
                BodyKind = BodyKind3D.Static
            });
            secondRoot.AddComponent(new BoxCollider3DComponent {
                Size = float3.One
            });
            HelPhysicsSceneBinder3D binder = HelPhysicsRuntimeFactory3D.Create(new HelPhysicsWorldSettings3D());

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                binder.BindHierarchies([firstRoot, secondRoot]));

            Assert.Contains("commit-time position read", exception.Message, StringComparison.Ordinal);
            Assert.Empty(binder.Bindings);
            Assert.DoesNotContain(firstRoot.Components, component => component is HelPhysicsEntityBindingLifecycle3D);
            Assert.DoesNotContain(secondRoot.Components, component => component is HelPhysicsEntityBindingLifecycle3D);
            binder.Step();
            Assert.Equal(0, binder.World.LastStepMetrics.BodyCount);
        }

        /// <summary>
        /// Ensures scene unloading detaches the adapter while all binding-owned entity components are still alive.
        /// </summary>
        [Fact]
        public void HandleUnloadingScene_WithAttachedRuntime_DetachesBeforeEntityDisposal() {
            HelPhysicsWorldSettings3D settings = new HelPhysicsWorldSettings3D();
            Core core = CreateCore(settings.FixedStepSeconds);
            Entity root = HelPhysicsTestSceneFactory3D.CreateNestedGroundAndFourBoxScene();
            HelPhysicsRuntimeComponentRegistration.RegisterConfigured(core, settings);
            HelPhysicsRuntimeComponentRegistration.HandleLoadedScene(core, [root]);
            HelPhysicsSceneRuntime3D runtime = Assert.IsType<HelPhysicsSceneRuntime3D>(core.PhysicsRuntime);
            HelPhysicsEntityBinding3D[] bindings = runtime.Binder.Bindings.ToArray();

            HelPhysicsRuntimeComponentRegistration.HandleUnloadingScene(core, [root]);

            Assert.Null(core.PhysicsRuntime);
            Assert.Empty(runtime.Binder.Bindings);
            Assert.All(bindings, binding => Assert.False(binding.IsValid));
        }

        /// <summary>
        /// Ensures a failed binding cleanup remains registered and retryable instead of losing its only owning reference.
        /// </summary>
        [Fact]
        public void HandleUnloadingScene_WhenLifecycleStateIsMalformed_RetainsRuntimeUntilCleanupRetrySucceeds() {
            HelPhysicsWorldSettings3D settings = new HelPhysicsWorldSettings3D();
            Core core = CreateCore(settings.FixedStepSeconds);
            Entity root = HelPhysicsTestSceneFactory3D.CreateBoxEntity(
                float3.Zero,
                float3.One,
                BodyKind3D.Static);
            HelPhysicsRuntimeComponentRegistration.RegisterConfigured(core, settings);
            HelPhysicsRuntimeComponentRegistration.HandleLoadedScene(core, [root]);
            HelPhysicsSceneRuntime3D runtime = Assert.IsType<HelPhysicsSceneRuntime3D>(core.PhysicsRuntime);
            HelPhysicsEntityBindingLifecycle3D lifecycle = Assert.IsType<HelPhysicsEntityBindingLifecycle3D>(
                root.Components.Single(component => component is HelPhysicsEntityBindingLifecycle3D));
            Assert.True(root.Components.Remove(lifecycle));

            Assert.Throws<InvalidOperationException>(() =>
                HelPhysicsRuntimeComponentRegistration.HandleUnloadingScene(core, [root]));

            Assert.Null(core.PhysicsRuntime);
            Assert.Same(runtime, HelPhysicsRuntimeComponentRegistration.RegisteredRuntime);
            Assert.Single(runtime.Binder.Bindings);
            root.Components.Add(lifecycle);

            HelPhysicsRuntimeComponentRegistration.HandleUnloadingScene(core, [root]);

            Assert.Null(HelPhysicsRuntimeComponentRegistration.RegisteredRuntime);
            Assert.Empty(runtime.Binder.Bindings);
        }

        /// <summary>
        /// Ensures explicit shutdown releases event ownership, runtime bindings, and static Core references.
        /// </summary>
        [Fact]
        public void UnregisterConfigured_WithAttachedRuntime_ReleasesRegistrationState() {
            HelPhysicsWorldSettings3D settings = new HelPhysicsWorldSettings3D();
            Core core = CreateCore(settings.FixedStepSeconds);
            Entity root = HelPhysicsTestSceneFactory3D.CreateBoxEntity(
                float3.Zero,
                float3.One,
                BodyKind3D.Static);
            HelPhysicsRuntimeComponentRegistration.RegisterConfigured(core, settings);
            HelPhysicsRuntimeComponentRegistration.HandleLoadedScene(core, [root]);
            HelPhysicsSceneRuntime3D runtime = Assert.IsType<HelPhysicsSceneRuntime3D>(core.PhysicsRuntime);

            HelPhysicsRuntimeComponentRegistration.UnregisterConfigured(core);

            Assert.Null(core.PhysicsRuntime);
            Assert.Null(HelPhysicsRuntimeComponentRegistration.RegisteredRuntime);
            Assert.Empty(runtime.Binder.Bindings);
            Assert.Throws<InvalidOperationException>(() =>
                HelPhysicsRuntimeComponentRegistration.HandleLoadedScene(core, [root]));
        }

        /// <summary>
        /// Creates one initialized Core whose scheduler uses the same fixed step as the configured physics runtime.
        /// </summary>
        /// <param name="fixedStepSeconds">Exact scheduler duration expected by HelPhysics.</param>
        /// <returns>Initialized Core ready for runtime attachment tests.</returns>
        static Core CreateCore(double fixedStepSeconds) {
            Core core = new Core(new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(AppContext.BaseDirectory),
                PhysicsFixedStepSeconds = fixedStepSeconds,
                SceneCatalog = new RuntimeSceneCatalog([])
            });
            core.Initialize(null, null, null, new PlatformInfo("test", "test-version"));
            return core;
        }
    }
}
