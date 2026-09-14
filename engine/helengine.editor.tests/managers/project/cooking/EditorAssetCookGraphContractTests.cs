using helengine;
using Xunit;

namespace helengine.editor.tests;

/// <summary>
/// Verifies the immutable and deterministic contracts used by the editor asset cook graph.
/// </summary>
public sealed class EditorAssetCookGraphContractTests {
    /// <summary>
    /// Ensures graph enumeration is ordinal and independent of insertion order.
    /// </summary>
    [Fact]
    public void Graph_EnumeratesNodesByOrdinalCookKey() {
        EditorAssetCookNodeKey firstKey = new("asset:a");
        EditorAssetCookNodeKey secondKey = new("asset:b");
        EditorAssetCookNode first = CreateNode(firstKey);
        EditorAssetCookNode second = CreateNode(secondKey);

        EditorAssetCookGraph graph = new(new[] { second, first });

        Assert.Equal(new[] { "asset:a", "asset:b" }, graph.Nodes.Select(node => node.Key.Value));
    }

    /// <summary>
    /// Ensures duplicate keys cannot create ambiguous graph nodes.
    /// </summary>
    [Fact]
    public void Graph_RejectsDuplicateKeys() {
        EditorAssetCookNode first = CreateNode(new EditorAssetCookNodeKey("asset:a"));
        EditorAssetCookNode second = CreateNode(new EditorAssetCookNodeKey("asset:a"));

        Assert.Throws<ArgumentException>(() => new EditorAssetCookGraph(new[] { first, second }));
    }

    /// <summary>
    /// Ensures node dependencies and artifacts cannot be mutated after construction.
    /// </summary>
    [Fact]
    public void NodeAndArtifact_ExposeImmutableCopies() {
        List<EditorAssetCookDependency> dependencies = new();
        EditorAssetCookNode node = new(
            new EditorAssetCookNodeKey("asset:a"),
            CreateReference("Models/a.model"),
            AssetEntryKind.Model,
            dependencies,
            "model");
        dependencies.Add(new EditorAssetCookDependency(new EditorAssetCookNodeKey("asset:b"), AssetEntryKind.Image));

        Assert.Empty(node.Dependencies);
        Assert.Throws<ArgumentException>(() => new EditorAssetCookNodeKey(" "));
        Assert.Throws<ArgumentException>(() => new CookedAssetArtifact(
            new EditorAssetCookNodeKey("asset:a"),
            AssetEntryKind.Model,
            "",
            "sha256:abc",
            3,
            "cook/objects/asset-a/model.bin",
            Array.Empty<EditorAssetCookNodeKey>(),
            "windows",
            "default"));
    }

    static EditorAssetCookNode CreateNode(EditorAssetCookNodeKey key) {
        return new EditorAssetCookNode(key, CreateReference(key.Value), AssetEntryKind.Model, Array.Empty<EditorAssetCookDependency>(), "model");
    }

    static SceneAssetReference CreateReference(string path) {
        return global::helengine.SceneAssetReferenceFactory.CreateFileSystemReference(
            "0123456789abcdef0123456789abcdef",
            path,
            "sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef");
    }
}
