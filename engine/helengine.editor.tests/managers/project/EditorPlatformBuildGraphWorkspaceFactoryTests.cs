using Xunit;

namespace helengine.editor.tests.managers.project {
    /// <summary>
    /// Verifies that queued platform-build executions receive private workspaces even when they originate from the same queue item.
    /// </summary>
    public sealed class EditorPlatformBuildGraphWorkspaceFactoryTests {
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

                resetMethod.Invoke(null, [executionRootPath, cookRootPath, packageRootPath, builderRootPath]);

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
    }
}
