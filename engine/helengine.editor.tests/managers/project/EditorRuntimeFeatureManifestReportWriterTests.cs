using helengine.baseplatform.Manifest;

namespace helengine.editor.tests.managers.project;

/// <summary>
/// Verifies runtime feature manifest reports are written to the build logs root.
/// </summary>
public sealed class EditorRuntimeFeatureManifestReportWriterTests : IDisposable {
    /// <summary>
    /// Temporary logs root used by the report-writer tests.
    /// </summary>
    readonly string LogsRootPath;

    /// <summary>
    /// Initializes the temporary logs root.
    /// </summary>
    public EditorRuntimeFeatureManifestReportWriterTests() {
        LogsRootPath = Path.Combine(Path.GetTempPath(), "helengine-runtime-feature-report-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(LogsRootPath);
    }

    /// <summary>
    /// Removes the temporary logs root after each test.
    /// </summary>
    public void Dispose() {
        if (Directory.Exists(LogsRootPath)) {
            Directory.Delete(LogsRootPath, true);
        }
    }

    /// <summary>
    /// Verifies one JSON report is written with required features and disabled feature ids.
    /// </summary>
    [Fact]
    public void Write_writes_json_report() {
        EditorRuntimeFeatureManifestReportWriter writer = new();
        PlatformBuildRuntimeFeatureManifest manifest = new(
            [
                new PlatformBuildRequiredRuntimeFeature(
                    "runtime_json",
                    RuntimeFeatureRequirementSourceKind.RuntimeType,
                    "helengine.editor.tests.GeneratedRuntimeModuleRegistrationTestComponent",
                    "Component requires runtime JSON.")
            ]);

        writer.Write(LogsRootPath, manifest, ["debug_overlay"]);

        string reportPath = Path.Combine(LogsRootPath, "runtime-feature-manifest.json");
        string reportJson = File.ReadAllText(reportPath);

        Assert.Contains("\"runtime_json\"", reportJson, StringComparison.Ordinal);
        Assert.Contains("\"debug_overlay\"", reportJson, StringComparison.Ordinal);
        Assert.Contains("\"RuntimeType\"", reportJson, StringComparison.Ordinal);
    }
}
