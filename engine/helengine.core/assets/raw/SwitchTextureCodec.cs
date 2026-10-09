namespace helengine {
    /// <summary>Owns immutable SWT1 GPU storage and a separately serialized, decoded RGBA preview for portable consumers.</summary>
    public static class SwitchTextureCodec {
        /// <summary>Identifies the little-endian SWT1 native descriptor.</summary>
        public const uint Magic = 0x31545753;
        /// <summary>Gets the sixteen-word native descriptor length.</summary>
        public const int HeaderLength = 64;
        /// <summary>Defines identity RGBA component selection using deko3d swizzle values.</summary>
        public const uint IdentitySwizzle = 0x05040302;
        /// <summary>Copies native texels and their authoritative decoded view without retaining caller-owned storage.</summary>
        [NativeOwnedReturn]
        public static TextureAsset CreateNative(int width, int height, int code, bool blockLinear, int gobHeightLog2,
            TextureAssetAlphaPrecision alpha, uint swizzle, [NativeNoEscape] byte[] texels, [NativeNoEscape] byte[] preview) {
            SwitchTextureLayout layout = new SwitchTextureLayout(SwitchTextureFormatCatalog.GetFormat(code, blockLinear), width, height, gobHeightLog2);
            if (texels == null || texels.Length != layout.TexelBytes || preview == null || preview.Length != PreviewBytes(width, height)) throw new ArgumentException("Invalid Switch native storage or preview length.");
            TextureAsset asset = new TextureAsset { Width = (ushort)width, Height = (ushort)height, ColorFormat = TextureAssetColorFormat.SwitchNative,
                AlphaPrecision = alpha, Colors = new byte[HeaderLength + texels.Length], PaletteColors = new byte[preview.Length] };
            for (int index = 0; index < texels.Length; index++) asset.Colors[HeaderLength + index] = texels[index];
            for (int index = 0; index < preview.Length; index++) asset.PaletteColors[index] = preview[index];
            Write(asset.Colors, 0, Magic); Write(asset.Colors, 4, 1); Write(asset.Colors, 8, (uint)code);
            Write(asset.Colors, 12, (uint)width); Write(asset.Colors, 16, (uint)height); Write(asset.Colors, 20, blockLinear ? 1u : 0u);
            Write(asset.Colors, 24, (uint)layout.Pitch); Write(asset.Colors, 28, (uint)layout.BlockHeightLog2); Write(asset.Colors, 32, (uint)layout.TexelBytes);
            Write(asset.Colors, 36, (uint)preview.Length); Write(asset.Colors, 40, swizzle);
            Write(asset.Colors, 44, Checksum(asset.Colors, HeaderLength)); Write(asset.Colors, 48, Checksum(asset.PaletteColors, 0));
            SwitchTextureLayout validated = ReadLayout(asset);
            return asset;
        }
        /// <summary>Validates native dimensions, allocation geometry, component mapping and independently owned preview integrity.</summary>
        [NativeOwnedReturn]
        public static SwitchTextureLayout ReadLayout([NativeNoEscape] TextureAsset asset) {
            if (asset == null || asset.ColorFormat != TextureAssetColorFormat.SwitchNative || asset.Colors == null || asset.Colors.Length < HeaderLength) throw new ArgumentException("A Switch texture requires an SWT1 descriptor.");
            byte[] data = asset.Colors;
            uint code = Read(data, 8), tiled = Read(data, 20), exponent = Read(data, 28), swizzle = Read(data, 40);
            if (Read(data, 0) != Magic || Read(data, 4) != 1 || code < 1 || code > 129 || tiled > 1 || exponent > 5
                || Read(data, 52) != 0 || Read(data, 56) != 0 || Read(data, 60) != 0) throw new ArgumentException("Invalid Switch native descriptor.");
            for (int channel = 0; channel < 4; channel++) if ((swizzle >> (channel * 8) & 255) > 5) throw new ArgumentException("Invalid Switch component selector.");
            SwitchTextureLayout layout = new SwitchTextureLayout(SwitchTextureFormatCatalog.GetFormat((int)code, tiled != 0), asset.Width, asset.Height, (int)exponent);
            if (Read(data, 12) != asset.Width || Read(data, 16) != asset.Height || Read(data, 24) != (uint)layout.Pitch
                || Read(data, 32) != (uint)layout.TexelBytes || data.Length != HeaderLength + layout.TexelBytes
                || asset.PaletteColors == null || asset.PaletteColors.Length != PreviewBytes(asset.Width, asset.Height)
                || Read(data, 36) != (uint)asset.PaletteColors.Length || Read(data, 44) != Checksum(data, HeaderLength)
                || Read(data, 48) != Checksum(asset.PaletteColors, 0)
                || (asset.AlphaPrecision != TextureAssetAlphaPrecision.Opaque && asset.AlphaPrecision != TextureAssetAlphaPrecision.Binary
                    && asset.AlphaPrecision != TextureAssetAlphaPrecision.A2 && asset.AlphaPrecision != TextureAssetAlphaPrecision.A4 && asset.AlphaPrecision != TextureAssetAlphaPrecision.A8)) throw new ArgumentException("Malformed Switch storage, alpha or decoded preview.");
            return layout;
        }
        /// <summary>Returns an independent RGBA view decoded by the build-time format codec, keeping compression libraries outside native core generation.</summary>
        [NativeOwnedReturn]
        public static byte[] Decode([NativeNoEscape] TextureAsset asset) {
            SwitchTextureLayout layout = ReadLayout(asset);
            byte[] result = new byte[asset.PaletteColors.Length];
            for (int index = 0; index < result.Length; index++) result[index] = asset.PaletteColors[index];
            return result;
        }
        /// <summary>Bounds decoded CPU caches separately from native GPU storage.</summary>
        public static int PreviewBytes(int width, int height) {
            long bytes = (long)width * height * 4;
            if (width < 1 || height < 1 || width > 16384 || height > 16384 || bytes > 64 * 1024 * 1024) throw new ArgumentException("Switch decoded previews must fit the 64 MiB CPU cache budget.");
            return (int)bytes;
        }
        /// <summary>Calculates FNV-1a integrity over one owned immutable native or preview buffer.</summary>
        public static uint Checksum([NativeNoEscape] byte[] data, int start) {
            uint hash = 2166136261;
            for (int index = start; index < data.Length; index++) hash = unchecked((hash ^ data[index]) * 16777619);
            return hash;
        }
        /// <summary>Reads a bounded little-endian native descriptor word.</summary>
        public static uint Read(byte[] data, int offset) {
            if (data == null || offset < 0 || data.Length - offset < 4) throw new ArgumentException("Truncated Switch native word.");
            return (uint)data[offset] | (uint)data[offset + 1] << 8 | (uint)data[offset + 2] << 16 | (uint)data[offset + 3] << 24;
        }
        /// <summary>Writes a little-endian native descriptor word.</summary>
        static void Write(byte[] data, int offset, uint value) { for (int index = 0; index < 4; index++) data[offset + index] = (byte)(value >> (index * 8)); }
    }
}
