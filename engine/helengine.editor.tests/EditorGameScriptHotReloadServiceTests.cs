using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies the scripting hot-reload workflow builds the generated solution and imports the new assembly.
    /// </summary>
    public sealed class EditorGameScriptHotReloadServiceTests : IDisposable {
        /// <summary>
        /// Temporary project root used by the current test instance.
        /// </summary>
        readonly string TempProjectRootPath;

        /// <summary>
        /// Initializes one isolated temporary project root for hot-reload tests.
        /// </summary>
        public EditorGameScriptHotReloadServiceTests() {
            TempProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-script-hot-reload-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(TempProjectRootPath, "assets", "Scripts"));
            File.WriteAllText(Path.Combine(TempProjectRootPath, "assets", "Scripts", "Player.cs"), "public sealed class Player { }");
        }

        /// <summary>
        /// Deletes temporary test state after each test.
        /// </summary>
        public void Dispose() {
            if (Directory.Exists(TempProjectRootPath)) {
                Directory.Delete(TempProjectRootPath, true);
            }
        }

        /// <summary>
        /// Ensures a successful build triggers a reload of the fresh output assembly.
        /// </summary>
        [Fact]
        public void BuildAndReload_WhenBuildSucceeds_RewritesSolutionAndReloadsAssembly() {
            EditorGameSolutionService solutionService = new EditorGameSolutionService(TempProjectRootPath, "SkyRider", new TestIdeLauncher());
            TestScriptBuildTool buildTool = new TestScriptBuildTool(EditorBuildExecutionResult.Success("build ok"));
            TestScriptAssemblyHost assemblyHost = new TestScriptAssemblyHost();
            EditorGameScriptHotReloadService service = new EditorGameScriptHotReloadService(solutionService, buildTool, assemblyHost);

            EditorBuildExecutionResult result = service.BuildAndReload();

            Assert.True(result.Succeeded);
            Assert.Equal(Path.Combine(TempProjectRootPath, "SkyRider.sln"), buildTool.SolutionPath);
            Assert.Equal(solutionService.GeneratedOutputDirectoryPath, assemblyHost.SourceOutputDirectoryPath);
            Assert.Equal(solutionService.GeneratedOutputAssemblyPath, assemblyHost.MainAssemblyPath);
            Assert.Equal(1, assemblyHost.ReloadCount);
            Assert.True(File.Exists(solutionService.GeneratedSolutionFilePath));
            Assert.True(File.Exists(solutionService.GeneratedProjectFilePath));
        }

        /// <summary>
        /// Ensures a build failure short-circuits the reload path.
        /// </summary>
        [Fact]
        public void BuildAndReload_WhenBuildFails_DoesNotReloadAssembly() {
            EditorGameSolutionService solutionService = new EditorGameSolutionService(TempProjectRootPath, "SkyRider", new TestIdeLauncher());
            TestScriptBuildTool buildTool = new TestScriptBuildTool(EditorBuildExecutionResult.Failure("build failed"));
            TestScriptAssemblyHost assemblyHost = new TestScriptAssemblyHost();
            EditorGameScriptHotReloadService service = new EditorGameScriptHotReloadService(solutionService, buildTool, assemblyHost);

            EditorBuildExecutionResult result = service.BuildAndReload();

            Assert.False(result.Succeeded);
            Assert.Equal(0, assemblyHost.ReloadCount);
            Assert.Equal(Path.Combine(TempProjectRootPath, "SkyRider.sln"), buildTool.SolutionPath);
        }

        /// <summary>
        /// Minimal build tool used to verify scripting hot-reload orchestration without invoking `dotnet`.
        /// </summary>
        sealed class TestScriptBuildTool : IEditorScriptBuildTool {
            /// <summary>
            /// Initializes one fake build tool with a fixed outcome.
            /// </summary>
            /// <param name="result">Build result returned by the fake tool.</param>
            public TestScriptBuildTool(EditorBuildExecutionResult result) {
                Result = result;
            }

            /// <summary>
            /// Gets the fixed result returned by the fake build tool.
            /// </summary>
            public EditorBuildExecutionResult Result { get; }

            /// <summary>
            /// Gets the solution path passed to the fake build tool.
            /// </summary>
            public string SolutionPath { get; private set; }

            /// <summary>
            /// Builds the supplied solution path and returns the fixed test result.
            /// </summary>
            /// <param name="solutionPath">Absolute path to the generated solution file.</param>
            /// <returns>Fixed build result configured for the test.</returns>
            public EditorBuildExecutionResult Build(string solutionPath) {
                SolutionPath = solutionPath;
                return Result;
            }
        }

        /// <summary>
        /// Minimal assembly host used to verify reload input without loading a real script assembly.
        /// </summary>
        sealed class TestScriptAssemblyHost : IEditorScriptAssemblyHost {
            /// <summary>
            /// Gets the number of reload requests received by the fake host.
            /// </summary>
            public int ReloadCount { get; private set; }

            /// <summary>
            /// Gets the source output directory path passed to the fake host.
            /// </summary>
            public string SourceOutputDirectoryPath { get; private set; }

            /// <summary>
            /// Gets the main assembly path passed to the fake host.
            /// </summary>
            public string MainAssemblyPath { get; private set; }

            /// <summary>
            /// Reloads the fake host state without touching the filesystem.
            /// </summary>
            /// <param name="sourceOutputDirectoryPath">Absolute path to the fresh build output directory.</param>
            /// <param name="mainAssemblyPath">Absolute path to the main scripting assembly.</param>
            public void Reload(string sourceOutputDirectoryPath, string mainAssemblyPath) {
                SourceOutputDirectoryPath = sourceOutputDirectoryPath;
                MainAssemblyPath = mainAssemblyPath;
                ReloadCount++;
            }

            /// <summary>
            /// Returns no script descriptors in the test harness.
            /// </summary>
            /// <param name="entity">Entity that would receive the reflected component.</param>
            /// <returns>Empty descriptor list.</returns>
            public IReadOnlyList<EditorComponentAddDescriptor> GetAvailableScriptComponents(Entity entity) {
                return Array.Empty<EditorComponentAddDescriptor>();
            }

            /// <summary>
            /// Disposes the fake host.
            /// </summary>
            public void Dispose() {
            }
        }

        /// <summary>
        /// Minimal IDE launcher used to satisfy the solution service constructor.
        /// </summary>
        sealed class TestIdeLauncher : IEditorIdeLauncher {
            /// <summary>
            /// Opens one solution without doing anything in the test harness.
            /// </summary>
            /// <param name="solutionPath">Absolute path to the generated solution file.</param>
            public void OpenSolution(string solutionPath) {
            }
        }
    }
}
