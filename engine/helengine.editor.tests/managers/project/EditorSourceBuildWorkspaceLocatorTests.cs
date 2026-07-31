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
        /// Name of the environment variable used to isolate shared source-build platform settings.
        /// </summary>
        const string SharedUserSettingsRootEnvironmentVariableName = "HELENGINE_SHARED_USER_SETTINGS_ROOT";

        /// <summary>
        /// Temporary repository root used by the current test instance.
        /// </summary>
        readonly string TemporaryRepositoryRootPath;

        /// <summary>
        /// Original environment-variable value restored after the test completes.
        /// </summary>
        readonly string OriginalEnvironmentVariableValue;

        /// <summary>
        /// Original shared-user-settings override restored after the test completes.
        /// </summary>
        readonly string OriginalSharedUserSettingsEnvironmentVariableValue;

        /// <summary>
        /// Initializes one temporary source-root override test fixture.
        /// </summary>
        public EditorSourceBuildWorkspaceLocatorTests() {
            TemporaryRepositoryRootPath = ResolveVisibleTestRootPath();
            OriginalEnvironmentVariableValue = Environment.GetEnvironmentVariable(HelEngineSourceRootEnvironmentVariableName);
            OriginalSharedUserSettingsEnvironmentVariableValue = Environment.GetEnvironmentVariable(SharedUserSettingsRootEnvironmentVariableName);
            Directory.CreateDirectory(Path.Combine(TemporaryRepositoryRootPath, "engine", "helengine.editor"));
            File.WriteAllText(Path.Combine(TemporaryRepositoryRootPath, "engine", "helengine.editor", "helengine.editor.csproj"), "<Project />");
        }

        /// <summary>
        /// Resolves a visible workspace build path without using an operating-system temporary directory.
        /// </summary>
        /// <returns>Unique test root beneath the workspace's visible builds directory.</returns>
        static string ResolveVisibleTestRootPath() {
            EditorSourceBuildWorkspaceLocator locator = new EditorSourceBuildWorkspaceLocator();
            string sharedHelEngineRootPath = locator.ResolveSharedHelEngineRootPath();
            DirectoryInfo workspaceDirectory = Directory.GetParent(sharedHelEngineRootPath);
            if (workspaceDirectory == null) {
                throw new InvalidOperationException("The source-build workspace parent directory could not be resolved for locator tests.");
            }

            return Path.Combine(
                workspaceDirectory.FullName,
                "builds",
                "helphysics-demodisc-stack-test",
                "tests",
                "source-root-locator",
                Guid.NewGuid().ToString("N"));
        }

        /// <summary>
        /// Restores the original environment-variable state and deletes the temporary repository root after each test.
        /// </summary>
        public void Dispose() {
            Environment.SetEnvironmentVariable(HelEngineSourceRootEnvironmentVariableName, OriginalEnvironmentVariableValue);
            Environment.SetEnvironmentVariable(
                SharedUserSettingsRootEnvironmentVariableName,
                OriginalSharedUserSettingsEnvironmentVariableValue);
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
        /// Ensures an existing shared-user-settings override is normalized and returned without consulting the shared checkout layout.
        /// </summary>
        [Fact]
        public void ResolveSharedEngineUserSettingsRootPath_WhenOverrideExists_ReturnsAbsoluteOverride() {
            string configuredRootPath = Path.Combine(TemporaryRepositoryRootPath, "isolated-user-settings");
            Directory.CreateDirectory(configuredRootPath);
            Environment.SetEnvironmentVariable(SharedUserSettingsRootEnvironmentVariableName, configuredRootPath);
            EditorSourceBuildWorkspaceLocator locator = new();

            string resolvedRootPath = locator.ResolveSharedEngineUserSettingsRootPath();

            Assert.Equal(Path.GetFullPath(configuredRootPath), resolvedRootPath);
        }

        /// <summary>
        /// Ensures a configured but missing shared-user-settings root fails instead of silently using the main checkout.
        /// </summary>
        [Fact]
        public void ResolveSharedEngineUserSettingsRootPath_WhenOverrideIsMissing_Throws() {
            string configuredRootPath = Path.Combine(TemporaryRepositoryRootPath, "missing-user-settings");
            Environment.SetEnvironmentVariable(SharedUserSettingsRootEnvironmentVariableName, configuredRootPath);
            EditorSourceBuildWorkspaceLocator locator = new();

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                locator.ResolveSharedEngineUserSettingsRootPath());

            Assert.Contains(Path.GetFullPath(configuredRootPath), exception.Message, StringComparison.Ordinal);
        }

        /// <summary>
        /// Ensures absent settings isolation preserves worktree-to-shared-checkout resolution for existing source builds.
        /// </summary>
        [Fact]
        public void ResolveSharedEngineUserSettingsRootPath_WhenOverrideIsAbsent_UsesSharedCheckoutUserSettings() {
            string sharedRootPath = Path.Combine(TemporaryRepositoryRootPath, "shared-engine");
            string worktreeRootPath = Path.Combine(sharedRootPath, ".worktrees", "test-worktree");
            string sharedEditorProjectDirectoryPath = Path.Combine(sharedRootPath, "engine", "helengine.editor");
            string worktreeEditorProjectDirectoryPath = Path.Combine(worktreeRootPath, "engine", "helengine.editor");
            Directory.CreateDirectory(sharedEditorProjectDirectoryPath);
            Directory.CreateDirectory(worktreeEditorProjectDirectoryPath);
            File.WriteAllText(Path.Combine(sharedEditorProjectDirectoryPath, "helengine.editor.csproj"), "<Project />");
            File.WriteAllText(Path.Combine(worktreeEditorProjectDirectoryPath, "helengine.editor.csproj"), "<Project />");
            Environment.SetEnvironmentVariable(HelEngineSourceRootEnvironmentVariableName, worktreeRootPath);
            Environment.SetEnvironmentVariable(SharedUserSettingsRootEnvironmentVariableName, null);
            EditorSourceBuildWorkspaceLocator locator = new();

            string resolvedRootPath = locator.ResolveSharedEngineUserSettingsRootPath();

            Assert.Equal(Path.Combine(Path.GetFullPath(sharedRootPath), "user_settings"), resolvedRootPath);
        }
    }
}
