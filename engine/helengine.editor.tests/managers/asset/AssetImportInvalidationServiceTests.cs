using Xunit;

namespace helengine.editor.tests.managers.asset {
    /// <summary>
    /// Verifies explicit source invalidation and typed execution ownership.
    /// </summary>
    public sealed class AssetImportInvalidationServiceTests : IDisposable {
        /// <summary>
        /// Isolated project root for imported output.
        /// </summary>
        readonly string ProjectRootPath;

        /// <summary>
        /// Isolated assets root for source files.
        /// </summary>
        readonly string AssetsRootPath;

        /// <summary>
        /// Initializes one invalidation fixture.
        /// </summary>
        public AssetImportInvalidationServiceTests() {
            ProjectRootPath = Path.Combine(AppContext.BaseDirectory, "invalidation-service-tests", Guid.NewGuid().ToString("N"));
            AssetsRootPath = Path.Combine(ProjectRootPath, "assets");
            Directory.CreateDirectory(AssetsRootPath);
        }

        /// <summary>
        /// Deletes the isolated invalidation fixture.
        /// </summary>
        public void Dispose() {
            if (Directory.Exists(ProjectRootPath)) {
                Directory.Delete(ProjectRootPath, true);
            }
        }

        /// <summary>
        /// Ensures one invalidated source executes once and clears its marker.
        /// </summary>
        [Fact]
        public void TryExecuteText_WhenSourceIsInvalidated_ExecutesOnce() {
            string sourcePath = Path.Combine(AssetsRootPath, "menu.txt");
            File.WriteAllText(sourcePath, "source");
            using ContentManager contentManager = new ContentManager(new HostFileSystemContentStreamSource(AssetsRootPath));
            using AssetImportManager manager = new AssetImportManager(ProjectRootPath, contentManager);
            manager.ImporterRegistry.RegisterTextImporter(new TextImporterRegistration("test-text", new TestTextImporter(), [".txt"]));

            manager.InvalidationService.InvalidateSource(sourcePath);
            Assert.True(manager.InvalidationService.IsInvalidated(sourcePath));
            Assert.True(manager.InvalidationService.TryExecuteText(sourcePath, out TextAsset asset));
            Assert.False(manager.InvalidationService.IsInvalidated(sourcePath));
            Assert.False(manager.InvalidationService.TryExecuteText(sourcePath, out _));
            Assert.Equal("processed", asset.Text);
        }

        /// <summary>
        /// Ensures disposal rejects future invalidation operations.
        /// </summary>
        [Fact]
        public void Dispose_WhenCalled_RejectsFutureInvalidation() {
            using ContentManager contentManager = new ContentManager(new HostFileSystemContentStreamSource(AssetsRootPath));
            using AssetImportManager manager = new AssetImportManager(ProjectRootPath, contentManager);
            AssetImportInvalidationService service = new AssetImportInvalidationService(manager.ExecutionService);
            service.Dispose();

            Assert.Throws<ObjectDisposedException>(() => service.InvalidateSource(Path.Combine(AssetsRootPath, "menu.txt")));
        }

        /// <summary>
        /// Provides one deterministic imported text asset.
        /// </summary>
        sealed class TestTextImporter : ITextImporter {
            /// <summary>
            /// Produces a deterministic text asset for invalidation tests.
            /// </summary>
            /// <param name="stream">Source stream.</param>
            /// <returns>Imported text asset.</returns>
            public TextAsset ImportText(Stream stream) {
                return new TextAsset {
                    Text = "processed"
                };
            }
        }
    }
}