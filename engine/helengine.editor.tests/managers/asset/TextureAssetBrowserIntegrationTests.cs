using Xunit;

namespace helengine.editor.tests.managers.asset {
    /// <summary>
    /// Verifies that texture file extensions from the shared texture catalog are classified as image assets.
    /// </summary>
    public sealed class TextureAssetBrowserIntegrationTests : IDisposable {
        /// <summary>
        /// Temporary project root used for filesystem-backed browser tests.
        /// </summary>
        readonly string ProjectRootPath;

        /// <summary>
        /// Initializes an isolated project root containing an assets folder.
        /// </summary>
        public TextureAssetBrowserIntegrationTests() {
            ProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-texture-browser-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(ProjectRootPath, "assets", "Textures"));
        }

        /// <summary>
        /// Deletes temporary filesystem state after each test.
        /// </summary>
        public void Dispose() {
            if (Directory.Exists(ProjectRootPath)) {
                Directory.Delete(ProjectRootPath, true);
            }
        }

        /// <summary>
        /// Ensures a representative modern texture extension is classified as an image entry by the asset browser.
        /// </summary>
        [Fact]
        public void LoadEntries_WhenWebpFileExists_ClassifiesEntryAsImage() {
            string texturePath = Path.Combine(ProjectRootPath, "assets", "Textures", "Diffuse.webp");
            File.WriteAllBytes(texturePath, new byte[] { 1, 2, 3, 4 });

            EditorAssetManager manager = new EditorAssetManager(ProjectRootPath);
            List<AssetBrowserEntry> entries = new List<AssetBrowserEntry>();
            Assert.True(manager.TryNavigateTo("Textures"));

            manager.LoadEntries(entries);

            AssetBrowserEntry entry = Assert.Single(entries);
            Assert.Equal(AssetEntryKind.Image, entry.EntryKind);
        }
    }
}
