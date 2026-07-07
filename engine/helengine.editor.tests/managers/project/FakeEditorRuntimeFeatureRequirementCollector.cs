using helengine.editor;
using helengine.baseplatform.Manifest;

namespace helengine.editor.tests.managers.project;

/// <summary>
/// Supplies deterministic runtime feature requirements for editor manifest aggregation tests.
/// </summary>
internal sealed class FakeEditorRuntimeFeatureRequirementCollector : IEditorRuntimeFeatureRequirementCollector {
    /// <summary>
    /// Initializes one fake collector with the supplied requirement records.
    /// </summary>
    /// <param name="requiredFeatures">The ordered requirement records this fake collector should emit.</param>
    public FakeEditorRuntimeFeatureRequirementCollector(params PlatformBuildRequiredRuntimeFeature[] requiredFeatures) {
        RequiredFeatures = requiredFeatures ?? throw new ArgumentNullException(nameof(requiredFeatures));
    }

    /// <summary>
    /// Gets the ordered requirement records this fake collector emits.
    /// </summary>
    PlatformBuildRequiredRuntimeFeature[] RequiredFeatures { get; }

    /// <summary>
    /// Returns the configured requirement records without inspecting the supplied build manifest.
    /// </summary>
    /// <param name="buildManifest">Build manifest being analyzed.</param>
    /// <returns>The configured ordered requirement records.</returns>
    public PlatformBuildRequiredRuntimeFeature[] Collect(PlatformBuildManifest buildManifest) {
        if (buildManifest == null) {
            throw new ArgumentNullException(nameof(buildManifest));
        }

        return [.. RequiredFeatures];
    }
}
