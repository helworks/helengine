using System.Buffers.Binary;

namespace helengine.editor;

/// <summary>Converts editor RGBA previews to RSX storage, including packed blocks and rectangular Morton addressing.</summary>
public static class PlayStation3GtfPixelCodec {
    /// <summary>Encodes one level into a prevalidated native payload, retaining linear pitch and RSX wide-texel swizzling.</summary>
    public static void EncodeLevel(byte[] rgba, int width, int height, PlayStation3TextureFormat format, bool linear, int pitch, byte[] output, int offset) {
        if (rgba == null || rgba.Length != checked(width * height * 4)) throw new InvalidDataException("RGBA input does not match its dimensions.");
        if (format.BlockWidth > 1) { EncodeBlocks(rgba, width, height, format, linear ? pitch : format.GetRowBytes(width), output, offset); return; }
        byte[] pixel = new byte[format.BytesPerBlock];
        for (int y = 0; y < height; y++) for (int x = 0; x < width; x++) {
            EncodePixel(format.Code, rgba, (y * width + x) * 4, pixel);
            for (int b = 0; b < pixel.Length; b++) output[offset + GetByteOffset(format, linear, pitch, width, height, x, y, b)] = pixel[b];
        }
    }
    /// <summary>Decodes the base level for an editor preview, applying persisted RSX component remapping after sampling.</summary>
    public static byte[] Decode(PlayStation3GtfTexture texture) {
        byte[] rgba = new byte[checked(texture.Width * texture.Height * 4)];
        if (texture.Format.BlockWidth > 1) DecodeBlocks(texture, rgba);
        else {
            byte[] pixel = new byte[texture.Format.BytesPerBlock];
            for (int y = 0; y < texture.Height; y++) for (int x = 0; x < texture.Width; x++) {
                for (int b = 0; b < pixel.Length; b++) pixel[b] = texture.Pixels[GetByteOffset(texture.Format, texture.Linear, texture.Pitch, texture.Width, texture.Height, x, y, b)];
                DecodePixel(texture.Format.Code, pixel, rgba, (y * texture.Width + x) * 4);
            }
        }
        ApplyRemap(rgba, texture.Remap, texture.Format.Code);
        return rgba;
    }
    /// <summary>Addresses native bytes; texels wider than 32 bits swizzle their constituent 32-bit lanes.</summary>
    public static int GetByteOffset(PlayStation3TextureFormat format, bool linear, int pitch, int width, int height, int x, int y, int byteIndex) {
        if (linear || format.BlockWidth > 1) return y * (linear ? pitch : format.GetRowBytes(width)) + x * format.BytesPerBlock + byteIndex;
        int unit = Math.Min(format.BytesPerBlock, 4);
        int lanes = format.BytesPerBlock / unit;
        int address = Morton(x * lanes + byteIndex / unit, y, width * lanes, height);
        return address * unit + byteIndex % unit;
    }
    /// <summary>Interleaves only existing axis bits so rectangular power-of-two textures remain tightly packed.</summary>
    static int Morton(int x, int y, int width, int height) {
        int result = 0, targetBit = 1;
        for (int bit = 1; bit < width || bit < height; bit <<= 1) {
            if (bit < width) { if ((x & bit) != 0) result |= targetBit; targetBit <<= 1; }
            if (bit < height) { if ((y & bit) != 0) result |= targetBit; targetBit <<= 1; }
        }
        return result;
    }
    /// <summary>Packs native integer, normalized and floating channels from an ordinary 8-bit source.</summary>
    static void EncodePixel(byte code, byte[] rgba, int p, byte[] target) {
        int r = rgba[p], g = rgba[p + 1], b = rgba[p + 2], a = rgba[p + 3];
        uint word;
        switch (code) {
            case 0x01: target[0] = (byte)r; return;
            case 0x02: case 0x1d: word = (uint)((code == 0x1d || a >= 128 ? 0x8000 : 0) | ((r >> 3) << 10) | ((g >> 3) << 5) | (b >> 3)); break;
            case 0x03: word = (uint)(((a >> 4) << 12) | ((r >> 4) << 8) | ((g >> 4) << 4) | (b >> 4)); break;
            case 0x04: word = (uint)(((r >> 3) << 11) | ((g >> 2) << 5) | (b >> 3)); break;
            case 0x05: case 0x1e: word = ((uint)(code == 0x1e ? 255 : a) << 24) | ((uint)r << 16) | ((uint)g << 8) | (uint)b; break;
            case 0x0b: target[0] = (byte)g; target[1] = (byte)r; return;
            case 0x0f: word = (uint)(((r >> 2) << 10) | ((g >> 3) << 5) | (b >> 3)); break;
            case 0x10: word = ((uint)Math.Round(r / 255d * 0xffffff) << 8); break;
            case 0x11: word = (BitConverter.SingleToUInt32Bits(r / 255f) >> 7) << 8; break;
            case 0x12: case 0x14: word = (uint)(r * 257); break;
            case 0x13: word = (uint)(Math.Max(0L, (long)BitConverter.SingleToUInt32Bits(r / 255f) - 0x3c000000L) >> 11); break;
            case 0x15: Write16(target, 0, (ushort)(r * 257)); Write16(target, 2, (ushort)(g * 257)); return;
            case 0x17: word = (uint)(((r >> 3) << 11) | ((g >> 3) << 6) | ((b >> 3) << 1) | (a >= 128 ? 1 : 0)); break;
            case 0x18: target[0] = (byte)g; target[1] = (byte)r; return;
            case 0x19: target[0] = (byte)Math.Round(g / 255d * 127); target[1] = (byte)Math.Round(r / 255d * 127); return;
            case 0x1a: for (int c = 0; c < 4; c++) Write16(target, c * 2, BitConverter.HalfToUInt16Bits((Half)(rgba[p + c] / 255f))); return;
            case 0x1b: for (int c = 0; c < 4; c++) Write32(target, c * 4, BitConverter.SingleToUInt32Bits(rgba[p + c] / 255f)); return;
            case 0x1c: word = BitConverter.SingleToUInt32Bits(r / 255f); break;
            case 0x1f: Write16(target, 0, BitConverter.HalfToUInt16Bits((Half)(g / 255f))); Write16(target, 2, BitConverter.HalfToUInt16Bits((Half)(r / 255f))); return;
            default: throw new InvalidDataException("Unsupported RSX pixel encoding.");
        }
        if (target.Length == 2) Write16(target, 0, (ushort)word); else Write32(target, 0, word);
    }
    /// <summary>Samples native channels into RGBA8; float and signed previews clamp to the display range.</summary>
    static void DecodePixel(byte code, byte[] pixel, byte[] rgba, int p) {
        uint word = pixel.Length == 1 ? pixel[0] : pixel.Length == 2 ? PlayStation3GtfTexture.Read16(pixel, 0) : PlayStation3GtfTexture.Read32(pixel, 0);
        int r = 0, g = 0, b = 0, a = 255;
        switch (code) {
            case 0x01: r = g = b = pixel[0]; break;
            case 0x02: case 0x1d: r = Expand((word >> 10) & 31, 31); g = Expand((word >> 5) & 31, 31); b = Expand(word & 31, 31); a = code == 0x1d || (word & 0x8000) != 0 ? 255 : 0; break;
            case 0x03: r = (int)((word >> 8) & 15) * 17; g = (int)((word >> 4) & 15) * 17; b = (int)(word & 15) * 17; a = (int)(word >> 12) * 17; break;
            case 0x04: r = Expand((word >> 11) & 31, 31); g = Expand((word >> 5) & 63, 63); b = Expand(word & 31, 31); break;
            case 0x05: case 0x1e: a = code == 0x1e ? 255 : (int)(word >> 24); r = (int)((word >> 16) & 255); g = (int)((word >> 8) & 255); b = (int)(word & 255); break;
            case 0x0b: r = b = pixel[1]; g = a = pixel[0]; break;
            case 0x0f: r = Expand((word >> 10) & 63, 63); g = Expand((word >> 5) & 31, 31); b = Expand(word & 31, 31); break;
            case 0x10: r = g = b = a = Expand(word >> 8, 0xffffff); break;
            case 0x11: r = g = b = a = ToByte(BitConverter.UInt32BitsToSingle((word >> 8) << 7)); break;
            case 0x12: r = g = b = a = Expand(word, 65535); break;
            case 0x13: r = g = b = a = ToByte(BitConverter.UInt32BitsToSingle((word << 11) + 0x3c000000u)); break;
            case 0x14: r = b = 255; g = a = Expand(word, 65535); break;
            case 0x15: r = b = Expand(PlayStation3GtfTexture.Read16(pixel, 0), 65535); g = a = Expand(PlayStation3GtfTexture.Read16(pixel, 2), 65535); break;
            case 0x17: r = Expand((word >> 11) & 31, 31); g = Expand((word >> 6) & 31, 31); b = Expand((word >> 1) & 31, 31); a = (word & 1) == 0 ? 0 : 255; break;
            case 0x18: case 0x19: a = g = code == 0x18 ? pixel[0] : ToByte((sbyte)pixel[0] / 127f); r = b = code == 0x18 ? pixel[1] : ToByte((sbyte)pixel[1] / 127f); break;
            case 0x1a: case 0x1b:
                for (int c = 0; c < 4; c++) rgba[p + c] = (byte)ToByte(code == 0x1a ? (float)BitConverter.UInt16BitsToHalf(PlayStation3GtfTexture.Read16(pixel, c * 2)) : BitConverter.UInt32BitsToSingle(PlayStation3GtfTexture.Read32(pixel, c * 4)));
                return;
            case 0x1c: r = g = b = a = ToByte(BitConverter.UInt32BitsToSingle(word)); break;
            case 0x1f: a = g = ToByte((float)BitConverter.UInt16BitsToHalf(PlayStation3GtfTexture.Read16(pixel, 0))); r = b = ToByte((float)BitConverter.UInt16BitsToHalf(PlayStation3GtfTexture.Read16(pixel, 2))); break;
            default: throw new InvalidDataException("Unsupported RSX pixel encoding.");
        }
        rgba[p] = (byte)r; rgba[p + 1] = (byte)g; rgba[p + 2] = (byte)b; rgba[p + 3] = (byte)a;
    }
    /// <summary>Encodes standard little-endian DXT blocks or shared-green/shared-blue pairs without changing block byte order.</summary>
    static void EncodeBlocks(byte[] rgba, int width, int height, PlayStation3TextureFormat format, int rowBytes, byte[] output, int offset) {
        XboxNativeTextureLayout dxt = format.BlockWidth == 4 ? CreateDxtLayout(format.Code) : null;
        byte[] block = new byte[64];
        for (int y = 0; y < height; y += format.BlockHeight) for (int x = 0; x < width; x += format.BlockWidth) {
            int target = offset + y / format.BlockHeight * rowBytes + x / format.BlockWidth * format.BytesPerBlock;
            if (dxt != null) {
                for (int by = 0; by < 4; by++) for (int bx = 0; bx < 4; bx++)
                    Buffer.BlockCopy(rgba, (Math.Min(y + by, height - 1) * width + Math.Min(x + bx, width - 1)) * 4, block, (by * 4 + bx) * 4, 4);
                XboxNativeTextureDxtCodec.Encode(block, 4, 4, dxt, format.AlphaPrecision, output, target);
            } else {
                int first = (y * width + x) * 4, second = (y * width + Math.Min(x + 1, width - 1)) * 4;
                byte blue = (byte)((rgba[first + 2] + rgba[second + 2] + 1) / 2), green = (byte)((rgba[first + 1] + rgba[second + 1] + 1) / 2);
                if (format.Code == 0x0d) { output[target] = blue; output[target + 1] = rgba[first]; output[target + 2] = green; output[target + 3] = rgba[second]; }
                else { output[target] = rgba[first]; output[target + 1] = blue; output[target + 2] = rgba[second]; output[target + 3] = green; }
            }
        }
    }
    /// <summary>Decodes block rows with bounded edge cropping and the stored linear block-row pitch.</summary>
    static void DecodeBlocks(PlayStation3GtfTexture texture, byte[] rgba) {
        PlayStation3TextureFormat format = texture.Format;
        XboxNativeTextureLayout dxt = format.BlockWidth == 4 ? CreateDxtLayout(format.Code) : null;
        byte[] block = new byte[64];
        int rowBytes = texture.Linear ? texture.Pitch : format.GetRowBytes(texture.Width);
        for (int y = 0; y < texture.Height; y += format.BlockHeight) for (int x = 0; x < texture.Width; x += format.BlockWidth) {
            int source = y / format.BlockHeight * rowBytes + x / format.BlockWidth * format.BytesPerBlock;
            if (dxt != null) {
                XboxNativeTextureDxtCodec.Decode(texture.Pixels, source, dxt, block);
                for (int by = 0; by < 4 && y + by < texture.Height; by++) for (int bx = 0; bx < 4 && x + bx < texture.Width; bx++)
                    Buffer.BlockCopy(block, (by * 4 + bx) * 4, rgba, ((y + by) * texture.Width + x + bx) * 4, 4);
            } else for (int bx = 0; bx < 2 && x + bx < texture.Width; bx++) {
                int target = (y * texture.Width + x + bx) * 4;
                rgba[target] = texture.Pixels[source + (format.Code == 0x0d ? 1 + bx * 2 : bx * 2)];
                rgba[target + 1] = texture.Pixels[source + (format.Code == 0x0d ? 2 : 3)];
                rgba[target + 2] = texture.Pixels[source + (format.Code == 0x0d ? 0 : 1)]; rgba[target + 3] = 255;
            }
        }
    }
    /// <summary>Reuses the engine's platform-independent DXT block algorithm through a single-block layout.</summary>
    static XboxNativeTextureLayout CreateDxtLayout(byte code) {
        string id = code == 6 ? "Xbox.DXT1" : code == 7 ? "Xbox.DXT3" : "Xbox.DXT5";
        if (!XboxNativeTextureFormatCatalog.TryGetFormat(id, out XboxNativeTextureFormatDefinition format)) throw new InvalidOperationException("The shared DXT codec is unavailable.");
        return new XboxNativeTextureLayout(format, 4, 4, 0);
    }
    /// <summary>Applies the GTF remap's ARGB selectors and zero/one operations to sampled preview channels.</summary>
    static void ApplyRemap(byte[] rgba, uint remap, byte code) {
        remap = GetSamplingRemap(code, remap);
        byte[] channels = new byte[4];
        for (int p = 0; p < rgba.Length; p += 4) {
            channels[0] = rgba[p + 3]; channels[1] = rgba[p]; channels[2] = rgba[p + 1]; channels[3] = rgba[p + 2];
            for (int c = 0; c < 4; c++) {
                int operation = (int)((remap >> (8 + c * 2)) & 3);
                if (operation == 3) throw new InvalidDataException("GTF uses a reserved component remap operation.");
                rgba[p + (c == 0 ? 3 : c - 1)] = operation == 0 ? (byte)0 : operation == 1 ? (byte)255 : channels[(remap >> (c * 2)) & 3];
            }
        }
    }
    /// <summary>Translates the special RSX 16-bit channel-pair selectors to ordinary ARGB preview selectors.</summary>
    public static uint GetSamplingRemap(byte code, uint remap) {
        uint selectors = remap & 255;
        bool overrideOrder = (remap & 0x10000) != 0;
        if (code == 0x1a || code == 0x1b || code == 0x1c || code == 0x1f) {
            if (selectors != 0xe4) throw new InvalidDataException("RSX floating texture channels require the identity selector.");
            if (code == 0x1f) selectors = overrideOrder ? 0x56u : 0x66u;
        } else if (code == 0x14 || code == 0x15 || code == 0x18 || code == 0x19) {
            selectors = selectors switch {
                0xe4 => overrideOrder ? 0x56u : 0x66u,
                0x4e => overrideOrder ? 0xa9u : 0x99u,
                0xee => 0xaau,
                0x44 => 0x55u,
                _ => throw new InvalidDataException("RSX 16-bit channel-pair remap contains invalid selectors.")
            };
        }
        return (remap & 0xff00) | selectors;
    }
    /// <summary>Expands a normalized unsigned channel to eight bits with nearest rounding.</summary>
    static int Expand(uint value, uint maximum) => (int)(((long)value * 255 + maximum / 2) / maximum);
    /// <summary>Clamps a finite normalized floating sample for an RGBA8 editor preview.</summary>
    static int ToByte(float value) => float.IsNaN(value) ? 0 : (int)Math.Round(Math.Clamp((double)value, 0, 1) * 255);
    /// <summary>Writes a native big-endian 16-bit channel or packed texel.</summary>
    public static void Write16(byte[] bytes, int offset, ushort value) => BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(offset, 2), value);
    /// <summary>Writes a native big-endian 32-bit channel, packed texel or GTF field.</summary>
    public static void Write32(byte[] bytes, int offset, uint value) => BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(offset, 4), value);
}
