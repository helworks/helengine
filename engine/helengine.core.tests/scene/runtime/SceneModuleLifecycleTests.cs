using System.Reflection;
using helengine;
using helengine.core.tests.managers.rendering;

namespace helengine.core.tests.scene.runtime {
    /// <summary>Verifies dependency ordering, asynchronous scene publication and code lifetime through real scene teardown.</summary>
    public sealed class SceneModuleLifecycleTests {
        /// <summary>Shares dependencies between scenes and unloads them only after the final dependent, retaining startup pins.</summary>
        [Fact]
        public void SharedReferencesAndResidentDependenciesHaveOrderedLifetime() {
            SceneModuleTestProvider provider = new SceneModuleTestProvider();
            Core core = CreateCore(provider, null, new[] {
                Entry("common"), Entry("resident", RuntimeCodeModuleLoadState.ResidentAtStartup, "common"),
                Entry("world", RuntimeCodeModuleLoadState.Unloadable, "common")
            }, "world");
            SceneModuleManager manager = core.SceneModuleManager;
            using SceneModuleLoadOperation first = manager.BeginAcquireScene("first");
            Complete(first); first.CommitSceneLease();
            using SceneModuleLoadOperation second = manager.BeginAcquireScene("second");
            Complete(second); second.CommitSceneLease();
            Assert.Equal(new[] { "begin:common", "register:common", "begin:resident", "register:resident", "begin:world", "register:world" }, provider.Events);
            manager.ReleaseScene("first");
            Assert.DoesNotContain("dispose:world", provider.Events);
            manager.ReleaseScene("second");
            Assert.Equal(new[] { "unregister:world", "dispose:world" }, provider.Events.TakeLast(2));
            Assert.Equal(2, manager.LoadedModuleCount);
            manager.Dispose(); manager.Dispose();
            Assert.Equal(new[] { "unregister:resident", "dispose:resident", "unregister:common", "dispose:common" }, provider.Events.TakeLast(4));
        }

        /// <summary>Rejects invalid manifests before starting native IO.</summary>
        [Theory]
        [InlineData("missing")]
        [InlineData("cycle")]
        [InlineData("duplicate")]
        public void InvalidManifestNeverStartsPlatformLoading(string kind) {
            SceneModuleTestProvider provider = new SceneModuleTestProvider();
            RuntimeCodeModuleManifestEntry[] entries = kind == "missing" ? new[] { Entry("world", RuntimeCodeModuleLoadState.Unloadable, "absent") }
                : kind == "cycle" ? new[] { Entry("world", RuntimeCodeModuleLoadState.Unloadable, "second"), Entry("second", RuntimeCodeModuleLoadState.Unloadable, "world") }
                : new[] { Entry("world"), Entry("WORLD") };
            Assert.Throws<InvalidOperationException>(() => CreateCore(provider, null, entries, "world"));
            Assert.Empty(provider.Events);
        }

        /// <summary>Never polls pending IO repeatedly in one budget advance and cancels acquired dependencies in reverse order.</summary>
        [Fact]
        public void PendingIoYieldsAndCancellationReleasesPartialClosure() {
            SceneModuleTestProvider provider = new SceneModuleTestProvider { AdvancesRequired = 2 };
            Core core = CreateCore(provider, null, new[] { Entry("common"), Entry("world", RuntimeCodeModuleLoadState.Unloadable, "common") }, "world");
            using SceneModuleLoadOperation operation = core.SceneModuleManager.BeginAcquireScene("first");
            operation.AdvanceForBudget(1000d, new SceneLoadingTestClock());
            Assert.Equal(1, provider.LastOperation.Advances);
            Assert.False(operation.IsCompleted);
            operation.Advance();
            operation.Advance();
            Assert.Equal(1, provider.LastOperation.Advances);
            operation.Dispose();
            Assert.True(provider.LastOperation.Disposed);
            Assert.Equal(new[] { "unregister:common", "dispose:common" }, provider.Events.TakeLast(2));
            Assert.Equal(0, core.SceneModuleManager.LoadedModuleCount);
            core.SceneModuleManager.Dispose();
        }

        /// <summary>Rolls back partially registered factories before dependency code can unload.</summary>
        [Fact]
        public void RegistrationFailureReversesFactoriesAndAcquiredDependencies() {
            SceneModuleTestProvider provider = new SceneModuleTestProvider { FailingModule = "world" };
            Core core = CreateCore(provider, null, new[] { Entry("common"), Entry("world", RuntimeCodeModuleLoadState.Unloadable, "common") }, "world");
            using SceneModuleLoadOperation operation = core.SceneModuleManager.BeginAcquireScene("first");
            Assert.Throws<InvalidOperationException>(() => Complete(operation));
            operation.Dispose();
            Assert.Equal(new[] { "unregister:world", "dispose:world", "unregister:common", "dispose:common" }, provider.Events.TakeLast(4));
            Assert.False(core.SceneRuntimeComponentRegistry.TryGet("module.world", out _));
            Assert.Equal(0, core.SceneModuleManager.LoadedModuleCount);
        }

        /// <summary>Runs real materialization while code survives scene unload and consumed content ends at scene publication.</summary>
        [Fact]
        public void TransitionPreloadsBeforeMaterializationAndRetainsCodeThroughUnload() {
            SceneModuleTestProvider provider = new SceneModuleTestProvider { AdvancesRequired = 2 };
            SceneModuleTestContent content = new SceneModuleTestContent();
            Core core = CreateCore(provider, content, new[] { Entry("world") }, "world");
            InstallProcessor(core, "world");
            core.SceneManager.SceneUnloaded += (manager, args) => {
                Assert.True(core.SceneRuntimeComponentRegistry.TryGet("module.world", out _));
                Assert.True(content.Operations[args.SceneId].Disposed);
                provider.Events.Add("scene-unloaded:" + args.SceneId);
            };
            core.SceneManager.RequestSceneTransition("first");
            core.SceneManager.CommitPendingOperationsAtFrameBoundary();
            Assert.Empty(provider.Events); Assert.Empty(content.Events);
            for (int frames = 0; core.SceneManager.IsSceneTransitionActive && frames < 100; frames++) {
                core.SceneManager.CommitPendingOperationsAtFrameBoundary();
                if (core.SceneManager.IsSceneTransitionActive) { Assert.Empty(core.SceneManager.LoadedScenes); }
            }
            Assert.False(core.SceneManager.IsSceneTransitionActive);
            Assert.Single(core.SceneManager.LoadedScenes);
            Assert.True(provider.Events.IndexOf("register:world") < provider.Events.IndexOf("create:world"));
            Assert.True(content.Operations["first"].Disposed);
            core.SceneManager.UnloadScene("first"); core.SceneManager.CommitPendingOperationsAtFrameBoundary();
            Assert.Equal(new[] { "component-dispose:world", "scene-unloaded:first", "unregister:world", "dispose:world" }, provider.Events.TakeLast(4));
            Assert.True(content.Operations["first"].Disposed);
            Assert.Empty(core.SceneManager.LoadedScenes);
            core.Dispose(); core.Dispose();
        }

        /// <summary>Keeps source bytes through loaded callbacks, then releases them independently of executable scene code.</summary>
        /// <param name="incremental">Uses the frame-budgeted transition instead of immediate scene preparation.</param>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void CompletedContentEndsAfterLoadedCallbacks(bool incremental) {
            SceneModuleTestProvider provider = new SceneModuleTestProvider();
            SceneModuleTestContent content = new SceneModuleTestContent { AdvancesRequired = 1 };
            Core core = CreateCore(provider, content, new[] { Entry("world") }, "world");
            InstallProcessor(core, "world");
            int callbacks = 0;
            core.SceneManager.SceneLoaded += (manager, args) => {
                Assert.False(content.Operations[args.SceneId].Disposed);
                using (content.OpenRead("scenes/first.hasset")) { }
                callbacks++;
            };
            if (incremental) { core.SceneManager.RequestSceneTransition("first"); }
            else { core.SceneManager.LoadScene("first", SceneLoadMode.Additive); }
            for (int step = 0; core.SceneManager.LoadedScenes.Count == 0 && step < 100; step++) {
                core.SceneManager.CommitPendingOperationsAtFrameBoundary();
            }
            Assert.Single(core.SceneManager.LoadedScenes);
            Assert.Equal(1, callbacks);
            Assert.True(content.Operations["first"].Disposed);
            Assert.True(core.SceneModuleManager.HasSceneLease("first"));
            Assert.True(core.SceneRuntimeComponentRegistry.TryGet("module.world", out _));
            Assert.Equal(1, content.Events.Count(value => value == "release:first"));
            core.SceneManager.UnloadScene("first");
            core.SceneManager.CommitPendingOperationsAtFrameBoundary();
            Assert.Equal(1, content.Events.Count(value => value == "release:first"));
            core.Dispose();
        }

        /// <summary>Ends consumed content even when a loaded callback throws after publication has already retained the scene code.</summary>
        /// <param name="incremental">Selects asynchronous scene publication.</param>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void LoadedCallbackFailureReleasesContent(bool incremental) {
            SceneModuleTestProvider provider = new SceneModuleTestProvider();
            SceneModuleTestContent content = new SceneModuleTestContent { AdvancesRequired = 1 };
            Core core = CreateCore(provider, content, new[] { Entry("world") }, "world");
            InstallProcessor(core, "world");
            core.SceneManager.SceneLoaded += (manager, args) => {
                using (content.OpenRead("scenes/first.hasset")) { }
                throw new InvalidOperationException("loaded callback failed");
            };
            if (incremental) { core.SceneManager.RequestSceneTransition("first"); }
            else { core.SceneManager.LoadScene("first", SceneLoadMode.Additive); }
            InvalidOperationException failure = Assert.Throws<InvalidOperationException>(() => {
                for (int step = 0; step < 100; step++) { core.SceneManager.CommitPendingOperationsAtFrameBoundary(); }
            });
            Assert.Equal("loaded callback failed", failure.Message);
            Assert.True(content.Operations["first"].Disposed);
            Assert.True(core.SceneModuleManager.HasSceneLease("first"));
            Assert.Equal(1, content.Events.Count(value => value == "release:first"));
            core.Dispose();
        }

        /// <summary>Reacquires consumed source bytes for a second load of the same scene without reviving the disposed preparation scope.</summary>
        /// <param name="incremental">Selects cooperative loading for both visits.</param>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ReloadStartsFreshContentPreparation(bool incremental) {
            SceneModuleTestProvider provider = new SceneModuleTestProvider();
            SceneModuleTestContent content = new SceneModuleTestContent { AdvancesRequired = 1 };
            Core core = CreateCore(provider, content, new[] { Entry("world") }, "world");
            InstallProcessor(core, "world");
            SceneModuleTestContentOperation previous = null;
            for (int visit = 0; visit < 2; visit++) {
                if (incremental) { core.SceneManager.RequestSceneTransition("first"); }
                else { core.SceneManager.LoadScene("first", SceneLoadMode.Additive); }
                for (int step = 0; core.SceneManager.LoadedScenes.Count == 0 && step < 100; step++) {
                    core.SceneManager.CommitPendingOperationsAtFrameBoundary();
                }
                Assert.Single(core.SceneManager.LoadedScenes);
                SceneModuleTestContentOperation current = content.Operations["first"];
                Assert.NotSame(previous, current);
                Assert.True(current.Disposed);
                previous = current;
                core.SceneManager.UnloadScene("first");
                core.SceneManager.CommitPendingOperationsAtFrameBoundary();
            }
            Assert.Equal(2, content.Events.Count(value => value == "preload:first"));
            Assert.Equal(2, content.Events.Count(value => value == "release:first"));
            core.Dispose();
        }

        /// <summary>Rejects asynchronous content in synchronous loading and cancels already acquired code and the partial cache lease.</summary>
        [Fact]
        public void SynchronousLoadRejectsPendingIoAndReleasesPreparations() {
            SceneModuleTestProvider provider = new SceneModuleTestProvider();
            SceneModuleTestContent content = new SceneModuleTestContent { AdvancesRequired = 2 };
            Core core = CreateCore(provider, content, new[] { Entry("world") }, "world");
            InstallProcessor(core, "world");
            core.SceneManager.LoadScene("first", SceneLoadMode.Additive);
            Assert.Throws<InvalidOperationException>(() => core.SceneManager.CommitPendingOperationsAtFrameBoundary());
            Assert.True(content.Operations["first"].Disposed);
            Assert.DoesNotContain("create:world", provider.Events);
            Assert.Equal(0, core.SceneModuleManager.LoadedModuleCount);
            Assert.Empty(core.SceneManager.LoadedScenes);
        }

        /// <summary>Preserves legacy synchronous scene loading without module or preload providers.</summary>
        [Fact]
        public void MonolithicSceneLoadingRetainsExistingSynchronousFlow() {
            Core core = CreateCore(null, null, new RuntimeCodeModuleManifestEntry[0], string.Empty);
            InstallProcessor(core, string.Empty);
            core.SceneManager.LoadScene("first", SceneLoadMode.Additive);
            core.SceneManager.CommitPendingOperationsAtFrameBoundary();
            Assert.Single(core.SceneManager.LoadedScenes);
            Assert.Null(core.SceneModuleManager);
            core.Dispose(); core.Dispose();
        }

        /// <summary>Releases both committed code dependencies and the partial cache when asynchronous preloading throws before entity construction.</summary>
        [Fact]
        public void AsyncContentFailureDoesNotPublishAndRollsBackCode() {
            SceneModuleTestProvider provider = new SceneModuleTestProvider();
            SceneModuleTestContent content = new SceneModuleTestContent { FailAdvance = true };
            Core core = CreateCore(provider, content, new[] { Entry("world") }, "world");
            InstallProcessor(core, "world");
            core.SceneManager.RequestSceneTransition("first");
            core.SceneManager.CommitPendingOperationsAtFrameBoundary();
            core.SceneManager.CommitPendingOperationsAtFrameBoundary();
            Assert.Throws<IOException>(() => core.SceneManager.CommitPendingOperationsAtFrameBoundary());
            Assert.True(content.Operations["first"].Disposed);
            Assert.DoesNotContain("create:world", provider.Events);
            Assert.Equal(0, core.SceneModuleManager.LoadedModuleCount);
            Assert.Empty(core.SceneManager.LoadedScenes);
            core.Dispose(); core.Dispose();
        }

        /// <summary>Cancels an active IO operation before disposing its manager and unloading already acquired dependencies.</summary>
        [Fact]
        public void CoreDisposeCancelsPendingModuleIoBeforeCodeTeardown() {
            SceneModuleTestProvider provider = new SceneModuleTestProvider { AdvancesRequired = 3 };
            Core core = CreateCore(provider, null, new[] { Entry("world") }, "world");
            core.SceneManager.RequestSceneTransition("first");
            core.SceneManager.CommitPendingOperationsAtFrameBoundary();
            core.SceneManager.CommitPendingOperationsAtFrameBoundary();
            Assert.Equal(1, provider.LastOperation.Advances);
            core.Dispose(); core.Dispose();
            Assert.True(provider.LastOperation.Disposed);
            Assert.Empty(core.SceneManager.LoadedScenes);
        }

        /// <summary>Releases the scene-owned content lease and registered code when disposal interrupts IO or the completed preload before publication.</summary>
        /// <param name="completePreload">Completes cache preparation without advancing entity materialization.</param>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void CoreDisposeCancelsContentBeforeAndAfterPreloadCompletion(bool completePreload) {
            SceneModuleTestProvider provider = new();
            SceneModuleTestContent content = new() { AdvancesRequired = completePreload ? 1 : 3 };
            Core core = CreateCore(provider, content, [Entry("world")], "world");
            core.SceneManager.RequestSceneTransition("first");
            core.SceneManager.CommitPendingOperationsAtFrameBoundary();
            core.SceneManager.CommitPendingOperationsAtFrameBoundary();
            core.SceneManager.CommitPendingOperationsAtFrameBoundary();
            Assert.Equal(completePreload, content.Operations["first"].IsCompleted);
            Assert.False(content.Operations["first"].Disposed);
            Assert.Empty(core.SceneManager.LoadedScenes);
            core.Dispose();
            core.Dispose();
            Assert.True(content.Operations["first"].Disposed);
            Assert.Equal(1, content.Events.Count(value => value == "release:first"));
            Assert.Equal(1, provider.Events.Count(value => value == "dispose:world"));
            Assert.DoesNotContain("create:world", provider.Events);
        }

        /// <summary>Only truly startup-resident code is returned by the native startup residency declaration.</summary>
        [Fact]
        public void SceneResidentModulesAreNotEagerStartupRoots() {
            RuntimeCodeModuleManifest manifest = new RuntimeCodeModuleManifest(new[] {
                Entry("core", RuntimeCodeModuleLoadState.ResidentAtStartup),
                Entry("game", RuntimeCodeModuleLoadState.SceneResident), Entry("extra")
            });
            Assert.Equal(new[] { "core" }, manifest.GetResidentModuleIds());
            Assert.True(manifest.CanUnloadModule("game"));
        }

        /// <summary>Rejects missing or incomplete provider contracts rather than silently selecting a monolithic fallback.</summary>
        [Fact]
        public void InvalidProviderPairAndSceneDependenciesFailBeforeIo() {
            SceneModuleTestProvider provider = new SceneModuleTestProvider();
            Assert.Throws<InvalidOperationException>(() => CreateCore(provider, null, new[] { Entry("other") }, "world"));
            Assert.Empty(provider.Events);
            Core core = new Core(new CoreInitializationOptions { ContentStreamSource = new SceneModuleTestStreamSource(), CodeModuleProvider = provider });
            Assert.Throws<InvalidOperationException>(() => core.Initialize(null, null, null, new PlatformInfo("test", "test")));
            Assert.Empty(provider.Events);
        }

        /// <summary>Accounts for actual elapsed time between dependency steps and rejects clock regressions.</summary>
        [Fact]
        public void ModuleBudgetYieldsBetweenReadyDependenciesAndRejectsBadClock() {
            SceneModuleTestProvider provider = new SceneModuleTestProvider();
            Core core = CreateCore(provider, null, new[] { Entry("common"), Entry("world", RuntimeCodeModuleLoadState.Unloadable, "common") }, "world");
            using SceneModuleLoadOperation operation = core.SceneModuleManager.BeginAcquireScene("first");
            operation.AdvanceForBudget(1d, new SceneLoadingTestClock());
            Assert.Single(provider.Events.Where(item => item.StartsWith("register:")));
            Assert.False(operation.IsCompleted);
            Assert.Throws<InvalidOperationException>(() => operation.AdvanceForBudget(2d, new SceneLoadingTestClock(10d, 9d)));
            operation.Dispose();
            Assert.Equal(0, core.SceneModuleManager.LoadedModuleCount);
        }

        /// <summary>Completes ready test operations with a bounded loop.</summary>
        /// <param name="operation">Owned acquisition under test.</param>
        static void Complete(SceneModuleLoadOperation operation) {
            for (int steps = 0; !operation.IsCompleted && steps < 100; steps++) { operation.Advance(); }
            Assert.True(operation.IsCompleted);
        }

        /// <summary>Creates an exact module declaration for dependency tests.</summary>
        /// <param name="id">Stable module identity.</param>
        /// <param name="state">Residency state.</param>
        /// <param name="dependencies">Required module identities.</param>
        /// <returns>Validated declaration.</returns>
        static RuntimeCodeModuleManifestEntry Entry(string id, RuntimeCodeModuleLoadState state = RuntimeCodeModuleLoadState.Unloadable, params string[] dependencies) {
            return new RuntimeCodeModuleManifestEntry(id, id, state, dependencies);
        }

        /// <summary>Creates a real initialized core with explicit module and content contracts.</summary>
        /// <param name="provider">Optional module provider.</param>
        /// <param name="content">Optional preload provider.</param>
        /// <param name="entries">Exact module manifest.</param>
        /// <param name="moduleId">Required module for both test scenes.</param>
        /// <returns>Initialized core.</returns>
        static Core CreateCore(SceneModuleTestProvider provider, SceneModuleTestContent content, RuntimeCodeModuleManifestEntry[] entries, string moduleId) {
            string[] required = string.IsNullOrEmpty(moduleId) ? new string[0] : new[] { moduleId };
            Core core = new Core(new CoreInitializationOptions {
                ContentStreamSource = content == null ? new SceneModuleTestStreamSource() : content,
                SceneCatalog = new RuntimeSceneCatalog(new[] { new RuntimeSceneCatalogEntry("first", "scenes/first.hasset", required), new RuntimeSceneCatalogEntry("second", "scenes/second.hasset", required) }),
                CodeModuleManifest = provider == null ? null : new RuntimeCodeModuleManifest(entries),
                CodeModuleProvider = provider,
                SceneContentProvider = content
            });
            core.Initialize(new MultiWindowTestRenderManager(), new Batch2DTestRenderManager(), null, new PlatformInfo("test", "test"));
            return core;
        }

        /// <summary>Replaces only the scene decoder in a test content manager so tests exercise the actual loading lifecycle.</summary>
        /// <param name="core">Initialized core.</param>
        /// <param name="moduleId">Persisted factory identity.</param>
        static void InstallProcessor(Core core, string moduleId) {
            Dictionary<string, ContentProcessorRegistration> registrations = (Dictionary<string, ContentProcessorRegistration>)typeof(ContentManager)
                .GetField("ProcessorRegistrationsById", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(core.ContentManager);
            registrations[RuntimeContentProcessorIds.SceneAsset] = new ContentProcessorRegistration(RuntimeContentProcessorIds.SceneAsset, new SceneModuleTestSceneProcessor(moduleId), new string[0]);
        }
    }
}
