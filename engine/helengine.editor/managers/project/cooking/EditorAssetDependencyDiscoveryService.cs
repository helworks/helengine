namespace helengine.editor;

/// <summary>
/// Builds a typed cook graph and reports complete dependency cycles.
/// </summary>
public sealed class EditorAssetDependencyDiscoveryService {
    readonly IReadOnlyDictionary<AssetEntryKind, IEditorAssetDependencyDiscoverer> DiscoverersByKind;

    /// <summary>
    /// Initializes dependency discovery with one discoverer per asset kind.
    /// </summary>
    /// <param name="discoverers">Registered discoverers.</param>
    public EditorAssetDependencyDiscoveryService(IEnumerable<IEditorAssetDependencyDiscoverer> discoverers) {
        if (discoverers == null) {
            throw new ArgumentNullException(nameof(discoverers));
        }

        DiscoverersByKind = discoverers.ToDictionary(discoverer => discoverer?.Kind ?? throw new ArgumentException("Discoverers cannot contain null values.", nameof(discoverers)));
    }

    /// <summary>
    /// Discovers and validates a deterministic graph from the supplied roots.
    /// </summary>
    /// <param name="roots">Root nodes in caller-provided order.</param>
    /// <returns>Graph containing all reachable nodes.</returns>
    public EditorAssetCookGraph Discover(IEnumerable<EditorAssetCookNode> roots) {
        if (roots == null) {
            throw new ArgumentNullException(nameof(roots));
        }

        Dictionary<string, EditorAssetCookNode> nodesByKey = new(StringComparer.Ordinal);
        HashSet<string> visiting = new(StringComparer.Ordinal);
        HashSet<string> visited = new(StringComparer.Ordinal);
        foreach (EditorAssetCookNode root in roots) {
            DiscoverNode(root, nodesByKey, visiting, visited);
        }

        return new EditorAssetCookGraph(nodesByKey.Values);
    }

    void DiscoverNode(
        EditorAssetCookNode node,
        IDictionary<string, EditorAssetCookNode> nodesByKey,
        ISet<string> visiting,
        ISet<string> visited) {
        if (node == null) {
            throw new ArgumentException("Dependency roots cannot contain null values.", nameof(node));
        }

        if (visited.Contains(node.Key.Value)) {
            return;
        }
        if (!visiting.Add(node.Key.Value)) {
            string cycle = string.Join(" -> ", visiting.Append(node.Key.Value));
            throw new InvalidOperationException("Cook graph dependency cycle: " + cycle);
        }

        if (nodesByKey.TryGetValue(node.Key.Value, out EditorAssetCookNode existing) && !ReferenceEquals(existing, node)) {
            throw new InvalidOperationException("Multiple nodes declare cook source key '" + node.Key.Value + "'.");
        }
        nodesByKey[node.Key.Value] = node;

        IReadOnlyList<EditorAssetCookNode> discovered = Array.Empty<EditorAssetCookNode>();
        if (DiscoverersByKind.TryGetValue(node.SourceKind, out IEditorAssetDependencyDiscoverer discoverer)) {
            discovered = discoverer.Discover(node) ?? throw new InvalidOperationException("Dependency discoverer returned no collection.");
        }
        foreach (EditorAssetCookNode dependency in discovered) {
            DiscoverNode(dependency, nodesByKey, visiting, visited);
        }
        foreach (EditorAssetCookDependency dependency in node.Dependencies) {
            if (!nodesByKey.ContainsKey(dependency.Key.Value)) {
                throw new InvalidOperationException("Cook graph dependency '" + dependency.Key.Value + "' was not supplied by a discoverer.");
            }
        }

        visiting.Remove(node.Key.Value);
        visited.Add(node.Key.Value);
    }
}
