using helengine;
namespace helengine.files.tests.assets.font {
    /// <summary>Serves one tiny atlas at every requested page path and records actual content reads.</summary>
    sealed class FontPageContentSource : IContentStreamSource {
        /// <summary>Retains serialized texture bytes shared by the test pages.</summary>
        readonly byte[] Payload;
        /// <summary>Creates a deterministic one-pixel RGBA atlas fixture.</summary>
        public FontPageContentSource() {
            using TextureAsset texture = new TextureAsset { Id = "page", Width = 1, Height = 1,
                ColorFormat = TextureAssetColorFormat.Rgba32, AlphaPrecision = TextureAssetAlphaPrecision.A8,
                Colors = new byte[] { 255, 255, 255, 255 } };
            using MemoryStream stream = new MemoryStream();
            helengine.files.EditorAssetBinarySerializer.Serialize(stream, texture);
            Payload = stream.ToArray();
        }
        /// <summary>Counts reads, allowing cache hits and construction to be distinguished from loads.</summary>
        public int Reads { get; private set; }
        /// <summary>Returns a fresh owned stream only when a page is actually requested.</summary>
        public Stream OpenRead(string assetPath) { Reads++; return new MemoryStream(Payload, false); }
    }
}
