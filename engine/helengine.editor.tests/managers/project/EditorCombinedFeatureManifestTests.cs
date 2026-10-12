using System.Text.Json;

namespace helengine.editor.tests.managers.project;

/// <summary>Verifies that the final feature producer receives the union of all converted project requirements through the official CLI.</summary>
public sealed class EditorCombinedFeatureManifestTests {
    /// <summary>Fixed visible fixture directory reserved for combined manifest regression tests.</summary>
    const string FixtureRoot = "C:/dev/helworks/builds/helengine-ps1/menu-20261007/scene-modules/feature-manifest-tests/fixtures";

    /// <summary>Requires menu features to survive later disabled gameplay decisions, independent of report merge order.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CombinedProducerReceivesUnionAndExactSelectedToolArguments(bool reverseOrder) {
        string root = Path.Combine(FixtureRoot, reverseOrder ? "reverse-order" : "official-order");
        string combinedRoot = Path.Combine(root, "generated core");
        string menuRoot = Path.Combine(root, "DemoDisc.Runtime");
        string gameplayRoot = Path.Combine(root, "Zombislayer.Runtime");
        WriteReport(combinedRoot, "helengine.core", false, false);
        WriteReport(menuRoot, "DemoDisc.Runtime", true, false);
        WriteReport(gameplayRoot, "Zombislayer.Runtime", false, true);
        string menuBefore = File.ReadAllText(Path.Combine(menuRoot, "cpp-conversion-report.json"));
        string gameplayBefore = File.ReadAllText(Path.Combine(gameplayRoot, "cpp-conversion-report.json"));
        EditorGeneratedCoreRegenerationService.MergeGeneratedConversionReport(reverseOrder ? gameplayRoot : menuRoot, combinedRoot);
        EditorGeneratedCoreRegenerationService.MergeGeneratedConversionReport(reverseOrder ? menuRoot : gameplayRoot, combinedRoot);

        FeatureManifestRecordingRunner runner = new();
        EditorPlatformCodeCookService service = new(root, runner);
        string toolPath = Path.Combine(root, "selected tool", "codegen.dll");
        string reportPath = Path.Combine(combinedRoot, "cpp-conversion-report.json");
        string reportBefore = File.ReadAllText(reportPath);
        service.WriteCombinedFeatureManifest(toolPath, combinedRoot);

        Assert.Equal(1, runner.InvocationCount);
        Assert.Equal(toolPath, runner.ToolPath);
        Assert.Equal(Path.GetFullPath(root), runner.WorkingDirectory);
        Assert.Equal(new[] {
            "--write-feature-manifest", "--report", reportPath,
            "--feature-catalog", EditorGeneratedCoreRegenerationService.ResolveHelengineFeatureCatalogPath(),
            "--output", combinedRoot
        }, runner.Arguments);
        Assert.True(File.Exists(runner.Arguments[4]));
        Assert.Equal(reportBefore, runner.ReportContents);
        Assert.Equal(reportBefore, File.ReadAllText(reportPath));
        Assert.Equal(menuBefore, File.ReadAllText(Path.Combine(menuRoot, "cpp-conversion-report.json")));
        Assert.Equal(gameplayBefore, File.ReadAllText(Path.Combine(gameplayRoot, "cpp-conversion-report.json")));
        using JsonDocument document = JsonDocument.Parse(runner.ReportContents);
        Assert.Equal("helengine.core", document.RootElement.GetProperty("assemblyName").GetString());
        JsonElement features = document.RootElement.GetProperty("buildFeatures");
        foreach (string feature in new[] { "render2d", "text2d", "sprites", "physics3d.diagnostics" }) {
            JsonElement decision = Assert.Single(features.GetProperty("decisions").EnumerateArray(), entry => entry.GetProperty("feature").GetString() == feature);
            Assert.True(decision.GetProperty("enabled").GetBoolean());
            Assert.Equal("AutoDetected", decision.GetProperty("origin").GetString());
        }
        JsonElement unused = Assert.Single(features.GetProperty("decisions").EnumerateArray(), entry => entry.GetProperty("feature").GetString() == "managed_metadata_only");
        Assert.False(unused.GetProperty("enabled").GetBoolean());
        Assert.Contains(features.GetProperty("detectedRoots").EnumerateArray(), entry => entry.GetProperty("rootId").GetString() == "DemoDisc.Runtime.MenuComponent");
        Assert.Contains(features.GetProperty("detectedRoots").EnumerateArray(), entry => entry.GetProperty("rootId").GetString() == "Zombislayer.Runtime.PhysicsComponent");
    }

    /// <summary>Requires a failed official producer to fail composition instead of retaining a silently stale native manifest.</summary>
    [Fact]
    public void CombinedProducerPropagatesConverterFailure() {
        string root = Path.Combine(FixtureRoot, "producer-failure");
        WriteReport(root, "helengine.core", true, false);
        InvalidOperationException failure = new("Official feature producer rejected the report.");
        FeatureManifestRecordingRunner runner = new() { Failure = failure };
        EditorPlatformCodeCookService service = new(root, runner);

        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => service.WriteCombinedFeatureManifest("selected-codegen.dll", root)));
        Assert.Equal(1, runner.InvocationCount);
    }

    /// <summary>Writes a converter-shaped report with independent menu and physics requirements and one disabled feature.</summary>
    static void WriteReport(string root, string assemblyName, bool menuEnabled, bool physicsEnabled) {
        Directory.CreateDirectory(root);
        object[] decisions = new[] { "render2d", "text2d", "sprites", "physics3d.diagnostics", "managed_metadata_only" }.Select(feature => (object)new {
            feature,
            enabled = feature == "physics3d.diagnostics" ? physicsEnabled : feature != "managed_metadata_only" && menuEnabled,
            origin = (feature == "physics3d.diagnostics" ? physicsEnabled : feature != "managed_metadata_only" && menuEnabled) ? "AutoDetected" : "NotIncluded"
        }).ToArray();
        List<object> roots = [];
        if (menuEnabled) {
            roots.Add(new { feature = "render2d", rootId = "DemoDisc.Runtime.MenuComponent", sourceKind = "TypeReference" });
        }
        if (physicsEnabled) {
            roots.Add(new { feature = "physics3d.diagnostics", rootId = "Zombislayer.Runtime.PhysicsComponent", sourceKind = "TypeReference" });
        }
        File.WriteAllText(Path.Combine(root, "cpp-conversion-report.json"), JsonSerializer.Serialize(new {
            assemblyName,
            buildFeatures = new { decisions, detectedRoots = roots, conflicts = Array.Empty<object>() }
        }));
    }
}
