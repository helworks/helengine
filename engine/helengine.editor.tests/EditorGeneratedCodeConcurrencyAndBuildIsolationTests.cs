using System.Xml.Linq;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Exercises generated project publication and the real dotnet build transport at their process boundary.
    /// </summary>
    public sealed class EditorGeneratedCodeConcurrencyAndBuildIsolationTests : IDisposable {
        /// <summary>
        /// Temporary authored project used by the generation and build probes.
        /// </summary>
        readonly string ProjectRootPath;

        /// <summary>
        /// Initializes one minimal authored project.
        /// </summary>
        public EditorGeneratedCodeConcurrencyAndBuildIsolationTests() {
            ProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-generated-code-isolation-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(ProjectRootPath, "assets", "Scripts"));
            File.WriteAllText(Path.Combine(ProjectRootPath, "assets", "Scripts", "Player.cs"), "public sealed class Player { }");
        }

        /// <summary>
        /// Removes only this test's temporary authored and execution roots.
        /// </summary>
        public void Dispose() {
            if (Directory.Exists(ProjectRootPath)) {
                Directory.Delete(ProjectRootPath, true);
            }
            string defaultOutputRootPath = Path.Combine(
                Path.GetDirectoryName(ProjectRootPath) ?? ProjectRootPath,
                "output",
                Path.GetFileName(ProjectRootPath));
            DeleteDirectoryIfPresent(defaultOutputRootPath);
        }

        /// <summary>
        /// Ensures concurrent same-route generators leave one complete, deterministic metadata set without temporary publications.
        /// </summary>
        [Fact]
        public async Task GenerateSolutionFiles_WhenSameRouteRunsConcurrently_PublishesOneCompleteStableMetadataSet() {
            string workspaceRootPath = Path.Combine(ProjectRootPath, "user_settings", "generated_code", "editor-command", "EditorFull");
            string firstExecutionRootPath = Path.Combine(Path.GetTempPath(), "helengine-generated-code-execution-tests", Guid.NewGuid().ToString("N"), "first");
            string secondExecutionRootPath = Path.Combine(Path.GetTempPath(), "helengine-generated-code-execution-tests", Guid.NewGuid().ToString("N"), "second");

            try {
                EditorGameSolutionService firstService = CreateService(workspaceRootPath, firstExecutionRootPath);
                EditorGameSolutionService secondService = CreateService(workspaceRootPath, secondExecutionRootPath);

                await Task.WhenAll(
                    Task.Run(firstService.GenerateSolutionFiles),
                    Task.Run(secondService.GenerateSolutionFiles));

                string projectFilePath = Path.Combine(workspaceRootPath, "projects", "gameplay", "gameplay.csproj");
                string solutionFilePath = Path.Combine(workspaceRootPath, "SkyRider.sln");
                XDocument projectDocument = XDocument.Load(projectFilePath);

                Assert.NotNull(projectDocument.Root);
                Assert.Contains("gameplay", File.ReadAllText(solutionFilePath), StringComparison.Ordinal);
                Assert.Empty(Directory.EnumerateFiles(workspaceRootPath, "*.tmp", SearchOption.AllDirectories));
                Assert.NotEqual(firstService.GeneratedOutputDirectoryPath, secondService.GeneratedOutputDirectoryPath);
            } finally {
                DeleteDirectoryIfPresent(firstExecutionRootPath);
                DeleteDirectoryIfPresent(secondExecutionRootPath);
            }
        }

        /// <summary>
        /// Ensures the workspace lease is an exclusive operating-system file lease, not only an in-process convention.
        /// </summary>
        [Fact]
        public void AcquireWorkspaceLease_WhenHeld_RejectsAnotherExclusiveFileHandle() {
            string workspaceRootPath = Path.Combine(ProjectRootPath, "user_settings", "generated_code", "editor-command", "EditorFull");
            EditorGameSolutionService service = CreateService(workspaceRootPath, Path.Combine(Path.GetTempPath(), "helengine-generated-code-execution-tests", Guid.NewGuid().ToString("N"), "lease"));

            using EditorGeneratedCodeWorkspaceLease lease = service.AcquireWorkspaceLease();

            Assert.Throws<IOException>(() => new FileStream(lease.LockFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.None));
        }

        /// <summary>
        /// Ensures a default non-override build uses its documented sibling output root before project evaluation.
        /// </summary>
        [Fact]
        public void BuildGeneratedSolution_WithoutOverride_UsesStableFallbackForIntermediateAndOutputState() {
            EditorGameSolutionService service = new EditorGameSolutionService(ProjectRootPath, "SkyRider", new TestIdeLauncher());
            string solutionPath = service.GenerateSolutionFiles();
            string stableOutputRootPath = Path.Combine(
                Path.GetDirectoryName(ProjectRootPath) ?? ProjectRootPath,
                "output",
                Path.GetFileName(ProjectRootPath));

            EditorBuildExecutionResult result = new EditorDotNetScriptBuildTool().Build(solutionPath);

            Assert.True(result.Succeeded, result.Message);
            string generatedWorkspaceRootPath = Path.Combine(ProjectRootPath, "user_settings", "generated_code");
            Assert.Empty(Directory.EnumerateDirectories(generatedWorkspaceRootPath, "obj", SearchOption.AllDirectories));
            Assert.Empty(Directory.EnumerateDirectories(generatedWorkspaceRootPath, "bin", SearchOption.AllDirectories));
            Assert.True(File.Exists(Path.Combine(stableOutputRootPath, "generated_code", "obj", "gameplay", "project.assets.json")));
            Assert.True(File.Exists(Path.Combine(stableOutputRootPath, "generated_code", "bin", "gameplay", "Debug", "net9.0", "gameplay.dll")));
        }

        /// <summary>
        /// Ensures the real dotnet build receives separator-heavy output roots without creating project-local intermediate state.
        /// </summary>
        [Fact]
        public void BuildGeneratedSolution_WhenExecutionRootContainsSeparators_UsesEarlyIsolatedIntermediateAndOutputPaths() {
            string workspaceRootPath = Path.Combine(ProjectRootPath, "user_settings", "generated_code", "editor-command", "EditorFull");
            string executionRootPath = Path.Combine(
                Path.GetTempPath(),
                "helengine generated,probe;percent% & apostrophe' " + Guid.NewGuid().ToString("N"));

            try {
                EditorGameSolutionService service = CreateService(workspaceRootPath, executionRootPath);
                string solutionPath = service.GenerateSolutionFiles();

                EditorBuildExecutionResult result = new EditorDotNetScriptBuildTool().Build(solutionPath, executionRootPath);

                Assert.True(result.Succeeded, result.Message);
                Assert.True(File.Exists(Path.Combine(executionRootPath, "generated_code", "bin", "gameplay", "Debug", "net9.0", "gameplay.dll")));
                Assert.Empty(Directory.EnumerateDirectories(workspaceRootPath, "obj", SearchOption.AllDirectories));
                Assert.True(File.Exists(Path.Combine(executionRootPath, "generated_code", "obj", "gameplay", "project.assets.json")));
            } finally {
                DeleteDirectoryIfPresent(executionRootPath);
            }
        }

        /// <summary>
        /// Ensures independent build workspaces can publish to one requested destination without exposing a partial tree.
        /// </summary>
        [Fact]
        public async Task BuildGeneratedSolutions_WhenIndependentInvocationsShareDestination_PublishCompleteTrees() {
            string secondProjectRootPath = Path.Combine(
                Path.GetTempPath(),
                "helengine-generated-code-isolation-tests",
                Guid.NewGuid().ToString("N"));
            string firstWorkspaceRootPath = Path.Combine(ProjectRootPath, "user_settings", "generated_code", "first");
            string secondWorkspaceRootPath = Path.Combine(secondProjectRootPath, "user_settings", "generated_code", "second");
            string firstExecutionRootPath = Path.Combine(Path.GetTempPath(), "helengine-generated-code-execution-tests", Guid.NewGuid().ToString("N"), "first");
            string secondExecutionRootPath = Path.Combine(Path.GetTempPath(), "helengine-generated-code-execution-tests", Guid.NewGuid().ToString("N"), "second");
            string destinationRootPath = Path.Combine(Path.GetTempPath(), "helengine-shared-build-destination", Guid.NewGuid().ToString("N"));

            try {
                Directory.CreateDirectory(Path.Combine(secondProjectRootPath, "assets", "Scripts"));
                File.WriteAllText(Path.Combine(secondProjectRootPath, "assets", "Scripts", "Player.cs"), "public sealed class Player { }");
                EditorGameSolutionService firstService = CreateService(ProjectRootPath, firstWorkspaceRootPath, firstExecutionRootPath);
                EditorGameSolutionService secondService = CreateService(secondProjectRootPath, secondWorkspaceRootPath, secondExecutionRootPath);
                string firstSolutionPath = firstService.GenerateSolutionFiles();
                string secondSolutionPath = secondService.GenerateSolutionFiles();
                EditorDotNetScriptBuildTool buildTool = new EditorDotNetScriptBuildTool();

                EditorBuildExecutionResult[] results = await Task.WhenAll(
                    Task.Run(() => buildTool.Build(firstSolutionPath, destinationRootPath)),
                    Task.Run(() => buildTool.Build(secondSolutionPath, destinationRootPath)));

                Assert.All(results, result => Assert.True(result.Succeeded, result.Message));
                Assert.True(File.Exists(Path.Combine(destinationRootPath, "generated_code", "bin", "gameplay", "Debug", "net9.0", "gameplay.dll")));
                string destinationParentPath = Path.GetDirectoryName(destinationRootPath);
                string destinationName = Path.GetFileName(destinationRootPath);
                Assert.Empty(Directory.GetDirectories(destinationParentPath, destinationName + ".staging-*", SearchOption.TopDirectoryOnly));
                Assert.Empty(Directory.GetDirectories(destinationParentPath, destinationName + ".backup-*", SearchOption.TopDirectoryOnly));
            } finally {
                DeleteDirectoryIfPresent(secondProjectRootPath);
                DeleteDirectoryIfPresent(firstExecutionRootPath);
                DeleteDirectoryIfPresent(secondExecutionRootPath);
                DeleteDirectoryIfPresent(destinationRootPath);
            }
        }

        /// <summary>
        /// Creates one isolated full-editor solution service with stable metadata and a unique execution root.
        /// </summary>
        /// <param name="workspaceRootPath">Stable generated metadata root.</param>
        /// <param name="executionRootPath">Unique compiler output root.</param>
        /// <returns>Configured solution service.</returns>
        EditorGameSolutionService CreateService(string workspaceRootPath, string executionRootPath) {
            return CreateService(ProjectRootPath, workspaceRootPath, executionRootPath);
        }

        /// <summary>
        /// Creates one isolated solution service for an explicitly supplied authored project root.
        /// </summary>
        /// <param name="projectRootPath">Authored project root.</param>
        /// <param name="workspaceRootPath">Stable generated metadata root.</param>
        /// <param name="executionRootPath">Unique compiler output root.</param>
        /// <returns>Configured solution service.</returns>
        static EditorGameSolutionService CreateService(string projectRootPath, string workspaceRootPath, string executionRootPath) {
            return new EditorGameSolutionService(
                projectRootPath,
                "SkyRider",
                new TestIdeLauncher(),
                executionRootPath,
                workspaceRootPath,
                EditorScriptCompilationMode.EditorFull,
                Path.Combine(workspaceRootPath, "output"));
        }

        /// <summary>
        /// Removes one temporary directory when a test created it.
        /// </summary>
        /// <param name="directoryPath">Directory to remove.</param>
        static void DeleteDirectoryIfPresent(string directoryPath) {
            if (Directory.Exists(directoryPath)) {
                Directory.Delete(directoryPath, true);
            }
        }

        /// <summary>
        /// Launcher implementation that keeps the probe on the public generation path without opening an IDE.
        /// </summary>
        sealed class TestIdeLauncher : IEditorIdeLauncher {
            /// <summary>
            /// Receives a generated solution without side effects.
            /// </summary>
            /// <param name="solutionPath">Generated solution path.</param>
            public void OpenSolution(string solutionPath) {
            }
        }
    }
}
