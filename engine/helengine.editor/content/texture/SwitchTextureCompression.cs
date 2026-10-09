using AstcSharp;
using AstcSharp.Core;
using BCnEncoder.Decoder;
using BCnEncoder.Encoder;
using BCnEncoder.Shared;
using AssetRipper.TextureDecoder.Etc;
using AssetRipper.TextureDecoder.Rgb.Formats;

namespace helengine.editor {
    /// <summary>Runs complete managed BC/ASTC decoders and native ETC/EAC block fitting during import and cooking.</summary>
    public static class SwitchTextureCompression {
        /// <summary>Encodes a tightly packed native compression stream, retaining the requested format rather than transcoding it to another family.</summary>
        public static byte[] Encode(byte[] rgba, int width, int height, SwitchTextureFormat format) {
            string name = format.Name;
            if (name.Contains("ASTC", StringComparison.Ordinal)) return AstcEncoder.CompressImage(rgba, width, height, Footprint(format));
            if (name.Contains("ETC2", StringComparison.Ordinal)) return SwitchEtcTextureEncoder.Encode(rgba, width, height, format);
            if (name.Contains("Snorm", StringComparison.Ordinal)) return SignedBc(rgba, width, height, name.Contains("BC5", StringComparison.Ordinal), true);
            BcEncoder encoder = new BcEncoder();
            encoder.OutputOptions.GenerateMipMaps = false; encoder.OutputOptions.Format = BcFormat(name); encoder.OutputOptions.Quality = CompressionQuality.Balanced;
            if (name.Contains("BC6H", StringComparison.Ordinal)) {
                ColorRgbFloat[] pixels = new ColorRgbFloat[width * height];
                for (int index = 0; index < pixels.Length; index++) pixels[index] = new ColorRgbFloat(rgba[index * 4] / 255f, rgba[index * 4 + 1] / 255f, rgba[index * 4 + 2] / 255f);
                return encoder.EncodeToRawBytesHdr(new CommunityToolkit.HighPerformance.ReadOnlyMemory2D<ColorRgbFloat>(pixels, height, width))[0];
            }
            return encoder.EncodeToRawBytes(rgba, width, height, PixelFormat.Rgba32)[0];
        }
        /// <summary>Decodes arbitrary legal native compression blocks before serializing the portable CPU view.</summary>
        public static byte[] Decode(byte[] blocks, int width, int height, SwitchTextureFormat format) {
            string name = format.Name;
            if (name.Contains("ASTC", StringComparison.Ordinal)) return AstcDecoder.DecompressImage(blocks, width, height, Footprint(format), name.EndsWith("sRGB", StringComparison.Ordinal) ? LdrDecodeMode.Srgb : LdrDecodeMode.Linear).ToArray();
            if (name.Contains("ETC2", StringComparison.Ordinal)) return DecodeEtc(blocks, width, height, name);
            if (name.Contains("Snorm", StringComparison.Ordinal)) return SignedBc(blocks, width, height, name.Contains("BC5", StringComparison.Ordinal), false);
            byte[] rgba = new byte[SwitchTextureCodec.PreviewBytes(width, height)]; BcDecoder decoder = new BcDecoder();
            if (name.Contains("BC6H", StringComparison.Ordinal)) {
                ColorRgbFloat[] pixels = decoder.DecodeRawHdr(blocks, width, height, BcFormat(name));
                for (int index = 0; index < pixels.Length; index++) {
                    rgba[index * 4] = SwitchTexturePixelCodec.Preview(pixels[index].r); rgba[index * 4 + 1] = SwitchTexturePixelCodec.Preview(pixels[index].g);
                    rgba[index * 4 + 2] = SwitchTexturePixelCodec.Preview(pixels[index].b); rgba[index * 4 + 3] = 255;
                }
            } else {
                ColorRgba32[] pixels = decoder.DecodeRaw(blocks, width, height, BcFormat(name));
                for (int index = 0; index < pixels.Length; index++) {
                    rgba[index * 4] = pixels[index].r; rgba[index * 4 + 1] = name.Contains("BC4", StringComparison.Ordinal) ? (byte)0 : pixels[index].g;
                    rgba[index * 4 + 2] = name.Contains("BC4", StringComparison.Ordinal) || name.Contains("BC5", StringComparison.Ordinal) ? (byte)0 : pixels[index].b;
                    rgba[index * 4 + 3] = name.StartsWith("RGB_", StringComparison.Ordinal) || name.StartsWith("R_", StringComparison.Ordinal) || name.StartsWith("RG_", StringComparison.Ordinal) ? (byte)255 : pixels[index].a;
                }
            }
            return rgba;
        }
        /// <summary>Resolves the fourteen ASTC footprints through the codec's documented enum names.</summary>
        static Footprint Footprint(SwitchTextureFormat format) => AstcSharp.Core.Footprint.FromFootprintType(Enum.Parse<FootprintType>("Footprint" + format.BlockWidth + "x" + format.BlockHeight));
        /// <summary>Maps native BC families and coverage to the managed block codec.</summary>
        static CompressionFormat BcFormat(string name) {
            if (name.Contains("BC1", StringComparison.Ordinal)) return name.StartsWith("RGBA", StringComparison.Ordinal) ? CompressionFormat.Bc1WithAlpha : CompressionFormat.Bc1;
            if (name.Contains("BC2", StringComparison.Ordinal)) return CompressionFormat.Bc2;
            if (name.Contains("BC3", StringComparison.Ordinal)) return CompressionFormat.Bc3;
            if (name.Contains("BC4", StringComparison.Ordinal)) return CompressionFormat.Bc4;
            if (name.Contains("BC5", StringComparison.Ordinal)) return CompressionFormat.Bc5;
            if (name.Contains("BC6H", StringComparison.Ordinal)) return name.Contains("SF16", StringComparison.Ordinal) ? CompressionFormat.Bc6S : CompressionFormat.Bc6U;
            if (name.Contains("BC7", StringComparison.Ordinal)) return CompressionFormat.Bc7;
            throw new ArgumentException("Unknown native BC format.");
        }
        /// <summary>Uses independent complete ETC2/EAC decoding, including T, H, planar and signed modes.</summary>
        static byte[] DecodeEtc(byte[] blocks, int width, int height, string name) {
            byte[] output = new byte[SwitchTextureCodec.PreviewBytes(width, height)];
            if (name.StartsWith("R_", StringComparison.Ordinal) || name.StartsWith("RG_", StringComparison.Ordinal))
                return SwitchEacTextureCodec.Decode(blocks, width, height, name.Contains("Snorm", StringComparison.Ordinal), name.StartsWith("RG_", StringComparison.Ordinal));
            else if (name.StartsWith("RGBA_", StringComparison.Ordinal)) EtcDecoder.DecompressETC2A8<ColorRGBA<byte>, byte>(blocks, width, height, output);
            else if (name.Contains("PTA", StringComparison.Ordinal)) EtcDecoder.DecompressETC2A1<ColorRGBA<byte>, byte>(blocks, width, height, output);
            else EtcDecoder.DecompressETC2<ColorRGBA<byte>, byte>(blocks, width, height, output);
            return output;
        }
        /// <summary>Fits or samples signed BC4/5 endpoints, interpreting -128 as the mandated -127 endpoint.</summary>
        static byte[] SignedBc(byte[] input, int width, int height, bool twoChannels, bool encode) {
            int columns = (width + 3) / 4, rows = (height + 3) / 4, channels = twoChannels ? 2 : 1;
            byte[] output = new byte[encode ? columns * rows * channels * 8 : SwitchTextureCodec.PreviewBytes(width, height)];
            if (!encode) for (int pixel = 0; pixel < width * height; pixel++) output[pixel * 4 + 3] = 255;
            for (int by = 0; by < rows; by++) for (int bx = 0; bx < columns; bx++) for (int channel = 0; channel < channels; channel++) {
                int offset = (by * columns + bx) * channels * 8 + channel * 8;
                int first = encode ? 0 : Math.Max(-127, (int)(sbyte)input[offset]); int second = encode ? 127 : Math.Max(-127, (int)(sbyte)input[offset + 1]);
                if (encode) for (int pixel = 0; pixel < 16; pixel++) {
                    int source = (Math.Min(by * 4 + pixel / 4, height - 1) * width + Math.Min(bx * 4 + pixel % 4, width - 1)) * 4 + channel;
                    int value = (input[source] * 127 + 127) / 255; first = Math.Max(first, value); second = Math.Min(second, value);
                }
                ulong selectors = encode ? 0 : SwitchTexturePixelCodec.Read(input, offset + 2, 6);
                for (int pixel = 0; pixel < 16; pixel++) {
                    int x = bx * 4 + pixel % 4, y = by * 4 + pixel / 4;
                    if (encode) {
                        int value = (input[(Math.Min(y, height - 1) * width + Math.Min(x, width - 1)) * 4 + channel] * 127 + 127) / 255, best = 0, error = int.MaxValue;
                        for (int selected = 0; selected < 8; selected++) { int difference = Math.Abs(value - SignedValue(first, second, selected)); if (difference < error) { error = difference; best = selected; } }
                        selectors |= (ulong)best << (pixel * 3);
                    } else if (x < width && y < height) output[(y * width + x) * 4 + channel] = SwitchTexturePixelCodec.Preview(SignedValue(first, second, (int)(selectors >> (pixel * 3) & 7)) / 127d);
                }
                if (encode) { output[offset] = (byte)first; output[offset + 1] = (byte)second; SwitchTexturePixelCodec.Write(output, offset + 2, 6, selectors); }
            }
            return output;
        }
        /// <summary>Evaluates the standard signed BC interpolation palette with integer truncation.</summary>
        static int SignedValue(int first, int second, int selector) => selector == 0 ? first : selector == 1 ? second
            : first > second ? ((8 - selector) * first + (selector - 1) * second) / 7
            : selector < 6 ? ((6 - selector) * first + (selector - 1) * second) / 5 : selector == 6 ? -127 : 127;
    }
}
