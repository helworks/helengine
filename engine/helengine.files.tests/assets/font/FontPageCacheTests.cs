using helengine;
namespace helengine.files.tests.assets.font {
    /// <summary>Verifies lazy residency, true least-recently-used eviction and renderer-owned destruction.</summary>
    public sealed class FontPageCacheTests {
        /// <summary>Proves no startup reads, cache hits, recency-sensitive eviction and reload after eviction.</summary>
        [Fact]
        public void Resolve_LoadsOnlyRequestedPagesAndEvictsLeastRecentlyUsed() {
            FontPageContentSource source = new FontPageContentSource();
            using ContentManager content = new ContentManager(source);
            content.RegisterProcessor<TextureAsset>(RuntimeContentProcessorIds.TextureAsset, new AssetContentProcessor<TextureAsset>());
            using NativeAtlasRenderManager renderer = new NativeAtlasRenderManager();
            FontPage[] pages = new[] { new FontPage("a.hasset", 1, 1), new FontPage("b.hasset", 1, 1), new FontPage("c.hasset", 1, 1) };
            using FontPageCache cache = new FontPageCache(pages, content, renderer, 8);
            Assert.Equal(0, source.Reads); Assert.Equal(0, cache.ResidentBytes);
            RuntimeTexture first = cache.Resolve(0), second = cache.Resolve(1);
            Assert.Same(first, cache.Resolve(0)); Assert.Equal(2, source.Reads);
            RuntimeTexture third = cache.Resolve(2);
            Assert.True(second.IsDisposed); Assert.False(first.IsDisposed); Assert.False(third.IsDisposed);
            Assert.Equal(8, cache.ResidentBytes); Assert.Equal(3, source.Reads);
            Assert.NotSame(second, cache.Resolve(1)); Assert.Equal(4, source.Reads);
            cache.Dispose(); Assert.Equal(0, cache.ResidentBytes); Assert.True(third.IsDisposed);
            Assert.Throws<ObjectDisposedException>(() => cache.Resolve(0));
        }
        /// <summary>Rejects an impossible budget before reading pixels or attempting an upload.</summary>
        [Fact]
        public void Constructor_RejectsPageLargerThanBudgetWithoutReading() {
            FontPageContentSource source = new FontPageContentSource();
            using ContentManager content = new ContentManager(source);
            using NativeAtlasRenderManager renderer = new NativeAtlasRenderManager();
            Assert.Throws<InvalidOperationException>(() => new FontPageCache(new[] { new FontPage("a.hasset", 2, 2) }, content, renderer, 8));
            Assert.Equal(0, source.Reads);
        }
    }
}
