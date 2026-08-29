using System.Security.Cryptography;
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
        /// Ensures the default root solution and its generated child projects share the lease used by the real build tool.
        /// </summary>
        [Fact]
        public void DefaultSolutionWorkspaceLease_CoversRootSolutionAndRealBuildValidation() {
            EditorGameSolutionService service = new EditorGameSolutionService(ProjectRootPath, "SkyRider", new TestIdeLauncher());
            string solutionPath = service.GenerateSolutionFiles();
            string missingSolutionPath = Path.Combine(ProjectRootPath, "validation-only-missing.sln");

            using EditorGeneratedCodeWorkspaceLease workspaceLease = service.AcquireWorkspaceLease();
            Assert.True(workspaceLease.Covers(Path.GetDirectoryName(solutionPath)));
            Assert.True(workspaceLease.Covers(Path.Combine(ProjectRootPath, "user_settings", "generated_code", "projects", "gameplay")));

            EditorBuildExecutionResult result = new EditorDotNetScriptBuildTool().Build(missingSolutionPath, string.Empty, workspaceLease);

            Assert.False(result.Succeeded);
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
                Assert.Empty(Directory.GetDirectories(destinationParentPath, destinationName + ".operation-*", SearchOption.TopDirectoryOnly));
            } finally {
                DeleteDirectoryIfPresent(secondProjectRootPath);
                DeleteDirectoryIfPresent(firstExecutionRootPath);
                DeleteDirectoryIfPresent(secondExecutionRootPath);
                DeleteDirectoryIfPresent(destinationRootPath);
            }
        }

        /// <summary>
        /// Ensures publication recovery never treats an unmarked user directory as an engine-owned crash artifact.
        /// </summary>
        [Fact]
        public void BuildGeneratedSolution_WhenDestinationHasUnmarkedMatchingSiblings_PreservesTheirBytes() {
            string workspaceRootPath = Path.Combine(ProjectRootPath, "user_settings", "generated_code", "foreign-publication");
            string executionRootPath = Path.Combine(Path.GetTempPath(), "helengine-generated-code-execution-tests", Guid.NewGuid().ToString("N"), "foreign-publication");
            string destinationRootPath = Path.Combine(Path.GetTempPath(), "helengine-shared-build-destination", Guid.NewGuid().ToString("N"));
            string destinationParentPath = Path.GetDirectoryName(destinationRootPath);
            string destinationName = Path.GetFileName(destinationRootPath);
            string foreignBackupPath = Path.Combine(destinationParentPath, destinationName + ".backup-userdata");
            string foreignStagingPath = Path.Combine(destinationParentPath, destinationName + ".staging-" + Guid.NewGuid().ToString("N"));

            try {
                Directory.CreateDirectory(foreignBackupPath);
                Directory.CreateDirectory(foreignStagingPath);
                string foreignBackupFilePath = Path.Combine(foreignBackupPath, "keep-backup.bin");
                string foreignStagingFilePath = Path.Combine(foreignStagingPath, "keep-staging.bin");
                File.WriteAllText(foreignBackupFilePath, "user-owned-backup");
                File.WriteAllText(foreignStagingFilePath, "user-owned-staging");

                EditorGameSolutionService service = CreateService(workspaceRootPath, executionRootPath);
                string solutionPath = service.GenerateSolutionFiles();
                EditorBuildExecutionResult result = new EditorDotNetScriptBuildTool().Build(solutionPath, destinationRootPath);

                Assert.True(result.Succeeded, result.Message);
                Assert.Equal("user-owned-backup", File.ReadAllText(foreignBackupFilePath));
                Assert.Equal("user-owned-staging", File.ReadAllText(foreignStagingFilePath));
            } finally {
                DeleteDirectoryIfPresent(destinationRootPath);
                DeleteDirectoryIfPresent(foreignBackupPath);
                DeleteDirectoryIfPresent(foreignStagingPath);
                DeleteDirectoryIfPresent(executionRootPath);
            }
        }

        /// <summary>
        /// Ensures positively marked interrupted publication artifacts are recovered and do not remain beside the destination.
        /// </summary>
        [Fact]
        public void BuildGeneratedSolution_WhenOwnedPublicationArtifactsRemain_RecoversOnlyThoseArtifacts() {
            string workspaceRootPath = Path.Combine(ProjectRootPath, "user_settings", "generated_code", "owned-publication");
            string executionRootPath = Path.Combine(Path.GetTempPath(), "helengine-generated-code-execution-tests", Guid.NewGuid().ToString("N"), "owned-publication");
            string destinationRootPath = Path.Combine(Path.GetTempPath(), "helengine-shared-build-destination", Guid.NewGuid().ToString("N"));
            string destinationParentPath = Path.GetDirectoryName(destinationRootPath);
            string destinationName = Path.GetFileName(destinationRootPath);
            string publicationToken = Guid.NewGuid().ToString("N");
            string operationRootPath = Path.Combine(destinationParentPath, destinationName + ".operation-" + publicationToken);
            string stagingRootPath = Path.Combine(destinationParentPath, destinationName + ".staging-" + publicationToken);
            string backupRootPath = Path.Combine(destinationParentPath, destinationName + ".backup-" + publicationToken);

            try {
                Directory.CreateDirectory(operationRootPath);
                Directory.CreateDirectory(stagingRootPath);
                Directory.CreateDirectory(backupRootPath);
                File.WriteAllText(Path.Combine(stagingRootPath, "staged-before-crash.txt"), "staged-before-crash");
                File.WriteAllText(Path.Combine(backupRootPath, "backup-before-crash.txt"), "backup-before-crash");
                WritePublicationOperationMarkerForTest(operationRootPath, destinationRootPath, publicationToken, "backup-moved");

                EditorGameSolutionService service = CreateService(workspaceRootPath, executionRootPath);
                string solutionPath = service.GenerateSolutionFiles();
                EditorBuildExecutionResult result = new EditorDotNetScriptBuildTool().Build(solutionPath, destinationRootPath);

                Assert.True(result.Succeeded, result.Message);
                Assert.False(Directory.Exists(stagingRootPath));
                Assert.False(Directory.Exists(backupRootPath));
                Assert.False(Directory.Exists(operationRootPath));
                Assert.True(Directory.Exists(destinationRootPath));
            } finally {
                DeleteDirectoryIfPresent(destinationRootPath);
                DeleteDirectoryIfPresent(stagingRootPath);
                DeleteDirectoryIfPresent(backupRootPath);
                DeleteDirectoryIfPresent(operationRootPath);
                DeleteDirectoryIfPresent(executionRootPath);
            }
        }

        /// <summary>
        /// Ensures a failure of the publication rename leaves the live destination exactly unchanged.
        /// </summary>
        [Fact]
        public void BuildGeneratedSolution_WhenPublicationMoveFails_LeavesDestinationTreeUnchangedAndCleansOwnedArtifacts() {
            string workspaceRootPath = Path.Combine(ProjectRootPath, "user_settings", "generated_code", "move-failure-publication");
            string executionRootPath = Path.Combine(Path.GetTempPath(), "helengine-generated-code-execution-tests", Guid.NewGuid().ToString("N"), "move-failure-publication");
            string destinationRootPath = Path.Combine(Path.GetTempPath(), "helengine-shared-build-destination", Guid.NewGuid().ToString("N"));
            string destinationParentPath = Path.GetDirectoryName(destinationRootPath);
            string destinationName = Path.GetFileName(destinationRootPath);
            string destinationIdentity = Path.GetFullPath(destinationRootPath);

            try {
                Directory.CreateDirectory(Path.Combine(destinationRootPath, "nested"));
                File.WriteAllText(Path.Combine(destinationRootPath, "existing-output.bin"), "existing-output");
                File.WriteAllText(Path.Combine(destinationRootPath, "nested", "existing-settings.json"), "{\"stable\":true}");
                Dictionary<string, string> beforeSnapshot = SnapshotDirectory(destinationRootPath);
                bool moveFailureInjected = false;
                Action<string, string> previousHook = EditorDotNetScriptBuildTool.PublicationMoveHookForTests;
                try {
                    EditorDotNetScriptBuildTool.PublicationMoveHookForTests = (sourcePath, targetPath) => {
                        if (!moveFailureInjected
                            && Path.GetFileName(sourcePath).StartsWith(destinationName + ".staging-", StringComparison.Ordinal)
                            && string.Equals(Path.GetFullPath(targetPath), destinationIdentity, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)) {
                            moveFailureInjected = true;
                            throw new IOException("Injected publication move failure.");
                        }
                    };

                    EditorGameSolutionService service = CreateService(workspaceRootPath, executionRootPath);
                    string solutionPath = service.GenerateSolutionFiles();

                    Assert.Throws<IOException>(() => new EditorDotNetScriptBuildTool().Build(solutionPath, destinationRootPath));
                } finally {
                    EditorDotNetScriptBuildTool.PublicationMoveHookForTests = previousHook;
                }

                Assert.True(moveFailureInjected);
                Assert.Equal(beforeSnapshot.Keys.OrderBy(path => path), SnapshotDirectory(destinationRootPath).Keys.OrderBy(path => path));
                Dictionary<string, string> afterSnapshot = SnapshotDirectory(destinationRootPath);
                foreach (KeyValuePair<string, string> entry in beforeSnapshot) {
                    Assert.Equal(entry.Value, afterSnapshot[entry.Key]);
                }

                string[] siblingNames = Directory.Exists(destinationParentPath)
                    ? Directory.EnumerateDirectories(destinationParentPath, "*", SearchOption.TopDirectoryOnly)
                        .Select(Path.GetFileName)
                        .Where(name => name.StartsWith(destinationName + ".operation-", StringComparison.Ordinal)
                            || name.StartsWith(destinationName + ".staging-", StringComparison.Ordinal)
                            || name.StartsWith(destinationName + ".backup-", StringComparison.Ordinal))
                        .ToArray()
                    : Array.Empty<string>();
                Assert.Empty(siblingNames);
            } finally {
                DeleteDirectoryIfPresent(destinationRootPath);
                DeleteDirectoryIfPresent(executionRootPath);
            }
        }

        /// <summary>
        /// Ensures a failed compiler process never replaces an already published destination tree.
        /// </summary>
        [Fact]
        public void BuildGeneratedSolution_WhenCompilationFails_RetainsExistingDestinationBytes() {
            string workspaceRootPath = Path.Combine(ProjectRootPath, "user_settings", "generated_code", "failed-publication");
            string executionRootPath = Path.Combine(Path.GetTempPath(), "helengine-generated-code-execution-tests", Guid.NewGuid().ToString("N"), "failed-publication");
            string destinationRootPath = Path.Combine(Path.GetTempPath(), "helengine-shared-build-destination", Guid.NewGuid().ToString("N"));

            try {
                Directory.CreateDirectory(destinationRootPath);
                string sentinelPath = Path.Combine(destinationRootPath, "existing-output.bin");
                File.WriteAllText(sentinelPath, "existing-output");
                EditorGameSolutionService service = CreateService(workspaceRootPath, executionRootPath);
                string solutionPath = service.GenerateSolutionFiles();
                File.WriteAllText(Path.Combine(ProjectRootPath, "assets", "Scripts", "Player.cs"), "public sealed class { invalid }");

                EditorBuildExecutionResult result = new EditorDotNetScriptBuildTool().Build(solutionPath, destinationRootPath);

                Assert.False(result.Succeeded);
                Assert.Equal("existing-output", File.ReadAllText(sentinelPath));
                Assert.True(File.Exists(sentinelPath));
            } finally {
                DeleteDirectoryIfPresent(destinationRootPath);
                DeleteDirectoryIfPresent(executionRootPath);
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
        /// Seeds an operation marker in the exact format consumed by the production publisher for an interrupted-artifact probe.
        /// </summary>
        static void WritePublicationOperationMarkerForTest(string operationRootPath, string destinationRootPath, string token, string phase) {
            string fullDestinationPath = Path.GetFullPath(destinationRootPath);
            string destinationRoot = Path.GetPathRoot(fullDestinationPath) ?? string.Empty;
            string normalizedDestination = fullDestinationPath.Length > destinationRoot.Length
                ? fullDestinationPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                : fullDestinationPath;
            if (OperatingSystem.IsWindows()) {
                normalizedDestination = normalizedDestination.ToUpperInvariant();
            }

            File.WriteAllText(
                Path.Combine(operationRootPath, ".helengine-publication-operation"),
                "helengine-publication\n" + normalizedDestination + "\n" + token + "\n" + phase + "\n");
        }

        /// <summary>
        /// Captures the exact relative file set and content hashes in one directory tree.
        /// </summary>
        static Dictionary<string, string> SnapshotDirectory(string directoryPath) {
            return Directory.GetFiles(directoryPath, "*", SearchOption.AllDirectories)
                .ToDictionary(
                    path => Path.GetRelativePath(directoryPath, path),
                    path => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))),
                    StringComparer.Ordinal);
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
