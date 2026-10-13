using helengine.core.tests.content;

namespace helengine.core.tests.scene.runtime {
    /// <summary>
    /// Verifies how the runtime scene asset resolver maps packaged file-backed reference paths with and without a host packaged path policy.
    /// The in-memory content source has no payloads, so the <see cref="FileNotFoundException"/> it raises names the exact path the resolver requested.
    /// </summary>
    public sealed class RuntimeSceneAssetReferenceResolverPackagedPathPolicyTests {
        /// <summary>
        /// Without a policy the desktop resolver keeps rejecting rooted packaged paths before any content is opened.
        /// </summary>
        [Fact]
        public void ResolveAnimationClip_NullPolicyRootedPath_ThrowsMustBeRelative() {
            MemoryContentSource source = CreateEmptySource();
            using Core core = CreateCore(source, null);
            using RuntimeSceneAssetReferenceResolver resolver = new RuntimeSceneAssetReferenceResolver(core, core.ContentManager);
            resolver.BeginOwnedAssetTracking();

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => resolver.ResolveAnimationClip(SceneAssetReferenceFactory.CreateFileSystemAnimationClip("/cooked/animations/logo.hele")));

            Assert.Contains("must be relative", exception.Message);
            resolver.CancelOwnedAssetTracking();
        }

        /// <summary>
        /// Without a policy a canonical relative packaged path reaches the content source unchanged.
        /// </summary>
        [Fact]
        public void ResolveAnimationClip_NullPolicyRelativePath_ReachesSourceUnchanged() {
            MemoryContentSource source = CreateEmptySource();
            using Core core = CreateCore(source, null);
            using RuntimeSceneAssetReferenceResolver resolver = new RuntimeSceneAssetReferenceResolver(core, core.ContentManager);
            resolver.BeginOwnedAssetTracking();

            FileNotFoundException exception = Assert.Throws<FileNotFoundException>(
                () => resolver.ResolveAnimationClip(SceneAssetReferenceFactory.CreateFileSystemAnimationClip("cooked/animations/logo.hele")));

            Assert.Equal("cooked/animations/logo.hele", exception.FileName);
            resolver.CancelOwnedAssetTracking();
        }

        /// <summary>
        /// With the content-root policy a rooted packaged path reaches the content source relative to the content root.
        /// </summary>
        [Fact]
        public void ResolveAnimationClip_ContentRootPolicyRootedPath_ReachesSourceStripped() {
            MemoryContentSource source = CreateEmptySource();
            using Core core = CreateCore(source, null);
            using RuntimeSceneAssetReferenceResolver resolver = new RuntimeSceneAssetReferenceResolver(core, core.ContentManager, new ContentRootPackagedAssetPathPolicy());
            resolver.BeginOwnedAssetTracking();

            FileNotFoundException exception = Assert.Throws<FileNotFoundException>(
                () => resolver.ResolveAnimationClip(SceneAssetReferenceFactory.CreateFileSystemAnimationClip("/cooked/animations/logo.hele")));

            Assert.Equal("cooked/animations/logo.hele", exception.FileName);
            resolver.CancelOwnedAssetTracking();
        }

        /// <summary>
        /// The policy configured on <see cref="CoreInitializationOptions"/> reaches the core-owned scene asset resolver.
        /// </summary>
        [Fact]
        public void CoreSceneAssetReferenceResolver_UsesPolicyFromInitializationOptions() {
            MemoryContentSource source = CreateEmptySource();
            using Core core = CreateCore(source, new ContentRootPackagedAssetPathPolicy());
            RuntimeSceneAssetReferenceResolver resolver = core.SceneAssetReferenceResolver;
            resolver.BeginOwnedAssetTracking();

            FileNotFoundException exception = Assert.Throws<FileNotFoundException>(
                () => resolver.ResolveAnimationClip(SceneAssetReferenceFactory.CreateFileSystemAnimationClip("/cooked/animations/logo.hele")));

            Assert.Equal("cooked/animations/logo.hele", exception.FileName);
            resolver.CancelOwnedAssetTracking();
        }

        /// <summary>
        /// Creates an in-memory content source without payloads so every open reports the requested path.
        /// </summary>
        /// <returns>Empty in-memory content source.</returns>
        static MemoryContentSource CreateEmptySource() {
            return new MemoryContentSource(new Dictionary<string, byte[]>(StringComparer.Ordinal));
        }

        /// <summary>
        /// Creates a headless core over the supplied source and packaged path policy.
        /// </summary>
        /// <param name="source">Content stream source used by the core-owned content manager.</param>
        /// <param name="policy">Packaged path policy for the core-owned resolver; null keeps the built-in rules.</param>
        /// <returns>Initialized headless core.</returns>
        static Core CreateCore(IContentStreamSource source, IPackagedAssetPathPolicy policy) {
            CoreInitializationOptions options = new CoreInitializationOptions {
                ContentStreamSource = source,
                PackagedAssetPathPolicy = policy
            };
            Core core = new Core(options);
            core.Initialize(null, null, null, new PlatformInfo("test", "test-version"));
            return core;
        }
    }
}
