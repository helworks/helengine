namespace helengine {
    /// <summary>Encodes and imports canonical VGT1 records without substituting RGBA storage for native GPU formats.</summary>
    public static class VitaNativeTextureCodec {
        /// <summary>Identifies the ASCII bytes VGT1 independently of outer container endian order.</summary>
        public const uint Magic = 0x31544756;
        /// <summary>Gets the sixteen-word version-one descriptor length.</summary>
        public const int HeaderLength = 64;
        /// <summary>Encodes image pixels using the selection's canonical native storage type.</summary>
        [NativeOwnedReturn]
        public static TextureAsset Encode(TextureAsset source, string id, TextureAssetAlphaPrecision alpha) {
            VitaNativeTextureFormatDefinition format = GetFormat(id, alpha);
            return Encode(source, id, alpha, format.DefaultLayoutType);
        }
        /// <summary>Encodes image pixels into an explicitly selected scanline, Morton or tiled native representation.</summary>
        [NativeOwnedReturn]
        public static TextureAsset Encode(TextureAsset source, string id, TextureAssetAlphaPrecision alpha, VitaNativeTextureLayoutType type) {
            if (source == null) throw new ArgumentNullException(nameof(source));
            VitaNativeTextureFormatDefinition format = GetFormat(id, alpha);
            if (!format.SupportsCooking) throw new NotSupportedException(format.UnsupportedReason);
            if (format.IsIndexed) return EncodeIndexed(source, format, alpha, type);
            byte[] rgba = TextureAssetPixelCodec.DecodeToRgba32(source);
            try { return EncodeRgba(source, rgba, format, alpha, type); }
            finally { NativeOwnership.Release(ref rgba); }
        }
        /// <summary>Imports exact native texels and palette bytes into newly owned storage, including specialized shader-only formats.</summary>
        [NativeOwnedReturn]
        public static TextureAsset WrapRaw(TextureAsset identity, string id, TextureAssetAlphaPrecision alpha, VitaNativeTextureLayoutType type, byte[] texels, byte[] palette) {
            if (identity == null) throw new ArgumentNullException(nameof(identity));
            VitaNativeTextureFormatDefinition format = GetFormat(id, alpha);
            VitaNativeTextureLayout layout = new VitaNativeTextureLayout(format, identity.Width, identity.Height, type);
            try { return ImportRaw(identity, layout, alpha, texels, palette); }
            finally { NativeOwnership.Release(ref layout); }
        }
        /// <summary>Returns a new native sampled RGBA preview or explicitly rejects specialized shader-data interpretations.</summary>
        [NativeOwnedReturn]
        public static byte[] Decode(TextureAsset asset) {
            VitaNativeTextureLayout layout = ReadLayout(asset);
            try { return DecodePixels(asset, layout); }
            finally { NativeOwnership.Release(ref layout); }
        }
        /// <summary>Validates exact format, layout, alpha, lengths and every stored palette index before returning a new layout.</summary>
        [NativeOwnedReturn]
        public static VitaNativeTextureLayout ReadLayout(TextureAsset asset) {
            if (asset == null || asset.ColorFormat != TextureAssetColorFormat.VitaNative || asset.Colors == null || asset.Colors.Length < HeaderLength) throw new ArgumentException("Texture is not a complete VGT1 record.");
            byte[] colors = asset.Colors;
            if (ReadWord(colors, 0) != Magic || ReadWord(colors, 4) != 1 || ReadWord(colors, 52) != 0 || ReadWord(colors, 56) != 0 || ReadWord(colors, 60) != 0) throw new ArgumentException("Invalid VGT1 magic, version or reserved words.");
            if (!VitaNativeTextureFormatCatalog.TryGetHardwareFormat(ReadWord(colors, 8), out VitaNativeTextureFormatDefinition format) || !format.SupportsAlpha(asset.AlphaPrecision)) throw new ArgumentException("Unknown GXM format word or invalid intrinsic alpha policy.");
            VitaNativeTextureLayout layout = new VitaNativeTextureLayout(format, asset.Width, asset.Height, (VitaNativeTextureLayoutType)ReadWord(colors, 12));
            try {
                if (ReadWord(colors, 16) != (uint)layout.RealWidth || ReadWord(colors, 20) != (uint)layout.RealHeight || ReadWord(colors, 24) != (uint)layout.StorageWidth || ReadWord(colors, 28) != (uint)layout.StorageHeight || ReadWord(colors, 32) != (uint)layout.PitchBytes || ReadWord(colors, 36) != (uint)layout.TexelLength || ReadWord(colors, 40) != (uint)layout.PaletteEntryCount || ReadWord(colors, 44) != (uint)layout.PaletteLength || ReadWord(colors, 48) != (uint)asset.AlphaPrecision || colors.Length != HeaderLength + layout.TexelLength) throw new ArgumentException("VGT1 layout or asset metadata is not canonical.");
                ValidatePalette(asset.PaletteColors, layout); ValidateNativeIndices(colors, HeaderLength, layout);
            } catch { NativeOwnership.Release(ref layout); throw; }
            return layout;
        }
        /// <summary>Creates a layout before packing borrowed decoded pixels.</summary>
        [NativeOwnedReturn]
        static TextureAsset EncodeRgba(TextureAsset source, byte[] rgba, VitaNativeTextureFormatDefinition format, TextureAssetAlphaPrecision alpha, VitaNativeTextureLayoutType type) {
            VitaNativeTextureLayout layout = new VitaNativeTextureLayout(format, source.Width, source.Height, type);
            try { return CreateRgbaAsset(source, rgba, layout, alpha); }
            finally { NativeOwnership.Release(ref layout); }
        }
        /// <summary>Allocates one owning asset and fills native component or compressed storage.</summary>
        [NativeOwnedReturn]
        static TextureAsset CreateRgbaAsset(TextureAsset source, byte[] rgba, [NativeNoEscape] VitaNativeTextureLayout layout, TextureAssetAlphaPrecision alpha) {
            TextureAsset asset = CreateAsset(source, layout, alpha);
            try { VitaNativeTexturePixelCodec.Encode(rgba, layout, alpha, asset.Colors, HeaderLength); }
            catch { NativeOwnership.DisposeAndRelease(ref asset); throw; }
            return asset;
        }
        /// <summary>Validates generic index bytes and real authored palette entries before allocating native indices.</summary>
        [NativeOwnedReturn]
        static TextureAsset EncodeIndexed(TextureAsset source, VitaNativeTextureFormatDefinition format, TextureAssetAlphaPrecision alpha, VitaNativeTextureLayoutType type) {
            if (source.ColorFormat != TextureAssetColorFormat.Indexed4 && source.ColorFormat != TextureAssetColorFormat.Indexed8 || !TextureAssetPixelCodec.IsAlphaPrecisionSupported(source.ColorFormat, source.AlphaPrecision) || source.Colors == null || source.Colors.Length != TextureAssetPixelCodec.GetPixelByteLength(source.ColorFormat, source.Width, source.Height)) throw new ArgumentException("Native GXM indices require a valid already-quantized generic source.");
            if (source.PaletteColors == null || source.PaletteColors.Length == 0 || source.PaletteColors.Length % 4 != 0 || source.PaletteColors.Length / 4 > format.PaletteEntryCount || source.PaletteColors.Length / 4 > (source.ColorFormat == TextureAssetColorFormat.Indexed4 ? 16 : 256)) throw new ArgumentException("Authored GXM palette exceeds native or generic capacity.");
            for (int pixel = 0; pixel < source.Width * source.Height; pixel++) if (SourceIndex(source, pixel) >= source.PaletteColors.Length / 4) throw new ArgumentException("Authored texture index exceeds its palette.");
            VitaNativeTextureLayout layout = new VitaNativeTextureLayout(format, source.Width, source.Height, type);
            try { return CreateIndexedAsset(source, layout, alpha); }
            finally { NativeOwnership.Release(ref layout); }
        }
        /// <summary>Packs native low-first indices and the exact format's swizzled ABGR32 palette, clamping complete edge storage.</summary>
        [NativeOwnedReturn]
        static TextureAsset CreateIndexedAsset(TextureAsset source, [NativeNoEscape] VitaNativeTextureLayout layout, TextureAssetAlphaPrecision alpha) {
            TextureAsset asset = CreateAsset(source, layout, alpha);
            try {
                for (int index = 0; index < source.PaletteColors.Length / 4; index++) VitaNativeTexturePixelCodec.EncodePaletteColor(source.PaletteColors, index * 4, layout.Format, alpha, asset.PaletteColors, index * 4);
                for (int y = 0; y < layout.StorageHeight; y++) for (int x = 0; x < layout.StorageWidth; x++) {
                    int value = SourceIndex(source, Math.Min(y, source.Height - 1) * source.Width + Math.Min(x, source.Width - 1));
                    int pixel = layout.GetPixelIndex(x, y);
                    if (layout.Format.IndexBitDepth == 4) asset.Colors[HeaderLength + pixel / 2] |= (byte)(value << (pixel % 2 * 4));
                    else asset.Colors[HeaderLength + pixel] = (byte)value;
                }
            } catch { NativeOwnership.DisposeAndRelease(ref asset); throw; }
            return asset;
        }
        /// <summary>Copies borrowed native bytes into an owning asset only after exact length validation.</summary>
        [NativeOwnedReturn]
        static TextureAsset ImportRaw(TextureAsset identity, [NativeNoEscape] VitaNativeTextureLayout layout, TextureAssetAlphaPrecision alpha, byte[] texels, byte[] palette) {
            if (texels == null || texels.Length != layout.TexelLength) throw new ArgumentException("Raw native GXM texel length is not canonical.");
            ValidatePalette(palette, layout); ValidateNativeIndices(texels, 0, layout);
            TextureAsset asset = CreateAsset(identity, layout, alpha);
            for (int index = 0; index < texels.Length; index++) asset.Colors[HeaderLength + index] = texels[index];
            for (int index = 0; index < layout.PaletteLength; index++) asset.PaletteColors[index] = palette[index];
            return asset;
        }
        /// <summary>Allocates independent owned asset buffers and writes a canonical little-endian VGT1 descriptor.</summary>
        [NativeOwnedReturn]
        static TextureAsset CreateAsset(TextureAsset source, [NativeNoEscape] VitaNativeTextureLayout layout, TextureAssetAlphaPrecision alpha) {
            TextureAsset asset = new TextureAsset();
            try {
                asset.Id = source.Id; asset.RuntimeAssetId = source.RuntimeAssetId; asset.AuthoringAssetId = source.AuthoringAssetId; asset.IsEngineOwned = source.IsEngineOwned;
                asset.FormerAuthoringAssetIds = CopyFormerIds(source.FormerAuthoringAssetIds);
                asset.Width = source.Width; asset.Height = source.Height; asset.ColorFormat = TextureAssetColorFormat.VitaNative; asset.AlphaPrecision = alpha;
                asset.Colors = new byte[HeaderLength + layout.TexelLength]; if (layout.Format.IsIndexed) asset.PaletteColors = new byte[layout.PaletteLength];
                WriteWord(asset.Colors, 0, Magic); WriteWord(asset.Colors, 4, 1); WriteWord(asset.Colors, 8, layout.Format.HardwareFormat); WriteWord(asset.Colors, 12, (uint)layout.LayoutType);
                WriteWord(asset.Colors, 16, (uint)layout.RealWidth); WriteWord(asset.Colors, 20, (uint)layout.RealHeight); WriteWord(asset.Colors, 24, (uint)layout.StorageWidth); WriteWord(asset.Colors, 28, (uint)layout.StorageHeight);
                WriteWord(asset.Colors, 32, (uint)layout.PitchBytes); WriteWord(asset.Colors, 36, (uint)layout.TexelLength); WriteWord(asset.Colors, 40, (uint)layout.PaletteEntryCount); WriteWord(asset.Colors, 44, (uint)layout.PaletteLength); WriteWord(asset.Colors, 48, (uint)alpha);
            } catch { NativeOwnership.DisposeAndRelease(ref asset); throw; }
            return asset;
        }
        /// <summary>Decodes validated native storage and explicitly rejects shader-only preview interpretations.</summary>
        [NativeOwnedReturn]
        static byte[] DecodePixels(TextureAsset asset, [NativeNoEscape] VitaNativeTextureLayout layout) {
            if (!layout.Format.SupportsPreview) throw new NotSupportedException(layout.Format.UnsupportedReason);
            byte[] rgba = new byte[layout.RealWidth * layout.RealHeight * 4];
            try {
                if (layout.Format.IsIndexed) {
                    for (int y = 0; y < layout.RealHeight; y++) for (int x = 0; x < layout.RealWidth; x++) VitaNativeTexturePixelCodec.DecodePaletteColor(asset.PaletteColors, NativeIndex(asset.Colors, HeaderLength, layout, x, y) * 4, layout.Format, rgba, (y * layout.RealWidth + x) * 4);
                } else VitaNativeTexturePixelCodec.Decode(asset.Colors, HeaderLength, layout, rgba);
            } catch { NativeOwnership.Release(ref rgba); throw; }
            return rgba;
        }
        /// <summary>Checks native palette length without admitting palettes on direct textures.</summary>
        static void ValidatePalette(byte[] palette, [NativeNoEscape] VitaNativeTextureLayout layout) {
            if (layout.Format.IsIndexed ? palette == null || palette.Length != layout.PaletteLength : palette != null && palette.Length != 0) throw new ArgumentException("Native GXM palette length is not canonical.");
        }
        /// <summary>Validates every native stored index, including invisible allocation padding.</summary>
        static void ValidateNativeIndices(byte[] colors, int offset, [NativeNoEscape] VitaNativeTextureLayout layout) {
            if (!layout.Format.IsIndexed) return;
            for (int y = 0; y < layout.StorageHeight; y++) for (int x = 0; x < layout.StorageWidth; x++) if (NativeIndex(colors, offset, layout, x, y) >= layout.PaletteEntryCount) throw new ArgumentException("Native GXM index exceeds its palette.");
        }
        /// <summary>Reads a native low-first P4 nibble or P8 byte using the declared storage layout.</summary>
        static int NativeIndex(byte[] colors, int offset, [NativeNoEscape] VitaNativeTextureLayout layout, int x, int y) {
            int pixel = layout.GetPixelIndex(x, y); return layout.Format.IndexBitDepth == 4 ? (colors[offset + pixel / 2] >> (pixel % 2 * 4)) & 15 : colors[offset + pixel];
        }
        /// <summary>Reads continuous low-first generic index storage.</summary>
        static int SourceIndex(TextureAsset source, int pixel) { return source.ColorFormat == TextureAssetColorFormat.Indexed4 ? (source.Colors[pixel / 2] >> (pixel % 2 * 4)) & 15 : source.Colors[pixel]; }
        /// <summary>Finds a borrowed catalog entry and checks exact intrinsic alpha metadata.</summary>
        [NativeBorrowedReturn]
        static VitaNativeTextureFormatDefinition GetFormat(string id, TextureAssetAlphaPrecision alpha) {
            if (!VitaNativeTextureFormatCatalog.TryGetFormat(id, out VitaNativeTextureFormatDefinition format) || !format.SupportsAlpha(alpha)) throw new ArgumentException("Unknown Vita GXM format or incompatible alpha precision.");
            return format;
        }
        /// <summary>Copies former authored identities into newly owned storage.</summary>
        [NativeOwnedReturn]
        static string[] CopyFormerIds(string[] source) {
            string[] result = new string[source == null ? 0 : source.Length]; for (int index = 0; index < result.Length; index++) result[index] = source[index]; return result;
        }
        /// <summary>Reads native little-endian descriptor or component words without outer-container conversion.</summary>
        public static uint ReadWord(byte[] bytes, int offset) { return (uint)bytes[offset] | (uint)bytes[offset + 1] << 8 | (uint)bytes[offset + 2] << 16 | (uint)bytes[offset + 3] << 24; }
        /// <summary>Writes native little-endian descriptor or component words without outer-container conversion.</summary>
        public static void WriteWord(byte[] bytes, int offset, uint value) { bytes[offset] = (byte)value; bytes[offset + 1] = (byte)(value >> 8); bytes[offset + 2] = (byte)(value >> 16); bytes[offset + 3] = (byte)(value >> 24); }
    }
}
