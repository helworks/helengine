using Xunit;

namespace helengine.editor.tests.managers.asset {
    /// <summary>
    /// Verifies typed import execution delegates to the manager-owned pipeline.
    /// </summary>
    public sealed class AssetImportExecutionServiceTests : IDisposable {
        /// <summary>
        /// Isolated project root for imported output.
        /// </summary>
        readonly string ProjectRootPath;

        /// <summary>
        /// Isolated assets root for source files.
        /// </summary>
        readonly string AssetsRootPath;

        /// <summary>
        /// Initializes one execution fixture.
        /// </summary>
        public AssetImportExecutionServiceTests() {
            ProjectRootPath = Path.Combine(AppContext.BaseDirectory, "execution-service-tests", Guid.NewGuid().ToString("N"));
            AssetsRootPath = Path.Combine(ProjectRootPath, "assets");
            Directory.CreateDirectory(AssetsRootPath);
        }

        /// <summary>
        /// Deletes the isolated execution fixture.
        /// </summary>
        public void Dispose() {
            if (Directory.Exists(ProjectRootPath)) {
                Directory.Delete(ProjectRootPath, true);
            }
        }

        /// <summary>
        /// Ensures text execution imports and publishes through the existing manager pipeline.
        /// </summary>
        [Fact]
        public void ImportText_DelegatesToManagerAndPublishesCacheAsset() {
            string sourcePath = WriteSource("menu.txt");
            using ContentManager contentManager = new ContentManager(new HostFileSystemContentStreamSource(AssetsRootPath));
            using AssetImportManager manager = new AssetImportManager(ProjectRootPath, contentManager);
            manager.ImporterRegistry.RegisterTextImporter(new TextImporterRegistration("test-text", new TestTextImporter(), [".txt"]));

            TextAsset asset = manager.ExecutionService.ImportText(sourcePath);

            Assert.NotNull(asset);
            Assert.Equal("processed", asset.Text);
            Assert.False(string.IsNullOrWhiteSpace(asset.Id));
            Assert.True(File.Exists(Path.Combine(ProjectRootPath, "cache", asset.Id)));
        }

        /// <summary>
        /// Writes one deterministic text source file.
        /// </summary>
        /// <param name="fileName">Source file name.</param>
        /// <returns>Absolute source path.</returns>
        string WriteSource(string fileName) {
            string sourcePath = Path.Combine(AssetsRootPath, fileName);
            File.WriteAllText(sourcePath, "source");
            return sourcePath;
        }

        /// <summary>
        /// Provides one deterministic imported text asset.
        /// </summary>
        sealed class TestTextImporter : ITextImporter {
            /// <summary>
            /// Produces a deterministic text asset for execution tests.
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