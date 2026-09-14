using Xunit;

namespace helengine.editor.tests.managers.asset {
    /// <summary>
    /// Verifies the importer registry bootstrap and freeze boundary.
    /// </summary>
    public sealed class AssetImporterRegistryTests : IDisposable {
        /// <summary>
        /// Project root used only for manager-owned importer state.
        /// </summary>
        readonly string ProjectRootPath;

        /// <summary>
        /// Content manager used by importer registrations.
        /// </summary>
        readonly ContentManager ContentManager;

        /// <summary>
        /// Initializes a registry test with an isolated project path.
        /// </summary>
        public AssetImporterRegistryTests() {
            ProjectRootPath = Path.Combine(AppContext.BaseDirectory, "registry-tests", Guid.NewGuid().ToString("N"));
            ContentManager = new ContentManager(new HostFileSystemContentStreamSource(AppContext.BaseDirectory));
        }

        /// <summary>
        /// Releases test content resources.
        /// </summary>
        public void Dispose() {
            ContentManager.Dispose();
            if (Directory.Exists(ProjectRootPath)) {
                Directory.Delete(ProjectRootPath, true);
            }
        }

        /// <summary>
        /// Ensures typed registration preserves importer identity and extension selection behavior.
        /// </summary>
        [Fact]
        public void RegisterTextureImporter_WhenExtensionIsRegistered_ExposesTypedSelection() {
            using AssetImportManager manager = new AssetImportManager(ProjectRootPath, ContentManager);
            AssetImporterRegistry registry = manager.ImporterRegistry;
            registry.RegisterTextureImporter(new TextureImporterRegistration("test-texture", new TestTextureImporter(), [".PNG"]));

            Assert.Equal(["test-texture"], registry.GetTextureImporterIds());
            Assert.Equal(["test-texture"], registry.GetImporterIdsForExtension(".png"));
        }

        /// <summary>
        /// Ensures host registration cannot mutate an active session after freeze.
        /// </summary>
        [Fact]
        public void RegisterAfterFreeze_ThrowsInvalidOperationException() {
            using AssetImportManager manager = new AssetImportManager(ProjectRootPath, ContentManager);
            AssetImporterRegistry registry = manager.ImporterRegistry;
            registry.RegisterTextureImporter(new TextureImporterRegistration("test-texture", new TestTextureImporter(), [".png"]));
            registry.Freeze();

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => registry.RegisterTextureImporter(new TextureImporterRegistration("second", new TestTextureImporter(), [".jpg"])));

            Assert.Contains("frozen", exception.Message, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Minimal texture importer used to exercise registration maps without importing a file.
        /// </summary>
        sealed class TestTextureImporter : ITextureImporter {
            /// <summary>
            /// Fails if the registry test accidentally performs an import.
            /// </summary>
            /// <param name="stream">Source stream.</param>
            /// <returns>No asset; this test only exercises registration.</returns>
            public TextureAsset ImportTexture(Stream stream) {
                throw new NotSupportedException("The registry test does not import textures.");
            }
        }
    }
}