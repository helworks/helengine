namespace helengine.baseplatform.Requests;

using helengine.baseplatform.Results;

/// <summary>
/// Supplies one platform shader artifact builder with the dependencies and output context required to stage independently compiled shader files.
/// </summary>
public sealed class PlatformShaderArtifactCookRequest {
    /// <summary>
    /// Stores material-selected shader dependencies including optional program-pair lookup keys.
    /// </summary>
    readonly PlatformShaderDependency[] ShaderDependenciesValue;
    /// <summary>
    /// Stores resolved authored sources keyed by shader asset identifier for shader-capable platform builders.
    /// </summary>
    readonly PlatformShaderArtifactCookSource[] ShaderSourcesValue;

    /// <summary>
    /// Initializes one shader artifact staging request with typed material shader dependencies.
    /// </summary>
    /// <param name="cookRootPath">Absolute cooked-content root that receives staged shader files.</param>
    /// <param name="platformId">Stable selected platform identifier.</param>
    /// <param name="buildProfileId">Selected build profile identifier.</param>
    /// <param name="graphicsProfileId">Selected graphics profile identifier.</param>
    /// <param name="shaderDependencies">Shader dependencies including material-selected program-pair keys.</param>
    public PlatformShaderArtifactCookRequest(
        string cookRootPath,
        string platformId,
        string buildProfileId,
        string graphicsProfileId,
        IReadOnlyList<PlatformShaderDependency> shaderDependencies)
        : this(cookRootPath, platformId, buildProfileId, graphicsProfileId, shaderDependencies, Array.Empty<PlatformShaderArtifactCookSource>()) {
    }

    /// <summary>
    /// Initializes one shader artifact staging request with complete material-reported dependencies.
    /// </summary>
    /// <param name="cookRootPath">Absolute cooked-content root that receives staged shader files.</param>
    /// <param name="platformId">Stable selected platform identifier.</param>
    /// <param name="buildProfileId">Selected build profile identifier.</param>
    /// <param name="graphicsProfileId">Selected graphics profile identifier.</param>
    /// <param name="shaderDependencies">Shader dependencies including material-selected program-pair keys.</param>
    public PlatformShaderArtifactCookRequest(
        string cookRootPath,
        string platformId,
        string buildProfileId,
        string graphicsProfileId,
        IReadOnlyList<PlatformShaderDependency> shaderDependencies,
        IReadOnlyList<PlatformShaderArtifactCookSource> shaderSources) {
        if (string.IsNullOrWhiteSpace(cookRootPath)) {
            throw new ArgumentException("Cook root path is required.", nameof(cookRootPath));
        } else if (string.IsNullOrWhiteSpace(platformId)) {
            throw new ArgumentException("Platform id is required.", nameof(platformId));
        } else if (shaderDependencies == null) {
            throw new ArgumentNullException(nameof(shaderDependencies));
        } else if (shaderSources == null) {
            throw new ArgumentNullException(nameof(shaderSources));
        }

        PlatformShaderDependency[] copiedDependencies = new PlatformShaderDependency[shaderDependencies.Count];
        for (int index = 0; index < copiedDependencies.Length; index++) {
            PlatformShaderDependency dependency = shaderDependencies[index] ?? throw new ArgumentException("Shader dependencies cannot contain null entries.", nameof(shaderDependencies));
            copiedDependencies[index] = dependency;
        }

        PlatformShaderArtifactCookSource[] copiedSources = new PlatformShaderArtifactCookSource[shaderSources.Count];
        HashSet<string> dependencyAssetIds = new(copiedDependencies.Select(dependency => dependency.ShaderAssetId), StringComparer.Ordinal);
        HashSet<string> sourceAssetIds = new(StringComparer.Ordinal);
        for (int index = 0; index < copiedSources.Length; index++) {
            PlatformShaderArtifactCookSource source = shaderSources[index] ?? throw new ArgumentException("Shader sources cannot contain null entries.", nameof(shaderSources));
            if (!dependencyAssetIds.Contains(source.ShaderAssetId) || !sourceAssetIds.Add(source.ShaderAssetId)) {
                throw new ArgumentException("Shader sources must contain at most one source for each requested shader asset id.", nameof(shaderSources));
            }

            copiedSources[index] = source;
        }
        if (copiedSources.Length != 0 && sourceAssetIds.Count != dependencyAssetIds.Count) {
            throw new ArgumentException("Resolved shader sources must cover every requested shader asset id.", nameof(shaderSources));
        }

        CookRootPath = Path.GetFullPath(cookRootPath);
        PlatformId = platformId;
        BuildProfileId = buildProfileId ?? string.Empty;
        GraphicsProfileId = graphicsProfileId ?? string.Empty;
        ShaderDependenciesValue = copiedDependencies;
        ShaderSourcesValue = copiedSources;
    }

    /// <summary>
    /// Gets the absolute cooked-content root that receives shader files.
    /// </summary>
    public string CookRootPath { get; }

    /// <summary>
    /// Gets the stable selected platform identifier.
    /// </summary>
    public string PlatformId { get; }

    /// <summary>
    /// Gets the selected build profile identifier.
    /// </summary>
    public string BuildProfileId { get; }

    /// <summary>
    /// Gets the selected graphics profile identifier.
    /// </summary>
    public string GraphicsProfileId { get; }

    /// <summary>
    /// Gets copied material-selected shader dependencies including program-pair lookup keys when available.
    /// </summary>
    public IReadOnlyList<PlatformShaderDependency> ShaderDependencies {
        get {
            return ShaderDependenciesValue;
        }
    }

    /// <summary>
    /// Gets resolved authored sources keyed by shader asset identifier for shader-capable platform builders.
    /// </summary>
    public IReadOnlyList<PlatformShaderArtifactCookSource> ShaderSources {
        get {
            return ShaderSourcesValue;
        }
    }

}
