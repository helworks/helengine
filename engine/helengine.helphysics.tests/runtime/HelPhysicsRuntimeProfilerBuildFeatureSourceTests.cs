namespace helengine.helphysics.tests.runtime;

/// <summary>
/// Audits the generated-player feature boundary that removes HelPhysics profiler contracts when runtime profiling is disabled.
/// </summary>
public sealed class HelPhysicsRuntimeProfilerBuildFeatureSourceTests {
    /// <summary>
    /// Verifies every HelPhysics dependency on core profiler types is enclosed by the same generated-runtime feature guard as those core types.
    /// </summary>
    [Fact]
    public void RuntimeProfiler_WhenGeneratedFeatureIsDisabled_IsExcludedFromHelPhysics() {
        string profilerMetricsSource = LoadSource("runtime", "HelPhysicsRuntimeProfilerMetrics3D.cs").Trim();
        Assert.StartsWith("#if !HELENGINE_CODEGEN_FEATURE_DISABLED_RUNTIME_PROFILER", profilerMetricsSource, StringComparison.Ordinal);
        Assert.EndsWith("#endif", profilerMetricsSource, StringComparison.Ordinal);

        string worldSource = LoadSource("runtime", "HelPhysicsWorld3D.cs");
        Assert.Contains("#if !HELENGINE_CODEGEN_FEATURE_DISABLED_RUNTIME_PROFILER\n        , IPhysicsRuntimeProfilerMetricsProvider\n#endif", worldSource, StringComparison.Ordinal);
        Assert.Contains("#if !HELENGINE_CODEGEN_FEATURE_DISABLED_RUNTIME_PROFILER\n        readonly HelPhysicsRuntimeProfilerMetrics3D RuntimeProfilerMetrics;", worldSource, StringComparison.Ordinal);
        Assert.Contains("#if !HELENGINE_CODEGEN_FEATURE_DISABLED_RUNTIME_PROFILER\n            RuntimeProfilerMetrics = new HelPhysicsRuntimeProfilerMetrics3D();", worldSource, StringComparison.Ordinal);
        Assert.Contains("#if !HELENGINE_CODEGEN_FEATURE_DISABLED_RUNTIME_PROFILER\n        public bool TryGetRuntimeProfilerMetrics", worldSource, StringComparison.Ordinal);
        Assert.Contains("#if !HELENGINE_CODEGEN_FEATURE_DISABLED_RUNTIME_PROFILER\n            RuntimeProfilerMetrics.Publish(", worldSource, StringComparison.Ordinal);

        string sceneRuntimeSource = LoadSource("runtime", "HelPhysicsSceneRuntime3D.cs");
        Assert.Contains("#if !HELENGINE_CODEGEN_FEATURE_DISABLED_RUNTIME_PROFILER\n        , IPhysicsRuntimeProfilerMetricsProvider\n#endif", sceneRuntimeSource, StringComparison.Ordinal);
        Assert.Contains("#if !HELENGINE_CODEGEN_FEATURE_DISABLED_RUNTIME_PROFILER\n        public bool TryGetRuntimeProfilerMetrics", sceneRuntimeSource, StringComparison.Ordinal);
    }

    /// <summary>
    /// Loads one HelPhysics source file with normalized line endings for deterministic source-boundary assertions.
    /// </summary>
    /// <param name="pathSegments">Path segments beneath the HelPhysics project directory.</param>
    /// <returns>Complete source text with line feeds normalized to <c>\n</c>.</returns>
    static string LoadSource(params string[] pathSegments) {
        string[] fullPathSegments = new string[pathSegments.Length + 3];
        fullPathSegments[0] = ResolveRepositoryRootPath();
        fullPathSegments[1] = "engine";
        fullPathSegments[2] = "helengine.helphysics";
        Array.Copy(pathSegments, 0, fullPathSegments, 3, pathSegments.Length);
        return File.ReadAllText(Path.Combine(fullPathSegments)).ReplaceLineEndings("\n");
    }

    /// <summary>
    /// Resolves the isolated HelEngine repository root from the test assembly output directory.
    /// </summary>
    /// <returns>Absolute repository root containing the engine and editor solution trees.</returns>
    static string ResolveRepositoryRootPath() {
        string currentDirectory = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(currentDirectory)) {
            if (Directory.Exists(Path.Combine(currentDirectory, "engine"))
                && Directory.Exists(Path.Combine(currentDirectory, "helengine.ui"))
                && File.Exists(Path.Combine(currentDirectory, "helengine.ui", "helengine.sln"))) {
                return currentDirectory;
            }

            currentDirectory = Path.GetDirectoryName(currentDirectory);
        }

        throw new InvalidOperationException("Unable to resolve the HelEngine repository root from the HelPhysics test assembly.");
    }
}
