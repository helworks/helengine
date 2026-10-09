namespace helengine {
    /// <summary>Encodes and imports canonical WGT1 records preserving actual GX2 format bytes and sampler controls.</summary>
    public static class WiiUNativeTextureCodec {
        /// <summary>Identifies the ASCII WGT1 bytes independently of the outer asset's endian order.</summary>
        public const uint Magic = 0x31544757;
        /// <summary>Gets the sixteen-word version-one native descriptor size.</summary>
        public const int HeaderLength = 64;
        /// <summary>Encodes representable RGBA source images without replacing native packed or compressed storage.</summary>
        [NativeOwnedReturn]
        public static TextureAsset Encode(TextureAsset source, string id, TextureAssetAlphaPrecision alpha) {
            if (source == null) throw new ArgumentNullException(nameof(source)); WiiUNativeTextureFormatDefinition format = GetFormat(id, alpha);
            if (!format.SupportsCooking) throw new NotSupportedException(format.UnsupportedReason);
            byte[] rgba = TextureAssetPixelCodec.DecodeToRgba32(source);
            try { return EncodeRgba(source, rgba, format, alpha); }
            finally { NativeOwnership.Release(ref rgba); }
        }
        /// <summary>Imports exact compact native bytes into newly owned storage, including integer, depth and NV12 shader data.</summary>
        [NativeOwnedReturn]
        public static TextureAsset WrapRaw(TextureAsset identity, string id, TextureAssetAlphaPrecision alpha, byte[] texels) {
            if (identity == null) throw new ArgumentNullException(nameof(identity)); WiiUNativeTextureFormatDefinition format = GetFormat(id, alpha);
            WiiUNativeTextureLayout layout = new WiiUNativeTextureLayout(format, identity.Width, identity.Height);
            try { return ImportRaw(identity, layout, alpha, texels); }
            finally { NativeOwnership.Release(ref layout); }
        }
        /// <summary>Returns newly owned sampled RGBA pixels or explicitly rejects incompatible shader-only interpretations.</summary>
        [NativeOwnedReturn]
        public static byte[] Decode(TextureAsset asset) {
            WiiUNativeTextureLayout layout = ReadLayout(asset);
            try { return DecodePixels(asset, layout); }
            finally { NativeOwnership.Release(ref layout); }
        }
        /// <summary>Checks exact format words, layout, alpha metadata, lengths and reserved fields before returning an owned layout.</summary>
        [NativeOwnedReturn]
        public static WiiUNativeTextureLayout ReadLayout(TextureAsset asset) {
            if (asset == null || asset.ColorFormat != TextureAssetColorFormat.WiiUNative || asset.Colors == null || asset.Colors.Length < HeaderLength) throw new ArgumentException("Texture is not a complete WGT1 record.");
            byte[] colors = asset.Colors;
            if (VitaNativeTextureCodec.ReadWord(colors, 0) != Magic || VitaNativeTextureCodec.ReadWord(colors, 4) != 1 || VitaNativeTextureCodec.ReadWord(colors, 52) != 0 || VitaNativeTextureCodec.ReadWord(colors, 60) != 0 || asset.PaletteColors != null && asset.PaletteColors.Length != 0) throw new ArgumentException("Invalid WGT1 magic, version, reserved words or palette.");
            if (!WiiUNativeTextureFormatCatalog.TryGetHardwareFormat(VitaNativeTextureCodec.ReadWord(colors, 8), out WiiUNativeTextureFormatDefinition format) || !format.SupportsAlpha(asset.AlphaPrecision)) throw new ArgumentException("Unknown GX2 format or incompatible alpha metadata.");
            WiiUNativeTextureLayout layout = new WiiUNativeTextureLayout(format, asset.Width, asset.Height);
            try {
                if (VitaNativeTextureCodec.ReadWord(colors, 12) != layout.TileMode || VitaNativeTextureCodec.ReadWord(colors, 16) != (uint)layout.RealWidth || VitaNativeTextureCodec.ReadWord(colors, 20) != (uint)layout.RealHeight || VitaNativeTextureCodec.ReadWord(colors, 24) != (uint)layout.StorageWidth || VitaNativeTextureCodec.ReadWord(colors, 28) != (uint)layout.StorageHeight || VitaNativeTextureCodec.ReadWord(colors, 32) != (uint)layout.PitchBytes || VitaNativeTextureCodec.ReadWord(colors, 36) != (uint)layout.TexelLength || VitaNativeTextureCodec.ReadWord(colors, 40) != format.ComponentMap || VitaNativeTextureCodec.ReadWord(colors, 44) != layout.EndianSwap || VitaNativeTextureCodec.ReadWord(colors, 48) != (uint)asset.AlphaPrecision || VitaNativeTextureCodec.ReadWord(colors, 56) != (uint)layout.UvPlaneOffset || colors.Length != HeaderLength + layout.TexelLength) throw new ArgumentException("WGT1 native storage or sampler metadata is not canonical.");
            } catch { NativeOwnership.Release(ref layout); throw; }
            return layout;
        }
        /// <summary>Creates a layout before packing borrowed source pixels into an owning asset.</summary>
        [NativeOwnedReturn]
        static TextureAsset EncodeRgba(TextureAsset source, byte[] rgba, WiiUNativeTextureFormatDefinition format, TextureAssetAlphaPrecision alpha) {
            WiiUNativeTextureLayout layout = new WiiUNativeTextureLayout(format, source.Width, source.Height);
            try { return CreateRgbaAsset(source, rgba, layout, alpha); }
            finally { NativeOwnership.Release(ref layout); }
        }
        /// <summary>Fills newly owned native storage and releases partial output if packing fails.</summary>
        [NativeOwnedReturn]
        static TextureAsset CreateRgbaAsset(TextureAsset source, byte[] rgba, [NativeNoEscape] WiiUNativeTextureLayout layout, TextureAssetAlphaPrecision alpha) {
            TextureAsset asset = CreateAsset(source, layout, alpha);
            try { WiiUNativeTexturePixelCodec.Encode(rgba, layout, alpha, asset.Colors, HeaderLength); }
            catch { NativeOwnership.DisposeAndRelease(ref asset); throw; }
            return asset;
        }
        /// <summary>Copies borrowed canonical native bytes only after complete length validation.</summary>
        [NativeOwnedReturn]
        static TextureAsset ImportRaw(TextureAsset source, [NativeNoEscape] WiiUNativeTextureLayout layout, TextureAssetAlphaPrecision alpha, byte[] texels) {
            if (texels == null || texels.Length != layout.TexelLength) throw new ArgumentException("Raw GX2 byte length is not canonical.");
            TextureAsset asset = CreateAsset(source, layout, alpha); for (int index = 0; index < texels.Length; index++) asset.Colors[HeaderLength + index] = texels[index]; return asset;
        }
        /// <summary>Allocates independent identity and native buffers and writes the little-endian WGT1 descriptor.</summary>
        [NativeOwnedReturn]
        static TextureAsset CreateAsset(TextureAsset source, [NativeNoEscape] WiiUNativeTextureLayout layout, TextureAssetAlphaPrecision alpha) {
            TextureAsset asset = new TextureAsset();
            try {
                asset.Id = source.Id; asset.RuntimeAssetId = source.RuntimeAssetId; asset.AuthoringAssetId = source.AuthoringAssetId; asset.IsEngineOwned = source.IsEngineOwned; asset.FormerAuthoringAssetIds = CopyFormerIds(source.FormerAuthoringAssetIds);
                asset.Width = source.Width; asset.Height = source.Height; asset.ColorFormat = TextureAssetColorFormat.WiiUNative; asset.AlphaPrecision = alpha; asset.Colors = new byte[HeaderLength + layout.TexelLength];
                VitaNativeTextureCodec.WriteWord(asset.Colors, 0, Magic); VitaNativeTextureCodec.WriteWord(asset.Colors, 4, 1); VitaNativeTextureCodec.WriteWord(asset.Colors, 8, layout.Format.HardwareFormat); VitaNativeTextureCodec.WriteWord(asset.Colors, 12, layout.TileMode);
                VitaNativeTextureCodec.WriteWord(asset.Colors, 16, (uint)layout.RealWidth); VitaNativeTextureCodec.WriteWord(asset.Colors, 20, (uint)layout.RealHeight); VitaNativeTextureCodec.WriteWord(asset.Colors, 24, (uint)layout.StorageWidth); VitaNativeTextureCodec.WriteWord(asset.Colors, 28, (uint)layout.StorageHeight);
                VitaNativeTextureCodec.WriteWord(asset.Colors, 32, (uint)layout.PitchBytes); VitaNativeTextureCodec.WriteWord(asset.Colors, 36, (uint)layout.TexelLength); VitaNativeTextureCodec.WriteWord(asset.Colors, 40, layout.Format.ComponentMap); VitaNativeTextureCodec.WriteWord(asset.Colors, 44, layout.EndianSwap); VitaNativeTextureCodec.WriteWord(asset.Colors, 48, (uint)alpha); VitaNativeTextureCodec.WriteWord(asset.Colors, 56, (uint)layout.UvPlaneOffset);
            } catch { NativeOwnership.DisposeAndRelease(ref asset); throw; }
            return asset;
        }
        /// <summary>Reconstructs sampled pixels and releases partially decoded output on invalid compressed input.</summary>
        [NativeOwnedReturn]
        static byte[] DecodePixels(TextureAsset asset, [NativeNoEscape] WiiUNativeTextureLayout layout) {
            if (!layout.Format.SupportsPreview) throw new NotSupportedException(layout.Format.UnsupportedReason); byte[] rgba = new byte[layout.RealWidth * layout.RealHeight * 4];
            try { WiiUNativeTexturePixelCodec.Decode(asset.Colors, HeaderLength, layout, rgba); }
            catch { NativeOwnership.Release(ref rgba); throw; }
            return rgba;
        }
        /// <summary>Finds a borrowed declared format and validates its intrinsic alpha policies.</summary>
        [NativeBorrowedReturn]
        static WiiUNativeTextureFormatDefinition GetFormat(string id, TextureAssetAlphaPrecision alpha) { if (!WiiUNativeTextureFormatCatalog.TryGetFormat(id, out WiiUNativeTextureFormatDefinition format) || !format.SupportsAlpha(alpha)) throw new ArgumentException("Unknown GX2 format or incompatible alpha precision."); return format; }
        /// <summary>Copies former authored identities into newly owned storage.</summary>
        [NativeOwnedReturn]
        static string[] CopyFormerIds(string[] source) { string[] result = new string[source == null ? 0 : source.Length]; for (int index = 0; index < result.Length; index++) result[index] = source[index]; return result; }
    }
}
