namespace helengine.editor;

/// <summary>
/// Coordinates typed dependency discovery and deterministic cook execution for one operation.
/// </summary>
public sealed class EditorAssetCookGraphPipeline {
    readonly EditorAssetDependencyDiscoveryService DependencyDiscoveryService;
    readonly EditorAssetCookGraphExecutor GraphExecutor;

    /// <summary>
    /// Initializes a graph pipeline from the operation's discovery and execution policies.
    /// </summary>
    /// <param name="dependencyDiscoveryService">Typed dependency discovery service.</param>
    /// <param name="graphExecutor">Deterministic graph executor.</param>
    public EditorAssetCookGraphPipeline(
        EditorAssetDependencyDiscoveryService dependencyDiscoveryService,
        EditorAssetCookGraphExecutor graphExecutor) {
        DependencyDiscoveryService = dependencyDiscoveryService ?? throw new ArgumentNullException(nameof(dependencyDiscoveryService));
        GraphExecutor = graphExecutor ?? throw new ArgumentNullException(nameof(graphExecutor));
    }

    /// <summary>
    /// Discovers all reachable nodes and executes them in dependency order.
    /// </summary>
    /// <param name="request">Canonical cook request.</param>
    /// <param name="rootNodes">Typed root nodes in authored build order.</param>
    /// <param name="cancellationToken">Operation cancellation token.</param>
    /// <returns>Published artifacts in deterministic cook-key order.</returns>
    public IReadOnlyList<CookedAssetArtifact> Execute(
        EditorAssetCookGraphRequest request,
        IEnumerable<EditorAssetCookNode> rootNodes,
        CancellationToken cancellationToken) {
        if (request == null) {
            throw new ArgumentNullException(nameof(request));
        }
        if (rootNodes == null) {
            throw new ArgumentNullException(nameof(rootNodes));
        }

        EditorAssetCookGraph graph = DependencyDiscoveryService.Discover(rootNodes);
        return GraphExecutor.Execute(
            graph,
            request.EngineVersion,
            request.PlatformId,
            request.ProfileId,
            cancellationToken);
    }
}
