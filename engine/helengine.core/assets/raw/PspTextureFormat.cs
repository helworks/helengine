namespace helengine {
    /// <summary>Describes one PSP GU texture format, storage order and native CLUT format.</summary>
    public sealed class PspTextureFormat {
        /// <summary>Creates an exact published format identifier.</summary>
        public PspTextureFormat(string id, int code, bool swizzled, int paletteCode) {
            Id = id;
            Code = code;
            Swizzled = swizzled;
            PaletteCode = paletteCode;
        }
        /// <summary>Gets the stable editor setting identifier.</summary>
        public string Id { get; }
        /// <summary>Gets the GU_PSM hardware code, from zero through ten.</summary>
        public int Code { get; }
        /// <summary>Gets whether texel bytes use PSP 16-byte by 8-row blocks.</summary>
        public bool Swizzled { get; }
        /// <summary>Gets the direct-color CLUT format, or zero for non-indexed storage.</summary>
        public int PaletteCode { get; }
        /// <summary>Gets whether the texture stores palette indices.</summary>
        public bool IsIndexed => Code >= 4 && Code <= 7;
        /// <summary>Gets whether the texture stores PSP-ordered DXT blocks.</summary>
        public bool IsCompressed => Code >= 8;
        /// <summary>Gets the number of bits per uncompressed texel or compressed average texel.</summary>
        public int BitsPerPixel => Code == 4 || Code == 8 ? 4 : Code == 5 || Code >= 9 ? 8 : Code == 3 || Code == 7 ? 32 : 16;
        /// <summary>Gets the maximum independent alpha precision supported by this format.</summary>
        public TextureAssetAlphaPrecision Alpha => (IsIndexed ? PaletteCode : Code) == 0 ? TextureAssetAlphaPrecision.Opaque
            : (IsIndexed ? PaletteCode : Code) == 1 || Code == 8 ? TextureAssetAlphaPrecision.Binary
            : (IsIndexed ? PaletteCode : Code) == 2 || Code == 9 ? TextureAssetAlphaPrecision.A4 : TextureAssetAlphaPrecision.A8;
        /// <summary>Tests an explicit alpha policy without silently changing its precision.</summary>
        public bool SupportsAlpha(TextureAssetAlphaPrecision alpha) => alpha == TextureAssetAlphaPrecision.Opaque || alpha == Alpha;
    }
}
