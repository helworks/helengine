namespace helengine {
    /// <summary>Describes one DS 3D texture encoding or shared OBJ/BG tile encoding and its intrinsic alpha.</summary>
    public sealed class NintendoDsTextureFormat {
        /// <summary>Initializes immutable storage and palette requirements.</summary>
        public NintendoDsTextureFormat(int code, string id, int bits, int paletteEntries, TextureAssetAlphaPrecision alpha) {
            Code = code; Id = id; BitsPerPixel = bits; PaletteEntries = paletteEntries; Alpha = alpha;
        }
        /// <summary>Gets the hardware texture code, or eight/nine for 4/8-bit 2D tiles.</summary>
        public int Code { get; }
        /// <summary>Gets the published platform override ID.</summary>
        public string Id { get; }
        /// <summary>Gets texel bits excluding compressed block descriptors.</summary>
        public int BitsPerPixel { get; }
        /// <summary>Gets fixed palette capacity, zero for direct color or variable compressed palettes.</summary>
        public int PaletteEntries { get; }
        /// <summary>Gets intrinsic alpha precision.</summary>
        public TextureAssetAlphaPrecision Alpha { get; }
        /// <summary>Gets whether indices use row-major 8-by-8 tiles.</summary>
        public bool IsTiled => Code >= 8;
        /// <summary>Accepts intrinsic precision or opaque cooking, rejecting unrelated generic precisions.</summary>
        public bool SupportsAlpha(TextureAssetAlphaPrecision alpha) => alpha == TextureAssetAlphaPrecision.Opaque || alpha == Alpha;
    }
}
