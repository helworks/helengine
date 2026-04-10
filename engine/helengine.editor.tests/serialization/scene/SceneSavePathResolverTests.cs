using helengine.editor;
using Xunit;

namespace helengine.editor.tests.serialization.scene {
    /// <summary>
    /// Verifies path validation and default naming for editor scene saves.
    /// </summary>
    public class SceneSavePathResolverTests : IDisposable {
        /// <summary>
        /// Temporary project root used by path resolution tests.
        /// </summary>
        readonly string ProjectRootPath;

        /// <summary>
        /// Initializes an isolated project root with an assets folder.
        /// </summary>
        public SceneSavePathResolverTests() {
            ProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-scene-save-path-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(ProjectRootPath, "assets", "Scenes"));
        }

        /// <summary>
        /// Deletes temporary project state after each test.
        /// </summary>
        public void Dispose() {
            if (Directory.Exists(ProjectRootPath)) {
                Directory.Delete(ProjectRootPath, true);
            }
        }

        /// <summary>
        /// Ensures save paths default to the `Scenes` folder when no current scene exists.
        /// </summary>
        [Fact]
        public void GetInitialRelativeDirectory_WhenCurrentScenePathIsEmpty_ReturnsScenes() {
            SceneSavePathResolver resolver = new SceneSavePathResolver(ProjectRootPath);

            Assert.Equal(SceneSavePathResolver.DefaultSceneDirectory, resolver.GetInitialRelativeDirectory(string.Empty));
        }

        /// <summary>
        /// Ensures missing `.helen` extensions are appended automatically.
        /// </summary>
        [Fact]
        public void BuildFullPath_WhenNameOmitsExtension_AppendsHelenExtension() {
            SceneSavePathResolver resolver = new SceneSavePathResolver(ProjectRootPath);
            string scenesDirectory = Path.Combine(ProjectRootPath, "assets", "Scenes");

            string fullPath = resolver.BuildFullPath(scenesDirectory, "Prototype");

            Assert.EndsWith(Path.Combine("assets", "Scenes", "Prototype.helen"), fullPath, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Ensures invalid scene file names fail clearly instead of producing broken paths.
        /// </summary>
        [Fact]
        public void BuildFullPath_WhenNameContainsInvalidCharacters_Throws() {
            SceneSavePathResolver resolver = new SceneSavePathResolver(ProjectRootPath);
            string scenesDirectory = Path.Combine(ProjectRootPath, "assets", "Scenes");

            Assert.Throws<InvalidOperationException>(() => resolver.BuildFullPath(scenesDirectory, "bad:name"));
        }
    }
}
