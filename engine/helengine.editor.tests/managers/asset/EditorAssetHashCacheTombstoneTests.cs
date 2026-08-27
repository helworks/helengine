using Xunit;

namespace helengine.editor.tests.managers.asset;

/// <summary>
/// Verifies that invalidated and deleted paths remain removed from persisted cache state.
/// </summary>
public sealed class EditorAssetHashCacheTombstoneTests : IDisposable {
    readonly string ProjectRootPath;

    public EditorAssetHashCacheTombstoneTests() {
        ProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-hash-tombstone-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(ProjectRootPath, "assets"));
    }

    public void Dispose() {
        if (Directory.Exists(ProjectRootPath)) {
            Directory.Delete(ProjectRootPath, true);
        }
    }

    [Fact]
    public void InvalidateContentHash_WhenFileIsDeleted_PersistsDeletionTombstone() {
        string assetPath = Path.Combine(ProjectRootPath, "assets", "Deleted.obj");
        File.WriteAllBytes(assetPath, new byte[] { 1, 2, 3 });
        using (EditorAssetHashCache cache = new EditorAssetHashCache(ProjectRootPath)) {
            cache.GetContentHash(assetPath);
            cache.Flush();
            File.Delete(assetPath);
            cache.InvalidateContentHash(assetPath);
            cache.Flush();
        }

        EditorAssetHashCacheDocument document = new FileEditorAssetHashCacheStore().Load(
            Path.Combine(ProjectRootPath, "cache", "editor", "asset-identity-index.json"));
        Assert.DoesNotContain(document.Entries, entry => entry.RelativePath == "Deleted.obj");
    }
}
