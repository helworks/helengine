using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests.managers.project {
    /// <summary>
    /// Verifies the central build-isolation resolver produces stable project-scoped platform roots for concurrent build workflows.
    /// </summary>
    [Collection(EditorBuildCacheEnvironmentCollection.Name)]
    public sealed class EditorBuildIsolationPathResolverTests : IDisposable {
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
        /// Initializes one resolver test with stable-cache mode disabled by default.
        /// </summary>
        public EditorBuildIsolationPathResolverTests() {
            Environment.SetEnvironmentVariable("HELENGINE_BUILD_CACHE_ROOT", null);
            Environment.SetEnvironmentVariable("HELENGINE_BUILD_CONFIGURATION", null);
            Environment.SetEnvironmentVariable("HELENGINE_BUILD_PROFILE", null);
            Environment.SetEnvironmentVariable("HELENGINE_BUILD_WORKSPACE_ROOT", null);
        }

        /// <summary>
        /// Restores every process environment variable changed by resolver tests.
        /// </summary>
        public void Dispose() {
            Environment.SetEnvironmentVariable("HELENGINE_BUILD_CACHE_ROOT", OriginalCacheRoot);
            Environment.SetEnvironmentVariable("HELENGINE_BUILD_CONFIGURATION", OriginalConfiguration);
            Environment.SetEnvironmentVariable("HELENGINE_BUILD_PROFILE", OriginalProfile);
            Environment.SetEnvironmentVariable("HELENGINE_BUILD_WORKSPACE_ROOT", OriginalWorkspaceRoot);
        }

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

        /// <summary>
        /// Ensures generated solution files use the same invocation-private workspace as their compiler outputs.
        /// </summary>
        [Fact]
        public void ResolveGeneratedCodeWorkspaceRootPath_WhenInvocationIsProvided_NestsWorkspaceFilesBelowThePrivateGeneratedRoot() {
            EditorBuildIsolationPathResolver resolver = new(Path.Combine(Path.GetTempPath(), "helengine-isolation-tests", "command-project"));

            string workspaceRootPath = resolver.ResolveGeneratedCodeWorkspaceRootPath("editor-command", "command-a");

            Assert.EndsWith(
                Path.Combine("editor-command", "workspace", "command-a", "generated-dotnet", "workspace"),
                workspaceRootPath,
                StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Ensures stable-cache mode ignores invocation ids and uses the wrapper-compatible project identity.
        /// </summary>
        [Fact]
        public void ResolveGeneratedCodeOutputRootPath_WhenStableCacheIsConfigured_ReturnsDeterministicProfileRoot() {
            string cacheRootPath = Path.Combine(Path.GetTempPath(), "helengine-stable-cache-tests", Guid.NewGuid().ToString("N"));
            Environment.SetEnvironmentVariable("HELENGINE_BUILD_CACHE_ROOT", cacheRootPath);
            Environment.SetEnvironmentVariable("HELENGINE_BUILD_CONFIGURATION", "debug");
            Environment.SetEnvironmentVariable("HELENGINE_BUILD_PROFILE", "profiler");
            EditorBuildIsolationPathResolver resolver = new("C:\\Dev\\HelWorks\\SampleProject\\");

            string firstOutputRootPath = resolver.ResolveGeneratedCodeOutputRootPath("ps2", "execution-a");
            string secondOutputRootPath = resolver.ResolveGeneratedCodeOutputRootPath("ps2", "execution-b");

            Assert.Equal(firstOutputRootPath, secondOutputRootPath);
            Assert.EndsWith(
                Path.Combine("v2", "8db35f03fc461cbce04997a159b92bcc", "b", "ps2", "debug", "profiler", "generated-dotnet"),
                firstOutputRootPath,
                StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Ensures caller casing cannot create a second cache identity for the same Windows project path.
        /// </summary>
        [Fact]
        public void ResolveGeneratedCodeOutputRootPath_WhenProjectCasingDiffers_ReturnsSameStablePath() {
            string cacheRootPath = Path.Combine(Path.GetTempPath(), "helengine-stable-cache-tests", Guid.NewGuid().ToString("N"));
            Environment.SetEnvironmentVariable("HELENGINE_BUILD_CACHE_ROOT", cacheRootPath);
            Environment.SetEnvironmentVariable("HELENGINE_BUILD_CONFIGURATION", "debug");
            Environment.SetEnvironmentVariable("HELENGINE_BUILD_PROFILE", "profiler");
            EditorBuildIsolationPathResolver mixedCaseResolver = new("C:\\Dev\\HelWorks\\SampleProject\\");
            EditorBuildIsolationPathResolver lowerCaseResolver = new("c:\\dev\\helworks\\sampleproject");

            string mixedCasePath = mixedCaseResolver.ResolveGeneratedCodeOutputRootPath("ps2", "execution-a");
            string lowerCasePath = lowerCaseResolver.ResolveGeneratedCodeOutputRootPath("ps2", "execution-b");

            Assert.Equal(mixedCasePath, lowerCasePath);
        }

        /// <summary>
        /// Ensures stable-cache mode requires both dimensions that identify a build profile root.
        /// </summary>
        /// <param name="configuration">Optional build configuration supplied to the resolver.</param>
        /// <param name="profile">Optional build profile supplied to the resolver.</param>
        [Theory]
        [InlineData(null, "profiler")]
        [InlineData("debug", null)]
        public void ResolveGeneratedCodeOutputRootPath_WhenStableDimensionIsMissing_Throws(string configuration, string profile) {
            Environment.SetEnvironmentVariable("HELENGINE_BUILD_CACHE_ROOT", Path.Combine(Path.GetTempPath(), "helengine-stable-cache-tests"));
            Environment.SetEnvironmentVariable("HELENGINE_BUILD_CONFIGURATION", configuration);
            Environment.SetEnvironmentVariable("HELENGINE_BUILD_PROFILE", profile);
            EditorBuildIsolationPathResolver resolver = new("C:\\Dev\\HelWorks\\SampleProject");

            Assert.Throws<InvalidOperationException>(() => resolver.ResolveGeneratedCodeOutputRootPath("ps2", "execution-a"));
        }

        /// <summary>
        /// Ensures stable path dimensions cannot traverse or alias another Windows cache directory.
        /// </summary>
        /// <param name="platformId">Platform segment supplied to the resolver.</param>
        /// <param name="configuration">Configuration segment supplied to the resolver.</param>
        /// <param name="profile">Profile segment supplied to the resolver.</param>
        [Theory]
        [InlineData("..", "debug", "profiler")]
        [InlineData("ps2", "debug.", "profiler")]
        [InlineData("ps2", "debug", "NUL")]
        public void ResolveGeneratedCodeOutputRootPath_WhenStableSegmentAliasesAWindowsPath_Throws(
            string platformId,
            string configuration,
            string profile) {
            Environment.SetEnvironmentVariable("HELENGINE_BUILD_CACHE_ROOT", Path.Combine(Path.GetTempPath(), "helengine-stable-cache-tests"));
            Environment.SetEnvironmentVariable("HELENGINE_BUILD_CONFIGURATION", configuration);
            Environment.SetEnvironmentVariable("HELENGINE_BUILD_PROFILE", profile);
            EditorBuildIsolationPathResolver resolver = new("C:\\Dev\\HelWorks\\SampleProject");

            Assert.ThrowsAny<ArgumentException>(() => resolver.ResolveGeneratedCodeOutputRootPath(platformId, "execution-a"));
        }

        /// <summary>
        /// Ensures the deprecated configured workspace layout remains byte-for-byte compatible outside stable mode.
        /// </summary>
        [Fact]
        public void ResolveWorkspaceExecutionRootPath_WhenDeprecatedWorkspaceRootIsConfigured_PreservesCompactLayout() {
            string workspaceRootPath = Path.Combine(Path.GetTempPath(), "helengine-configured-workspace-tests", Guid.NewGuid().ToString("N"));
            Environment.SetEnvironmentVariable("HELENGINE_BUILD_WORKSPACE_ROOT", workspaceRootPath);
            EditorBuildIsolationPathResolver resolver = new(Path.Combine(Path.GetTempPath(), "helengine-isolation-tests", "configured-project"));

            string queueRootPath = resolver.ResolveWorkspaceExecutionRootPath("ps2", "queue-a");
            string executionRootPath = resolver.ResolveWorkspaceExecutionRootPath("ps2", "queue-a", "execution-a");
            string generatedRootPath = resolver.ResolveGeneratedCodeOutputRootPath("ps2", "execution-a");

            Assert.Equal(Path.Combine(workspaceRootPath, "ps2", "queue-a"), queueRootPath);
            Assert.Equal(Path.Combine(workspaceRootPath, "ps2", "execution-a"), executionRootPath);
            Assert.Equal(Path.Combine(workspaceRootPath, "ps2", "execution-a", "generated-dotnet"), generatedRootPath);
        }
    }
}
