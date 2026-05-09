using helengine.ui.managers;

namespace helengine.editor.tests.managers.asset {
    /// <summary>
    /// Verifies the Assimp-backed model format catalog is normalized and available to editor consumers.
    /// </summary>
    public sealed class AssimpModelFormatCatalogTests {
        /// <summary>
        /// Ensures the shared catalog exposes the common Assimp model extensions with editor-friendly normalization.
        /// </summary>
        [Fact]
        public void AllModelExtensions_WhenLoaded_ContainsCommonAssimpFormats() {
            IReadOnlyList<string> modelExtensions = AssimpModelFormatCatalog.AllModelExtensions;

            Assert.NotEmpty(modelExtensions);
            Assert.Contains(".obj", modelExtensions);
            Assert.Contains(".fbx", modelExtensions);
            Assert.Contains(".dae", modelExtensions);
            Assert.Contains(".3ds", modelExtensions);
            Assert.Contains(".blend", modelExtensions);
            Assert.Contains(".gltf", modelExtensions);
            Assert.Contains(".glb", modelExtensions);
            Assert.Contains(".x", modelExtensions);
        }

        /// <summary>
        /// Ensures the asset cache recognizes the Assimp catalog as supported asset content.
        /// </summary>
        [Fact]
        public void AssetCache_WhenCatalogExtensionsAreChecked_TracksAsSupportedAssets() {
            AssetCache cache = new AssetCache();

            Assert.All(AssimpModelFormatCatalog.AllModelExtensions, extension => Assert.Contains(extension, cache.GetSupportedExtensions()));
        }
    }
}
