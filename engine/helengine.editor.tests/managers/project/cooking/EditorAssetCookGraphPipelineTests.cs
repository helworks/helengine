using helengine;
using Xunit;

namespace helengine.editor.tests;

/// <summary>
/// Verifies the unified graph pipeline composes discovery and execution once.
/// </summary>
public sealed class EditorAssetCookGraphPipelineTests : IDisposable {
    readonly string StoreRootPath = Path.Combine("C:\\dev\\helworks\\builds\\helengine\\refactors", "cook-graph-pipeline-tests", Guid.NewGuid().ToString("N"));

    /// <summary>
    /// Removes artifacts created by the test operation.
    /// </summary>
    public void Dispose() {
        if (Directory.Exists(StoreRootPath)) {
            Directory.Delete(StoreRootPath, true);
        }
    }

    /// <summary>
    /// Ensures discovery and execution share one typed operation boundary.
    /// </summary>
    [Fact]
    public void Execute_DiscoversAndExecutesReachableNodes() {
        EditorAssetCookNode root = CreateNode("root");
        EditorAssetDependencyDiscoveryService discovery = new(Array.Empty<IEditorAssetDependencyDiscoverer>());
        EditorCookArtifactStore store = new(StoreRootPath);
        EditorAssetCookGraphExecutor executor = new(store, new[] { new EchoProcessor() });
        EditorAssetCookGraphPipeline pipeline = new(discovery, executor);
        EditorAssetCookGraphRequest request = new(
            new[] { root.SourceReference },
            "windows",
            "debug",
            "engine-1",
            StoreRootPath);

        IReadOnlyList<CookedAssetArtifact> artifacts = pipeline.Execute(request, new[] { root }, CancellationToken.None);

        Assert.Single(artifacts);
        Assert.Equal("windows", artifacts[0].PlatformId);
        Assert.Equal("debug", artifacts[0].ProfileId);
    }

    sealed class EchoProcessor : IEditorAssetCookProcessor {
        public string ProcessorId => "processor:model";

        public byte[] Cook(EditorAssetCookNode node, IReadOnlyList<CookedAssetArtifact> dependencies, CancellationToken cancellationToken) {
            return System.Text.Encoding.UTF8.GetBytes(node.SourceReference.RelativePath);
        }
    }

    static EditorAssetCookNode CreateNode(string path) {
        return new EditorAssetCookNode(
            new EditorAssetCookNodeKey("source:" + path),
            global::helengine.SceneAssetReferenceFactory.CreateFileSystemReference(
                "0123456789abcdef0123456789abcdef",
                path,
                "sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"),
            AssetEntryKind.Model,
            Array.Empty<EditorAssetCookDependency>(),
            "processor:model");
    }
}
