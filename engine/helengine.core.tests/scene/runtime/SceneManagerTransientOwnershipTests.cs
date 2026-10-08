using System.Reflection;
using helengine;

namespace helengine.core.tests.scene.runtime {
    /// <summary>
    /// Verifies transient entity orders and scene-owned clips are released without invalidating live scenes.
    /// </summary>
    public sealed class SceneManagerTransientOwnershipTests {
        /// <summary>
        /// Ensures cleanup detaches both heap-backed orders, including empty arrays, and the shared empty singleton.
        /// </summary>
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        public void ReleaseTransientEntity_DetachesOverrideOrderAndPreservesOtherAssetLifetimes(int orderKind) {
            SceneOverrideScopeStepKind[] order = orderKind == 0 ? Array.Empty<SceneOverrideScopeStepKind>()
                : orderKind == 1 ? new SceneOverrideScopeStepKind[0]
                : new[] { SceneOverrideScopeStepKind.Platform, SceneOverrideScopeStepKind.BuildConfig };
            SceneEntityAsset asset = new SceneEntityAsset {
                HasOverrideLevelOrder = orderKind != 0,
                OverrideLevelOrder = order
            };
            SceneOverrideScopeStepKind[] unrelatedOrder = new[] { SceneOverrideScopeStepKind.Group };
            SceneEntityAsset unrelated = new SceneEntityAsset { OverrideLevelOrder = unrelatedOrder };

            InvokeStatic("ReleaseTransientSceneEntityAsset", asset);

            Assert.Null(asset.OverrideLevelOrder);
            Assert.Same(unrelatedOrder, unrelated.OverrideLevelOrder);
            Assert.Empty(Array.Empty<SceneOverrideScopeStepKind>());
            InvokeStatic("ReleaseTransientSceneEntityAsset", unrelated);
        }

        /// <summary>
        /// Ensures a clip shared by two scenes survives the first unload and is disposed once after both roots.
        /// Duplicate references within one scene must also balance without releasing the clip prematurely.
        /// </summary>
        [Fact]
        public void UnloadScene_SharedAnimationClipLivesUntilLastRootIsDisposed() {
            Core core = new Core(new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(AppContext.BaseDirectory),
                SceneCatalog = new RuntimeSceneCatalog(new[] {
                    new RuntimeSceneCatalogEntry("first", "scenes/first.hasset"),
                    new RuntimeSceneCatalogEntry("second", "scenes/second.hasset")
                })
            });
            core.Initialize(null, null, null, new PlatformInfo("test", "test-version"));
            Entity firstRoot = new Entity(core);
            Entity secondRoot = new Entity(core);
            SceneLifecycleAnimationClipAsset clip = new SceneLifecycleAnimationClipAsset(new[] { firstRoot, secondRoot });
            TrackScene(core.SceneManager, "first", firstRoot, new[] { clip, clip });
            TrackScene(core.SceneManager, "second", secondRoot, new[] { clip });

            Assert.Equal(1, core.SceneManager.ActiveOwnedAnimationClipReferenceCount);
            InvokeInstance(core.SceneManager, "UnloadSceneImmediate", "first");
            Assert.True(firstRoot.IsDisposed);
            Assert.False(secondRoot.IsDisposed);
            Assert.Equal(0, clip.DisposeCount);
            Assert.Equal(1, core.SceneManager.ActiveOwnedAnimationClipReferenceCount);

            InvokeInstance(core.SceneManager, "UnloadSceneImmediate", "second");
            Assert.Equal(1, clip.DisposeCount);
            Assert.True(clip.RootsWereDisposedAtRelease);
            Assert.Equal(0, core.SceneManager.ActiveOwnedAnimationClipReferenceCount);
        }

        /// <summary>
        /// Ensures consuming a completed operation transfers container identities once and cannot restart scene materialization.
        /// </summary>
        [Fact]
        public void TakeResult_TransfersOriginalContainersOnceAndKeepsCompletionState() {
            RuntimeSceneLoadOperation operation = CreateOperation();
            Assert.Throws<InvalidOperationException>(() => operation.TakeResult());
            operation.Advance();
            IReadOnlyList<Entity> roots = operation.Result.RootEntities;
            RuntimeSceneOwnedAssetSet assets = operation.Result.OwnedAssets;

            RuntimeSceneLoadResult result = operation.TakeResult();
            Assert.Same(roots, result.RootEntities);
            Assert.Same(assets, result.OwnedAssets);
            Assert.Null(typeof(RuntimeSceneLoadOperation).GetField("ResultValue",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(operation));
            Assert.True(operation.IsCompleted);
            Assert.Equal(1f, operation.Progress);
            Assert.Throws<InvalidOperationException>(() => operation.TakeResult());
            Assert.Throws<InvalidOperationException>(() => operation.Result);
            operation.Advance();
            Assert.Single(result.RootEntities);
            operation.Dispose();
            operation.Dispose();
            Assert.False(Assert.Single(result.RootEntities).IsDisposed);
            Assert.Throws<InvalidOperationException>(() => operation.Advance());
            Assert.Throws<InvalidOperationException>(() => operation.TakeResult());
            Assert.True(typeof(RuntimeSceneLoadOperation).GetMethod("TakeResult").IsDefined(typeof(NativeOwnedReturnAttribute), false));
            Assert.True(typeof(RuntimeSceneLoadOperation).GetField("ResultValue",
                BindingFlags.Instance | BindingFlags.NonPublic).IsDefined(typeof(NativeOwnedMemberAttribute), false));
            Assert.Single(result.RootEntities).Dispose();
            result.OwnedAssets.Dispose();
        }

        /// <summary>
        /// Ensures disposal releases an unconsumed wrapper and temporary fixups without destroying its live scene data.
        /// </summary>
        [Fact]
        public void Dispose_UnconsumedCompletedResultClearsOwnerAndPreservesSceneData() {
            RuntimeSceneLoadOperation operation = CreateOperation();
            operation.Advance();
            IReadOnlyList<Entity> roots = operation.Result.RootEntities;
            RuntimeSceneOwnedAssetSet assets = operation.Result.OwnedAssets;

            operation.Dispose();
            operation.Dispose();

            Assert.Null(typeof(RuntimeSceneLoadOperation).GetField("ResultValue",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(operation));
            Assert.Null(typeof(RuntimeSceneLoadOperation).GetField("ReferenceFixups",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(operation));
            Assert.Throws<InvalidOperationException>(() => operation.Result);
            Assert.False(Assert.Single(roots).IsDisposed);
            Assert.NotNull(assets.OwnedAnimationClips);
            Assert.Single(roots).Dispose();
            assets.Dispose();
        }

        /// <summary>Creates one real incremental load with a component-free root for result-transfer lifetime tests.</summary>
        /// <returns>Operation whose scene materializes without render or input dependencies.</returns>
        static RuntimeSceneLoadOperation CreateOperation() {
            Core core = new Core(new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(AppContext.BaseDirectory)
            });
            core.Initialize(null, null, null, new PlatformInfo("test", "test-version"));
            RuntimeSceneLoadService loader = new RuntimeSceneLoadService(core,
                new RuntimeSceneAssetReferenceResolver(core, core.ContentManager));
            return loader.CreateTrackedLoadOperation(new SceneAsset {
                RootEntities = new[] { new SceneEntityAsset { Name = "ResultTransferRoot" } }
            });
        }

        /// <summary>
        /// Registers a materialized scene through the same tracking and reference-table operations used by normal loads.
        /// </summary>
        /// <param name="manager">Manager that owns the live scene records.</param>
        /// <param name="sceneId">Stable scene identifier.</param>
        /// <param name="root">Root entity that must be disposed before its clips.</param>
        /// <param name="clips">Animation references borrowed by the scene's owned-asset container.</param>
        static void TrackScene(SceneManager manager, string sceneId, Entity root, IReadOnlyList<AnimationClipAsset> clips) {
            RuntimeSceneOwnedAssetSet assets = new RuntimeSceneOwnedAssetSet(
                Array.Empty<RuntimeTexture>(), Array.Empty<FontAsset>(), Array.Empty<AudioAsset>(),
                Array.Empty<RuntimeModel>(), Array.Empty<RuntimeMaterial>(), clips);
            LoadedSceneRecord record = new LoadedSceneRecord(sceneId, "scenes/" + sceneId + ".hasset",
                new List<Entity> { root }, assets, false);
            InvokeInstance(manager, "TrackLoadedSceneRecord", record);
            InvokeInstance(manager, "RegisterOwnedAssets", assets, sceneId);
        }

        /// <summary>
        /// Invokes one internal instance lifecycle seam without exposing it in the public engine API.
        /// </summary>
        /// <param name="manager">Manager whose lifecycle operation is exercised.</param>
        /// <param name="methodName">Internal operation name.</param>
        /// <param name="arguments">Arguments supplied to the operation.</param>
        static void InvokeInstance(SceneManager manager, string methodName, params object[] arguments) {
            typeof(SceneManager).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(manager, arguments);
        }

        /// <summary>
        /// Invokes one internal transient cleanup seam without changing its production visibility.
        /// </summary>
        /// <param name="methodName">Transient cleanup operation name.</param>
        /// <param name="asset">Asset that transfers to the cleanup operation.</param>
        static void InvokeStatic(string methodName, object asset) {
            typeof(SceneManager).GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new[] { asset });
        }
    }
}
