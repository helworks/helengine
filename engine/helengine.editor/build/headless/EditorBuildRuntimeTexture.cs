namespace helengine.editor {
    /// <summary>Owns decoded RGBA8 pixels for authoring operations that require texture data without a graphics device.</summary>
    public sealed class EditorBuildRuntimeTexture : RuntimeTexture {
        /// <summary>Independent row-major pixel storage released with this runtime texture.</summary>
        byte[] PixelsValue;
        /// <summary>Original decoded row stride, independent of mutable runtime metadata.</summary>
        readonly int RowPitch;

        /// <summary>Decodes and copies the borrowed raw texture into renderer-owned CPU storage.</summary>
        /// <param name="owner">Renderer responsible for releasing this texture.</param>
        /// <param name="asset">Borrowed source texture whose buffers remain caller-owned.</param>
        internal EditorBuildRuntimeTexture(EditorBuildRenderManager2D owner, TextureAsset asset) {
            Owner = owner ?? throw new ArgumentNullException(nameof(owner));
            PixelsValue = TextureAssetPixelCodec.DecodeToRgba32(asset);
            Width = asset.Width;
            Height = asset.Height;
            IsEngineOwned = asset.IsEngineOwned;
            RowPitch = checked(Width * 4);
        }

        /// <summary>Gets the renderer that created this CPU texture.</summary>
        internal EditorBuildRenderManager2D Owner { get; }
        /// <summary>Gets a read-only view of the owned pixels; access after release is an error.</summary>
        public ReadOnlyMemory<byte> Pixels {
            get {
                ObjectDisposedException.ThrowIf(IsDisposed, this);
                return PixelsValue;
            }
        }

        /// <summary>Copies validated rectangular rows into CPU storage without retaining the source buffer.</summary>
        /// <param name="x">Destination left edge.</param>
        /// <param name="y">Destination top edge.</param>
        /// <param name="width">Number of pixels copied from each source row.</param>
        /// <param name="height">Number of rows copied.</param>
        /// <param name="pixels">Borrowed RGBA8 source buffer.</param>
        /// <param name="sourceRowPitch">Distance in bytes between source rows.</param>
        internal void UpdateRegion(int x, int y, int width, int height, byte[] pixels, int sourceRowPitch) {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            if (Width != RowPitch / 4 || Height != PixelsValue.Length / RowPitch) {
                throw new InvalidOperationException("Headless authoring texture dimensions cannot change after materialization.");
            }
            for (int row = 0; row < height; row++) {
                Buffer.BlockCopy(pixels, row * sourceRowPitch, PixelsValue, (y + row) * RowPitch + x * 4, width * 4);
            }
        }

        /// <summary>Releases the renderer-owned pixel array without changing the borrowed source texture.</summary>
        public override void Dispose() {
            PixelsValue = null;
            base.Dispose();
        }
    }
}
