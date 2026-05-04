namespace helengine.baseplatform.Manifest;

/// <summary>
/// Describes one authored runtime code module packaged by the build graph.
/// </summary>
public sealed class PlatformBuildCodeModule {
    /// <summary>
    /// Initializes one code-module entry.
    /// </summary>
    public PlatformBuildCodeModule(
        string moduleId,
        string artifactId,
        string runtimeSpecializationId,
        string[] loadScopes,
        string[] dependencyModuleIds) {
        if (string.IsNullOrWhiteSpace(moduleId)) {
            throw new ArgumentException("Code module id is required.", nameof(moduleId));
        } else if (string.IsNullOrWhiteSpace(artifactId)) {
            throw new ArgumentException("Code module artifact id is required.", nameof(artifactId));
        } else if (string.IsNullOrWhiteSpace(runtimeSpecializationId)) {
            throw new ArgumentException("Code module runtime specialization id is required.", nameof(runtimeSpecializationId));
        } else if (loadScopes == null) {
            throw new ArgumentNullException(nameof(loadScopes), "Code module load scopes are required.");
        } else if (Array.Exists(loadScopes, loadScope => string.IsNullOrWhiteSpace(loadScope))) {
            throw new ArgumentException("Code module load scopes cannot contain blank entries.", nameof(loadScopes));
        } else if (dependencyModuleIds == null) {
            throw new ArgumentNullException(nameof(dependencyModuleIds), "Code module dependencies are required.");
        } else if (Array.Exists(dependencyModuleIds, dependencyModuleId => string.IsNullOrWhiteSpace(dependencyModuleId))) {
            throw new ArgumentException("Code module dependencies cannot contain blank entries.", nameof(dependencyModuleIds));
        }

        ModuleId = moduleId;
        ArtifactId = artifactId;
        RuntimeSpecializationId = runtimeSpecializationId;
        LoadScopes = [.. loadScopes];
        DependencyModuleIds = [.. dependencyModuleIds];
    }

    /// <summary>
    /// Gets the stable module identifier.
    /// </summary>
    public string ModuleId { get; }

    /// <summary>
    /// Gets the stable artifact identity that carries the module payload.
    /// </summary>
    public string ArtifactId { get; }

    /// <summary>
    /// Gets the runtime specialization id emitted for the module payload.
    /// </summary>
    public string RuntimeSpecializationId { get; }

    /// <summary>
    /// Gets the declared runtime load scopes for the module.
    /// </summary>
    public string[] LoadScopes { get; }

    /// <summary>
    /// Gets the referenced module dependencies.
    /// </summary>
    public string[] DependencyModuleIds { get; }
}
