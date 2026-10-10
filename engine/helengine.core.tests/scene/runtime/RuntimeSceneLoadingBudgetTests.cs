using System.Reflection;
using helengine;

namespace helengine.core.tests.scene.runtime {
    /// <summary>Verifies timed loading yields inside roots and publishes only initialized, laid-out scenes without gameplay updates.</summary>
    public sealed class RuntimeSceneLoadingBudgetTests {
        /// <summary>Proves one large root yields between its components and children while larger budgets advance multiple steps.</summary>
        [Fact]
        public void BudgetYieldsInsideRootAndPreservesAuthoredEnabled() {
            Core core = CreateCore(3d);
            ScenePreparationProbeDeserializer probe = new ScenePreparationProbeDeserializer("test.probe");
            RuntimeSceneLoadOperation operation = CreateOperation(core, probe, new SceneEntityAsset {
                Enabled = true,
                Components = new[] { Record("test.probe"), Record("test.probe"), Record("test.probe") },
                Children = new[] { new SceneEntityAsset { Enabled = false, Components = new[] { Record("test.probe") } } }
            });
            SceneLoadingTestClock clock = new SceneLoadingTestClock();
            operation.AdvanceForBudget(1d, clock);
            Entity root = Assert.Single(Roots(operation));
            Assert.True(root.Enabled);
            Assert.True(root.RuntimeSuppressed);
            Assert.False(root.IsHierarchyEnabled);
            Assert.Empty(probe.Probes);
            operation.AdvanceForBudget(2d, clock);
            Assert.Equal(2, probe.Probes.Count);
            Assert.False(operation.IsCompleted);
            Assert.All(probe.Probes, item => Assert.Equal(0, item.InitializedCount));
            while (!operation.IsCompleted) {
                Assert.False(root.IsHierarchyEnabled);
                Assert.True(operation.Progress < 1f);
                operation.AdvanceForBudget(1d, clock);
            }
            Assert.True(root.Enabled);
            Assert.False(root.RuntimeSuppressed);
            Assert.True(root.IsHierarchyEnabled);
            Entity child = Assert.Single(root.Children);
            Assert.False(child.Enabled);
            Assert.False(child.IsHierarchyEnabled);
            Assert.All(probe.Probes, item => {
                Assert.Equal(1, item.InitializedCount);
                Assert.Equal(1, item.PreparedCount);
                Assert.True(item.StayedSuppressed);
                Assert.False(item.Updated);
            });
            operation.Dispose();
        }

        /// <summary>Proves readiness callbacks observe final viewport dimensions before the target is activated.</summary>
        [Fact]
        public void PublicationWaitsForViewportLayoutAndExplicitPreparation() {
            Core core = CreateCore(8d);
            ScenePreparationProbeDeserializer probe = new ScenePreparationProbeDeserializer("test.probe");
            RuntimeSceneLoadOperation operation = CreateOperation(core, probe, new SceneEntityAsset {
                Enabled = true,
                Components = new[] { Record("test.viewport"), Record("test.text"), Record("test.probe") }
            });
            while (!operation.IsCompleted) { operation.Advance(); }
            ScenePreparationProbeComponent prepared = Assert.Single(probe.Probes);
            Assert.Equal(140, prepared.PreparedTextWidth);
            Assert.True(prepared.StayedSuppressed);
            Assert.False(prepared.Updated);
            Assert.True(Assert.Single(operation.Result.RootEntities).IsHierarchyEnabled);
            operation.Dispose();
        }

        /// <summary>Ensures a preparation failure propagates while all target roots remain hidden and progress remains incomplete.</summary>
        [Fact]
        public void ReadinessFailureDoesNotPublishTarget() {
            Core core = CreateCore(8d);
            ScenePreparationProbeDeserializer probe = new ScenePreparationProbeDeserializer("test.probe") { FailPreparation = true };
            RuntimeSceneLoadOperation operation = CreateOperation(core, probe, new SceneEntityAsset {
                Enabled = true, Components = new[] { Record("test.probe") }
            });
            Assert.Throws<InvalidOperationException>(() => {
                while (!operation.IsCompleted) { operation.Advance(); }
            });
            Assert.False(operation.IsCompleted);
            Assert.True(operation.Progress < 1f);
            Assert.All(Roots(operation), root => Assert.False(root.IsHierarchyEnabled));
            operation.Dispose();
        }

        /// <summary>Ensures host options reach the runtime manager and callers can change the effective budget later.</summary>
        [Fact]
        public void RuntimeBudgetIsInitializedAndMutable() {
            Core core = CreateCore(3.5d);
            Assert.Equal(3.5d, core.SceneManager.SceneLoadingBudgetMilliseconds);
            core.SceneManager.SceneLoadingBudgetMilliseconds = 1.25d;
            Assert.Equal(1.25d, core.SceneManager.SceneLoadingBudgetMilliseconds);
        }

        /// <summary>Ensures both clock owners clear their owned references exactly once during repeated disposal.</summary>
        [Fact]
        public void ClockAndManagerReleaseTheirOwnedTimersIdempotently() {
            RuntimeSceneLoadingClock clock = new RuntimeSceneLoadingClock();
            Assert.True(clock.ElapsedMilliseconds >= 0d);
            clock.Dispose();
            clock.Dispose();
            Assert.Null(typeof(RuntimeSceneLoadingClock).GetField("Timer", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(clock));
            Assert.Throws<InvalidOperationException>(() => clock.ElapsedMilliseconds);
            Core core = CreateCore(8d);
            RuntimeSceneLoadingClock managerClock = (RuntimeSceneLoadingClock)typeof(SceneManager)
                .GetField("SceneLoadingClock", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(core.SceneManager);
            core.Dispose();
            core.Dispose();
            Assert.Null(typeof(SceneManager).GetField("SceneLoadingClock", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(core.SceneManager));
            Assert.Throws<InvalidOperationException>(() => managerClock.ElapsedMilliseconds);
        }

        /// <summary>Preserves incremental manual advancement while empty scenes can complete without meaningless phases.</summary>
        [Fact]
        public void ManualAdvanceIsIncrementalAndEmptySceneCompletesImmediately() {
            Core core = CreateCore(8d);
            ScenePreparationProbeDeserializer probe = new ScenePreparationProbeDeserializer("test.probe");
            using RuntimeSceneLoadOperation operation = CreateOperation(core, probe,
                new SceneEntityAsset { Components = new[] { Record("test.probe") } });
            operation.Advance();
            Assert.Single(Roots(operation));
            Assert.Empty(probe.Probes);
            Assert.False(operation.IsCompleted);
            while (!operation.IsCompleted) { operation.Advance(); }
            RuntimeSceneLoadService loader = new RuntimeSceneLoadService(core,
                new RuntimeSceneAssetReferenceResolver(core, core.ContentManager), RuntimeComponentRegistry.CreateDefault());
            using RuntimeSceneLoadOperation empty = loader.CreateTrackedLoadOperation(new SceneAsset());
            empty.Advance();
            Assert.True(empty.IsCompleted);
            Assert.Empty(empty.Result.RootEntities);
        }

        /// <summary>Detects a regression relative to the previous observation, even when it remains above the start time.</summary>
        [Fact]
        public void ClockCannotRegressBetweenSteps() {
            using RuntimeSceneLoadOperation operation = CreateOperation(CreateCore(8d),
                new ScenePreparationProbeDeserializer("test.probe"), new SceneEntityAsset());
            Assert.Throws<InvalidOperationException>(() => operation.AdvanceForBudget(20d, new SceneLoadingTestClock(10d, 15d, 12d)));
            Assert.False(operation.IsCompleted);
            Assert.All(Roots(operation), root => Assert.True(root.RuntimeSuppressed));
        }

        /// <summary>Rejects non-finite clocks before they can cause an unbounded frame or publish an incomplete target.</summary>
        [Theory]
        [InlineData(double.NaN, false)]
        [InlineData(double.PositiveInfinity, false)]
        [InlineData(double.NaN, true)]
        [InlineData(double.PositiveInfinity, true)]
        public void NonFiniteClockFailsExplicitly(double invalid, bool afterStep) {
            using RuntimeSceneLoadOperation operation = CreateOperation(CreateCore(8d),
                new ScenePreparationProbeDeserializer("test.probe"), new SceneEntityAsset());
            SceneLoadingTestClock clock = afterStep ? new SceneLoadingTestClock(0d, invalid) : new SceneLoadingTestClock(invalid);
            Assert.Throws<InvalidOperationException>(() => operation.AdvanceForBudget(20d, clock));
            Assert.False(operation.IsCompleted);
        }

        /// <summary>Allows initialization callbacks to remove components without indexing a stale captured count.</summary>
        [Fact]
        public void SelfRemovingInitializationDoesNotReadPastLiveComponents() {
            ScenePreparationProbeDeserializer probe = new ScenePreparationProbeDeserializer("test.probe") { RemoveDuringInitialization = true };
            using RuntimeSceneLoadOperation operation = CreateOperation(CreateCore(8d), probe,
                new SceneEntityAsset { Enabled = true, Components = new[] { Record("test.probe") } });
            while (!operation.IsCompleted) { operation.Advance(); }
            Assert.Equal(1, Assert.Single(probe.Probes).InitializedCount);
            Assert.Empty(Assert.Single(operation.Result.RootEntities).Components);
            Assert.True(Assert.Single(operation.Result.RootEntities).IsHierarchyEnabled);
        }

        /// <summary>Dispatches loading and returns a drawable boundary before the first synchronous target asset read.</summary>
        [Fact]
        public void FirstTransitionBoundaryArmsPresentationBeforeContentRead() {
            SceneLoadingContentProbe source = new SceneLoadingContentProbe();
            Core core = new Core(new CoreInitializationOptions {
                ContentStreamSource = source,
                SceneCatalog = new RuntimeSceneCatalog(new[] { new RuntimeSceneCatalogEntry("target", "scenes/target.hasset") })
            });
            core.Initialize(new MultiWindowTestRenderManager(),
                new managers.rendering.Batch2DTestRenderManager(), null, new PlatformInfo("test", "test"));
            int loadingEvents = 0;
            core.SceneManager.SceneLoading += (manager, args) => {
                loadingEvents++;
                Assert.True(manager.IsSceneTransitionActive);
                Assert.Equal(0, source.OpenCount);
            };
            core.SceneManager.RequestSceneTransition("target");
            core.SceneManager.CommitPendingOperationsAtFrameBoundary();
            Assert.Equal(1, loadingEvents);
            Assert.Equal(0, source.OpenCount);
            Assert.True(core.SceneManager.IsSceneTransitionActive);
            Assert.True(core.SceneManager.SceneTransitionProgress < 1f);
            Assert.Throws<IOException>(() => core.SceneManager.CommitPendingOperationsAtFrameBoundary());
            Assert.Equal(1, source.OpenCount);
            Assert.True(core.SceneManager.IsSceneTransitionActive);
            Assert.Empty(core.SceneManager.LoadedScenes);
        }

        /// <summary>Rejects invalid budgets at initialization, runtime configuration and cooperative advancement.</summary>
        [Theory]
        [InlineData(0d)]
        [InlineData(-1d)]
        [InlineData(double.NaN)]
        [InlineData(double.PositiveInfinity)]
        public void InvalidBudgetFailsExplicitly(double invalid) {
            Assert.Throws<InvalidOperationException>(() => CreateCore(invalid));
            Core core = CreateCore(8d);
            Assert.Throws<ArgumentOutOfRangeException>(() => core.SceneManager.SceneLoadingBudgetMilliseconds = invalid);
            RuntimeSceneLoadOperation operation = CreateOperation(core, new ScenePreparationProbeDeserializer("test.probe"),
                new SceneEntityAsset { Enabled = true });
            Assert.Throws<ArgumentOutOfRangeException>(() => operation.AdvanceForBudget(invalid, new SceneLoadingTestClock()));
            operation.Dispose();
        }

        /// <summary>Creates a headless core with the same option validation and runtime scene-manager initialization as real hosts.</summary>
        static Core CreateCore(double budget) {
            Core core = new Core(new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(AppContext.BaseDirectory),
                SceneCatalog = new RuntimeSceneCatalog(Array.Empty<RuntimeSceneCatalogEntry>()),
                SceneLoadingBudgetMilliseconds = budget
            });
            core.Initialize(null, null, null, new PlatformInfo("test", "test"));
            return core;
        }

        /// <summary>Creates a real tracked loader and registers only fixture component types.</summary>
        static RuntimeSceneLoadOperation CreateOperation(Core core, ScenePreparationProbeDeserializer probe, SceneEntityAsset root) {
            RuntimeComponentRegistry registry = RuntimeComponentRegistry.CreateDefault();
            registry.Register(probe);
            registry.Register(new ScenePreparationProbeDeserializer("test.viewport"));
            registry.Register(new ScenePreparationProbeDeserializer("test.text"));
            RuntimeSceneLoadService loader = new RuntimeSceneLoadService(core,
                new RuntimeSceneAssetReferenceResolver(core, core.ContentManager), registry);
            return loader.CreateTrackedLoadOperation(new SceneAsset { RootEntities = new[] { root } });
        }

        /// <summary>Builds a component record dispatched by the production registry.</summary>
        static SceneComponentAssetRecord Record(string id) { return new SceneComponentAssetRecord { ComponentTypeId = id }; }

        /// <summary>Borrows unfinished roots only in tests so suppression can be checked before the public result exists.</summary>
        static IReadOnlyList<Entity> Roots(RuntimeSceneLoadOperation operation) {
            return (List<Entity>)typeof(RuntimeSceneLoadOperation).GetField("RootEntities", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(operation);
        }
    }
}
