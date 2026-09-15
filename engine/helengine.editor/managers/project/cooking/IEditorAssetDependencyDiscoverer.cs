namespace helengine.editor {

    /// <summary>
    /// Discovers direct authored dependencies for one cook node.
    /// </summary>
    public interface IEditorAssetDependencyDiscoverer {
        /// <summary>
        /// Gets the asset kind handled by this discoverer.
        /// </summary>
        AssetEntryKind Kind { get; }

        /// <summary>
        /// Returns direct dependency nodes for the supplied source node.
        /// </summary>
        /// <param name="node">Node being inspected.</param>
        /// <returns>Direct dependency nodes.</returns>
        IReadOnlyList<EditorAssetCookNode> Discover(EditorAssetCookNode node);
    }
}
