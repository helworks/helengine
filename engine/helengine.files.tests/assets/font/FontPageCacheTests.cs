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
        /// <summary>Pins resident pages across cache pressure and trims excess residency when visible users release their pages.</summary>
        [Fact]
        public void RetainedPagesSurvivePressureUntilLastRelease() {
            FontPageContentSource source = new FontPageContentSource();
            using ContentManager content = new ContentManager(source);
            content.RegisterProcessor<TextureAsset>(RuntimeContentProcessorIds.TextureAsset, new AssetContentProcessor<TextureAsset>());
            using NativeAtlasRenderManager renderer = new NativeAtlasRenderManager();
            using FontPageCache cache = new FontPageCache(new[] { new FontPage("a.hasset", 1, 1), new FontPage("b.hasset", 1, 1) }, content, renderer, 4);
            RuntimeTexture first = cache.Retain(0);
            Assert.Same(first, cache.Retain(0));
            RuntimeTexture second = cache.Retain(1);
            Assert.False(first.IsDisposed); Assert.False(second.IsDisposed); Assert.Equal(8, cache.ResidentBytes);
            cache.Release(0); Assert.False(first.IsDisposed);
            cache.Release(0); Assert.True(first.IsDisposed); Assert.False(second.IsDisposed); Assert.Equal(4, cache.ResidentBytes);
            Assert.Throws<InvalidOperationException>(() => cache.Release(0));
            cache.Release(1); cache.Resolve(0); Assert.True(second.IsDisposed);
        }
        /// <summary>Normal 2D requests retain one transient page above pinned residency and evict that transient page on the next miss.</summary>
        [Fact]
        public void BorrowedPagesRemainUsableWhenVisiblePinsFillTheBudget() {
            FontPageContentSource source = new FontPageContentSource(); using ContentManager content = new ContentManager(source);
            content.RegisterProcessor<TextureAsset>(RuntimeContentProcessorIds.TextureAsset, new AssetContentProcessor<TextureAsset>());
            using NativeAtlasRenderManager renderer = new NativeAtlasRenderManager();
            using FontPageCache cache = new FontPageCache(new[] {
                new FontPage("a.hasset", 1, 1), new FontPage("b.hasset", 1, 1), new FontPage("c.hasset", 1, 1), new FontPage("d.hasset", 1, 1)
            }, content, renderer, 4);
            RuntimeTexture first = cache.Retain(0), second = cache.Retain(1), transient = cache.Resolve(2);
            Assert.Equal(12, cache.ResidentBytes); Assert.Same(transient, cache.Resolve(2));
            RuntimeTexture next = cache.Resolve(3);
            Assert.True(transient.IsDisposed); Assert.False(first.IsDisposed); Assert.False(second.IsDisposed); Assert.False(next.IsDisposed);
            Assert.Equal(12, cache.ResidentBytes);
            cache.Release(0); Assert.True(first.IsDisposed); Assert.True(next.IsDisposed); Assert.False(second.IsDisposed);
            Assert.Equal(4, cache.ResidentBytes); cache.Release(1);
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
