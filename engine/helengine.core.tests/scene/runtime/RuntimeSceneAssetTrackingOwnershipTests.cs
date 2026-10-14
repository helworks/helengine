using System.Reflection;
using helengine;
using helengine.core.tests.managers.rendering;

namespace helengine.core.tests.scene.runtime {
    /// <summary>Verifies commit transfers asset borrows and cancellation performs renderer-aware cleanup exactly once.</summary>
    public sealed class RuntimeSceneAssetTrackingOwnershipTests {
        /// <summary>Publishes all tracked asset kinds without disposing them, then exercises the actual scene manager unload seam.</summary>
        [Fact]
        public void CompleteTracking_PreservesAssetsUntilSceneUnload() {
            Core core = CreateCore();
            RuntimeSceneAssetReferenceResolver resolver = new RuntimeSceneAssetReferenceResolver(core, core.ContentManager);
            RuntimeSceneOwnedAssetSet suppliedAssets = CreateAssets();
            resolver.BeginOwnedAssetTracking();
            TrackAssets(resolver, suppliedAssets);

            RuntimeSceneOwnedAssetSet committedAssets = resolver.CompleteOwnedAssetTracking();
            Assert.Same(Assert.Single(suppliedAssets.OwnedFonts), Assert.Single(committedAssets.OwnedFonts));
            AssertLiveAssets(suppliedAssets);
            resolver.Dispose();
            resolver.Dispose();
            AssertLiveAssets(suppliedAssets);

            Entity root = new Entity(core);
            LoadedSceneRecord record = new LoadedSceneRecord("owned", "scenes/owned.hasset", new List<Entity> { root }, committedAssets, false);
            Invoke(core.SceneManager, "TrackLoadedSceneRecord", record);
            Invoke(core.SceneManager, "RegisterOwnedAssets", committedAssets, "owned");
            Invoke(core.SceneManager, "UnloadSceneImmediate", "owned");

            Assert.True(root.IsDisposed);
            AssertReleasedAssets(suppliedAssets);
            committedAssets.Dispose();
            suppliedAssets.Dispose();
            core.Dispose();
        }

        /// <summary>Both abort and resolver disposal release partial assets through their native-resource seams and tolerate repeated cleanup.</summary>
        /// <param name="disposeResolver">Selects resolver disposal instead of explicit cancellation.</param>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void IncompleteTracking_ReleasesEveryAssetExactlyOnce(bool disposeResolver) {
            Core core = CreateCore();
            RuntimeSceneAssetReferenceResolver resolver = new RuntimeSceneAssetReferenceResolver(core, core.ContentManager);
            RuntimeSceneOwnedAssetSet assets = CreateAssets();
            resolver.BeginOwnedAssetTracking();
            TrackAssets(resolver, assets);
            AssertLiveAssets(assets);

            if (disposeResolver) {
                resolver.Dispose();
            } else {
                resolver.CancelOwnedAssetTracking();
            }
            resolver.CancelOwnedAssetTracking();
            resolver.Dispose();
            resolver.Dispose();

            AssertReleasedAssets(assets);
            assets.Dispose();
            core.Dispose();
        }

        /// <summary>Creates a real core with renderer release paths and a catalog for the managed unload operation.</summary>
        /// <returns>Initialized core whose runtime assets have explicit renderer owners.</returns>
        static Core CreateCore() {
            Core core = new Core(new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(AppContext.BaseDirectory),
                SceneCatalog = new RuntimeSceneCatalog(new[] { new RuntimeSceneCatalogEntry("owned", "scenes/owned.hasset") })
            });
            core.Initialize(new SceneAssetOwnershipTestRenderManager(), new Batch2DTestRenderManager(), null, new PlatformInfo("test", "test"));
            return core;
        }

        /// <summary>Creates six asset kinds including an atlas that is owned exclusively by its font.</summary>
        /// <returns>Borrowing fixture containers used to observe resources across commit or abort.</returns>
        static RuntimeSceneOwnedAssetSet CreateAssets() {
            FontAsset font = new FontAsset(new FontInfo("fixture", 12, 3f),
                new SceneAssetOwnershipTestTexture { Width = 2, Height = 2 },
                new Dictionary<char, FontChar>(), 12f, 2, 2);
            AudioAsset audio = new AudioAsset { EncodedBytes = new byte[] { 1, 2, 3 } };
            return new RuntimeSceneOwnedAssetSet(
                new[] { new SceneAssetOwnershipTestTexture { Width = 2, Height = 2 } },
                new[] { font }, new[] { audio }, new[] { new SceneAssetOwnershipTestModel() },
                new[] { new RuntimeMaterial() }, new[] { new SceneLifecycleAnimationClipAsset(Array.Empty<Entity>()) });
        }

        /// <summary>Uses the production ownership-transfer seams without introducing filesystem cook dependencies.</summary>
        /// <param name="resolver">Resolver whose active tracking scope receives the assets.</param>
        /// <param name="assets">New runtime instances retained by fixture borrows for verification.</param>
        static void TrackAssets(RuntimeSceneAssetReferenceResolver resolver, RuntimeSceneOwnedAssetSet assets) {
            Invoke(resolver, "TrackOwnedTexture", Assert.Single(assets.OwnedTextures));
            Invoke(resolver, "TrackOwnedFont", Assert.Single(assets.OwnedFonts));
            Invoke(resolver, "TrackOwnedAudio", Assert.Single(assets.OwnedAudio));
            Invoke(resolver, "TrackOwnedModel", Assert.Single(assets.OwnedModels));
            Invoke(resolver, "TrackOwnedMaterial", Assert.Single(assets.OwnedMaterials));
            Invoke(resolver, "TrackOwnedAnimationClip", Assert.Single(assets.OwnedAnimationClips));
        }

        /// <summary>Confirms commit has not performed renderer or payload cleanup prematurely.</summary>
        /// <param name="assets">Borrowed fixture references whose resources must still be valid.</param>
        static void AssertLiveAssets(RuntimeSceneOwnedAssetSet assets) {
            Assert.Equal(0, ((SceneAssetOwnershipTestTexture)Assert.Single(assets.OwnedTextures)).DisposeCount);
            Assert.False(Assert.Single(assets.OwnedFonts).IsDisposed);
            Assert.False(Assert.Single(assets.OwnedFonts).Texture.IsDisposed);
            Assert.NotNull(Assert.Single(assets.OwnedAudio).EncodedBytes);
            Assert.Equal(0, ((SceneAssetOwnershipTestModel)Assert.Single(assets.OwnedModels)).DisposeCount);
            Assert.NotNull(Assert.Single(assets.OwnedMaterials).RenderState);
            Assert.Equal(0, ((SceneLifecycleAnimationClipAsset)Assert.Single(assets.OwnedAnimationClips)).DisposeCount);
        }

        /// <summary>Confirms every category used its final resource release rather than only deleting its tracking container.</summary>
        /// <param name="assets">Fixture borrows that remain readable under the managed test runtime.</param>
        static void AssertReleasedAssets(RuntimeSceneOwnedAssetSet assets) {
            Assert.Equal(1, ((SceneAssetOwnershipTestTexture)Assert.Single(assets.OwnedTextures)).DisposeCount);
            Assert.True(Assert.Single(assets.OwnedFonts).IsDisposed);
            Assert.Null(Assert.Single(assets.OwnedFonts).Texture);
            Assert.Null(Assert.Single(assets.OwnedAudio).EncodedBytes);
            Assert.Equal(1, ((SceneAssetOwnershipTestModel)Assert.Single(assets.OwnedModels)).DisposeCount);
            Assert.Null(Assert.Single(assets.OwnedMaterials).RenderState);
            Assert.Equal(1, ((SceneLifecycleAnimationClipAsset)Assert.Single(assets.OwnedAnimationClips)).DisposeCount);
        }

        /// <summary>Invokes one existing nonpublic lifetime boundary without expanding the engine's public API.</summary>
        /// <param name="target">Resolver or scene manager whose real boundary is exercised.</param>
        /// <param name="methodName">Existing internal operation.</param>
        /// <param name="arguments">Arguments borrowed by that operation.</param>
        static void Invoke(object target, string methodName, params object[] arguments) {
            target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, arguments);
        }
    }
}
