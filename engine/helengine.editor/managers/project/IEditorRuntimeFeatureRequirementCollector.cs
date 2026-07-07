using helengine.baseplatform.Manifest;

namespace helengine.editor;

/// <summary>
/// Collects runtime feature requirements from one editor-owned analysis source for the active build manifest.
/// </summary>
public interface IEditorRuntimeFeatureRequirementCollector {
    /// <summary>
    /// Collects the required runtime features discovered from the supplied build manifest.
    /// </summary>
    /// <param name="buildManifest">Build manifest currently being prepared for packaging.</param>
    /// <returns>Ordered requirement records discovered by the collector.</returns>
    PlatformBuildRequiredRuntimeFeature[] Collect(PlatformBuildManifest buildManifest);
}
