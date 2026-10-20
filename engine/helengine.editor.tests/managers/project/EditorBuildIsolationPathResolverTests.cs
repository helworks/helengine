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
        /// Ensures one default queue workspace stays beneath the resolved platform root without exposing the queue identifier.
        /// </summary>
        [Fact]
        public void ResolveWorkspaceExecutionRootPath_WhenQueueItemIsProvided_UsesCompactDeterministicQueueSegment() {
            EditorBuildIsolationPathResolver resolver = new(Path.Combine(Path.GetTempPath(), "helengine-isolation-tests", "workspace-project"));

            string executionRootPath = resolver.ResolveWorkspaceExecutionRootPath("windows", "queue-123");
            string repeatedRootPath = resolver.ResolveWorkspaceExecutionRootPath("windows", "queue-123");
            string platformRootPath = resolver.ResolvePlatformRootPath("windows");
            string[] relativeSegments = Path.GetRelativePath(platformRootPath, executionRootPath)
                .Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);

            Assert.Equal(executionRootPath, repeatedRootPath);
            Assert.StartsWith(platformRootPath + Path.DirectorySeparatorChar, executionRootPath, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(["w"], relativeSegments[..1]);
            Assert.Matches("^[0-9a-v]{13}$", relativeSegments[1]);
            Assert.DoesNotContain("queue-123", executionRootPath, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Ensures default queue and execution identifiers produce deterministic but isolated invocation roots.
        /// </summary>
        [Fact]
        public void ResolveWorkspaceExecutionRootPath_WhenQueueOrExecutionChanges_ReturnsDistinctCompactRoots() {
            EditorBuildIsolationPathResolver resolver = new(Path.Combine(Path.GetTempPath(), "helengine-isolation-tests", "invocation-project"));

            string firstRootPath = resolver.ResolveWorkspaceExecutionRootPath("windows", "queue-a", "execution-a");
            string repeatedRootPath = resolver.ResolveWorkspaceExecutionRootPath("windows", "queue-a", "execution-a");
            string otherQueueRootPath = resolver.ResolveWorkspaceExecutionRootPath("windows", "queue-b", "execution-a");
            string otherExecutionRootPath = resolver.ResolveWorkspaceExecutionRootPath("windows", "queue-a", "execution-b");
            string platformRootPath = resolver.ResolvePlatformRootPath("windows");
            string[] relativeSegments = Path.GetRelativePath(platformRootPath, firstRootPath)
                .Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);

            Assert.Equal(firstRootPath, repeatedRootPath);
            Assert.NotEqual(firstRootPath, otherQueueRootPath);
            Assert.NotEqual(firstRootPath, otherExecutionRootPath);
            Assert.Equal(3, relativeSegments.Length);
            Assert.Equal("w", relativeSegments[0]);
            Assert.Matches("^[0-9a-v]{13}$", relativeSegments[1]);
            Assert.Matches("^[0-9a-v]{13}$", relativeSegments[2]);
            Assert.DoesNotContain("queue-a", firstRootPath, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("execution-a", firstRootPath, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Ensures generated script builds receive distinct invocation-scoped output roots outside the authored project tree.
        /// </summary>
        [Fact]
        public void ResolveGeneratedCodeOutputRootPath_WhenInvocationsDiffer_ReturnsDistinctInvocationScopedOutputRoots() {
            EditorBuildIsolationPathResolver resolver = new(Path.Combine(Path.GetTempPath(), "helengine-isolation-tests", "code-project"));

            string firstOutputRootPath = resolver.ResolveGeneratedCodeOutputRootPath("vita", "cli-build-a");
            string secondOutputRootPath = resolver.ResolveGeneratedCodeOutputRootPath("vita", "cli-build-b");
            string platformRootPath = resolver.ResolvePlatformRootPath("vita");

            Assert.NotEqual(firstOutputRootPath, secondOutputRootPath);
            Assert.StartsWith(platformRootPath + Path.DirectorySeparatorChar, firstOutputRootPath, StringComparison.OrdinalIgnoreCase);
            Assert.StartsWith(platformRootPath + Path.DirectorySeparatorChar, secondOutputRootPath, StringComparison.OrdinalIgnoreCase);
            Assert.EndsWith(Path.Combine("generated-dotnet"), firstOutputRootPath, StringComparison.OrdinalIgnoreCase);
            Assert.EndsWith(Path.Combine("generated-dotnet"), secondOutputRootPath, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("cli-build-a", firstOutputRootPath, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("cli-build-b", secondOutputRootPath, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Ensures generated solution files use the same invocation-private workspace as their compiler outputs.
        /// </summary>
        [Fact]
        public void ResolveGeneratedCodeWorkspaceRootPath_WhenInvocationIsProvided_NestsWorkspaceFilesBelowThePrivateGeneratedRoot() {
            EditorBuildIsolationPathResolver resolver = new(Path.Combine(Path.GetTempPath(), "helengine-isolation-tests", "command-project"));

            string workspaceRootPath = resolver.ResolveGeneratedCodeWorkspaceRootPath("editor-command", "command-a");
            string platformRootPath = resolver.ResolvePlatformRootPath("editor-command");

            Assert.StartsWith(platformRootPath + Path.DirectorySeparatorChar, workspaceRootPath, StringComparison.OrdinalIgnoreCase);
            Assert.EndsWith(Path.Combine("generated-dotnet", "workspace"), workspaceRootPath, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("command-a", workspaceRootPath, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Ensures authored generated-code project files have one stable project-scoped workspace while compiler outputs remain invocation-isolated.
        /// </summary>
        [Fact]
        public void ResolveGeneratedCodeProjectWorkspaceRootPath_WhenInvocationsDiffer_ReturnsStableWorkspaceAlongsideDistinctExecutionOutputs() {
            EditorBuildIsolationPathResolver resolver = new(Path.Combine(Path.GetTempPath(), "helengine-isolation-tests", "stable-code-project"));

            string firstWorkspaceRootPath = resolver.ResolveGeneratedCodeProjectWorkspaceRootPath();
            string repeatedWorkspaceRootPath = resolver.ResolveGeneratedCodeProjectWorkspaceRootPath();
            string firstOutputRootPath = resolver.ResolveGeneratedCodeOutputRootPath("editor-command", "command-a");
            string secondOutputRootPath = resolver.ResolveGeneratedCodeOutputRootPath("editor-command", "command-b");

            Assert.Equal(firstWorkspaceRootPath, repeatedWorkspaceRootPath);
            Assert.EndsWith(Path.Combine("cache", "generated_code"), firstWorkspaceRootPath, StringComparison.OrdinalIgnoreCase);
            Assert.NotEqual(firstOutputRootPath, secondOutputRootPath);
            Assert.DoesNotContain("command-a", firstWorkspaceRootPath, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("command-b", firstWorkspaceRootPath, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Ensures editor-full and runtime-only solution routes never publish different project sets into one metadata root.
        /// </summary>
        [Fact]
        public void ResolveGeneratedCodeProjectWorkspaceRootPath_WhenRoutesDiffer_ReturnsStableRouteScopedRoots() {
            EditorBuildIsolationPathResolver resolver = new(Path.Combine(Path.GetTempPath(), "helengine-isolation-tests", "route-scoped-code-project"));

            string editorWorkspaceRootPath = resolver.ResolveGeneratedCodeProjectWorkspaceRootPath("editor-command", EditorScriptCompilationMode.EditorFull);
            string repeatedEditorWorkspaceRootPath = resolver.ResolveGeneratedCodeProjectWorkspaceRootPath("editor-command", EditorScriptCompilationMode.EditorFull);
            string runtimeWorkspaceRootPath = resolver.ResolveGeneratedCodeProjectWorkspaceRootPath("platform-build", EditorScriptCompilationMode.RuntimeOnly);

            Assert.Equal(editorWorkspaceRootPath, repeatedEditorWorkspaceRootPath);
            Assert.NotEqual(editorWorkspaceRootPath, runtimeWorkspaceRootPath);
            Assert.EndsWith(Path.Combine("cache", "generated_code", "editor-command", "EditorFull"), editorWorkspaceRootPath, StringComparison.OrdinalIgnoreCase);
            Assert.EndsWith(Path.Combine("cache", "generated_code", "platform-build", "RuntimeOnly"), runtimeWorkspaceRootPath, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Ensures route identifiers cannot escape or alias a route-scoped metadata root on any supported filesystem.
        /// </summary>
        [Theory]
        [InlineData("..")]
        [InlineData(".")]
        [InlineData("route/child")]
        [InlineData("route\\child")]
        [InlineData("route.")]
        [InlineData("route ")]
        [InlineData("route:child")]
        [InlineData("CON.txt")]
        [InlineData("COM1")]
        [InlineData("NUL")]
        public void ResolveGeneratedCodeProjectWorkspaceRootPath_WhenRouteIdIsUnsafe_Throws(string routeId) {
            EditorBuildIsolationPathResolver resolver = new(Path.Combine(Path.GetTempPath(), "helengine-isolation-tests", "unsafe-route-project"));

            Assert.ThrowsAny<ArgumentException>(() => resolver.ResolveGeneratedCodeProjectWorkspaceRootPath(routeId, EditorScriptCompilationMode.EditorFull));
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

        /// <summary>Separates native option and profile scopes beneath a deterministic project-owned cache namespace.</summary>
        [Fact]
        public void ResolveNativeObjectCacheRootPath_StableCache_PreservesProjectAndInputIsolation() {
            string root = Path.Combine(Directory.GetCurrentDirectory(), "builds", "native-cache-path-tests");
            Environment.SetEnvironmentVariable("HELENGINE_BUILD_CACHE_ROOT", Path.Combine(root, "cache"));
            string project = Path.Combine(root, "first-project");
            string scope = Path.Combine(root, "profile", "debug", "native");
            string fingerprint = new string('a', 64);
            EditorBuildIsolationPathResolver resolver = new(project);
            string first = resolver.ResolveNativeObjectCacheRootPath(scope, fingerprint);
            Assert.Equal(first, new EditorBuildIsolationPathResolver(project).ResolveNativeObjectCacheRootPath(scope, fingerprint));
            Assert.NotEqual(first, new EditorBuildIsolationPathResolver(Path.Combine(root, "second-project")).ResolveNativeObjectCacheRootPath(scope, fingerprint));
            Assert.NotEqual(first, resolver.ResolveNativeObjectCacheRootPath(Path.Combine(root, "profile", "release", "native"), fingerprint));
            Assert.NotEqual(first, resolver.ResolveNativeObjectCacheRootPath(scope, new string('b', 64)));
            Assert.StartsWith(Path.Combine(root, "cache", "v2"), first, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("n", Path.GetFileName(Path.GetDirectoryName(first)));
            Assert.Matches("^[a-z2-7]{52}$", Path.GetFileName(first));
        }

        /// <summary>Keeps direct-editor native objects inside the authored project's visible cache when no wrapper cache is configured.</summary>
        [Fact]
        public void ResolveNativeObjectCacheRootPath_DirectEditor_UsesAuthoredProjectCache() {
            string project = Path.Combine(Directory.GetCurrentDirectory(), "builds", "native-cache-path-tests", "project");
            EditorBuildIsolationPathResolver resolver = new(project);
            string resolved = resolver.ResolveNativeObjectCacheRootPath(Path.Combine(project, "cache", "build", "windows", "debug", "native"), new string('a', 64));
            Assert.StartsWith(Path.Combine(project, "cache", "native"), resolved, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Reproduces the long host filename that exceeded MAX_PATH and verifies the compact layout also fits CMake's object-path budget.</summary>
        [Fact]
        public void ResolveNativeObjectCacheRootPath_CanonicalHostObject_StaysBelowCMakeAndMsvcLimits() {
            Environment.SetEnvironmentVariable("HELENGINE_BUILD_CACHE_ROOT", @"C:\dev\helworks\builds\helengine\cache");
            EditorBuildIsolationPathResolver resolver = new(@"C:\dev\helprojs\demodisc");
            string fingerprint = new string('a', 64);
            string objectRoot = resolver.ResolveNativeObjectCacheRootPath(
                @"C:\dev\helworks\builds\helengine\cache\v2\5b89b8be7cb64a4decc58e30c0a34e39\b\windows\debug\debug\native", fingerprint);
            string objectPath = Path.Combine(objectRoot, "CMakeFiles", "helengine_windows.dir", "src", "platform",
                "windows", "runtime", "runtime_player_profile_configuration_error.cpp.obj");
            Assert.True(objectPath.Length < 250, objectPath);
        }

        /// <summary>Rejects missing cache scope or option identity instead of sharing a default object directory.</summary>
        [Fact]
        public void ResolveNativeObjectCacheRootPath_MissingIdentity_Throws() {
            EditorBuildIsolationPathResolver resolver = new(Directory.GetCurrentDirectory());
            Assert.Throws<ArgumentException>(() => resolver.ResolveNativeObjectCacheRootPath(null, new string('a', 64)));
            Assert.Throws<ArgumentException>(() => resolver.ResolveNativeObjectCacheRootPath(Directory.GetCurrentDirectory(), ""));
        }
        /// <summary>Headless generated project metadata honors the same external stable cache as its compiler output.</summary>
        [Fact]
        public void GeneratedProjectMetadataUsesExternalStableCache() {
            string cache = @"C:\temp\numeric-precision\windows\cache";
            Environment.SetEnvironmentVariable("HELENGINE_BUILD_CACHE_ROOT", cache);
            Environment.SetEnvironmentVariable("HELENGINE_BUILD_CONFIGURATION", "debug");
            Environment.SetEnvironmentVariable("HELENGINE_BUILD_PROFILE", "profiler");
            EditorBuildIsolationPathResolver resolver = new(@"C:\dev\helprojs\demodisc");
            string workspace = resolver.ResolveGeneratedCodeProjectWorkspaceRootPath("windows", EditorScriptCompilationMode.RuntimeOnly);
            Assert.StartsWith(cache + Path.DirectorySeparatorChar, workspace, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(workspace, resolver.ResolveGeneratedCodeProjectWorkspaceRootPath("windows", EditorScriptCompilationMode.RuntimeOnly));
            Assert.StartsWith(workspace + Path.DirectorySeparatorChar,
                resolver.ResolveGeneratedCodeProjectOutputRootPath("windows", EditorScriptCompilationMode.RuntimeOnly));
        }
    }
}
