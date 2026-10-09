namespace helengine {
    /// <summary>
    /// Identifies how one <see cref="TextureAsset"/> stores its serialized color payload.
    /// </summary>
    public enum TextureAssetColorFormat : byte {
        /// <summary>
        /// Stores one texel as four 8-bit RGBA channels.
        /// </summary>
        Rgba32 = 0,

        /// <summary>
        /// Stores one texel as four 4-bit RGBA channels packed into a 16-bit word.
        /// </summary>
        Rgba4444 = 1,

        /// <summary>
        /// Stores one texel as one 4-bit palette index.
        /// </summary>
        Indexed4 = 2,

        /// <summary>
        /// Stores one texel as one 8-bit palette index.
        /// </summary>
        Indexed8 = 3,

        /// <summary>
        /// Stores one texel as one GX RGB5A3 word laid out in native 4x4 tiled order.
        /// </summary>
        GxRgb5A3 = 4,

        /// <summary>
        /// Stores little-endian 16-bit words: R5 in bits 15..11, G5 in 10..6, B5 in 5..1 and alpha in bit 0.
        /// </summary>
        Rgba5551 = 5,

        /// <summary>
        /// Stores I3/A1 texels, first texel in the high nibble; each row ends on a byte boundary.
        /// </summary>
        Ia4 = 6,

        /// <summary>
        /// Stores one I4/A4 texel per byte, intensity in the high nibble and alpha in the low nibble.
        /// </summary>
        Ia8 = 7,

        /// <summary>
        /// Stores little-endian I8/A8 words with intensity in bits 15..8: bytes are alpha followed by intensity.
        /// </summary>
        Ia16 = 8,

        /// <summary>
        /// Stores 4-bit intensity replicated into RGBA; first texel uses the high nibble and rows are byte-aligned.
        /// </summary>
        I4 = 9,

        /// <summary>
        /// Stores 8-bit intensity replicated into all four RGBA channels, including alpha.
        /// </summary>
        I8 = 10,

        /// <summary>
        /// Stores BT.601 limited-range YUV422 bytes Y0/U/Y1/V per horizontal pair;
        /// rows with odd width duplicate their last pixel to complete the final pair.
        /// </summary>
        Yuv16 = 11,

        /// <summary>Stores one PS1 BGR555 texel as a little-endian 16-bit word; zero is transparent and bit 15 is STP.</summary>
        Ps1Bgr555 = 12,

        /// <summary>Stores an XTX1 native Xbox header followed by linear, Morton or DXT GPU texels and an optional native BGRA palette.</summary>
        XboxNative = 13,

        /// <summary>Stores a canonical X3T1 header followed by linear Xbox 360 GPU block or component rows.</summary>
        Xbox360Native = 14,

        /// <summary>Stores a little-endian PGT1 header with GPU-ready PSP texels and a separate native CLUT.</summary>
        PspNative = 15,

        /// <summary>Stores an N3T1 descriptor followed by GPU-ready Nintendo 3DS PICA200 tiles or ETC blocks.</summary>
        Nintendo3DsNative = 16,

        /// <summary>Stores a GXN1 descriptor followed by GPU-ready tiled GX texels and a separate native TLUT.</summary>
        GxNative = 17,

        /// <summary>Stores a DST1 descriptor, native DS texture or 2D tiles, optional compressed block descriptors and RGB555 palette.</summary>
        NintendoDsNative = 18,

        /// <summary>Stores a VGT1 descriptor with native PS Vita GXM texels and an optional GPU palette.</summary>
        VitaNative = 19,

        /// <summary>Stores an SWT1 descriptor and native Tegra texels with an independently cached RGBA preview.</summary>
        SwitchNative = 20,

        /// <summary>Stores a WGT1 descriptor with native Wii U GX2 linear transport texels.</summary>
        WiiUNative = 21
    }
}
