namespace helengine {
    /// <summary>Owns canonical GXN1 encoding, strict descriptor validation and native GX preview decoding.</summary>
    public static class GxNativeTextureCodec {
        /// <summary>Identifies the ASCII bytes GXN1 independently of the outer container byte order.</summary>
        public const uint Magic = 0x314E5847;
        /// <summary>Gets the twelve-word version-one descriptor size.</summary>
        public const int HeaderLength = 48;
        /// <summary>Encodes direct generic pixels or already-quantized generic Indexed4/8 sources into native tiled GX storage.</summary>
        [NativeOwnedReturn]
        public static TextureAsset Encode(TextureAsset source, string formatId, TextureAssetAlphaPrecision alpha) {
            if (source == null) throw new ArgumentNullException(nameof(source));
            GxNativeTextureFormatDefinition format = GetFormat(formatId, alpha);
            format.GetPixelByteLength(source.Width, source.Height);
            if (format.IsIndexed) return EncodeGenericIndexed(source, format, alpha);
            byte[] rgba = TextureAssetPixelCodec.DecodeToRgba32(source);
            try { return EncodeDirect(source, rgba, format, alpha); }
            finally { NativeOwnership.Release(ref rgba); }
        }
        /// <summary>Encodes all fourteen CI14X2 index bits from explicit ushort indices and a logical RGBA palette of up to 16384 entries.</summary>
        [NativeOwnedReturn]
        public static TextureAsset EncodeIndexed(TextureAsset identity, ushort[] indices, byte[] rgbaPalette, string formatId, TextureAssetAlphaPrecision alpha) {
            if (identity == null) throw new ArgumentNullException(nameof(identity));
            GxNativeTextureFormatDefinition format = GetFormat(formatId, alpha);
            format.GetPixelByteLength(identity.Width, identity.Height);
            if (!format.IsIndexed || indices == null || indices.Length != identity.Width * identity.Height) throw new ArgumentException("GX indexed encoding requires one index per logical pixel.");
            int entries = ValidatePalette(rgbaPalette, format);
            for (int index = 0; index < indices.Length; index++) if (indices[index] >= entries) throw new ArgumentException("GX index exceeds the authored palette.");
            return EncodeExplicitIndexed(identity, indices, rgbaPalette, format, alpha, entries);
        }
        /// <summary>Returns independent RGBA preview storage after validating native descriptor and all padded palette indices.</summary>
        [NativeOwnedReturn]
        public static byte[] Decode(TextureAsset asset) {
            GxNativeTextureLayout layout = ReadLayout(asset);
            try { return DecodePixels(asset, layout); }
            finally { NativeOwnership.Release(ref layout); }
        }
        /// <summary>Reads a canonical descriptor and rejects unknown codes, reserved bits, mismatched metadata or malformed native indices.</summary>
        [NativeOwnedReturn]
        public static GxNativeTextureLayout ReadLayout(TextureAsset asset) {
            if (asset == null || asset.ColorFormat != TextureAssetColorFormat.GxNative || asset.Colors == null || asset.Colors.Length < HeaderLength) throw new ArgumentException("Texture is not a complete GXN1 record.");
            byte[] colors = asset.Colors;
            uint code = Read(colors, 8); uint tlut = Read(colors, 24); uint count = Read(colors, 28);
            if (Read(colors, 0) != Magic || Read(colors, 4) != 1 || code > 255 || (tlut != uint.MaxValue && tlut > 2) || count > 16384 || Read(colors, 40) != 0 || Read(colors, 44) != 0) throw new ArgumentException("Invalid GXN1 descriptor or reserved fields.");
            if (!GxNativeTextureFormatCatalog.TryGetHardwareFormat((int)code, tlut == uint.MaxValue ? -1 : (int)tlut, out GxNativeTextureFormatDefinition format) || !format.SupportsAlpha(asset.AlphaPrecision)) throw new ArgumentException("Unsupported GXN1 format or alpha policy.");
            int bytes = format.GetPixelByteLength(asset.Width, asset.Height);
            format.GetVramByteLength(asset.Width, asset.Height, (int)count);
            if (Read(colors, 12) != asset.Width || Read(colors, 16) != asset.Height || Read(colors, 20) != (uint)bytes || Read(colors, 32) != count * 2 || Read(colors, 36) != (uint)asset.AlphaPrecision || colors.Length != HeaderLength + bytes ||
                (format.IsIndexed ? asset.PaletteColors == null || asset.PaletteColors.Length != count * 2 : asset.PaletteColors != null && asset.PaletteColors.Length != 0)) throw new ArgumentException("GXN1 dimensions, alpha metadata or native buffer lengths disagree.");
            GxNativeTextureLayout layout = new GxNativeTextureLayout(format, asset.Width, asset.Height, (int)count);
            try { ValidateIndices(asset.Colors, layout); }
            catch { NativeOwnership.Release(ref layout); throw; }
            return layout;
        }
        /// <summary>Allocates an owning asset before writing direct native pixels, releasing it if any packing operation fails.</summary>
        [NativeOwnedReturn]
        static TextureAsset EncodeDirect(TextureAsset source, byte[] rgba, GxNativeTextureFormatDefinition format, TextureAssetAlphaPrecision alpha) {
            GxNativeTextureLayout layout = new GxNativeTextureLayout(format, source.Width, source.Height, 0);
            try { return CreateDirectAsset(source, rgba, layout, alpha); }
            finally { NativeOwnership.Release(ref layout); }
        }
        /// <summary>Creates an independent native asset from borrowed decoded pixels and a validated borrowed layout.</summary>
        [NativeOwnedReturn]
        static TextureAsset CreateDirectAsset(TextureAsset source, byte[] rgba, GxNativeTextureLayout layout, TextureAssetAlphaPrecision alpha) {
            TextureAsset asset = CreateAsset(source, layout, alpha);
            try {
                if (layout.Format.HardwareFormat == 14) GxNativeTextureCmprCodec.Encode(rgba, layout, alpha, asset.Colors, HeaderLength);
                else for (int y = 0; y < layout.StorageHeight; y++) for (int x = 0; x < layout.StorageWidth; x++) {
                    int sourcePixel = (Math.Min(y, source.Height - 1) * source.Width + Math.Min(x, source.Width - 1)) * 4;
                    GxNativeTexturePixelCodec.EncodePixel(rgba, sourcePixel, layout, alpha, asset.Colors, HeaderLength, x, y);
                }
            } catch { NativeOwnership.DisposeAndRelease(ref asset); throw; }
            return asset;
        }
        /// <summary>Validates generic indexed storage and authored indices before allocating native output.</summary>
        [NativeOwnedReturn]
        static TextureAsset EncodeGenericIndexed(TextureAsset source, GxNativeTextureFormatDefinition format, TextureAssetAlphaPrecision alpha) {
            if (source.ColorFormat != TextureAssetColorFormat.Indexed4 && source.ColorFormat != TextureAssetColorFormat.Indexed8 || !TextureAssetPixelCodec.IsAlphaPrecisionSupported(source.ColorFormat, source.AlphaPrecision) || source.Colors == null || source.Colors.Length != TextureAssetPixelCodec.GetPixelByteLength(source.ColorFormat, source.Width, source.Height)) throw new ArgumentException("GX indexed storage requires a valid generic Indexed4/8 source.");
            int entries = ValidatePalette(source.PaletteColors, format);
            if (entries > (source.ColorFormat == TextureAssetColorFormat.Indexed4 ? 16 : 256)) throw new ArgumentException("Generic palette exceeds source index capacity.");
            for (int index = 0; index < source.Width * source.Height; index++) if (SourceIndex(source, index) >= entries) throw new ArgumentException("GX index exceeds the authored palette.");
            GxNativeTextureLayout layout = new GxNativeTextureLayout(format, source.Width, source.Height, (entries + 15) / 16 * 16);
            try { return CreateGenericIndexedAsset(source, layout, alpha, entries); }
            finally { NativeOwnership.Release(ref layout); }
        }
        /// <summary>Creates a generic-indexed native asset with clamped tiles and an aligned native TLUT.</summary>
        [NativeOwnedReturn]
        static TextureAsset CreateGenericIndexedAsset(TextureAsset source, GxNativeTextureLayout layout, TextureAssetAlphaPrecision alpha, int entries) {
            TextureAsset asset = CreateAsset(source, layout, alpha);
            try {
                PackPalette(source.PaletteColors, entries, layout, alpha, asset.PaletteColors);
                for (int y = 0; y < layout.StorageHeight; y++) for (int x = 0; x < layout.StorageWidth; x++) GxNativeTexturePixelCodec.WriteIndex(asset.Colors, HeaderLength, layout, x, y, SourceIndex(source, Math.Min(y, source.Height - 1) * source.Width + Math.Min(x, source.Width - 1)));
            } catch { NativeOwnership.DisposeAndRelease(ref asset); throw; }
            return asset;
        }
        /// <summary>Constructs a temporary validated layout for explicit full-width native index sources.</summary>
        [NativeOwnedReturn]
        static TextureAsset EncodeExplicitIndexed(TextureAsset source, ushort[] indices, byte[] palette, GxNativeTextureFormatDefinition format, TextureAssetAlphaPrecision alpha, int entries) {
            GxNativeTextureLayout layout = new GxNativeTextureLayout(format, source.Width, source.Height, (entries + 15) / 16 * 16);
            try { return CreateExplicitIndexedAsset(source, indices, palette, layout, alpha, entries); }
            finally { NativeOwnership.Release(ref layout); }
        }
        /// <summary>Creates a full-range CI14-capable asset without reducing ushort indices to bytes.</summary>
        [NativeOwnedReturn]
        static TextureAsset CreateExplicitIndexedAsset(TextureAsset source, ushort[] indices, byte[] palette, GxNativeTextureLayout layout, TextureAssetAlphaPrecision alpha, int entries) {
            TextureAsset asset = CreateAsset(source, layout, alpha);
            try {
                PackPalette(palette, entries, layout, alpha, asset.PaletteColors);
                for (int y = 0; y < layout.StorageHeight; y++) for (int x = 0; x < layout.StorageWidth; x++) GxNativeTexturePixelCodec.WriteIndex(asset.Colors, HeaderLength, layout, x, y, indices[Math.Min(y, source.Height - 1) * source.Width + Math.Min(x, source.Width - 1)]);
            } catch { NativeOwnership.DisposeAndRelease(ref asset); throw; }
            return asset;
        }
        /// <summary>Allocates all owned asset buffers and writes the canonical little-endian descriptor.</summary>
        [NativeOwnedReturn]
        static TextureAsset CreateAsset(TextureAsset source, GxNativeTextureLayout layout, TextureAssetAlphaPrecision alpha) {
            TextureAsset asset = new TextureAsset();
            try {
                asset.Id = source.Id; asset.RuntimeAssetId = source.RuntimeAssetId; asset.AuthoringAssetId = source.AuthoringAssetId;
                asset.FormerAuthoringAssetIds = CopyFormerIds(source.FormerAuthoringAssetIds); asset.IsEngineOwned = source.IsEngineOwned;
                asset.Width = source.Width; asset.Height = source.Height; asset.ColorFormat = TextureAssetColorFormat.GxNative; asset.AlphaPrecision = alpha;
                asset.Colors = new byte[HeaderLength + layout.TexelLength];
                if (layout.Format.IsIndexed) asset.PaletteColors = new byte[layout.PaletteLength];
                Write(asset.Colors, 0, Magic); Write(asset.Colors, 4, 1); Write(asset.Colors, 8, (uint)layout.Format.NativeFormat);
                Write(asset.Colors, 12, source.Width); Write(asset.Colors, 16, source.Height); Write(asset.Colors, 20, (uint)layout.TexelLength);
                Write(asset.Colors, 24, layout.Format.TlutFormat < 0 ? uint.MaxValue : (uint)layout.Format.TlutFormat);
                Write(asset.Colors, 28, (uint)layout.PaletteEntryCount); Write(asset.Colors, 32, (uint)layout.PaletteLength); Write(asset.Colors, 36, (uint)alpha);
            } catch { NativeOwnership.DisposeAndRelease(ref asset); throw; }
            return asset;
        }
        /// <summary>Decodes validated native storage into newly owned logical RGBA pixels.</summary>
        [NativeOwnedReturn]
        static byte[] DecodePixels(TextureAsset asset, GxNativeTextureLayout layout) {
            byte[] rgba = new byte[layout.RealWidth * layout.RealHeight * 4];
            if (layout.Format.HardwareFormat == 14) GxNativeTextureCmprCodec.Decode(asset.Colors, HeaderLength, layout, rgba);
            else for (int y = 0; y < layout.RealHeight; y++) for (int x = 0; x < layout.RealWidth; x++) {
                int target = (y * layout.RealWidth + x) * 4;
                if (layout.Format.IsIndexed) GxNativeTexturePixelCodec.DecodeColor(asset.PaletteColors, GxNativeTexturePixelCodec.ReadIndex(asset.Colors, HeaderLength, layout, x, y) * 2, layout.Format.TlutFormat, rgba, target);
                else GxNativeTexturePixelCodec.DecodePixel(asset.Colors, HeaderLength, layout, x, y, rgba, target);
            }
            return rgba;
        }
        /// <summary>Rejects reserved CI14 bits and out-of-range indices even in padded texels.</summary>
        static void ValidateIndices(byte[] colors, GxNativeTextureLayout layout) {
            if (!layout.Format.IsIndexed) return;
            for (int y = 0; y < layout.StorageHeight; y++) for (int x = 0; x < layout.StorageWidth; x++) if (GxNativeTexturePixelCodec.ReadIndex(colors, HeaderLength, layout, x, y) >= layout.PaletteEntryCount) throw new ArgumentException("GX native index exceeds its aligned TLUT or sets reserved CI14 bits.");
        }
        /// <summary>Encodes authored palette entries in logical order, leaving aligned padding initialized to zero.</summary>
        static void PackPalette(byte[] palette, int entries, GxNativeTextureLayout layout, TextureAssetAlphaPrecision alpha, byte[] output) {
            for (int index = 0; index < entries; index++) GxNativeTexturePixelCodec.EncodeColor(palette, index * 4, layout.Format.TlutFormat, alpha, output, index * 2);
        }
        /// <summary>Checks palette byte stride and real native entry capacity before allocation.</summary>
        static int ValidatePalette(byte[] palette, GxNativeTextureFormatDefinition format) {
            if (palette == null || palette.Length == 0 || palette.Length % 4 != 0 || palette.Length / 4 > format.PaletteEntryLimit) throw new ArgumentException("Invalid authored GX RGBA palette length.");
            return palette.Length / 4;
        }
        /// <summary>Reads generic continuous low-nibble-first indices without changing source storage.</summary>
        static int SourceIndex(TextureAsset source, int pixel) { return source.ColorFormat == TextureAssetColorFormat.Indexed8 ? source.Colors[pixel] : (source.Colors[pixel / 2] >> (pixel % 2 * 4)) & 15; }
        /// <summary>Finds a borrowed selection and validates its intrinsic alpha precision.</summary>
        [NativeBorrowedReturn]
        static GxNativeTextureFormatDefinition GetFormat(string id, TextureAssetAlphaPrecision alpha) {
            if (!GxNativeTextureFormatCatalog.TryGetFormat(id, out GxNativeTextureFormatDefinition format) || !format.SupportsAlpha(alpha)) throw new ArgumentException("Unknown GX format or incompatible alpha precision.");
            return format;
        }
        /// <summary>Copies former identities into independent owned storage.</summary>
        [NativeOwnedReturn]
        static string[] CopyFormerIds(string[] source) {
            string[] result = new string[source == null ? 0 : source.Length];
            for (int index = 0; index < result.Length; index++) result[index] = source[index];
            return result;
        }
        /// <summary>Reads a little-endian descriptor word without converting native GPU bytes.</summary>
        static uint Read(byte[] colors, int offset) { return (uint)colors[offset] | (uint)colors[offset + 1] << 8 | (uint)colors[offset + 2] << 16 | (uint)colors[offset + 3] << 24; }
        /// <summary>Writes a little-endian descriptor word without converting native GPU bytes.</summary>
        static void Write(byte[] colors, int offset, uint value) { colors[offset] = (byte)value; colors[offset + 1] = (byte)(value >> 8); colors[offset + 2] = (byte)(value >> 16); colors[offset + 3] = (byte)(value >> 24); }
    }
}
