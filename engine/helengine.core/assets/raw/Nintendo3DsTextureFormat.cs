namespace helengine {
    /// <summary>Describes one PICA200 texture encoding with its hardware code and intrinsic alpha precision.</summary>
    public sealed class Nintendo3DsTextureFormat {
        /// <summary>Creates immutable metadata for a published hardware encoding.</summary>
        public Nintendo3DsTextureFormat(string id, int code) {
            if (code < 0 || code > 13) throw new ArgumentOutOfRangeException(nameof(code));
            Id = id; Code = code;
        }
        /// <summary>Gets the editor and cooker setting identifier.</summary>
        public string Id { get; }
        /// <summary>Gets the libctru GPU_TEXCOLOR value.</summary>
        public int Code { get; }
        /// <summary>Gets component bits per pixel or average ETC bits per pixel.</summary>
        public int BitsPerPixel => Code == 0 ? 32 : Code == 1 ? 24 : Code >= 2 && Code <= 6 ? 16 : Code == 10 || Code == 11 || Code == 12 ? 4 : 8;
        /// <summary>Gets whether storage consists of four ETC blocks per 8-by-8 tile.</summary>
        public bool IsCompressed => Code >= 12;
        /// <summary>Gets the encoding's independently stored alpha precision.</summary>
        public TextureAssetAlphaPrecision Alpha => Code == 0 || Code == 5 || Code == 8 ? TextureAssetAlphaPrecision.A8
            : Code == 2 ? TextureAssetAlphaPrecision.Binary : Code == 4 || Code == 9 || Code == 11 || Code == 13 ? TextureAssetAlphaPrecision.A4 : TextureAssetAlphaPrecision.Opaque;
        /// <summary>Accepts opaque cooking or the encoding's intrinsic alpha precision.</summary>
        public bool SupportsAlpha(TextureAssetAlphaPrecision alpha) => alpha == TextureAssetAlphaPrecision.Opaque || alpha == Alpha;
    }
}
