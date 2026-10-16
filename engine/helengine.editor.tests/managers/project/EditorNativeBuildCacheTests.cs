using Xunit;

namespace helengine.editor.tests.managers.project {
    /// <summary>
    /// Verifies shared generated-core cache synchronization preserves stable native build inputs.
    /// </summary>
    public sealed class EditorNativeBuildCacheTests : IDisposable {
        /// <summary>
        /// Workspace-owned directory used by this cache synchronization test.
        /// </summary>
        readonly string TestRootPath;

        /// <summary>
        /// Creates a unique build-output directory for generated-source cache fixtures.
        /// </summary>
        public EditorNativeBuildCacheTests() {
            TestRootPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "builds",
                "native-build-cache-tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(TestRootPath);
        }

        /// <summary>
        /// Removes only this test's workspace-owned fixture tree.
        /// </summary>
        public void Dispose() {
            if (Directory.Exists(TestRootPath)) {
                Directory.Delete(TestRootPath, true);
            }
        }

        /// <summary>
        /// Keeps identical file times, refreshes changed inputs, prunes removed inputs, and retains builder-owned manifests.
        /// </summary>
        [Fact]
        public void SyncGeneratedCore_PreservesNativeCacheInputsByContentAndOwnership() {
            string sourceRootPath = Path.Combine(TestRootPath, "generated");
            string cacheRootPath = Path.Combine(TestRootPath, "cache", "generated-core");
            string editorSourceBaselineRootPath = Path.Combine(TestRootPath, "cache", "generated-core-editor");
            string ownedRelativePath = Path.Combine("runtime", "platform_manifest.cpp");
            string cacheOnlyOwnedRelativePath = Path.Combine("runtime", "GeneratedRuntimeLegacyDeserializer.cpp");
            Directory.CreateDirectory(Path.Combine(sourceRootPath, "runtime"));
            Directory.CreateDirectory(Path.Combine(cacheRootPath, "runtime"));
            File.WriteAllText(Path.Combine(sourceRootPath, "unchanged.cpp"), "same");
            File.WriteAllText(Path.Combine(sourceRootPath, "changed.cpp"), "before");
            File.WriteAllText(Path.Combine(sourceRootPath, "removed.cpp"), "remove me");
            File.WriteAllText(Path.Combine(sourceRootPath, ownedRelativePath), "editor manifest");
            File.WriteAllText(Path.Combine(cacheRootPath, ownedRelativePath), "builder manifest");
            File.WriteAllText(Path.Combine(cacheRootPath, cacheOnlyOwnedRelativePath), "cached Vita deserializer");

            EditorNativeBuildCache.SyncGeneratedCore(sourceRootPath, cacheRootPath, [ownedRelativePath, cacheOnlyOwnedRelativePath], editorSourceBaselineRootPath);
            File.WriteAllText(Path.Combine(cacheRootPath, ownedRelativePath), "builder manifest");
            DateTime originalTime = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            string unchangedCachePath = Path.Combine(cacheRootPath, "unchanged.cpp");
            string changedCachePath = Path.Combine(cacheRootPath, "changed.cpp");
            File.SetLastWriteTimeUtc(unchangedCachePath, originalTime);
            File.SetLastWriteTimeUtc(changedCachePath, originalTime);
            File.WriteAllText(Path.Combine(sourceRootPath, "changed.cpp"), "after");
            File.WriteAllText(Path.Combine(sourceRootPath, ownedRelativePath), "updated editor manifest");
            File.Delete(Path.Combine(sourceRootPath, "removed.cpp"));

            EditorNativeBuildCache.SyncGeneratedCore(sourceRootPath, cacheRootPath, [ownedRelativePath, cacheOnlyOwnedRelativePath], editorSourceBaselineRootPath);

            Assert.Equal(originalTime, File.GetLastWriteTimeUtc(unchangedCachePath));
            Assert.NotEqual(originalTime, File.GetLastWriteTimeUtc(changedCachePath));
            Assert.Equal("after", File.ReadAllText(changedCachePath));
            Assert.False(File.Exists(Path.Combine(cacheRootPath, "removed.cpp")));
            Assert.Equal("updated editor manifest", File.ReadAllText(Path.Combine(cacheRootPath, ownedRelativePath)));
            Assert.Equal("cached Vita deserializer", File.ReadAllText(Path.Combine(cacheRootPath, cacheOnlyOwnedRelativePath)));

            File.WriteAllText(Path.Combine(cacheRootPath, ownedRelativePath), "builder manifest after build");
            EditorNativeBuildCache.SyncGeneratedCore(sourceRootPath, cacheRootPath, [ownedRelativePath, cacheOnlyOwnedRelativePath], editorSourceBaselineRootPath);
            Assert.Equal("builder manifest after build", File.ReadAllText(Path.Combine(cacheRootPath, ownedRelativePath)));
        }
    }
}
