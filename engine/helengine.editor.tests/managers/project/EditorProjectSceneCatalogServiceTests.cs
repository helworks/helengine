using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies project scene catalog ids are derived from scene asset names and can be resolved back to authored paths.
    /// </summary>
    public sealed class EditorProjectSceneCatalogServiceTests : IDisposable {
        /// <summary>
        /// Gets the isolated temporary project root used by the current test instance.
        /// </summary>
        string TempProjectRootPath { get; }

        /// <summary>
        /// Initializes one isolated temporary project root for scene catalog tests.
        /// </summary>
        public EditorProjectSceneCatalogServiceTests() {
            TempProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-project-scene-catalog-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(TempProjectRootPath, "assets", "Scenes"));
            Directory.CreateDirectory(Path.Combine(TempProjectRootPath, "assets", "Levels"));
        }

        /// <summary>
        /// Deletes the isolated temporary project root after the current test completes.
        /// </summary>
        public void Dispose() {
            if (Directory.Exists(TempProjectRootPath)) {
                Directory.Delete(TempProjectRootPath, true);
            }
        }

        /// <summary>
        /// Ensures scene ids are derived from the authored scene asset file names instead of project-relative paths.
        /// </summary>
        [Fact]
        public void GetSceneIds_WhenScenesExist_ReturnsSceneAssetNamesWithoutExtensions() {
            WriteScene("Scenes/DemoDiscMainMenu.helen");
            WriteScene("Levels/CubeTest.helen");

            EditorProjectSceneCatalogService service = new EditorProjectSceneCatalogService(TempProjectRootPath);

            Assert.Equal(new[] { "CubeTest", "DemoDiscMainMenu" }, service.GetSceneIds());
        }

        /// <summary>
        /// Ensures a unique scene id resolves back to its project-relative authored scene path.
        /// </summary>
        [Fact]
        public void ResolveScenePath_WhenSceneIdMatchesOneScene_ReturnsProjectRelativePath() {
            WriteScene("Scenes/DemoDiscMainMenu.helen");
            WriteScene("Levels/CubeTest.helen");

            EditorProjectSceneCatalogService service = new EditorProjectSceneCatalogService(TempProjectRootPath);

            Assert.Equal("Scenes/DemoDiscMainMenu.helen", service.ResolveScenePath("DemoDiscMainMenu"));
            Assert.Equal("Levels/CubeTest.helen", service.ResolveScenePath("CubeTest"));
        }

        /// <summary>
        /// Ensures duplicate scene ids fail fast instead of letting editor callers pick an arbitrary scene path.
        /// </summary>
        [Fact]
        public void ResolveScenePath_WhenSceneIdMatchesMultipleScenes_ThrowsInvalidOperationException() {
            WriteScene("Scenes/DemoDiscMainMenu.helen");
            WriteScene("Levels/DemoDiscMainMenu.helen");

            EditorProjectSceneCatalogService service = new EditorProjectSceneCatalogService(TempProjectRootPath);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => service.ResolveScenePath("DemoDiscMainMenu"));
            Assert.Contains("DemoDiscMainMenu", exception.Message, StringComparison.Ordinal);
        }

        /// <summary>
        /// Writes one empty scene file that the scene catalog can enumerate.
        /// </summary>
        /// <param name="sceneRelativePath">Project-relative scene asset path to create beneath `assets`.</param>
        void WriteScene(string sceneRelativePath) {
            string scenePath = Path.Combine(TempProjectRootPath, "assets", sceneRelativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(scenePath));
            File.WriteAllText(scenePath, string.Empty);
        }
    }
}
