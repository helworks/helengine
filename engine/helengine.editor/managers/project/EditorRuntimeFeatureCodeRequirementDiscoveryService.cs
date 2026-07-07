using helengine.baseplatform.Manifest;
using System.Reflection;

namespace helengine.editor;

/// <summary>
/// Discovers generic runtime feature requirements declared by used runtime types and active generated runtime modules.
/// </summary>
public sealed class EditorRuntimeFeatureCodeRequirementDiscoveryService : IEditorRuntimeFeatureRequirementCollector {
    /// <summary>
    /// Optional shared script type resolver used to map cooked component type ids back to runtime types.
    /// </summary>
    readonly IScriptTypeResolver ScriptTypeResolver;

    /// <summary>
    /// Initializes one code requirement discovery service.
    /// </summary>
    /// <param name="scriptTypeResolver">Optional shared script type resolver used to map cooked component type ids back to runtime types.</param>
    public EditorRuntimeFeatureCodeRequirementDiscoveryService(IScriptTypeResolver scriptTypeResolver) {
        ScriptTypeResolver = scriptTypeResolver;
    }

    /// <summary>
    /// Collects generic runtime feature requirements declared by used runtime types and active generated runtime module registrations.
    /// </summary>
    /// <param name="buildManifest">Build manifest currently being prepared for packaging.</param>
    /// <returns>Ordered generic runtime feature requirements declared by code and runtime module registration types.</returns>
    public PlatformBuildRequiredRuntimeFeature[] Collect(PlatformBuildManifest buildManifest) {
        if (buildManifest == null) {
            throw new ArgumentNullException(nameof(buildManifest));
        }

        IReadOnlyList<Type> usedRuntimeTypes = ResolveUsedRuntimeTypes(buildManifest);
        List<PlatformBuildRequiredRuntimeFeature> requiredFeatures = [];
        for (int index = 0; index < usedRuntimeTypes.Count; index++) {
            Type runtimeType = usedRuntimeTypes[index];
            AppendTypeRequirements(requiredFeatures, runtimeType, RuntimeFeatureRequirementSourceKind.RuntimeType, runtimeType.FullName ?? runtimeType.Name);
        }

        IReadOnlyList<GeneratedRuntimeModuleManifestAttribute> activeRuntimeModules = ResolveActiveRuntimeModules(usedRuntimeTypes);
        for (int index = 0; index < activeRuntimeModules.Count; index++) {
            GeneratedRuntimeModuleManifestAttribute manifest = activeRuntimeModules[index];
            AppendTypeRequirements(requiredFeatures, manifest.RegistrationType, RuntimeFeatureRequirementSourceKind.Plugin, manifest.ModuleId);
        }

        return [.. requiredFeatures];
    }

    /// <summary>
    /// Resolves the used runtime types referenced by the cooked build scenes.
    /// </summary>
    /// <param name="buildManifest">Build manifest whose scene metadata should be inspected.</param>
    /// <returns>Deterministically ordered used runtime types referenced by the cooked scenes.</returns>
    IReadOnlyList<Type> ResolveUsedRuntimeTypes(PlatformBuildManifest buildManifest) {
        HashSet<Type> usedRuntimeTypes = [];
        PlatformBuildScene[] scenes = buildManifest.Scenes ?? [];
        for (int sceneIndex = 0; sceneIndex < scenes.Length; sceneIndex++) {
            PlatformBuildScene scene = scenes[sceneIndex];
            string componentTypeIds = ResolveSceneMetadataValue(scene, PlatformBuildSceneMetadataKeys.AutomaticRuntimeComponentTypeIds);
            if (string.IsNullOrWhiteSpace(componentTypeIds)) {
                continue;
            }

            string[] splitIds = componentTypeIds.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            for (int typeIndex = 0; typeIndex < splitIds.Length; typeIndex++) {
                usedRuntimeTypes.Add(ResolveRuntimeType(splitIds[typeIndex]));
            }
        }

        return usedRuntimeTypes
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>
    /// Resolves the active generated runtime module manifests contributed by the used runtime types.
    /// </summary>
    /// <param name="usedRuntimeTypes">Runtime types referenced by the cooked build.</param>
    /// <returns>Deterministically ordered active generated runtime module manifests.</returns>
    static IReadOnlyList<GeneratedRuntimeModuleManifestAttribute> ResolveActiveRuntimeModules(IReadOnlyList<Type> usedRuntimeTypes) {
        if (usedRuntimeTypes == null) {
            throw new ArgumentNullException(nameof(usedRuntimeTypes));
        }

        IReadOnlyList<Assembly> assemblies = usedRuntimeTypes
            .Select(type => type.Assembly)
            .Distinct()
            .ToArray();
        IReadOnlyList<GeneratedRuntimeModuleManifestAttribute> manifests =
            EditorGeneratedCoreRegenerationService.DiscoverGeneratedRuntimeModuleManifests(assemblies);
        return EditorGeneratedCoreRegenerationService.ResolveActiveGeneratedRuntimeModuleManifests(manifests, usedRuntimeTypes);
    }

    /// <summary>
    /// Appends all runtime feature requirements declared by the supplied type.
    /// </summary>
    /// <param name="requiredFeatures">Mutable output requirement list.</param>
    /// <param name="declaringType">Runtime type whose feature declarations should be appended.</param>
    /// <param name="sourceKind">Source kind that should be recorded for the emitted requirements.</param>
    /// <param name="sourceId">Stable source id that should be recorded for the emitted requirements.</param>
    static void AppendTypeRequirements(
        List<PlatformBuildRequiredRuntimeFeature> requiredFeatures,
        Type declaringType,
        RuntimeFeatureRequirementSourceKind sourceKind,
        string sourceId) {
        if (requiredFeatures == null) {
            throw new ArgumentNullException(nameof(requiredFeatures));
        }
        if (declaringType == null) {
            throw new ArgumentNullException(nameof(declaringType));
        }
        if (string.IsNullOrWhiteSpace(sourceId)) {
            throw new ArgumentException("Source id must be provided.", nameof(sourceId));
        }

        RuntimeFeatureRequirementAttribute[] attributes = declaringType
            .GetCustomAttributes<RuntimeFeatureRequirementAttribute>()
            .OrderBy(attribute => attribute.FeatureId, StringComparer.Ordinal)
            .ToArray();
        for (int index = 0; index < attributes.Length; index++) {
            RuntimeFeatureRequirementAttribute attribute = attributes[index];
            requiredFeatures.Add(new PlatformBuildRequiredRuntimeFeature(attribute.FeatureId, sourceKind, sourceId, attribute.Reason));
        }
    }

    /// <summary>
    /// Resolves one runtime type from its stable automatic component type id.
    /// </summary>
    /// <param name="componentTypeId">Stable automatic component type id read from cooked scene metadata.</param>
    /// <returns>Resolved runtime type for the component id.</returns>
    Type ResolveRuntimeType(string componentTypeId) {
        if (string.IsNullOrWhiteSpace(componentTypeId)) {
            throw new ArgumentException("Component type id must be provided.", nameof(componentTypeId));
        }

        Type runtimeType = PersistedComponentTypeResolver.TryResolve(componentTypeId);
        if (runtimeType != null) {
            return runtimeType;
        }
        if (ScriptTypeResolver == null) {
            throw new InvalidOperationException($"Runtime feature code discovery requires a script type resolver to resolve component type id '{componentTypeId}'.");
        }

        runtimeType = ScriptTypeResolver.Resolve(componentTypeId);
        if (runtimeType == null) {
            throw new InvalidOperationException($"Runtime feature code discovery could not resolve component type id '{componentTypeId}'.");
        }

        return runtimeType;
    }

    /// <summary>
    /// Resolves one metadata value from the supplied build scene.
    /// </summary>
    /// <param name="scene">Build scene whose metadata should be searched.</param>
    /// <param name="metadataKey">Stable metadata key to resolve.</param>
    /// <returns>Resolved metadata value or an empty string when the key is absent.</returns>
    static string ResolveSceneMetadataValue(PlatformBuildScene scene, string metadataKey) {
        if (scene?.ResolvedMetadata == null || string.IsNullOrWhiteSpace(metadataKey)) {
            return string.Empty;
        }

        for (int index = 0; index < scene.ResolvedMetadata.Length; index++) {
            KeyValuePair<string, string> metadata = scene.ResolvedMetadata[index];
            if (string.Equals(metadata.Key, metadataKey, StringComparison.OrdinalIgnoreCase)) {
                return metadata.Value ?? string.Empty;
            }
        }

        return string.Empty;
    }
}
