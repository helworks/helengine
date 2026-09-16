using Xunit;

namespace helengine.editor.tests.managers.asset {
    /// <summary>
    /// Verifies the fresh-session change log replay trusts stamp-validated hash cache entries instead of re-hashing every logged file.
    /// </summary>
    public sealed class EditorNativeAssetWriteServiceReplayTests : IDisposable {
        /// <summary>
        /// Temporary project root used by the current test instance.
        /// </summary>
        readonly string ProjectRootPath;

        /// <summary>
        /// Creates one isolated project root with an assets folder.
        /// </summary>
        public EditorNativeAssetWriteServiceReplayTests() {
            ProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-write-replay-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(ProjectRootPath, "assets"));
        }

        /// <summary>
        /// Deletes temporary test state.
        /// </summary>
        public void Dispose() {
            if (Directory.Exists(ProjectRootPath)) {
                Directory.Delete(ProjectRootPath, true);
            }
        }

        /// <summary>
        /// Ensures a new session replaying an old logged change reuses the persisted hash entry when the file is unchanged.
        /// </summary>
        [Fact]
        public void Construct_WhenLoggedFileIsUnchangedSinceCaching_DoesNotRehashIt() {
            string assetPath = Path.Combine(ProjectRootPath, "assets", "hero.png");
            File.WriteAllBytes(assetPath, new byte[] { 1, 2, 3, 4 });

            // Establish the identity sidecar first so the later reconcile has nothing to repair.
            using (EditorAssetHashCache seedCache = new EditorAssetHashCache(ProjectRootPath))
            using (EditorAssetIdentityIndex seedIndex = new EditorAssetIdentityIndex(ProjectRootPath, null, null, seedCache, new EditorAssetRepairReport())) {
                seedIndex.Initialize();
            }
            Assert.True(File.Exists(assetPath + ".hmeta"));

            CountingHasher firstHasher = new CountingHasher(ProjectRootPath);
            using (EditorAssetHashCache firstCache = new EditorAssetHashCache(ProjectRootPath, firstHasher)) {
                firstCache.GetContentHash(assetPath);
                firstCache.Flush();
            }
            Assert.Equal(1, firstHasher.Count);

            using (EditorProjectWriteLock projectWriteLock = EditorProjectWriteLock.Acquire(ProjectRootPath)) {
                new FileEditorProjectWriteChangeLog(ProjectRootPath).PublishChange("hero.png");
            }

            CountingHasher secondHasher = new CountingHasher(ProjectRootPath);
            using EditorAssetHashCache secondCache = new EditorAssetHashCache(ProjectRootPath, secondHasher);
            using EditorAssetIdentityIndex identityIndex = new EditorAssetIdentityIndex(ProjectRootPath, null, null, secondCache, new EditorAssetRepairReport());
            identityIndex.Initialize();
            int hashesBeforeReplay = secondHasher.Count;

            using EditorNativeAssetWriteService writeService = new EditorNativeAssetWriteService(ProjectRootPath, identityIndex, secondCache);

            Assert.Equal(0, hashesBeforeReplay);
            Assert.Equal(0, secondHasher.Count);
            Assert.Equal(firstHasher.LastHash, secondCache.GetContentHash(assetPath));
        }

        /// <summary>
        /// Hasher that counts how often file contents are hashed.
        /// </summary>
        sealed class CountingHasher : AssetFileHasher {
            public CountingHasher(string projectRootPath) : base(projectRootPath) {
            }

            public int Count { get; private set; }

            public string LastHash { get; private set; }

            public override string ComputeHash(Stream stream) {
                Count++;
                string hash = base.ComputeHash(stream);
                LastHash = "sha256:" + hash;
                return hash;
            }
        }
    }
}
