using Xunit;

namespace helengine.editor.tests.managers.project {
    /// <summary>
    /// Verifies the source-build workspace locator can resolve the HelEngine source root even when the editor binary runs outside the repository tree.
    /// </summary>
    public sealed class EditorSourceBuildWorkspaceLocatorTests : IDisposable {
        /// <summary>
        /// Name of the environment variable used to override source-root discovery.
        /// </summary>
        const string HelEngineSourceRootEnvironmentVariableName = "HELENGINE_SOURCE_ROOT";

        /// <summary>
        /// Name of the environment variable used to override engine user-settings discovery.
        /// </summary>
        const string EngineUserSettingsRootEnvironmentVariableName = "HELENGINE_ENGINE_USER_SETTINGS_ROOT";

        /// <summary>
        /// Temporary repository root used by the current test instance.
        /// </summary>
        readonly string TemporaryRepositoryRootPath;

        /// <summary>
        /// Original environment-variable value restored after the test completes.
        /// </summary>
        readonly string OriginalEnvironmentVariableValue;

        /// <summary>
        /// Original engine user-settings environment-variable value restored after the test completes.
        /// </summary>
        readonly string OriginalEngineUserSettingsRootEnvironmentVariableValue;

        /// <summary>
        /// Initializes one temporary source-root override test fixture.
        /// </summary>
        public EditorSourceBuildWorkspaceLocatorTests() {
            TemporaryRepositoryRootPath = Path.Combine(Path.GetTempPath(), "helengine-source-root-locator-tests", Guid.NewGuid().ToString("N"));
            OriginalEnvironmentVariableValue = Environment.GetEnvironmentVariable(HelEngineSourceRootEnvironmentVariableName);
            OriginalEngineUserSettingsRootEnvironmentVariableValue = Environment.GetEnvironmentVariable(EngineUserSettingsRootEnvironmentVariableName);
            Directory.CreateDirectory(Path.Combine(TemporaryRepositoryRootPath, "engine", "helengine.editor"));
            File.WriteAllText(Path.Combine(TemporaryRepositoryRootPath, "engine", "helengine.editor", "helengine.editor.csproj"), "<Project />");
        }

        /// <summary>
        /// Restores the original environment-variable state and deletes the temporary repository root after each test.
        /// </summary>
        public void Dispose() {
            Environment.SetEnvironmentVariable(HelEngineSourceRootEnvironmentVariableName, OriginalEnvironmentVariableValue);
            Environment.SetEnvironmentVariable(EngineUserSettingsRootEnvironmentVariableName, OriginalEngineUserSettingsRootEnvironmentVariableValue);
            if (Directory.Exists(TemporaryRepositoryRootPath)) {
                Directory.Delete(TemporaryRepositoryRootPath, true);
            }
        }

        /// <summary>
        /// Ensures the locator honors an explicit source-root override before falling back to the runtime output layout.
        /// </summary>
        [Fact]
        public void ResolveHelEngineRootPath_WhenEnvironmentOverrideIsProvided_ReturnsOverrideRoot() {
            Environment.SetEnvironmentVariable(HelEngineSourceRootEnvironmentVariableName, TemporaryRepositoryRootPath);
            EditorSourceBuildWorkspaceLocator locator = new();

            string resolvedRootPath = locator.ResolveHelEngineRootPath();

            Assert.Equal(Path.GetFullPath(TemporaryRepositoryRootPath), resolvedRootPath);
        }

        /// <summary>
        /// Ensures the locator canonicalizes and returns an explicit engine user-settings root.
        /// </summary>
        [Fact]
        public void ResolveSharedEngineUserSettingsRootPath_WhenEnvironmentOverrideIsProvided_ReturnsCanonicalOverrideRoot() {
            string configuredRootPath = Path.Combine(TemporaryRepositoryRootPath, "settings-parent", "..", "engine-user-settings");
            Environment.SetEnvironmentVariable(EngineUserSettingsRootEnvironmentVariableName, configuredRootPath);
            EditorSourceBuildWorkspaceLocator locator = new();

            string resolvedRootPath = locator.ResolveSharedEngineUserSettingsRootPath();

            Assert.Equal(Path.GetFullPath(configuredRootPath), resolvedRootPath);
        }

        /// <summary>
        /// Ensures missing engine user-settings overrides preserve shared source-root discovery.
        /// </summary>
        [Fact]
        public void ResolveSharedEngineUserSettingsRootPath_WhenEnvironmentOverrideIsAbsent_ReturnsSharedSourceRootSettings() {
            Environment.SetEnvironmentVariable(HelEngineSourceRootEnvironmentVariableName, TemporaryRepositoryRootPath);
            Environment.SetEnvironmentVariable(EngineUserSettingsRootEnvironmentVariableName, null);
            EditorSourceBuildWorkspaceLocator locator = new();

            string resolvedRootPath = locator.ResolveSharedEngineUserSettingsRootPath();

            Assert.Equal(Path.Combine(Path.GetFullPath(TemporaryRepositoryRootPath), "user_settings"), resolvedRootPath);
        }

        /// <summary>
        /// Ensures a worktree whose conventional parent does not contain the main checkout resolves that checkout from its standard gitdir pointer.
        /// </summary>
        [Fact]
        public void ResolveSharedHelEngineRootPath_WhenWorktreeParentDiffersFromMainCheckout_UsesGitdirPointer() {
            const string worktreeName = "feature-copy";
            string worktreeRootPath = Path.Combine(TemporaryRepositoryRootPath, "alternate-workspace", ".worktrees", worktreeName);
            string worktreeMarkerPath = Path.Combine(worktreeRootPath, "engine", "helengine.editor");
            string gitMetadataPath = Path.Combine(TemporaryRepositoryRootPath, ".git", "worktrees", worktreeName);
            Directory.CreateDirectory(worktreeMarkerPath);
            Directory.CreateDirectory(gitMetadataPath);
            File.WriteAllText(Path.Combine(worktreeMarkerPath, "helengine.editor.csproj"), "<Project />");
            File.WriteAllText(Path.Combine(worktreeRootPath, ".git"), "gitdir: " + gitMetadataPath);
            Environment.SetEnvironmentVariable(HelEngineSourceRootEnvironmentVariableName, worktreeRootPath);

            EditorSourceBuildWorkspaceLocator locator = new();

            string resolvedRootPath = locator.ResolveSharedHelEngineRootPath();

            Assert.Equal(Path.GetFullPath(TemporaryRepositoryRootPath), resolvedRootPath);
        }
    }
}
