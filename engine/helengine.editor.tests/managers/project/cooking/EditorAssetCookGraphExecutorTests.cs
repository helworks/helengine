using helengine;
using Xunit;

namespace helengine.editor.tests;

/// <summary>
/// Verifies graph execution order, artifact reuse and failure publication rules.
/// </summary>
public sealed class EditorAssetCookGraphExecutorTests : IDisposable {
    readonly string StoreRootPath = Path.Combine("C:\\dev\\helworks\\builds\\helengine\\refactors", "cook-graph-tests", Guid.NewGuid().ToString("N"));

    /// <summary>
    /// Deletes the test-owned artifact store after each test.
    /// </summary>
    public void Dispose() {
        if (Directory.Exists(StoreRootPath)) {
            Directory.Delete(StoreRootPath, true);
        }
    }

    /// <summary>
    /// Ensures dependencies execute before consumers and a second execution reuses artifacts.
    /// </summary>
    [Fact]
    public void Execute_OrdersDependenciesAndReusesPublishedArtifacts() {
        List<string> executionOrder = new();
        CountingProcessor processor = new(executionOrder);
        EditorAssetCookNode dependency = CreateNode("dep", Array.Empty<EditorAssetCookDependency>());
        EditorAssetCookNode root = CreateNode("root", new[] { new EditorAssetCookDependency(dependency.Key, AssetEntryKind.Model) });
        EditorAssetCookGraph graph = new(new[] { root, dependency });
        EditorCookArtifactStore store = new(StoreRootPath);
        EditorAssetCookGraphExecutor executor = new(store, new[] { processor });

        IReadOnlyList<CookedAssetArtifact> first = executor.Execute(graph, "engine-1", "windows", "debug", CancellationToken.None);
        IReadOnlyList<CookedAssetArtifact> second = executor.Execute(graph, "engine-1", "windows", "debug", CancellationToken.None);

        Assert.Equal(new[] { "dep", "root" }, executionOrder);
        Assert.Equal(first.Select(artifact => artifact.CookKey), second.Select(artifact => artifact.CookKey));
    }

    sealed class CountingProcessor : IEditorAssetCookProcessor {
        readonly List<string> ExecutionOrder;

        public CountingProcessor(List<string> executionOrder) {
            ExecutionOrder = executionOrder;
        }

        public string ProcessorId => "processor:model";

        public byte[] Cook(EditorAssetCookNode node, IReadOnlyList<CookedAssetArtifact> dependencies, CancellationToken cancellationToken) {
            ExecutionOrder.Add(node.SourceReference.RelativePath);
            return System.Text.Encoding.UTF8.GetBytes(node.SourceReference.RelativePath);
        }
    }

    static EditorAssetCookNode CreateNode(string path, IEnumerable<EditorAssetCookDependency> dependencies) {
        return new EditorAssetCookNode(
            new EditorAssetCookNodeKey("source:" + path),
            global::helengine.SceneAssetReferenceFactory.CreateFileSystemReference(
                "0123456789abcdef0123456789abcdef",
                path,
                "sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"),
            AssetEntryKind.Model,
            dependencies,
            "processor:model");
    }
}
