using helengine;
using Xunit;

namespace helengine.editor.tests;

/// <summary>
/// Verifies deterministic cook-key inputs and invalidation boundaries.
/// </summary>
public sealed class EditorAssetCookKeyBuilderTests {
    /// <summary>
    /// Ensures equivalent inputs produce the same key regardless of settings insertion order.
    /// </summary>
    [Fact]
    public void Build_UsesCanonicalOrdinalInputs() {
        EditorAssetCookNode node = CreateNode("Models/a.model", "processor:model");
        EditorAssetCookNodeKey first = EditorAssetCookKeyBuilder.Build(
            node,
            "engine-1",
            "windows",
            "debug",
            new Dictionary<string, string> { ["b"] = "2", ["a"] = "1" },
            Array.Empty<EditorAssetCookNodeKey>());
        EditorAssetCookNodeKey second = EditorAssetCookKeyBuilder.Build(
            node,
            "engine-1",
            "windows",
            "debug",
            new Dictionary<string, string> { ["a"] = "1", ["b"] = "2" },
            Array.Empty<EditorAssetCookNodeKey>());

        Assert.Equal(first, second);
    }

    /// <summary>
    /// Ensures target and dependency changes invalidate the key.
    /// </summary>
    [Fact]
    public void Build_ChangesWhenTargetOrDependencyChanges() {
        EditorAssetCookNode node = CreateNode("Models/a.model", "processor:model");
        EditorAssetCookNodeKey first = EditorAssetCookKeyBuilder.Build(node, "engine-1", "windows", "debug", null, Array.Empty<EditorAssetCookNodeKey>());
        EditorAssetCookNodeKey target = EditorAssetCookKeyBuilder.Build(node, "engine-1", "ps2", "debug", null, Array.Empty<EditorAssetCookNodeKey>());
        EditorAssetCookNodeKey dependency = EditorAssetCookKeyBuilder.Build(node, "engine-1", "windows", "debug", null, new[] { new EditorAssetCookNodeKey("dep:b") });

        Assert.NotEqual(first, target);
        Assert.NotEqual(first, dependency);
    }

    static EditorAssetCookNode CreateNode(string path, string processorId) {
        return new EditorAssetCookNode(
            new EditorAssetCookNodeKey("source:" + path),
            global::helengine.SceneAssetReferenceFactory.CreateFileSystemReference(
                "0123456789abcdef0123456789abcdef",
                path,
                "sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"),
            AssetEntryKind.Model,
            Array.Empty<EditorAssetCookDependency>(),
            processorId);
    }
}
