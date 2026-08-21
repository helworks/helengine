namespace helengine.editor.tests {
    /// <summary>
    /// Verifies per-project editor session state persists and restores the last open scene.
    /// </summary>
    public sealed class EditorSessionStateServiceTests : IDisposable {
        /// <summary>
        /// Temporary project root used by the state tests.
        /// </summary>
        readonly string ProjectRootPath;

        /// <summary>
        /// Creates one isolated temporary project root per test.
        /// </summary>
        public EditorSessionStateServiceTests() {
            ProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-session-state-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(ProjectRootPath);
        }

        /// <summary>
        /// Deletes the temporary project root.
        /// </summary>
        public void Dispose() {
            if (Directory.Exists(ProjectRootPath)) {
                Directory.Delete(ProjectRootPath, true);
            }
        }

        /// <summary>
        /// Ensures the last scene path round-trips through the state file as a project-relative path.
        /// </summary>
        [Fact]
        public void SetLastScenePath_WithProjectScene_RoundTripsRelativePath() {
            EditorSessionStateService service = new EditorSessionStateService(ProjectRootPath);
            string scenePath = Path.Combine(ProjectRootPath, "assets", "scenes", "level_01.helen");

            service.SetLastScenePath(scenePath);

            string stateText = File.ReadAllText(Path.Combine(ProjectRootPath, "user_settings", "editor_session.json"));
            Assert.DoesNotContain(ProjectRootPath.Replace('\\', '/'), stateText.Replace("\\\\", "/").Replace('\\', '/'));
            Assert.Equal(Path.GetFullPath(scenePath), new EditorSessionStateService(ProjectRootPath).TryGetLastScenePath());
        }

        /// <summary>
        /// Ensures scenes outside the project root round-trip as absolute paths.
        /// </summary>
        [Fact]
        public void SetLastScenePath_WithExternalScene_RoundTripsAbsolutePath() {
            EditorSessionStateService service = new EditorSessionStateService(ProjectRootPath);
            string externalScenePath = Path.Combine(Path.GetTempPath(), "helengine-session-state-tests", "external.helen");

            service.SetLastScenePath(externalScenePath);

            Assert.Equal(Path.GetFullPath(externalScenePath), service.TryGetLastScenePath());
        }

        /// <summary>
        /// Ensures a missing state file reads as no recorded scene.
        /// </summary>
        [Fact]
        public void TryGetLastScenePath_WithoutStateFile_ReturnsNull() {
            Assert.Null(new EditorSessionStateService(ProjectRootPath).TryGetLastScenePath());
        }

        /// <summary>
        /// Ensures a corrupt state file reads as no recorded scene instead of throwing.
        /// </summary>
        [Fact]
        public void TryGetLastScenePath_WithCorruptStateFile_ReturnsNull() {
            string settingsDirectory = Path.Combine(ProjectRootPath, "user_settings");
            Directory.CreateDirectory(settingsDirectory);
            File.WriteAllText(Path.Combine(settingsDirectory, "editor_session.json"), "{not json");

            Assert.Null(new EditorSessionStateService(ProjectRootPath).TryGetLastScenePath());
        }
    }
}
