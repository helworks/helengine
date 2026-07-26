namespace helengine.baseplatform.Requests;

using helengine.baseplatform.Results;

/// <summary>
/// Supplies one platform shader artifact builder with the dependencies and output context required to stage independently compiled shader files.
/// </summary>
public sealed class PlatformShaderArtifactCookRequest {
    /// <summary>
    /// Stores the shader asset identifiers requested by cooked material outputs.
    /// </summary>
    readonly string[] ShaderAssetIdsValue;
    /// <summary>
    /// Stores material-selected shader dependencies including optional program-pair lookup keys.
    /// </summary>
    readonly PlatformShaderDependency[] ShaderDependenciesValue;
    /// <summary>
    /// Stores resolved authored sources keyed by shader asset identifier for shader-capable platform builders.
    /// </summary>
    readonly PlatformShaderArtifactCookSource[] ShaderSourcesValue;

    /// <summary>
    /// Initializes one shader artifact staging request.
    /// </summary>
    /// <param name="cookRootPath">Absolute cooked-content root that receives staged shader files.</param>
    /// <param name="platformId">Stable selected platform identifier.</param>
    /// <param name="buildProfileId">Selected build profile identifier.</param>
    /// <param name="graphicsProfileId">Selected graphics profile identifier.</param>
    /// <param name="shaderAssetIds">Shader asset identifiers required by cooked materials.</param>
    public PlatformShaderArtifactCookRequest(
        string cookRootPath,
        string platformId,
        string buildProfileId,
        string graphicsProfileId,
        IReadOnlyList<string> shaderAssetIds)
        : this(cookRootPath, platformId, buildProfileId, graphicsProfileId, CreateIdOnlyDependencies(shaderAssetIds), Array.Empty<PlatformShaderArtifactCookSource>()) {
    }

    /// <summary>
    /// Initializes one shader artifact staging request with complete material-reported dependencies.
    /// </summary>
    /// <param name="cookRootPath">Absolute cooked-content root that receives staged shader files.</param>
    /// <param name="platformId">Stable selected platform identifier.</param>
    /// <param name="buildProfileId">Selected build profile identifier.</param>
    /// <param name="graphicsProfileId">Selected graphics profile identifier.</param>
    /// <param name="shaderDependencies">Shader dependencies including material-selected program-pair keys.</param>
    PlatformShaderArtifactCookRequest(
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
        string[] shaderAssetIds = new string[shaderDependencies.Count];
        for (int index = 0; index < copiedDependencies.Length; index++) {
            PlatformShaderDependency dependency = shaderDependencies[index] ?? throw new ArgumentException("Shader dependencies cannot contain null entries.", nameof(shaderDependencies));
            copiedDependencies[index] = dependency;
            shaderAssetIds[index] = dependency.ShaderAssetId;
        }

        PlatformShaderArtifactCookSource[] copiedSources = new PlatformShaderArtifactCookSource[shaderSources.Count];
        HashSet<string> dependencyAssetIds = new(shaderAssetIds, StringComparer.Ordinal);
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
        ShaderAssetIdsValue = shaderAssetIds;
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
    /// Gets the copied shader asset identifiers required by cooked material outputs.
    /// </summary>
    public IReadOnlyList<string> ShaderAssetIds {
        get {
            return ShaderAssetIdsValue;
        }
    }

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

    /// <summary>
    /// Creates one staging request with complete material-selected shader dependencies.
    /// </summary>
    /// <param name="cookRootPath">Absolute cooked-content root that receives staged shader files.</param>
    /// <param name="platformId">Stable selected platform identifier.</param>
    /// <param name="buildProfileId">Selected build profile identifier.</param>
    /// <param name="graphicsProfileId">Selected graphics profile identifier.</param>
    /// <param name="shaderDependencies">Shader dependencies including material-selected program-pair keys.</param>
    /// <returns>Validated shader artifact staging request.</returns>
    public static PlatformShaderArtifactCookRequest CreateWithDependencies(
        string cookRootPath,
        string platformId,
        string buildProfileId,
        string graphicsProfileId,
        IReadOnlyList<PlatformShaderDependency> shaderDependencies) {
        return new PlatformShaderArtifactCookRequest(cookRootPath, platformId, buildProfileId, graphicsProfileId, shaderDependencies, Array.Empty<PlatformShaderArtifactCookSource>());
    }

    /// <summary>
    /// Creates one staging request with both material lookup keys and resolved source text.
    /// </summary>
    /// <param name="cookRootPath">Absolute cooked-content root that receives staged shader files.</param>
    /// <param name="platformId">Stable selected platform identifier.</param>
    /// <param name="buildProfileId">Selected build profile identifier.</param>
    /// <param name="graphicsProfileId">Selected graphics profile identifier.</param>
    /// <param name="shaderDependencies">Shader dependencies including material-selected program-pair keys.</param>
    /// <param name="shaderSources">Resolved authored sources keyed by shader asset identifier.</param>
    /// <returns>Validated shader artifact staging request.</returns>
    public static PlatformShaderArtifactCookRequest CreateWithDependenciesAndSources(
        string cookRootPath,
        string platformId,
        string buildProfileId,
        string graphicsProfileId,
        IReadOnlyList<PlatformShaderDependency> shaderDependencies,
        IReadOnlyList<PlatformShaderArtifactCookSource> shaderSources) {
        return new PlatformShaderArtifactCookRequest(cookRootPath, platformId, buildProfileId, graphicsProfileId, shaderDependencies, shaderSources);
    }

    /// <summary>
    /// Converts legacy shader asset identifiers into dependencies without material-selected program-pair keys.
    /// </summary>
    /// <param name="shaderAssetIds">Legacy shader asset identifiers.</param>
    /// <returns>Equivalent id-only dependencies.</returns>
    static PlatformShaderDependency[] CreateIdOnlyDependencies(IReadOnlyList<string> shaderAssetIds) {
        if (shaderAssetIds == null) {
            throw new ArgumentNullException(nameof(shaderAssetIds));
        }

        PlatformShaderDependency[] dependencies = new PlatformShaderDependency[shaderAssetIds.Count];
        for (int index = 0; index < dependencies.Length; index++) {
            dependencies[index] = new PlatformShaderDependency(shaderAssetIds[index], string.Empty, string.Empty, string.Empty);
        }

        return dependencies;
    }
}
