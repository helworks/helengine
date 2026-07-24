using Xunit;

namespace helengine.editor.tests.managers.project {
    /// <summary>
    /// Verifies the central build-isolation resolver produces stable project-scoped platform roots for concurrent build workflows.
    /// </summary>
    public sealed class EditorBuildIsolationPathResolverTests {
        /// <summary>
        /// Ensures the same project and platform always resolve to the same platform root while different projects remain isolated.
        /// </summary>
        [Fact]
        public void ResolvePlatformRootPath_WhenProjectAndPlatformAreRepeated_ReturnsStableProjectScopedRoot() {
            string firstProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-isolation-tests", "first-project");
            string secondProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-isolation-tests", "second-project");
            EditorBuildIsolationPathResolver firstResolver = new(firstProjectRootPath);
            EditorBuildIsolationPathResolver secondResolver = new(firstProjectRootPath);
            EditorBuildIsolationPathResolver thirdResolver = new(secondProjectRootPath);

            string firstPath = firstResolver.ResolvePlatformRootPath("ds");
            string repeatedPath = secondResolver.ResolvePlatformRootPath("ds");
            string otherProjectPath = thirdResolver.ResolvePlatformRootPath("ds");

            Assert.Equal(firstPath, repeatedPath);
            Assert.NotEqual(firstPath, otherProjectPath);
            Assert.Contains(Path.Combine("helengine-builds"), firstPath, StringComparison.OrdinalIgnoreCase);
            Assert.EndsWith(Path.Combine("ds"), firstPath, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Ensures one queue item execution root stays beneath the resolved platform workspace root.
        /// </summary>
        [Fact]
        public void ResolveWorkspaceExecutionRootPath_WhenQueueItemIsProvided_NestsQueueItemBeneathPlatformWorkspaceRoot() {
            EditorBuildIsolationPathResolver resolver = new(Path.Combine(Path.GetTempPath(), "helengine-isolation-tests", "workspace-project"));

            string executionRootPath = resolver.ResolveWorkspaceExecutionRootPath("windows", "queue-123");

            Assert.EndsWith(Path.Combine("windows", "workspace", "queue-123"), executionRootPath, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Ensures generated script builds receive distinct invocation-scoped output roots outside the authored project tree.
        /// </summary>
        [Fact]
        public void ResolveGeneratedCodeOutputRootPath_WhenInvocationsDiffer_ReturnsDistinctInvocationScopedOutputRoots() {
            EditorBuildIsolationPathResolver resolver = new(Path.Combine(Path.GetTempPath(), "helengine-isolation-tests", "code-project"));

            string firstOutputRootPath = resolver.ResolveGeneratedCodeOutputRootPath("vita", "cli-build-a");
            string secondOutputRootPath = resolver.ResolveGeneratedCodeOutputRootPath("vita", "cli-build-b");

            Assert.NotEqual(firstOutputRootPath, secondOutputRootPath);
            Assert.EndsWith(Path.Combine("vita", "workspace", "cli-build-a", "generated-dotnet"), firstOutputRootPath, StringComparison.OrdinalIgnoreCase);
            Assert.EndsWith(Path.Combine("vita", "workspace", "cli-build-b", "generated-dotnet"), secondOutputRootPath, StringComparison.OrdinalIgnoreCase);
        }
    }
}
