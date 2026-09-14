using Xunit;

namespace helengine.editor.tests.managers.asset {
    /// <summary>
    /// Verifies typed import-settings persistence through the repository boundary.
    /// </summary>
    public sealed class AssetImportSettingsRepositoryTests : IDisposable {
        /// <summary>
        /// Isolated project root for sidecar persistence.
        /// </summary>
        readonly string ProjectRootPath;

        /// <summary>
        /// Isolated assets root for source files and sidecars.
        /// </summary>
        readonly string AssetsRootPath;

        /// <summary>
        /// Initializes a sidecar repository test.
        /// </summary>
        public AssetImportSettingsRepositoryTests() {
            ProjectRootPath = Path.Combine(AppContext.BaseDirectory, "settings-repository-tests", Guid.NewGuid().ToString("N"));
            AssetsRootPath = Path.Combine(ProjectRootPath, "assets");
            Directory.CreateDirectory(AssetsRootPath);
        }

        /// <summary>
        /// Removes the isolated sidecar fixture.
        /// </summary>
        public void Dispose() {
            if (Directory.Exists(ProjectRootPath)) {
                Directory.Delete(ProjectRootPath, true);
            }
        }

        /// <summary>
        /// Ensures typed settings round-trip through the repository without changing the sidecar format.
        /// </summary>
        [Fact]
        public void SaveAndLoadImportSettings_RoundTripsTypedImporterSelection() {
            string sourcePath = Path.Combine(AssetsRootPath, "menu.txt");
            File.WriteAllText(sourcePath, "menu");
            using ContentManager contentManager = new ContentManager(new HostFileSystemContentStreamSource(AssetsRootPath));
            using AssetImportManager manager = new AssetImportManager(ProjectRootPath, contentManager);
            manager.ImporterRegistry.RegisterTextImporter(new TextImporterRegistration("test-text", new TestTextImporter(), [".txt"]));
            AssetImportSettingsRepository repository = manager.SettingsRepository;

            AssetImportSettings settings = repository.LoadOrCreateImportSettings(sourcePath);
            repository.SaveImportSettings(sourcePath, settings);
            bool loaded = repository.TryLoadOrCreateImportSettings(sourcePath, out AssetImportSettings reloaded);

            Assert.True(loaded);
            Assert.NotNull(reloaded);
            Assert.NotNull(reloaded.Importer);
            Assert.Equal("test-text", reloaded.Importer.ImporterId);
        }

        /// <summary>
        /// Minimal text importer used to create a deterministic default sidecar.
        /// </summary>
        sealed class TestTextImporter : ITextImporter {
            /// <summary>
            /// Imports the source text into a typed text asset.
            /// </summary>
            /// <param name="stream">Source stream.</param>
            /// <returns>Imported text asset.</returns>
            public TextAsset ImportText(Stream stream) {
                throw new NotSupportedException("The settings repository test does not import text.");
            }
        }
    }
}