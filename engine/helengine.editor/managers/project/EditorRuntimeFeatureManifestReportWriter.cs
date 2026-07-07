using helengine.baseplatform.Manifest;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace helengine.editor;

/// <summary>
/// Writes one durable JSON report describing the resolved runtime feature manifest and disabled feature selections.
/// </summary>
public sealed class EditorRuntimeFeatureManifestReportWriter {
    /// <summary>
    /// JSON serializer options used for runtime feature manifest reports.
    /// </summary>
    static JsonSerializerOptions JsonSerializerOptions { get; } = new() {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    /// <summary>
    /// Initializes serializer behavior for runtime feature manifest reports.
    /// </summary>
    static EditorRuntimeFeatureManifestReportWriter() {
        JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    }

    /// <summary>
    /// Writes one runtime feature manifest report into the supplied logs root.
    /// </summary>
    /// <param name="logsRootPath">Build logs root that should receive the report.</param>
    /// <param name="manifest">Resolved runtime feature manifest for the current build.</param>
    /// <param name="disabledFeatureIds">Normalized disabled runtime feature identifiers requested by the user.</param>
    /// <exception cref="ArgumentException">Thrown when the logs root path is missing.</exception>
    /// <exception cref="ArgumentNullException">Thrown when the manifest or disabled-feature collection is missing.</exception>
    public void Write(
        string logsRootPath,
        PlatformBuildRuntimeFeatureManifest manifest,
        IReadOnlyList<string> disabledFeatureIds) {
        if (string.IsNullOrWhiteSpace(logsRootPath)) {
            throw new ArgumentException("Logs root path is required.", nameof(logsRootPath));
        } else if (manifest == null) {
            throw new ArgumentNullException(nameof(manifest));
        } else if (disabledFeatureIds == null) {
            throw new ArgumentNullException(nameof(disabledFeatureIds));
        }

        Directory.CreateDirectory(logsRootPath);
        string reportPath = Path.Combine(logsRootPath, "runtime-feature-manifest.json");
        string json = JsonSerializer.Serialize(new {
            DisabledFeatureIds = disabledFeatureIds.ToArray(),
            RequiredFeatures = manifest.RequiredFeatures.ToArray()
        }, JsonSerializerOptions);
        File.WriteAllText(reportPath, json);
    }
}
