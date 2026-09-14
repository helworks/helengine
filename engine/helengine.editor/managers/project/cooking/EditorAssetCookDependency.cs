namespace helengine.editor;

/// <summary>
/// Describes one typed dependency of a cook node.
/// </summary>
public sealed class EditorAssetCookDependency {
    /// <summary>
    /// Initializes one cook dependency.
    /// </summary>
    /// <param name="key">Referenced node key.</param>
    /// <param name="kind">Referenced asset kind.</param>
    public EditorAssetCookDependency(EditorAssetCookNodeKey key, AssetEntryKind kind) {
        Key = key ?? throw new ArgumentNullException(nameof(key));
        Kind = kind;
    }

    /// <summary>
    /// Gets the referenced node key.
    /// </summary>
    public EditorAssetCookNodeKey Key { get; }

    /// <summary>
    /// Gets the referenced asset kind.
    /// </summary>
    public AssetEntryKind Kind { get; }
}
