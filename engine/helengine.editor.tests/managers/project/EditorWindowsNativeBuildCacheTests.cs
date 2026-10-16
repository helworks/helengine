using helengine.editor;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies stable Windows native inputs change only when their generated contents change.
    /// </summary>
    public sealed class EditorWindowsNativeBuildCacheTests : IDisposable {
        /// <summary>
        /// Workspace-owned root for the generated-source cache test.
        /// </summary>
        readonly string TestRootPath;

        /// <summary>
        /// Creates an isolated cache test directory outside the system temporary folder.
        /// </summary>
        public EditorWindowsNativeBuildCacheTests() {
            TestRootPath = Path.Combine(Directory.GetCurrentDirectory(), "builds", "windows-native-cache-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(TestRootPath);
        }

        /// <summary>
        /// Removes the isolated cache test directory after the test completes.
        /// </summary>
        public void Dispose() {
            if (Directory.Exists(TestRootPath)) {
                Directory.Delete(TestRootPath, true);
            }
        }

        /// <summary>
        /// Keeps unchanged inputs and builder-owned settings intact while replacing changed files and removing stale generated files.
        /// </summary>
        [Fact]
        public void SyncGeneratedCore_preserves_unchanged_file_times_and_removes_stale_files() {
            string sourceRootPath = Path.Combine(TestRootPath, "source");
            string cacheRootPath = Path.Combine(TestRootPath, "cache");
            Directory.CreateDirectory(sourceRootPath);
            File.WriteAllText(Path.Combine(sourceRootPath, "unchanged.cpp"), "same");
            File.WriteAllText(Path.Combine(sourceRootPath, "changed.cpp"), "before");
            File.WriteAllText(Path.Combine(sourceRootPath, "stale.cpp"), "obsolete");
            Directory.CreateDirectory(Path.Combine(sourceRootPath, "runtime"));
            File.WriteAllText(Path.Combine(sourceRootPath, "runtime", "runtime_startup_manifest.cpp"), "editor version");
            EditorNativeBuildCache.SyncGeneratedCore(
                sourceRootPath,
                cacheRootPath,
                EditorWindowsNativeBuildCache.BuilderOwnedRelativePaths,
                Path.Combine(Path.GetDirectoryName(cacheRootPath), "generated-core-editor"));

            string unchangedCachePath = Path.Combine(cacheRootPath, "unchanged.cpp");
            string changedCachePath = Path.Combine(cacheRootPath, "changed.cpp");
            string builderOwnedPath = Path.Combine(cacheRootPath, "runtime", "player_settings.cpp");
            string sharedManifestPath = Path.Combine(cacheRootPath, "runtime", "runtime_startup_manifest.cpp");
            Directory.CreateDirectory(Path.GetDirectoryName(builderOwnedPath));
            File.WriteAllText(builderOwnedPath, "builder-owned");
            File.WriteAllText(sharedManifestPath, "windows version");
            DateTime stableTime = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            File.SetLastWriteTimeUtc(unchangedCachePath, stableTime);
            File.SetLastWriteTimeUtc(changedCachePath, stableTime);
            File.WriteAllText(Path.Combine(sourceRootPath, "changed.cpp"), "after");
            File.Delete(Path.Combine(sourceRootPath, "stale.cpp"));

            EditorWindowsNativeBuildCache.SyncGeneratedCore(sourceRootPath, cacheRootPath);

            Assert.Equal(stableTime, File.GetLastWriteTimeUtc(unchangedCachePath));
            Assert.NotEqual(stableTime, File.GetLastWriteTimeUtc(changedCachePath));
            Assert.Equal("after", File.ReadAllText(changedCachePath));
            Assert.False(File.Exists(Path.Combine(cacheRootPath, "stale.cpp")));
            Assert.Equal("builder-owned", File.ReadAllText(builderOwnedPath));
            Assert.Equal("windows version", File.ReadAllText(sharedManifestPath));
        }

        /// <summary>
        /// Places different native build profiles into separate ignored project-cache directories.
        /// </summary>
        [Fact]
        public void ResolveCacheRootPath_isolates_build_profiles() {
            Assert.Equal(
                Path.Combine(TestRootPath, "cache", "build", "windows", "debug"),
                EditorWindowsNativeBuildCache.ResolveCacheRootPath(TestRootPath, "debug"));
            Assert.NotEqual(
                EditorWindowsNativeBuildCache.ResolveCacheRootPath(TestRootPath, "debug"),
                EditorWindowsNativeBuildCache.ResolveCacheRootPath(TestRootPath, "release"));
            Assert.Throws<ArgumentException>(() => EditorWindowsNativeBuildCache.ResolveCacheRootPath(TestRootPath, "../release"));
        }
    }
}
