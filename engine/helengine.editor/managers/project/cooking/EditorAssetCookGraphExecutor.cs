namespace helengine.editor;

/// <summary>
/// Executes cook nodes in dependency order and reuses immutable artifacts.
/// </summary>
public sealed class EditorAssetCookGraphExecutor {
    readonly EditorCookArtifactStore ArtifactStore;
    readonly IReadOnlyDictionary<string, IEditorAssetCookProcessor> ProcessorsById;

    /// <summary>
    /// Initializes one graph executor.
    /// </summary>
    /// <param name="artifactStore">Immutable artifact store.</param>
    /// <param name="processors">Registered leaf processors.</param>
    public EditorAssetCookGraphExecutor(EditorCookArtifactStore artifactStore, IEnumerable<IEditorAssetCookProcessor> processors) {
        ArtifactStore = artifactStore ?? throw new ArgumentNullException(nameof(artifactStore));
        if (processors == null) {
            throw new ArgumentNullException(nameof(processors));
        }

        ProcessorsById = processors.ToDictionary(processor => processor?.ProcessorId ?? throw new ArgumentException("Processors cannot contain null values.", nameof(processors)), StringComparer.Ordinal);
    }

    /// <summary>
    /// Executes a graph and returns artifacts in ordinal cook-key order.
    /// </summary>
    /// <param name="graph">Graph to execute.</param>
    /// <param name="engineVersion">Exact engine version.</param>
    /// <param name="platformId">Target platform.</param>
    /// <param name="profileId">Target profile.</param>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>Published artifacts.</returns>
    public IReadOnlyList<CookedAssetArtifact> Execute(EditorAssetCookGraph graph, string engineVersion, string platformId, string profileId, CancellationToken cancellationToken) {
        if (graph == null) {
            throw new ArgumentNullException(nameof(graph));
        }

        Dictionary<string, EditorAssetCookNode> nodesBySourceKey = graph.Nodes.ToDictionary(node => node.Key.Value, StringComparer.Ordinal);
        Dictionary<string, CookedAssetArtifact> completedArtifacts = new(StringComparer.Ordinal);
        HashSet<string> visitingKeys = new(StringComparer.Ordinal);
        foreach (EditorAssetCookNode rootNode in graph.Nodes) {
            ExecuteNode(rootNode, nodesBySourceKey, completedArtifacts, visitingKeys, engineVersion, platformId, profileId, cancellationToken);
        }

        return completedArtifacts.Values.OrderBy(artifact => artifact.CookKey.Value, StringComparer.Ordinal).ToArray();
    }

    CookedAssetArtifact ExecuteNode(
        EditorAssetCookNode node,
        IReadOnlyDictionary<string, EditorAssetCookNode> nodesBySourceKey,
        IDictionary<string, CookedAssetArtifact> completedArtifacts,
        ISet<string> visitingKeys,
        string engineVersion,
        string platformId,
        string profileId,
        CancellationToken cancellationToken) {
        if (completedArtifacts.TryGetValue(node.Key.Value, out CookedAssetArtifact existing)) {
            return existing;
        }
        if (!visitingKeys.Add(node.Key.Value)) {
            throw new InvalidOperationException("Cook graph contains a dependency cycle at '" + node.Key.Value + "'.");
        }

        List<CookedAssetArtifact> dependencies = new();
        foreach (EditorAssetCookDependency dependency in node.Dependencies) {
            if (!nodesBySourceKey.TryGetValue(dependency.Key.Value, out EditorAssetCookNode dependencyNode)) {
                throw new InvalidOperationException("Cook graph dependency '" + dependency.Key.Value + "' was not declared.");
            }
            dependencies.Add(ExecuteNode(dependencyNode, nodesBySourceKey, completedArtifacts, visitingKeys, engineVersion, platformId, profileId, cancellationToken));
        }

        cancellationToken.ThrowIfCancellationRequested();
        EditorAssetCookNodeKey cookKey = EditorAssetCookKeyBuilder.Build(node, engineVersion, platformId, profileId, null, dependencies.Select(dependency => dependency.CookKey));
        if (ArtifactStore.TryRead(cookKey, out byte[] cachedPayload, out EditorCookArtifactReceipt cachedReceipt)) {
            existing = new CookedAssetArtifact(cookKey, node.SourceKind, "current", cachedReceipt.ContentHash, cachedReceipt.ByteLength, cachedReceipt.StorePath, dependencies.Select(dependency => dependency.CookKey), platformId, profileId);
            completedArtifacts[node.Key.Value] = existing;
            visitingKeys.Remove(node.Key.Value);
            return existing;
        }

        if (!ProcessorsById.TryGetValue(node.ProcessorId, out IEditorAssetCookProcessor processor)) {
            throw new InvalidOperationException("No cook processor is registered for '" + node.ProcessorId + "'.");
        }
        byte[] payload = processor.Cook(node, dependencies, cancellationToken) ?? throw new InvalidOperationException("Cook processor returned no payload for '" + node.Key.Value + "'.");
        EditorCookArtifactReceipt receipt = ArtifactStore.Publish(cookKey, payload);
        existing = new CookedAssetArtifact(cookKey, node.SourceKind, "current", receipt.ContentHash, receipt.ByteLength, receipt.StorePath, dependencies.Select(dependency => dependency.CookKey), platformId, profileId);
        completedArtifacts[node.Key.Value] = existing;
        visitingKeys.Remove(node.Key.Value);
        return existing;
    }
}
