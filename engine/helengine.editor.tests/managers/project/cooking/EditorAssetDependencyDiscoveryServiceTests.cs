using helengine;
using Xunit;

namespace helengine.editor.tests;

/// <summary>
/// Verifies typed dependency discovery and cycle diagnostics.
/// </summary>
public sealed class EditorAssetDependencyDiscoveryServiceTests {
    /// <summary>
    /// Ensures a discoverer contributes each reachable dependency once.
    /// </summary>
    [Fact]
    public void Discover_ReturnsReachableTypedNodes() {
        EditorAssetCookNode dependency = CreateNode("dependency", AssetEntryKind.Image);
        EditorAssetCookNode root = CreateNode("root", AssetEntryKind.Model, new EditorAssetCookDependency(dependency.Key, AssetEntryKind.Image));
        EditorAssetDependencyDiscoveryService service = new(new[] { new StaticDiscoverer(AssetEntryKind.Model, dependency) });

        EditorAssetCookGraph graph = service.Discover(new[] { root });

        Assert.Equal(new[] { "source:dependency", "source:root" }, graph.Nodes.Select(node => node.Key.Value));
        Assert.Equal(AssetEntryKind.Image, graph.Nodes.Single(node => node.Key.Value == "source:dependency").SourceKind);
    }

    /// <summary>
    /// Ensures a recursive discoverer reports a complete cycle instead of overflowing the stack.
    /// </summary>
    [Fact]
    public void Discover_ReportsDependencyCycle() {
        EditorAssetCookNode first = CreateNode("first", AssetEntryKind.Model);
        EditorAssetCookNode second = CreateNode("second", AssetEntryKind.Model);
        StaticDiscoverer discoverer = new(AssetEntryKind.Model, second);
        discoverer.Next = first;
        EditorAssetDependencyDiscoveryService service = new(new[] { discoverer });

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => service.Discover(new[] { first }));

        Assert.Contains("source:first", exception.Message);
        Assert.Contains("source:second", exception.Message);
    }

    sealed class StaticDiscoverer : IEditorAssetDependencyDiscoverer {
        readonly EditorAssetCookNode Dependency;

        public StaticDiscoverer(AssetEntryKind kind, EditorAssetCookNode dependency) {
            Kind = kind;
            Dependency = dependency;
        }

        public AssetEntryKind Kind { get; }
        public EditorAssetCookNode Next { get; set; }

        public IReadOnlyList<EditorAssetCookNode> Discover(EditorAssetCookNode node) {
            if (Next != null && node.Key.Value == "source:second") {
                return new[] { Next };
            }
            return node.Key.Value == "source:root" || node.Key.Value == "source:first" ? new[] { Dependency } : Array.Empty<EditorAssetCookNode>();
        }
    }

    static EditorAssetCookNode CreateNode(string path, AssetEntryKind kind, params EditorAssetCookDependency[] dependencies) {
        return new EditorAssetCookNode(
            new EditorAssetCookNodeKey("source:" + path),
            global::helengine.SceneAssetReferenceFactory.CreateFileSystemReference(
                "0123456789abcdef0123456789abcdef",
                path,
                "sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"),
            kind,
            dependencies,
            "processor:" + kind);
    }
}
