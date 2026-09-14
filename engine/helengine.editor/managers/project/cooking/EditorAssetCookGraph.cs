namespace helengine.editor;

/// <summary>
/// Stores a deterministic immutable set of asset-cook nodes.
/// </summary>
public sealed class EditorAssetCookGraph {
    /// <summary>
    /// Initializes a graph and rejects duplicate keys.
    /// </summary>
    /// <param name="nodes">Nodes to include in the graph.</param>
    public EditorAssetCookGraph(IEnumerable<EditorAssetCookNode> nodes) {
        if (nodes == null) {
            throw new ArgumentNullException(nameof(nodes));
        }

        EditorAssetCookNode[] orderedNodes = nodes
            .OrderBy(node => node?.Key.Value, StringComparer.Ordinal)
            .ToArray();
        if (orderedNodes.Any(node => node == null)) {
            throw new ArgumentException("Cook graph cannot contain null nodes.", nameof(nodes));
        }
        if (orderedNodes.Zip(orderedNodes.Skip(1)).Any(pair => pair.First.Key.Equals(pair.Second.Key))) {
            throw new ArgumentException("Cook graph cannot contain duplicate node keys.", nameof(nodes));
        }

        Nodes = Array.AsReadOnly(orderedNodes);
    }

    /// <summary>
    /// Gets nodes in ordinal key order.
    /// </summary>
    public IReadOnlyList<EditorAssetCookNode> Nodes { get; }
}
