namespace helengine.editor.tests.managers.project;

/// <summary>
/// Verifies the checked-in helengine codegen feature catalog remains present and declares the expected caller-owned feature ids.
/// </summary>
public class HelengineFeatureCatalogIntegrityTests {
    /// <summary>
    /// Verifies the checked-in helengine feature catalog includes the currently expected feature ids used by generated-core builds.
    /// </summary>
    [Fact]
    public void HelengineFeatureCatalog_declares_expected_feature_ids() {
        string normalizedFilePath = ResolveFeatureCatalogPath();

        Assert.True(File.Exists(normalizedFilePath));

        string json = File.ReadAllText(normalizedFilePath);
        Assert.Contains("\"shaders\"", json, StringComparison.Ordinal);
        Assert.Contains("\"render2d\"", json, StringComparison.Ordinal);
        Assert.Contains("\"host_file_system\"", json, StringComparison.Ordinal);
    }

    /// <summary>
    /// Resolves the checked-in helengine feature catalog path by searching upward from the test process base directory.
    /// </summary>
    /// <returns>Absolute path to the checked-in helengine feature catalog.</returns>
    static string ResolveFeatureCatalogPath() {
        string relativeCatalogPath = Path.Combine(
            "engine",
            "helengine.editor",
            "codegen",
            "features",
            "helengine-feature-catalog.json");
        DirectoryInfo currentDirectory = new DirectoryInfo(Path.GetFullPath(AppContext.BaseDirectory));

        for (int depth = 0; depth < 10 && currentDirectory != null; depth++) {
            string candidatePath = Path.Combine(currentDirectory.FullName, relativeCatalogPath);
            if (File.Exists(candidatePath)) {
                return candidatePath;
            }

            currentDirectory = currentDirectory.Parent;
        }

        return Path.Combine(AppContext.BaseDirectory, relativeCatalogPath);
    }
}
