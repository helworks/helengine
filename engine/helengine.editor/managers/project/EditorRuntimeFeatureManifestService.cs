using helengine.baseplatform.Manifest;

namespace helengine.editor;

/// <summary>
/// Aggregates runtime feature requirements from all registered editor collectors into one build manifest payload.
/// </summary>
public sealed class EditorRuntimeFeatureManifestService {
    /// <summary>
    /// Initializes one runtime feature manifest service.
    /// </summary>
    /// <param name="collectors">Ordered collectors that contribute runtime feature requirements.</param>
    public EditorRuntimeFeatureManifestService(IReadOnlyList<IEditorRuntimeFeatureRequirementCollector> collectors) {
        Collectors = collectors ?? throw new ArgumentNullException(nameof(collectors));
    }

    /// <summary>
    /// Gets the ordered collectors that contribute runtime feature requirements.
    /// </summary>
    IReadOnlyList<IEditorRuntimeFeatureRequirementCollector> Collectors { get; }

    /// <summary>
    /// Builds one runtime feature manifest for the supplied build manifest.
    /// </summary>
    /// <param name="buildManifest">Build manifest currently being prepared for packaging.</param>
    /// <returns>Aggregated runtime feature manifest for the build.</returns>
    public PlatformBuildRuntimeFeatureManifest Build(PlatformBuildManifest buildManifest) {
        if (buildManifest == null) {
            throw new ArgumentNullException(nameof(buildManifest));
        }

        if (Collectors.Count == 0) {
            return PlatformBuildRuntimeFeatureManifest.Empty;
        }

        List<PlatformBuildRequiredRuntimeFeature> requiredFeatures = [];
        for (int index = 0; index < Collectors.Count; index++) {
            IEditorRuntimeFeatureRequirementCollector collector = Collectors[index]
                ?? throw new InvalidOperationException("Runtime feature requirement collectors cannot contain null entries.");
            PlatformBuildRequiredRuntimeFeature[] collectorFeatures = collector.Collect(buildManifest)
                ?? throw new InvalidOperationException("Runtime feature requirement collectors cannot return null collections.");
            requiredFeatures.AddRange(collectorFeatures);
        }

        if (requiredFeatures.Count == 0) {
            return PlatformBuildRuntimeFeatureManifest.Empty;
        }

        return new PlatformBuildRuntimeFeatureManifest([.. requiredFeatures]);
    }
}
