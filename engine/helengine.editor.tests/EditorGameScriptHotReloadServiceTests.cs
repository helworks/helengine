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
            Assert.Equal(Path.Combine(TempProjectRootPath, "SkyRider.production.slnf"), buildTool.SolutionPath);
            Assert.Single(assemblyHost.Assemblies);
            Assert.Equal("gameplay", assemblyHost.Assemblies[0].ModuleId);
            Assert.Equal(solutionService.GeneratedOutputDirectoryPath, assemblyHost.Assemblies[0].OutputDirectoryPath);
            Assert.Equal(solutionService.GeneratedOutputAssemblyPath, assemblyHost.Assemblies[0].AssemblyPath);
            Assert.Equal(EditorCodeModuleKind.Runtime, assemblyHost.Assemblies[0].ModuleKind);
            Assert.Equal(1, assemblyHost.ReloadCount);
            Assert.True(File.Exists(solutionService.GeneratedSolutionFilePath));
            Assert.True(File.Exists(solutionService.GeneratedProjectFilePath));
        }

        /// <summary>
        /// Ensures an isolated solution build receives its unique compiler-output root without changing authored project metadata.
        /// </summary>
        [Fact]
        public void BuildAndReload_WhenExecutionOutputIsConfigured_ForwardsUniqueOutputRootToBuildTool() {
            string executionOutputRootPath = Path.Combine(Path.GetTempPath(), "helengine-script-hot-reload-tests", Guid.NewGuid().ToString("N"), "execution");
            string generatedWorkspaceRootPath = Path.Combine(Path.GetTempPath(), "helengine-script-hot-reload-tests", Guid.NewGuid().ToString("N"), "workspace");
            string generatedProjectOutputRootPath = Path.Combine(generatedWorkspaceRootPath, "output");

            try {
                EditorGameSolutionService solutionService = new EditorGameSolutionService(
                    TempProjectRootPath,
                    "SkyRider",
                    new TestIdeLauncher(),
                    executionOutputRootPath,
                    generatedWorkspaceRootPath,
                    EditorScriptCompilationMode.EditorFull,
                    generatedProjectOutputRootPath);
                TestScriptBuildTool buildTool = new TestScriptBuildTool(EditorBuildExecutionResult.Success("build ok"));
                TestScriptAssemblyHost assemblyHost = new TestScriptAssemblyHost();
                EditorGameScriptHotReloadService service = new EditorGameScriptHotReloadService(solutionService, buildTool, assemblyHost);

                EditorBuildExecutionResult result = service.BuildAndReload();

                Assert.True(result.Succeeded);
                Assert.Equal(executionOutputRootPath, buildTool.ExecutionOutputRootPath);
                Assert.NotEqual(generatedProjectOutputRootPath, buildTool.ExecutionOutputRootPath);
            } finally {
                string testRootPath = Path.GetDirectoryName(generatedWorkspaceRootPath) ?? generatedWorkspaceRootPath;
                if (Directory.Exists(testRootPath)) {
                    Directory.Delete(testRootPath, true);
                }
            }
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
            Assert.Equal(Path.Combine(TempProjectRootPath, "SkyRider.production.slnf"), buildTool.SolutionPath);
        }

        /// <summary>
        /// Ensures the hot-reload service forwards contributed menu items surfaced by the loaded editor assemblies.
        /// </summary>
        [Fact]
        public void GetAvailableEditorMenuItems_WhenAssembliesAreLoaded_ForwardsContributedMenusFromTheAssemblyHost() {
            EditorGameSolutionService solutionService = new EditorGameSolutionService(TempProjectRootPath, "SkyRider", new TestIdeLauncher());
            TestScriptBuildTool buildTool = new TestScriptBuildTool(EditorBuildExecutionResult.Success("ok"));
            TestScriptAssemblyHost assemblyHost = new TestScriptAssemblyHost {
                AvailableEditorMenuItems = [
                    new EditorMenuItemDescriptor(
                        "demo",
                        "Demo",
                        100,
                        "demo.regenerate-main-menu",
                        "Regenerate Main Menu...",
                        100,
                        "menu.regenerate-demo-disc-main-menu")
                ]
            };
            EditorGameScriptHotReloadService service = new EditorGameScriptHotReloadService(solutionService, buildTool, assemblyHost);

            IReadOnlyList<EditorMenuItemDescriptor> items = service.GetAvailableEditorMenuItems();

            EditorMenuItemDescriptor item = Assert.Single(items);
            Assert.Equal("demo.regenerate-main-menu", item.MenuItemId);
        }

        /// <summary>
        /// Ensures a second build with unchanged inputs and existing outputs reloads without invoking the build tool.
        /// </summary>
        [Fact]
        public void BuildAndReload_WhenInputsAreUnchangedAndOutputsExist_SkipsBuildAndReloads() {
            EditorGameSolutionService solutionService = new EditorGameSolutionService(TempProjectRootPath, "SkyRider", new TestIdeLauncher());
            TestScriptBuildTool buildTool = new TestScriptBuildTool(EditorBuildExecutionResult.Success("build ok")) {
                OutputFilePathToCreate = solutionService.GeneratedOutputAssemblyPath
            };
            TestScriptAssemblyHost assemblyHost = new TestScriptAssemblyHost();
            EditorGameScriptHotReloadService service = new EditorGameScriptHotReloadService(solutionService, buildTool, assemblyHost);

            Assert.True(service.BuildAndReload().Succeeded);
            EditorBuildExecutionResult second = service.BuildAndReload();

            Assert.True(second.Succeeded);
            Assert.Equal(1, buildTool.BuildCount);
            Assert.Equal(2, assemblyHost.ReloadCount);
            Assert.Contains("up to date", second.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(solutionService.GeneratedOutputAssemblyPath, assemblyHost.Assemblies[0].AssemblyPath);
        }

        /// <summary>
        /// Ensures editing a script source file after a successful build triggers a rebuild.
        /// </summary>
        [Fact]
        public void BuildAndReload_WhenSourceChangesAfterBuild_RebuildsScripts() {
            EditorGameSolutionService solutionService = new EditorGameSolutionService(TempProjectRootPath, "SkyRider", new TestIdeLauncher());
            TestScriptBuildTool buildTool = new TestScriptBuildTool(EditorBuildExecutionResult.Success("build ok")) {
                OutputFilePathToCreate = solutionService.GeneratedOutputAssemblyPath
            };
            EditorGameScriptHotReloadService service = new EditorGameScriptHotReloadService(solutionService, buildTool, new TestScriptAssemblyHost());

            Assert.True(service.BuildAndReload().Succeeded);
            File.WriteAllText(Path.Combine(TempProjectRootPath, "assets", "Scripts", "Player.cs"), "public sealed class Player { public int Health; }");
            Assert.True(service.BuildAndReload().Succeeded);

            Assert.Equal(2, buildTool.BuildCount);
        }

        /// <summary>
        /// Ensures editing a sibling test project's source after a successful build does not rebuild the production scripts.
        /// </summary>
        [Fact]
        public void BuildAndReload_WhenTestSourceChangesAfterBuild_DoesNotRebuildProductionScripts() {
            string testFolderPath = Path.Combine(TempProjectRootPath, "assets", "codebase", "gameplay.tests");
            Directory.CreateDirectory(testFolderPath);
            File.WriteAllText(Path.Combine(testFolderPath, "PlayerTests.cs"), "public sealed class PlayerTests { }");
            EditorGameSolutionService solutionService = new EditorGameSolutionService(TempProjectRootPath, "SkyRider", new TestIdeLauncher());
            TestScriptBuildTool buildTool = new TestScriptBuildTool(EditorBuildExecutionResult.Success("build ok")) {
                OutputFilePathToCreate = solutionService.GeneratedOutputAssemblyPath
            };
            EditorGameScriptHotReloadService service = new EditorGameScriptHotReloadService(solutionService, buildTool, new TestScriptAssemblyHost());

            Assert.True(service.BuildAndReload().Succeeded);
            File.WriteAllText(Path.Combine(testFolderPath, "PlayerTests.cs"), "public sealed class PlayerTests { public void Broken() { undefined(); } }");
            Assert.True(service.BuildAndReload().Succeeded);

            Assert.Equal(1, buildTool.BuildCount);
        }

        /// <summary>
        /// Ensures a forced build ignores a matching fingerprint.
        /// </summary>
        [Fact]
        public void BuildAndReload_WhenForced_RebuildsDespiteMatchingFingerprint() {
            EditorGameSolutionService solutionService = new EditorGameSolutionService(TempProjectRootPath, "SkyRider", new TestIdeLauncher());
            TestScriptBuildTool buildTool = new TestScriptBuildTool(EditorBuildExecutionResult.Success("build ok")) {
                OutputFilePathToCreate = solutionService.GeneratedOutputAssemblyPath
            };
            EditorGameScriptHotReloadService service = new EditorGameScriptHotReloadService(solutionService, buildTool, new TestScriptAssemblyHost());

            Assert.True(service.BuildAndReload().Succeeded);
            Assert.True(service.BuildAndReload(forceBuild: true).Succeeded);

            Assert.Equal(2, buildTool.BuildCount);
        }

        /// <summary>
        /// Ensures a matching fingerprint does not skip the build when the module DLL is missing.
        /// </summary>
        [Fact]
        public void BuildAndReload_WhenOutputIsMissing_RebuildsDespiteMatchingFingerprint() {
            EditorGameSolutionService solutionService = new EditorGameSolutionService(TempProjectRootPath, "SkyRider", new TestIdeLauncher());
            TestScriptBuildTool buildTool = new TestScriptBuildTool(EditorBuildExecutionResult.Success("build ok")) {
                OutputFilePathToCreate = solutionService.GeneratedOutputAssemblyPath
            };
            EditorGameScriptHotReloadService service = new EditorGameScriptHotReloadService(solutionService, buildTool, new TestScriptAssemblyHost());

            Assert.True(service.BuildAndReload().Succeeded);
            File.Delete(solutionService.GeneratedOutputAssemblyPath);
            Assert.True(service.BuildAndReload().Succeeded);

            Assert.Equal(2, buildTool.BuildCount);
        }

        /// <summary>
        /// Ensures a failed build leaves no fingerprint behind, so the next attempt builds again.
        /// </summary>
        [Fact]
        public void BuildAndReload_WhenBuildFails_DoesNotRecordFingerprint() {
            EditorGameSolutionService solutionService = new EditorGameSolutionService(TempProjectRootPath, "SkyRider", new TestIdeLauncher());
            TestScriptBuildTool buildTool = new TestScriptBuildTool(EditorBuildExecutionResult.Failure("build failed"));
            EditorGameScriptHotReloadService service = new EditorGameScriptHotReloadService(solutionService, buildTool, new TestScriptAssemblyHost());

            Assert.False(service.BuildAndReload().Succeeded);
            Directory.CreateDirectory(Path.GetDirectoryName(solutionService.GeneratedOutputAssemblyPath));
            File.WriteAllBytes(solutionService.GeneratedOutputAssemblyPath, new byte[] { 1 });
            Assert.False(service.BuildAndReload().Succeeded);

            Assert.Equal(2, buildTool.BuildCount);
        }

        /// <summary>
        /// Minimal build tool used to verify scripting hot-reload orchestration without invoking `dotnet`.
        /// </summary>
        sealed class TestScriptBuildTool : IEditorScriptBuildToolWithOutputRoot {
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
            /// Gets the number of build requests received by the fake tool.
            /// </summary>
            public int BuildCount { get; private set; }

            /// <summary>
            /// Gets or sets an optional file the fake tool writes on each successful build, standing in for the module DLL.
            /// </summary>
            public string OutputFilePathToCreate { get; set; }

            /// <summary>
            /// Gets the invocation-specific compiler-output root passed by isolated hot-reload orchestration.
            /// </summary>
            public string ExecutionOutputRootPath { get; private set; }

            /// <summary>
            /// Builds the supplied solution path and returns the fixed test result.
            /// </summary>
            /// <param name="solutionPath">Absolute path to the generated solution file.</param>
            /// <returns>Fixed build result configured for the test.</returns>
            public EditorBuildExecutionResult Build(string solutionPath) {
                SolutionPath = solutionPath;
                BuildCount++;
                WriteOutputFile();
                return Result;
            }

            /// <summary>
            /// Writes the stand-in module DLL when one is configured and the fixed result is a success.
            /// </summary>
            void WriteOutputFile() {
                if (!Result.Succeeded || string.IsNullOrWhiteSpace(OutputFilePathToCreate)) {
                    return;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(OutputFilePathToCreate));
                File.WriteAllBytes(OutputFilePathToCreate, new byte[] { (byte)BuildCount });
            }

            /// <summary>
            /// Records the invocation-specific compiler-output root and returns the fixed test result.
            /// </summary>
            /// <param name="solutionPath">Absolute path to the generated solution file.</param>
            /// <param name="executionOutputRootPath">Unique compiler-output root for the invocation.</param>
            /// <returns>Fixed build result configured for the test.</returns>
            public EditorBuildExecutionResult Build(string solutionPath, string executionOutputRootPath) {
                SolutionPath = solutionPath;
                ExecutionOutputRootPath = executionOutputRootPath;
                BuildCount++;
                WriteOutputFile();
                return Result;
            }
        }

        /// <summary>
        /// Minimal assembly host used to verify reload input without loading a real script assembly.
        /// </summary>
        sealed class TestScriptAssemblyHost : IEditorScriptAssemblyHost {
            /// <summary>
            /// Initializes one fake host with a shared script resolver.
            /// </summary>
            public TestScriptAssemblyHost() {
                ScriptTypeResolver = new ScriptTypeResolver();
                Assemblies = [];
            }

            /// <summary>
            /// Gets the number of reload requests received by the fake host.
            /// </summary>
            public int ReloadCount { get; private set; }

            /// <summary>
            /// Gets the shared script type resolver surfaced by the fake host.
            /// </summary>
            public IScriptTypeResolver ScriptTypeResolver { get; }

            /// <summary>
            /// Gets the assembly descriptors passed to the fake host.
            /// </summary>
            public IReadOnlyList<EditorScriptAssemblyDescriptor> Assemblies { get; private set; }

            /// <summary>
            /// Gets or sets the contributed editor menu items surfaced by the fake host.
            /// </summary>
            public IReadOnlyList<EditorMenuItemDescriptor> AvailableEditorMenuItems { get; set; } = Array.Empty<EditorMenuItemDescriptor>();

            /// <summary>
            /// Reloads the fake host state without touching the filesystem.
            /// </summary>
            /// <param name="assemblies">Descriptors for the freshly built module assemblies.</param>
            public void Reload(IReadOnlyList<EditorScriptAssemblyDescriptor> assemblies) {
                Assemblies = assemblies;
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
            /// Returns no editor commands in the test harness.
            /// </summary>
            /// <returns>Empty editor command descriptor list.</returns>
            public IReadOnlyList<EditorProjectCommandDescriptor> GetAvailableEditorCommands() {
                return Array.Empty<EditorProjectCommandDescriptor>();
            }

            /// <summary>
            /// Returns no editor menu items in the test harness.
            /// </summary>
            /// <returns>Empty editor menu item descriptor list.</returns>
            public IReadOnlyList<EditorMenuItemDescriptor> GetAvailableEditorMenuItems() {
                return AvailableEditorMenuItems;
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
