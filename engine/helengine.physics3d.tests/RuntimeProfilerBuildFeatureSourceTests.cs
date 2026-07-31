namespace helengine {
    /// <summary>
    /// Audits the source boundary that removes generic runtime profiling from ordinary generated players.
    /// </summary>
    public sealed class RuntimeProfilerBuildFeatureSourceTests {
        /// <summary>
        /// Ensures profiler data contracts, core collection, and physics providers all honor the generated-runtime feature boundary.
        /// </summary>
        [Fact]
        public void RuntimeProfiler_WhenGeneratedFeatureIsDisabled_IsExcludedFromCoreAndPhysicsProviders() {
            AssertProfilerFileIsFeatureGuarded("diagnostics", "RuntimeProfilerMetrics.cs");
            AssertProfilerFileIsFeatureGuarded("diagnostics", "RuntimeProfilerMetricsSnapshot.cs");
            AssertProfilerFileIsFeatureGuarded("diagnostics", "RuntimePhysicsProfilerMetrics.cs");
            AssertProfilerFileIsFeatureGuarded("physics", "IPhysicsRuntimeProfilerMetricsProvider.cs");

            string coreSource = LoadSource("helengine.core", "Core.cs");
            Assert.Contains("#if !HELENGINE_CODEGEN_FEATURE_DISABLED_RUNTIME_PROFILER\n        readonly RuntimeProfilerMetrics RuntimeProfilerMetricsValue;", coreSource, StringComparison.Ordinal);
            Assert.Contains("#if !HELENGINE_CODEGEN_FEATURE_DISABLED_RUNTIME_PROFILER\n            RuntimeProfilerMetricsValue.BeginFrame();", coreSource, StringComparison.Ordinal);
            Assert.Contains("#if !HELENGINE_CODEGEN_FEATURE_DISABLED_RUNTIME_PROFILER\n            RuntimeProfilerMetricsValue.SetFixedUpdateCount(consumedStepCount);\n            if (PhysicsRuntimeValue is IPhysicsRuntimeProfilerMetricsProvider", coreSource, StringComparison.Ordinal);

            AssertPhysicsProviderIsFeatureGuarded("helengine.bepu", "BepuPhysicsWorld3D.cs");
            AssertPhysicsProviderIsFeatureGuarded("helengine.physics3d", "PhysicsWorld3D.cs");
            AssertPhysicsProviderIsFeatureGuarded("helengine.physics3d", "PhysicsWorld3DCompatibilityRuntime.cs");
        }

        /// <summary>
        /// Verifies one profiler-only source file is enclosed by the generated-runtime profiler guard.
        /// </summary>
        /// <param name="directoryName">Directory beneath <c>helengine.core</c> that owns the source file.</param>
        /// <param name="fileName">Profiler-only source filename.</param>
        static void AssertProfilerFileIsFeatureGuarded(string directoryName, string fileName) {
            string source = LoadSource("helengine.core", directoryName, fileName).Trim();
            Assert.StartsWith("#if !HELENGINE_CODEGEN_FEATURE_DISABLED_RUNTIME_PROFILER", source, StringComparison.Ordinal);
            Assert.EndsWith("#endif", source, StringComparison.Ordinal);
        }

        /// <summary>
        /// Verifies one physics runtime conditionally implements and defines the profiler metrics provider contract.
        /// </summary>
        /// <param name="projectDirectoryName">Physics project directory beneath <c>engine</c>.</param>
        /// <param name="fileName">Physics runtime source filename.</param>
        static void AssertPhysicsProviderIsFeatureGuarded(string projectDirectoryName, string fileName) {
            string source = LoadSource(projectDirectoryName, fileName);
            Assert.Contains("#if !HELENGINE_CODEGEN_FEATURE_DISABLED_RUNTIME_PROFILER\n        , IPhysicsRuntimeProfilerMetricsProvider\n#endif", source, StringComparison.Ordinal);
            Assert.Contains("#if !HELENGINE_CODEGEN_FEATURE_DISABLED_RUNTIME_PROFILER\n        public bool TryGetRuntimeProfilerMetrics", source, StringComparison.Ordinal);
        }

        /// <summary>
        /// Loads one engine source file for a deterministic feature-boundary assertion.
        /// </summary>
        /// <param name="pathSegments">Path segments beneath the repository's <c>engine</c> directory.</param>
        /// <returns>Complete source text from the requested file.</returns>
        static string LoadSource(params string[] pathSegments) {
            string[] fullPathSegments = new string[pathSegments.Length + 2];
            fullPathSegments[0] = ResolveRepositoryRootPath();
            fullPathSegments[1] = "engine";
            Array.Copy(pathSegments, 0, fullPathSegments, 2, pathSegments.Length);
            return File.ReadAllText(Path.Combine(fullPathSegments)).Replace("\r\n", "\n", StringComparison.Ordinal);
        }

        /// <summary>
        /// Resolves the repository root for source-audit assertions.
        /// </summary>
        /// <returns>Absolute HelEngine repository root path.</returns>
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

            throw new InvalidOperationException("Unable to resolve the HelEngine repository root from the current test directory.");
        }
    }
}
