namespace helengine.editor;

/// <summary>
/// Produces one runtime payload for a resolved cook node.
/// </summary>
public interface IEditorAssetCookProcessor {
    /// <summary>
    /// Gets the stable processor identity included in cook keys.
    /// </summary>
    string ProcessorId { get; }

    /// <summary>
    /// Produces runtime bytes for one node after its dependencies are cooked.
    /// </summary>
    /// <param name="node">Node being cooked.</param>
    /// <param name="dependencies">Resolved dependency artifacts.</param>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>Runtime payload bytes.</returns>
    byte[] Cook(EditorAssetCookNode node, IReadOnlyList<CookedAssetArtifact> dependencies, CancellationToken cancellationToken);
}
