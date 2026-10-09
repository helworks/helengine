namespace helengine.editor {
    /// <summary>Imports bounded NX BNTX containers with explicit texture, array-layer and mip selection while preserving native texels.</summary>
    public sealed class SwitchBntxTextureImporter : ITextureImporter {
        /// <summary>Bounds source archives independently of per-texture GPU and CPU allocation budgets.</summary>
        const int MaximumContainerBytes = 256 * 1024 * 1024;
        /// <summary>Imports the first texture's first layer and mip as a decoded editor image.</summary>
        public TextureAsset ImportTexture(Stream stream) => ImportTexture(stream, 0, 0, 0);
        /// <summary>Imports a selected subresource as independently owned RGBA pixels.</summary>
        public TextureAsset ImportTexture(Stream stream, int textureIndex, int layerIndex, int mipIndex) {
            using TextureAsset native = ImportNativeTexture(stream, textureIndex, layerIndex, mipIndex);
            return new TextureAsset { Id = native.Id, Width = native.Width, Height = native.Height, ColorFormat = TextureAssetColorFormat.Rgba32,
                AlphaPrecision = TextureAssetAlphaPrecision.A8, Colors = SwitchTextureCodec.Decode(native) };
        }
        /// <summary>Preserves selected native texels, GOB geometry and component mapping and caches their actual decoded CPU view.</summary>
        public TextureAsset ImportNativeTexture(Stream stream, int textureIndex = 0, int layerIndex = 0, int mipIndex = 0) {
            if (stream == null || !stream.CanRead) throw new ArgumentException("A readable BNTX stream is required.");
            if (textureIndex < 0 || layerIndex < 0 || mipIndex < 0) throw new ArgumentOutOfRangeException(nameof(textureIndex));
            using MemoryStream buffer = new MemoryStream(); byte[] chunk = new byte[8192]; int count;
            while ((count = stream.Read(chunk, 0, chunk.Length)) != 0) {
                if (buffer.Length + count > MaximumContainerBytes) throw new InvalidDataException("BNTX archive exceeds its import budget.");
                buffer.Write(chunk, 0, count);
            }
            byte[] data = buffer.ToArray();
            if (data.Length < 88 || SwitchTextureCodec.Read(data, 0) != 0x58544e42 || SwitchTextureCodec.Read(data, 4) != 0
                || Word(data, 12) != 0xfeff || data[15] != 8 || SwitchTextureCodec.Read(data, 28) != (uint)data.Length
                || SwitchTextureCodec.Read(data, 32) != 0x2020584e) throw new InvalidDataException("Invalid little-endian NX BNTX header.");
            uint textureCount = SwitchTextureCodec.Read(data, 36);
            if (textureCount == 0 || textureCount > 4096 || (uint)textureIndex >= textureCount) throw new InvalidDataException("Invalid BNTX texture selection.");
            int pointers = Pointer(data, 40, (int)textureCount * 8);
            int info = Pointer(data, pointers + textureIndex * 8, 160);
            if (SwitchTextureCodec.Read(data, info) != 0x49545242) throw new InvalidDataException("Missing BNTX BRTI descriptor.");
            int tileMode = Word(data, info + 18), mips = Word(data, info + 22), samples = Word(data, info + 24);
            uint nativeFormat = SwitchTextureCodec.Read(data, info + 28);
            uint widthWord = SwitchTextureCodec.Read(data, info + 36), heightWord = SwitchTextureCodec.Read(data, info + 40), depth = SwitchTextureCodec.Read(data, info + 44);
            uint layers = SwitchTextureCodec.Read(data, info + 48), layoutWord = SwitchTextureCodec.Read(data, info + 52), imageBytes = SwitchTextureCodec.Read(data, info + 80);
            uint fileSwizzle = SwitchTextureCodec.Read(data, info + 88);
            if (data[info + 17] != 2 || (data[info + 16] & 6) != 0 || samples > 1 || depth != 1 || layers < 1 || layers > 2048
                || widthWord < 1 || widthWord > 16384 || heightWord < 1 || heightWord > 16384 || tileMode > 1 || (layoutWord & 7) > 5
                || mips < 1 || mips > 15 || mipIndex >= mips || (uint)layerIndex >= layers || imageBytes == 0 || imageBytes > MaximumContainerBytes
                || imageBytes % layers != 0) throw new InvalidDataException("Unsupported or malformed BNTX image dimensions, layout or subresources.");
            int width = Math.Max(1, (int)widthWord >> mipIndex), height = Math.Max(1, (int)heightWord >> mipIndex);
            SwitchTextureCodec.PreviewBytes(width, height);
            SwitchTextureFormat format = Format(nativeFormat, tileMode == 0);
            int exponent = tileMode == 0 ? (int)(layoutWord & 7) : 0;
            int mipRows = (height + format.BlockHeight - 1) / format.BlockHeight;
            // Later mip levels reduce the physical GOB height as the block row count shrinks.
            if (mipIndex > 0) while (exponent > 0 && mipRows <= (8 << (exponent - 1))) exponent--;
            SwitchTextureLayout layout = new SwitchTextureLayout(format, width, height, exponent);
            int mipPointers = Pointer(data, info + 112, mips * 8);
            int first = Pointer(data, mipPointers, (int)imageBytes);
            int selected = Pointer(data, mipPointers + mipIndex * 8, 0);
            int stride = (int)(imageBytes / layers), relative = selected - first;
            if (relative < 0 || relative > stride || layout.TexelBytes > stride - relative) throw new InvalidDataException("BNTX mip storage exceeds its array layer.");
            for (int index = 0; index < mips; index++) {
                int offset = Pointer(data, mipPointers + index * 8, 0) - first;
                if (offset < 0 || offset >= stride || (index > 0 && offset <= Pointer(data, mipPointers + (index - 1) * 8, 0) - first)) throw new InvalidDataException("Invalid BNTX mip pointer order.");
                if (index == mipIndex + 1 && relative + layout.TexelBytes > offset) throw new InvalidDataException("Overlapping BNTX mip storage.");
            }
            int address = first + layerIndex * stride + relative;
            byte[] texels = new byte[layout.TexelBytes]; Array.Copy(data, address, texels, 0, texels.Length);
            uint swizzle = 0;
            for (int channel = 0; channel < 4; channel++) {
                uint selector = fileSwizzle >> ((3 - channel) * 8) & 255;
                if (selector > 5) throw new InvalidDataException("Invalid BNTX component selector.");
                swizzle |= selector << (channel * 8);
            }
            byte[] tight = SwitchNativeTextureCooker.ToTight(texels, layout);
            byte[] preview = SwitchNativeTextureCooker.DecodeTight(tight, width, height, format, swizzle);
            TextureAsset result = SwitchTextureCodec.CreateNative(width, height, format.Code, format.BlockLinear, exponent,
                swizzle == SwitchTextureCodec.IdentitySwizzle ? format.Alpha : TextureAssetAlphaPrecision.A8, swizzle, texels, preview);
            int nameAddress = Pointer(data, info + 96, 2), nameLength = Word(data, nameAddress);
            if (nameLength > 4096 || nameLength > data.Length - nameAddress - 2) throw new InvalidDataException("Invalid BNTX texture name.");
            result.Id = System.Text.Encoding.UTF8.GetString(data, nameAddress + 2, nameLength);
            return result;
        }
        /// <summary>Maps supported BNTX surface words to exact native deko3d color formats; unsupported words fail explicitly.</summary>
        static SwitchTextureFormat Format(uint value, bool tiled) {
            int code = (int)(value >> 8), type = (int)(value & 255); string name;
            string numeric = type == 1 ? "Unorm" : type == 2 ? "Snorm" : type == 3 ? "Uint" : type == 4 ? "Sint" : type == 5 ? "Float" : type == 6 ? "Unorm_sRGB" : null;
            if (code >= 0x2d && code <= 0x3a && (type == 1 || type == 6)) {
                string[] footprints = new[] { "4x4", "5x4", "5x5", "6x5", "6x6", "8x5", "8x6", "8x8", "10x5", "10x6", "10x8", "10x10", "12x10", "12x12" };
                name = "RGBA_ASTC_" + footprints[code - 0x2d] + (type == 6 ? "_sRGB" : "");
            } else if (code >= 0x1a && code <= 0x20) {
                if (code <= 0x1c && (type == 1 || type == 6)) name = "RGBA_BC" + (code - 0x19) + (type == 6 ? "_sRGB" : "");
                else if ((code == 0x1d || code == 0x1e) && (type == 1 || type == 2)) name = (code == 0x1d ? "R_BC4_" : "RG_BC5_") + numeric;
                else if (code == 0x1f && (type == 5 || type == 10)) name = type == 5 ? "RGBA_BC6H_SF16_Float" : "RGBA_BC6H_UF16_Float";
                else if (code == 0x20 && (type == 1 || type == 6)) name = "RGBA_BC7_" + numeric;
                else throw new InvalidDataException("Unsupported BNTX compressed numeric type.");
            } else {
                string prefix = code == 2 ? "R8" : code == 9 ? "RG8" : code == 0xb ? "RGBA8" : code == 0xc ? "BGRA8"
                    : code == 3 ? "RGBA4" : code == 5 ? "RGB5A1" : code == 6 ? "A1BGR5" : code == 7 ? "RGB565" : code == 8 ? "BGR565"
                    : code == 0xe ? "RGB10A2" : code == 0xf ? "RG11B10" : code == 0x3b ? "BGR5A1" : null;
                if (prefix == null || numeric == null) throw new InvalidDataException("Unsupported BNTX surface format.");
                name = prefix + "_" + numeric;
            }
            if (!SwitchTextureFormatCatalog.TryGetFormat("Switch." + (tiled ? "BlockLinear." : "Linear.") + name, out SwitchTextureFormat format)) throw new InvalidDataException("BNTX format has no native Switch catalog mapping.");
            return format;
        }
        /// <summary>Resolves a full unsigned 64-bit file pointer only after checking the entire requested region.</summary>
        static int Pointer(byte[] data, int field, int length) {
            if (field < 0 || data.Length - field < 8 || length < 0) throw new InvalidDataException("Truncated BNTX pointer.");
            ulong value = SwitchTexturePixelCodec.Read(data, field, 8);
            if (value > (ulong)data.Length || (ulong)length > (ulong)data.Length - value) throw new InvalidDataException("BNTX pointer exceeds the archive.");
            return (int)value;
        }
        /// <summary>Reads a checked little-endian 16-bit BNTX field.</summary>
        static int Word(byte[] data, int offset) {
            if (offset < 0 || data.Length - offset < 2) throw new InvalidDataException("Truncated BNTX word.");
            return data[offset] | data[offset + 1] << 8;
        }
    }
}
