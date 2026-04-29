using Xunit;

namespace helengine.editor.tests.managers.asset {
    /// <summary>
    /// Verifies that serialized scene assets are classified correctly by content and browser flows.
    /// </summary>
    public class SceneAssetBrowserIntegrationTests : IDisposable {
        /// <summary>
        /// Temporary project root used for filesystem-backed browser tests.
        /// </summary>
        readonly string ProjectRootPath;

        /// <summary>
        /// Initializes an isolated project root containing an assets folder.
        /// </summary>
        public SceneAssetBrowserIntegrationTests() {
            ProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-scene-browser-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(ProjectRootPath, "assets", "Scenes"));
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
        /// Ensures `.helen` files are classified as scene entries in the asset browser.
        /// </summary>
        [Fact]
        public void LoadEntries_WhenHelenFileExists_ClassifiesEntryAsScene() {
            string scenePath = Path.Combine(ProjectRootPath, "assets", "Scenes", "Sample.helen");
            using (FileStream stream = new FileStream(scenePath, FileMode.Create, FileAccess.Write, FileShare.None)) {
                AssetSerializer.Serialize(stream, new SceneAsset {
                    Id = "Scenes/Sample.helen",
                    RootEntities = Array.Empty<SceneEntityAsset>()
                });
            }

            EditorAssetManager manager = new EditorAssetManager(ProjectRootPath);
            List<AssetBrowserEntry> entries = new List<AssetBrowserEntry>();
            Assert.True(manager.TryNavigateTo("Scenes"));

            manager.LoadEntries(entries);

            AssetBrowserEntry entry = Assert.Single(entries);
            Assert.Equal(AssetEntryKind.Scene, entry.EntryKind);
        }
    }
}
