namespace helengine.editor.tests.managers.asset {
    /// <summary>
    /// Verifies that model file extensions from the shared model catalog are classified as model assets.
    /// </summary>
    public sealed class ModelAssetBrowserIntegrationTests : IDisposable {
        /// <summary>
        /// Temporary project root used for filesystem-backed browser tests.
        /// </summary>
        readonly string ProjectRootPath;

        /// <summary>
        /// Initializes an isolated project root containing an assets folder.
        /// </summary>
        public ModelAssetBrowserIntegrationTests() {
            ProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-model-browser-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(ProjectRootPath, "assets", "Models"));
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
        /// Ensures a DirectX `.x` model extension is classified as a model entry by the asset browser.
        /// </summary>
        [Fact]
        public void LoadEntries_WhenXFileExists_ClassifiesEntryAsModel() {
            string modelPath = Path.Combine(ProjectRootPath, "assets", "Models", "Legacy.x");
            File.WriteAllText(modelPath, "model source");

            EditorAssetManager manager = new EditorAssetManager(ProjectRootPath);
            List<AssetBrowserEntry> entries = new List<AssetBrowserEntry>();
            Assert.True(manager.TryNavigateTo("Models"));

            manager.LoadEntries(entries);

            AssetBrowserEntry entry = Assert.Single(entries);
            Assert.Equal(AssetEntryKind.Model, entry.EntryKind);
        }
    }
}
