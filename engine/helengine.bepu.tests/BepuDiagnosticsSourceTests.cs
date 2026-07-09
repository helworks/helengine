namespace helengine.bepu.tests {
    /// <summary>
    /// Verifies the optional BEPU diagnostics sources can be stripped through the generic disabled-feature codegen seam.
    /// </summary>
    public sealed class BepuDiagnosticsSourceTests {
        /// <summary>
        /// Ensures the managed BEPU diagnostics bridge can be compiled down to the generic disabled-feature path instead of always carrying the heavy trace implementation.
        /// </summary>
        [Fact]
        public void BepuPhysicsWorld3DDiagnostics_source_uses_generic_disabled_feature_guard() {
            string sourcePath = Path.Combine(
                ResolveRepositoryRootPath(),
                "engine",
                "helengine.bepu",
                "BepuPhysicsWorld3DDiagnostics.cs");

            string source = File.ReadAllText(sourcePath);

            Assert.Contains("#if HELENGINE_CODEGEN_FEATURE_DISABLED_PHYSICS3D_DIAGNOSTICS", source, StringComparison.Ordinal);
        }

        /// <summary>
        /// Ensures the vendor-side BEPU native-conversion diagnostics source also respects the same generic disabled-feature guard.
        /// </summary>
        [Fact]
        public void BepuNativeConversionDiagnostics_source_uses_generic_disabled_feature_guard() {
            string sourcePath = Path.Combine(
                ResolveRepositoryRootPath(),
                "engine",
                "vendor",
                "bepuphysics2",
                "BepuPhysics",
                "BepuNativeConversionDiagnostics.cs");

            string source = File.ReadAllText(sourcePath);

            Assert.Contains("#if HELENGINE_CODEGEN_FEATURE_DISABLED_PHYSICS3D_DIAGNOSTICS", source, StringComparison.Ordinal);
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
