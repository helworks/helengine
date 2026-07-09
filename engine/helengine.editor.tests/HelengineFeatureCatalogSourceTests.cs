namespace helengine.editor.tests {
    /// <summary>
    /// Verifies the HelEngine-owned codegen feature catalog exposes engine runtime features that platform profiles may force-disable.
    /// </summary>
    public sealed class HelengineFeatureCatalogSourceTests {
        /// <summary>
        /// Ensures the shared feature catalog registers the optional 3D physics diagnostics feature so platform codegen profiles can disable it without crashing codegen.
        /// </summary>
        [Fact]
        public void Feature_catalog_registers_physics3d_diagnostics_feature() {
            string sourcePath = Path.Combine(
                ResolveRepositoryRootPath(),
                "engine",
                "helengine.editor",
                "codegen",
                "features",
                "helengine-feature-catalog.json");

            string source = File.ReadAllText(sourcePath);

            Assert.Contains("\"id\": \"physics3d.diagnostics\"", source, StringComparison.Ordinal);
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
