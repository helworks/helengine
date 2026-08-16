using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests.managers.project {
    /// <summary>
    /// Verifies that queued platform-build executions receive private workspaces even when they originate from the same queue item.
    /// </summary>
    [Collection(EditorBuildCacheEnvironmentCollection.Name)]
    public sealed class EditorPlatformBuildGraphWorkspaceFactoryTests : IDisposable {
        /// <summary>
        /// Original stable cache root inherited by the test process.
        /// </summary>
        readonly string OriginalCacheRoot = Environment.GetEnvironmentVariable("HELENGINE_BUILD_CACHE_ROOT");

        /// <summary>
        /// Original stable build configuration inherited by the test process.
        /// </summary>
        readonly string OriginalConfiguration = Environment.GetEnvironmentVariable("HELENGINE_BUILD_CONFIGURATION");

        /// <summary>
        /// Original stable build profile inherited by the test process.
        /// </summary>
        readonly string OriginalProfile = Environment.GetEnvironmentVariable("HELENGINE_BUILD_PROFILE");

        /// <summary>
        /// Original deprecated workspace root inherited by the test process.
        /// </summary>
        readonly string OriginalWorkspaceRoot = Environment.GetEnvironmentVariable("HELENGINE_BUILD_WORKSPACE_ROOT");

        /// <summary>
        /// Initializes one factory test with stable-cache mode disabled by default.
        /// </summary>
        public EditorPlatformBuildGraphWorkspaceFactoryTests() {
            Environment.SetEnvironmentVariable("HELENGINE_BUILD_CACHE_ROOT", null);
            Environment.SetEnvironmentVariable("HELENGINE_BUILD_CONFIGURATION", null);
            Environment.SetEnvironmentVariable("HELENGINE_BUILD_PROFILE", null);
            Environment.SetEnvironmentVariable("HELENGINE_BUILD_WORKSPACE_ROOT", null);
        }

        /// <summary>
        /// Restores every process environment variable changed by factory tests.
        /// </summary>
        public void Dispose() {
            Environment.SetEnvironmentVariable("HELENGINE_BUILD_CACHE_ROOT", OriginalCacheRoot);
            Environment.SetEnvironmentVariable("HELENGINE_BUILD_CONFIGURATION", OriginalConfiguration);
            Environment.SetEnvironmentVariable("HELENGINE_BUILD_PROFILE", OriginalProfile);
            Environment.SetEnvironmentVariable("HELENGINE_BUILD_WORKSPACE_ROOT", OriginalWorkspaceRoot);
        }

        /// <summary>
        /// Ensures repeated executions of one queue item cannot share generated outputs, cook data, package scratch data, or platform-builder working files.
        /// </summary>
        [Fact]
        public void Create_WhenQueueItemIsRepeated_ReturnsDistinctInvocationWorkspaces() {
            EditorPlatformBuildGraphWorkspaceFactory factory = new(Path.Combine(Path.GetTempPath(), "helengine-isolation-tests", "factory-project"));

            EditorPlatformBuildGraphWorkspace firstWorkspace = factory.Create("ps2", "queue-123");
            EditorPlatformBuildGraphWorkspace secondWorkspace = factory.Create("ps2", "queue-123");

            Assert.NotEqual(firstWorkspace.ExecutionRootPath, secondWorkspace.ExecutionRootPath);
            Assert.StartsWith(Path.Combine(Path.GetTempPath(), "helengine-builds"), firstWorkspace.ExecutionRootPath, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(Path.Combine("ps2", "workspace", "queue-123"), firstWorkspace.ExecutionRootPath, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(Path.Combine(firstWorkspace.ExecutionRootPath, "generated-core"), firstWorkspace.GeneratedCoreRootPath);
            Assert.Equal(Path.Combine(firstWorkspace.ExecutionRootPath, "builder"), firstWorkspace.BuilderWorkingRootPath);
        }

        /// <summary>
        /// Ensures workspace reset removes only invocation-private intermediate data and leaves the caller-owned final output root intact.
        /// </summary>
        [Fact]
        public void ResetExecutionDirectories_WhenOutputAlreadyExists_PreservesFinalOutputRoot() {
            string rootPath = Path.Combine(Path.GetTempPath(), "helengine-isolation-tests", Guid.NewGuid().ToString("N"));
            string executionRootPath = Path.Combine(rootPath, "workspace");
            string outputRootPath = Path.Combine(rootPath, "output");
            string generatedCoreRootPath = Path.Combine(executionRootPath, "generated-core");
            string cookRootPath = Path.Combine(executionRootPath, "cooked");
            string packageRootPath = Path.Combine(executionRootPath, "package");
            string builderRootPath = Path.Combine(executionRootPath, "builder");
            string outputSentinelPath = Path.Combine(outputRootPath, "existing-output.txt");
            System.Reflection.MethodInfo resetMethod = typeof(EditorPlatformBuildGraphRunner).GetMethod("ResetExecutionDirectories", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);

            try {
                Directory.CreateDirectory(executionRootPath);
                Directory.CreateDirectory(outputRootPath);
                File.WriteAllText(Path.Combine(executionRootPath, "stale.txt"), "stale");
                File.WriteAllText(outputSentinelPath, "final output");

                resetMethod.Invoke(null, [executionRootPath, generatedCoreRootPath, cookRootPath, packageRootPath, builderRootPath]);

                Assert.False(File.Exists(Path.Combine(executionRootPath, "stale.txt")));
                Assert.True(File.Exists(outputSentinelPath));
                Assert.True(Directory.Exists(cookRootPath));
                Assert.True(Directory.Exists(packageRootPath));
                Assert.True(Directory.Exists(builderRootPath));
            } finally {
                if (Directory.Exists(rootPath)) {
                    Directory.Delete(rootPath, true);
                }
            }
        }

        /// <summary>
        /// Ensures concurrently requested executions for one platform and queue item never receive the same mutable workspace root.
        /// </summary>
        [Fact]
        public void Create_WhenInvocationsRunConcurrently_ReturnsUniqueWorkspaceRoots() {
            EditorPlatformBuildGraphWorkspaceFactory factory = new(Path.Combine(Path.GetTempPath(), "helengine-isolation-tests", "parallel-project"));
            Task<EditorPlatformBuildGraphWorkspace>[] workspaceTasks = Enumerable.Range(0, 16)
                .Select(_ => Task.Run(() => factory.Create("ps2", "queue-123")))
                .ToArray();

            Task.WaitAll(workspaceTasks);
            string[] workspaceRootPaths = workspaceTasks.Select(workspaceTask => workspaceTask.Result.ExecutionRootPath).ToArray();

            Assert.Equal(workspaceRootPaths.Length, workspaceRootPaths.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        }

        /// <summary>
        /// Ensures headless builds reuse deterministic graph, generated-core, and native roots outside interactive isolation.
        /// </summary>
        [Fact]
        public void Create_WhenStableCacheIsConfigured_ReturnsDeterministicSeparatedRoots() {
            string cacheRootPath = Path.Combine(Path.GetTempPath(), "helengine-stable-factory-tests", Guid.NewGuid().ToString("N"));
            Environment.SetEnvironmentVariable("HELENGINE_BUILD_CACHE_ROOT", cacheRootPath);
            Environment.SetEnvironmentVariable("HELENGINE_BUILD_CONFIGURATION", "debug");
            Environment.SetEnvironmentVariable("HELENGINE_BUILD_PROFILE", "profiler");
            EditorPlatformBuildGraphWorkspaceFactory factory = new("C:\\Dev\\HelWorks\\SampleProject\\");

            EditorPlatformBuildGraphWorkspace firstWorkspace = factory.Create("ps2", "queue-a");
            EditorPlatformBuildGraphWorkspace secondWorkspace = factory.Create("ps2", "queue-b");

            Assert.Equal(firstWorkspace.ExecutionRootPath, secondWorkspace.ExecutionRootPath);
            Assert.Equal(firstWorkspace.GeneratedCoreRootPath, secondWorkspace.GeneratedCoreRootPath);
            Assert.Equal(firstWorkspace.BuilderWorkingRootPath, secondWorkspace.BuilderWorkingRootPath);
            Assert.EndsWith(Path.Combine("debug", "profiler", "build-graph"), firstWorkspace.ExecutionRootPath, StringComparison.OrdinalIgnoreCase);
            Assert.EndsWith(Path.Combine("debug", "profiler", "generated-core"), firstWorkspace.GeneratedCoreRootPath, StringComparison.OrdinalIgnoreCase);
            Assert.EndsWith(Path.Combine("debug", "profiler", "native"), firstWorkspace.BuilderWorkingRootPath, StringComparison.OrdinalIgnoreCase);
            Assert.False(
                firstWorkspace.BuilderWorkingRootPath.StartsWith(
                    firstWorkspace.ExecutionRootPath + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Ensures independently managed workspace roots cannot be equal or nested in either direction.
        /// </summary>
        /// <param name="executionRelativePath">Execution root relative to the test root.</param>
        /// <param name="generatedCoreRelativePath">Generated-core root relative to the test root.</param>
        /// <param name="nativeRelativePath">Native root relative to the test root.</param>
        /// <param name="firstConflictingRootName">First root name expected in the validation error.</param>
        /// <param name="secondConflictingRootName">Second root name expected in the validation error.</param>
        [Theory]
        [InlineData("Shared", "shared", "native", "Execution root", "generated-core root")]
        [InlineData("graph", "graph/generated-core", "native", "Execution root", "generated-core root")]
        [InlineData("generated-core/graph", "generated-core", "native", "Execution root", "generated-core root")]
        [InlineData("Shared", "generated-core", "shared", "Execution root", "native root")]
        [InlineData("graph", "generated-core", "graph/native", "Execution root", "native root")]
        [InlineData("native/graph", "generated-core", "native", "Execution root", "native root")]
        [InlineData("graph", "Shared", "shared", "generated-core root", "native root")]
        [InlineData("graph", "generated-core", "generated-core/native", "generated-core root", "native root")]
        [InlineData("graph", "native/generated-core", "native", "generated-core root", "native root")]
        public void ThreeRootConstructor_WhenRootsOverlap_Throws(
            string executionRelativePath,
            string generatedCoreRelativePath,
            string nativeRelativePath,
            string firstConflictingRootName,
            string secondConflictingRootName) {
            string rootPath = Path.Combine(Path.GetTempPath(), "helengine-workspace-overlap-tests", Guid.NewGuid().ToString("N"));
            string executionRootPath = Path.Combine(rootPath, executionRelativePath.Replace('/', Path.DirectorySeparatorChar));
            string generatedCoreRootPath = Path.Combine(rootPath, generatedCoreRelativePath.Replace('/', Path.DirectorySeparatorChar));
            string nativeRootPath = Path.Combine(rootPath, nativeRelativePath.Replace('/', Path.DirectorySeparatorChar));

            ArgumentException exception = Assert.Throws<ArgumentException>(() => new EditorPlatformBuildGraphWorkspace(
                executionRootPath,
                generatedCoreRootPath,
                nativeRootPath));

            Assert.Contains(firstConflictingRootName, exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(secondConflictingRootName, exception.Message, StringComparison.OrdinalIgnoreCase);
        }
    }
}
