namespace helengine {
    /// <summary>Loads additional atlas textures on demand and evicts the least recently used page within a fixed GPU budget.</summary>
    public sealed class FontPageCache : IDisposable {
        /// <summary>Borrows the immutable descriptors retained by the font.</summary>
        readonly FontPage[] Pages;
        /// <summary>Borrows the package content source; Load does not retain decoded assets.</summary>
        readonly ContentManager Content;
        /// <summary>Borrows the renderer responsible for uploads and safe texture destruction.</summary>
        readonly RenderManager2D Renderer;
        /// <summary>Owns the array of renderer-owned texture handles.</summary>
        [NativeOwnedMember] RuntimeTexture[] Textures;
        /// <summary>Owns the recency sequence for each optional atlas.</summary>
        [NativeOwnedMember] long[] LastUse;
        /// <summary>Limits additional atlas residency independently of the always-present base atlas.</summary>
        readonly long Budget;
        /// <summary>Counts the current RGBA allocation estimate.</summary>
        long Bytes;
        /// <summary>Orders requests without depending on wall-clock changes.</summary>
        long Sequence;

        /// <summary>Creates empty cache storage; no page texture is read or uploaded here.</summary>
        public FontPageCache(FontPage[] pages, ContentManager content, RenderManager2D renderer, long budget) {
            if (pages == null || content == null || renderer == null) { throw new ArgumentNullException(nameof(pages)); }
            if (budget <= 0) { throw new ArgumentOutOfRangeException(nameof(budget)); }
            for (int index = 0; index < pages.Length; index++) {
                if (pages[index] == null || pages[index].ResidentBytes > budget) { throw new InvalidOperationException("Every font page must fit the resident texture budget."); }
            }
            Pages = pages; Content = content; Renderer = renderer; Budget = budget;
            Textures = new RuntimeTexture[pages.Length]; LastUse = new long[pages.Length];
        }
        /// <summary>Gets the additional texture residency in bytes, excluding the base atlas.</summary>
        public long ResidentBytes => Bytes;
        /// <summary>Returns a borrowed page texture, uploading only on a cache miss.</summary>
        [NativeBorrowedReturn]
        public RuntimeTexture Resolve(int index) {
            if (Textures == null) { throw new ObjectDisposedException(nameof(FontPageCache)); }
            if (index < 0 || index >= Pages.Length) { throw new ArgumentOutOfRangeException(nameof(index)); }
            LastUse[index] = ++Sequence;
            if (Textures[index] != null) { return Textures[index]; }
            FontPage page = Pages[index];
            while (Bytes + page.ResidentBytes > Budget) { EvictOldest(); }
            TextureAsset source = Content.Load<TextureAsset>(page.TexturePath, RuntimeContentProcessorIds.TextureAsset);
            try {
                if (source.Width != page.Width || source.Height != page.Height || source.ColorFormat != TextureAssetColorFormat.Rgba32) {
                    throw new InvalidOperationException("Font page pixels must match their exported RGBA dimensions.");
                }
                Textures[index] = Renderer.BuildTextureFromRaw(source);
                if (Textures[index] == null) { throw new InvalidOperationException("Font page upload returned no texture."); }
                Bytes += page.ResidentBytes;
            } finally { NativeOwnership.DisposeAndDelete(source); }
            return Textures[index];
        }
        /// <summary>Releases the oldest resident atlas through its owning renderer.</summary>
        void EvictOldest() {
            int oldest = -1;
            for (int index = 0; index < Textures.Length; index++) {
                if (Textures[index] != null && (oldest < 0 || LastUse[index] < LastUse[oldest])) { oldest = index; }
            }
            if (oldest < 0) { throw new InvalidOperationException("Font page budget accounting has no eviction candidate."); }
            Renderer.ReleaseTexture(Textures[oldest]); Textures[oldest] = null; Bytes -= Pages[oldest].ResidentBytes;
        }
        /// <summary>Releases all additional GPU pages before the font's metadata is destroyed.</summary>
        public void Dispose() {
            if (Textures != null) {
                for (int index = 0; index < Textures.Length; index++) {
                    if (Textures[index] != null) { Renderer.ReleaseTexture(Textures[index]); Textures[index] = null; }
                }
            }
            Bytes = 0; NativeOwnership.Release(ref Textures); NativeOwnership.Release(ref LastUse);
        }
    }
}
