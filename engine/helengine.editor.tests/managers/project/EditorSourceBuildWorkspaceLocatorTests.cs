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
        /// Temporary repository root used by the current test instance.
        /// </summary>
        readonly string TemporaryRepositoryRootPath;

        /// <summary>
        /// Original environment-variable value restored after the test completes.
        /// </summary>
        readonly string OriginalEnvironmentVariableValue;

        /// <summary>
        /// Initializes one temporary source-root override test fixture.
        /// </summary>
        public EditorSourceBuildWorkspaceLocatorTests() {
            TemporaryRepositoryRootPath = Path.Combine(Path.GetTempPath(), "helengine-source-root-locator-tests", Guid.NewGuid().ToString("N"));
            OriginalEnvironmentVariableValue = Environment.GetEnvironmentVariable(HelEngineSourceRootEnvironmentVariableName);
            Directory.CreateDirectory(Path.Combine(TemporaryRepositoryRootPath, "engine", "helengine.editor"));
            File.WriteAllText(Path.Combine(TemporaryRepositoryRootPath, "engine", "helengine.editor", "helengine.editor.csproj"), "<Project />");
        }

        /// <summary>
        /// Restores the original environment-variable state and deletes the temporary repository root after each test.
        /// </summary>
        public void Dispose() {
            Environment.SetEnvironmentVariable(HelEngineSourceRootEnvironmentVariableName, OriginalEnvironmentVariableValue);
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
    }
}
