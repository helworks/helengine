using System.Collections.Generic;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests.managers.asset {
    /// <summary>
    /// Verifies merged filesystem and generated navigation in the asset browser data source.
    /// </summary>
    public class AssetBrowserDataSourceTests : IDisposable {
        /// <summary>
        /// Temporary project root used for filesystem-backed browser tests.
        /// </summary>
        readonly string ProjectRootPath;

        /// <summary>
        /// Initializes an isolated project root for each test.
        /// </summary>
        public AssetBrowserDataSourceTests() {
            ProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-asset-browser-data-source-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(ProjectRootPath, "assets", "Scenes"));
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
        /// Ensures the root entry list merges filesystem folders and generated folders.
        /// </summary>
        [Fact]
        public void LoadEntries_WhenAtRoot_MergesFileSystemAndGeneratedEntries() {
            using GeneratedAssetProviderRegistry registry = new GeneratedAssetProviderRegistry();
            registry.Register(new TestGeneratedAssetProvider(
                "engine",
                new[] {
                    AssetBrowserEntry.CreateGeneratedDirectory("Engine", "Engine", "engine")
                },
                new TestRuntimeModel()));

            using AssetBrowserDataSource dataSource = new AssetBrowserDataSource(new EditorAssetManager(ProjectRootPath), registry);
            List<AssetBrowserEntry> entries = new List<AssetBrowserEntry>();

            dataSource.LoadEntries(entries);

            Assert.Contains(entries, entry => entry.Name == "Scenes" && !entry.IsGenerated);
            Assert.Contains(entries, entry => entry.Name == "Engine" && entry.IsGenerated);
        }

        /// <summary>
        /// Ensures the generated engine root folder stays pinned ahead of other directory entries at the asset root.
        /// </summary>
        [Fact]
        public void LoadEntries_WhenAtRoot_PinsGeneratedEngineFolderBeforeOtherDirectories() {
            Directory.CreateDirectory(Path.Combine(ProjectRootPath, "assets", "Aardvark"));
            using GeneratedAssetProviderRegistry registry = new GeneratedAssetProviderRegistry();
            registry.Register(new TestGeneratedAssetProvider(
                "engine",
                new[] {
                    AssetBrowserEntry.CreateGeneratedDirectory("Engine", "Engine", "engine")
                },
                new TestRuntimeModel()));

            using AssetBrowserDataSource dataSource = new AssetBrowserDataSource(new EditorAssetManager(ProjectRootPath), registry);
            List<AssetBrowserEntry> entries = new List<AssetBrowserEntry>();

            dataSource.LoadEntries(entries);

            Assert.Equal("Engine", entries[0].Name);
            Assert.True(entries[0].IsEngineGeneratedRootDirectory);
            Assert.Equal("Aardvark", entries[1].Name);
            Assert.Equal("Scenes", entries[2].Name);
        }

        /// <summary>
        /// Ensures generated navigation switches the browser into read-only mode.
        /// </summary>
        [Fact]
        public void TryNavigateTo_WhenPathIsGenerated_DisablesFileCreation() {
            using GeneratedAssetProviderRegistry registry = new GeneratedAssetProviderRegistry();
            registry.Register(new TestGeneratedAssetProvider(
                "engine",
                new[] {
                    AssetBrowserEntry.CreateGeneratedDirectory("Engine", "Engine", "engine"),
                    AssetBrowserEntry.CreateGeneratedDirectory("Models", "Engine/Models", "engine")
                },
                new TestRuntimeModel()));

            using AssetBrowserDataSource dataSource = new AssetBrowserDataSource(new EditorAssetManager(ProjectRootPath), registry);

            Assert.True(dataSource.TryNavigateTo("Engine"));
            Assert.False(dataSource.CanCreateFileSystemEntries);
        }

        /// <summary>
        /// Ensures filesystem-only browser mode suppresses generated roots.
        /// </summary>
        [Fact]
        public void LoadEntries_WhenGeneratedRootsAreDisabled_DoesNotAppendGeneratedRoots() {
            using GeneratedAssetProviderRegistry registry = new GeneratedAssetProviderRegistry();
            registry.Register(new TestGeneratedAssetProvider(
                "engine",
                new[] {
                    AssetBrowserEntry.CreateGeneratedDirectory("Engine", "Engine", "engine")
                },
                new TestRuntimeModel()));

            using AssetBrowserDataSource dataSource = new AssetBrowserDataSource(new EditorAssetManager(ProjectRootPath), registry, false);
            List<AssetBrowserEntry> entries = new List<AssetBrowserEntry>();

            dataSource.LoadEntries(entries);

            Assert.DoesNotContain(entries, entry => entry.IsGenerated);
        }
    }
}
