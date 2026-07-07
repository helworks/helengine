using helengine.baseplatform.Manifest;

namespace helengine.editor;

/// <summary>
/// Converts scene-derived 3D physics feature analysis into generic runtime feature requirements.
/// </summary>
public sealed class EditorPhysics3DRuntimeFeatureRequirementCollector : IEditorRuntimeFeatureRequirementCollector {
    /// <summary>
    /// Shared 3D physics scene analysis service used to inspect authored scenes.
    /// </summary>
    readonly EditorPhysics3DCodegenFeatureSymbolService FeatureSymbolService;

    /// <summary>
    /// Initializes one 3D physics runtime feature collector for the supplied project root.
    /// </summary>
    /// <param name="projectRootPath">Absolute or relative project root path.</param>
    public EditorPhysics3DRuntimeFeatureRequirementCollector(string projectRootPath) {
        if (string.IsNullOrWhiteSpace(projectRootPath)) {
            throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
        }

        FeatureSymbolService = new EditorPhysics3DCodegenFeatureSymbolService(projectRootPath);
    }

    /// <summary>
    /// Collects scene-scoped generic runtime feature requirements for all scenes in the supplied build manifest.
    /// </summary>
    /// <param name="buildManifest">Build manifest currently being prepared for packaging.</param>
    /// <returns>Ordered scene-scoped runtime feature requirements derived from physics analysis.</returns>
    public PlatformBuildRequiredRuntimeFeature[] Collect(PlatformBuildManifest buildManifest) {
        if (buildManifest == null) {
            throw new ArgumentNullException(nameof(buildManifest));
        }

        List<PlatformBuildRequiredRuntimeFeature> requiredFeatures = [];
        PlatformBuildScene[] scenes = buildManifest.Scenes ?? [];
        for (int index = 0; index < scenes.Length; index++) {
            PlatformBuildScene scene = scenes[index];
            PhysicsSceneFeatureFlags3D featureFlags = FeatureSymbolService.ResolveFeatureFlags([scene.SceneId]);
            IReadOnlyList<string> featureIds = PhysicsSceneFeatureSymbolCatalog3D.BuildRuntimeFeatureIds(featureFlags);
            for (int featureIndex = 0; featureIndex < featureIds.Count; featureIndex++) {
                string featureId = featureIds[featureIndex];
                requiredFeatures.Add(
                    new PlatformBuildRequiredRuntimeFeature(
                        featureId,
                        RuntimeFeatureRequirementSourceKind.Scene,
                        scene.SceneId,
                        BuildRequirementReason(scene.SceneId, featureId)));
            }
        }

        return [.. requiredFeatures];
    }

    /// <summary>
    /// Builds one human-readable reason string for a scene-derived 3D physics runtime feature requirement.
    /// </summary>
    /// <param name="sceneId">Stable scene id that required the feature.</param>
    /// <param name="featureId">Stable generic runtime feature id inferred from the scene.</param>
    /// <returns>Human-readable requirement reason.</returns>
    static string BuildRequirementReason(string sceneId, string featureId) {
        if (string.IsNullOrWhiteSpace(sceneId)) {
            throw new ArgumentException("Scene id must be provided.", nameof(sceneId));
        }
        if (string.IsNullOrWhiteSpace(featureId)) {
            throw new ArgumentException("Feature id must be provided.", nameof(featureId));
        }

        return "Scene '" + sceneId + "' requires runtime feature '" + featureId + "' based on serialized 3D physics components.";
    }
}
