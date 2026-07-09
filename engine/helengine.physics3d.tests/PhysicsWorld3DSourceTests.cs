namespace helengine {
    /// <summary>
    /// Verifies the player-facing custom physics world source no longer roots cooked-scene feature analysis at runtime.
    /// </summary>
    public sealed class PhysicsWorld3DSourceTests {
        /// <summary>
        /// Ensures scene binding does not re-analyze authored physics features during runtime binding.
        /// </summary>
        [Fact]
        public void BindScene_source_does_not_reanalyze_scene_features_at_runtime() {
            string sourcePath = Path.Combine(
                ResolveRepositoryRootPath(),
                "engine",
                "helengine.physics3d",
                "PhysicsWorld3D.cs");

            string source = File.ReadAllText(sourcePath);

            Assert.DoesNotContain("PhysicsSceneFeatureAnalyzer3D.Analyze(rootEntities)", source, StringComparison.Ordinal);
            Assert.DoesNotContain("RequiredSceneFeatures", source, StringComparison.Ordinal);
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
