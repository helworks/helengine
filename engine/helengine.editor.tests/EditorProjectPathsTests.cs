using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies editor-owned project paths include the generated code workspace outside authored assets.
    /// </summary>
    public sealed class EditorProjectPathsTests {
        /// <summary>
        /// Ensures initializing editor project paths resolves the generated code workspace beneath `user_settings`.
        /// </summary>
        [Fact]
        public void Initialize_WhenProjectRootIsProvided_ResolvesGeneratedCodeWorkspaceOutsideAssets() {
            string projectRootPath = Path.Combine(Path.GetTempPath(), "helengine-project-path-tests", Guid.NewGuid().ToString("N"));

            EditorProjectPaths.Initialize(projectRootPath);

            Assert.Equal(Path.Combine(Path.GetFullPath(projectRootPath), "user_settings", "generated_code"), EditorProjectPaths.GeneratedCodeRoot);
            Assert.Equal(Path.Combine(Path.GetFullPath(projectRootPath), "user_settings", "generated_code", "projects"), EditorProjectPaths.GeneratedCodeProjectsRoot);
        }
    }
}
