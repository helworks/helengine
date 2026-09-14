using Xunit;

namespace helengine.editor.tests.managers.asset {
    /// <summary>
    /// Verifies typed runtime resolution delegates to the manager cache policy.
    /// </summary>
    public sealed class ImportedAssetRuntimeResolverTests : IDisposable {
        /// <summary>
        /// Isolated project root for imported output.
        /// </summary>
        readonly string ProjectRootPath;

        /// <summary>
        /// Isolated assets root for source files.
        /// </summary>
        readonly string AssetsRootPath;

        /// <summary>
        /// Initializes one resolver fixture.
        /// </summary>
        public ImportedAssetRuntimeResolverTests() {
            ProjectRootPath = Path.Combine(AppContext.BaseDirectory, "runtime-resolver-tests", Guid.NewGuid().ToString("N"));
            AssetsRootPath = Path.Combine(ProjectRootPath, "assets");
            Directory.CreateDirectory(AssetsRootPath);
        }

        /// <summary>
        /// Deletes the isolated resolver fixture.
        /// </summary>
        public void Dispose() {
            if (Directory.Exists(ProjectRootPath)) {
                Directory.Delete(ProjectRootPath, true);
            }
        }

        /// <summary>
        /// Ensures a published text asset resolves from its source through the runtime resolver.
        /// </summary>
        [Fact]
        public void TryResolveText_WhenCacheWasPublished_ReturnsRuntimeAsset() {
            string sourcePath = Path.Combine(AssetsRootPath, "menu.txt");
            File.WriteAllText(sourcePath, "source");
            using ContentManager contentManager = new ContentManager(new HostFileSystemContentStreamSource(AssetsRootPath));
            using AssetImportManager manager = new AssetImportManager(ProjectRootPath, contentManager);
            manager.ImporterRegistry.RegisterTextImporter(new TextImporterRegistration("test-text", new TestTextImporter(), [".txt"]));
            manager.ExecutionService.ImportText(sourcePath);

            bool resolved = manager.RuntimeResolver.TryResolveText(sourcePath, out TextAsset asset);

            Assert.True(resolved);
            Assert.NotNull(asset);
            Assert.Equal("processed", asset.Text);
        }

        /// <summary>
        /// Provides one deterministic imported text asset.
        /// </summary>
        sealed class TestTextImporter : ITextImporter {
            /// <summary>
            /// Produces a deterministic text asset for resolver tests.
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