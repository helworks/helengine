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

        using System.Text.Json.JsonDocument document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(normalizedFilePath));
        string[] featureIds = document.RootElement.GetProperty("features").EnumerateArray()
            .Select(feature => feature.GetProperty("id").GetString()).ToArray();
        Assert.Contains("shaders", featureIds);
        Assert.Contains("render2d", featureIds);
        Assert.Contains("managed_metadata_only", featureIds);
        Assert.Contains("host_file_system", featureIds);
        Assert.DoesNotContain("runtime_json", featureIds);
    }

    /// <summary>
    /// Verifies the core content manager remains available without forcing the text-processing feature on every filesystem build.
    /// </summary>
    [Fact]
    public void HelengineFeatureCatalog_keeps_text_processing_owned_by_explicit_text_registration() {
        string normalizedFilePath = ResolveFeatureCatalogPath();

        Assert.True(File.Exists(normalizedFilePath));

        using System.Text.Json.JsonDocument document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(normalizedFilePath));
        Dictionary<string, string[]> rules = document.RootElement.GetProperty("rootRules").EnumerateArray()
            .ToDictionary(rule => rule.GetProperty("typeName").GetString(),
                rule => rule.GetProperty("featureIds").EnumerateArray().Select(id => id.GetString()).ToArray());
        Assert.Equal(new[] { "host_file_system" }, rules["helengine.HostFileSystemContentStreamSource"]);
        Assert.Equal(new[] { "text_processing" }, rules["helengine.TextContentManagerConfiguration"]);
        Assert.Equal(new[] { "managed_metadata_only" }, rules["helengine.GeneratedRuntimeModuleManifestAttribute"]);
        Assert.Equal(new[] { "managed_metadata_only" }, rules["helengine.RuntimeFeatureRequirementAttribute"]);
        Assert.DoesNotContain("helengine.core.content.RuntimeManifestJsonReader", rules.Keys);
        Assert.DoesNotContain("helengine.core.content.RuntimeStartupManifest", rules.Keys);
        Assert.DoesNotContain("helengine.core.content.RuntimeCodeModuleManifest", rules.Keys);
    }

    /// <summary>
    /// Resolves the checked-in feature catalog from the source root embedded when the test project was built.
    /// </summary>
    /// <returns>Absolute path to the checked-in helengine feature catalog.</returns>
    static string ResolveFeatureCatalogPath() {
        string relativeCatalogPath = Path.Combine(
            "engine",
            "helengine.editor",
            "codegen",
            "features",
            "helengine-feature-catalog.json");
        return Path.Combine(TestSourceRepositoryLocator.ResolveHelEngineRootPath(), relativeCatalogPath);
    }
}
