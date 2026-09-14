using Xunit;

namespace helengine.editor.tests;

/// <summary>
/// Verifies the explicit execution, runtime-resolution and invalidation boundaries.
/// </summary>
public sealed class AssetImportServiceBoundaryTests {
    /// <summary>
    /// Ensures invalidations deduplicate and drain deterministically.
    /// </summary>
    [Fact]
    public void InvalidationService_DeduplicatesAndDrainsSortedPaths() {
        using AssetImportInvalidationService service = new();
        List<string> notifications = new();
        service.Invalidated += path => notifications.Add(path);

        Assert.True(service.Invalidate("z.asset"));
        Assert.True(service.Invalidate("a.asset"));
        Assert.False(service.Invalidate("Z.asset"));

        Assert.Equal(
            new[] { Path.GetFullPath("a.asset"), Path.GetFullPath("z.asset") },
            service.Drain());
        Assert.Equal(2, notifications.Count);
        Assert.Empty(service.Drain());
    }

    /// <summary>
    /// Ensures disposal cancels operation work for dependent import services.
    /// </summary>
    [Fact]
    public void InvalidationService_DisposeCancelsItsToken() {
        AssetImportInvalidationService service = new();
        CancellationToken token = service.CancellationToken;

        service.Dispose();
        service.Dispose();
        Assert.True(token.IsCancellationRequested);
    }
}
