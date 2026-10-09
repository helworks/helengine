namespace helengine {
    /// <summary>
    /// Converts the engine's linear texture payloads without depending on a renderer's byte order or palette API.
    /// Intensity comes from Rec.601 luma (0.299R + 0.587G + 0.114B), rounded to the nearest byte.
    /// I4/I8 deliberately replace independent source alpha with intensity, matching intensity texture sampling.
    /// GX tiled textures are outside this linear codec and must use their platform-specific decoder.
    /// </summary>
    public static class TextureAssetPixelCodec {
        /// <summary>
        /// Gets the exact texel-buffer length, excluding indexed palettes. New four-bit formats pad each row;
        /// legacy Indexed4 packs the entire image continuously. YUV pads the final horizontal pair in each row.
        /// </summary>
        /// <param name="format">Linear texture storage format.</param>
        /// <param name="width">Positive image width in pixels.</param>
        /// <param name="height">Positive image height in pixels.</param>
        /// <returns>Required byte count, rejecting unsupported formats and lengths exceeding a managed array.</returns>
        public static int GetPixelByteLength(TextureAssetColorFormat format, int width, int height) {
            if (width <= 0 || height <= 0) {
                throw new ArgumentOutOfRangeException(nameof(width), "Texture dimensions must be positive.");
            }
            long pixels = (long)width * height;
            if (pixels > 2L * int.MaxValue) {
                throw new ArgumentOutOfRangeException(nameof(width), "Texture byte length exceeds the managed array limit.");
            }
            long bytes;
            switch (format) {
                case TextureAssetColorFormat.Rgba32:
                    bytes = pixels * 4;
                    break;
                case TextureAssetColorFormat.Rgba4444:
                case TextureAssetColorFormat.Rgba5551:
                case TextureAssetColorFormat.Ia16:
                case TextureAssetColorFormat.Ps1Bgr555:
                    bytes = pixels * 2;
                    break;
                case TextureAssetColorFormat.Indexed4:
                    bytes = (pixels + 1) / 2;
                    break;
                case TextureAssetColorFormat.Ia4:
                case TextureAssetColorFormat.I4:
                    bytes = ((long)width + 1) / 2 * height;
                    break;
                case TextureAssetColorFormat.Indexed8:
                case TextureAssetColorFormat.Ia8:
                case TextureAssetColorFormat.I8:
                    bytes = pixels;
                    break;
                case TextureAssetColorFormat.Yuv16:
                    bytes = ((long)width + 1) / 2 * 4 * height;
                    break;
                default:
                    throw new NotSupportedException("This texture format has no linear pixel codec.");
            }
            if (bytes > int.MaxValue) {
                throw new ArgumentOutOfRangeException(nameof(width), "Texture byte length exceeds the managed array limit.");
            }
            return (int)bytes;
        }

        /// <summary>
        /// Validates and decodes a linear texture into straight RGBA bytes without modifying its pixels or palette.
        /// Every format returns a new caller-owned array, including RGBA32, so native callers have one ownership contract.
        /// New format alpha metadata must be compatible with the format's intrinsic storage precision.
        /// </summary>
        /// <param name="asset">Texture whose exact payload length and palette indices must be valid.</param>
        /// <returns>New row-major RGBA32 pixels whose contents can be changed without modifying the source asset.</returns>
        [NativeOwnedReturn]
        public static byte[] DecodeToRgba32(TextureAsset asset) {
            if (asset != null && asset.ColorFormat == TextureAssetColorFormat.SwitchNative) return SwitchTextureCodec.Decode(asset);
            if (asset == null) {
                throw new ArgumentNullException(nameof(asset));
            }
            if (asset.ColorFormat == TextureAssetColorFormat.Ps1Bgr555) {
                return Ps1TexturePixelCodec.Decode(asset);
            }
            if (asset.ColorFormat == TextureAssetColorFormat.XboxNative) {
                return XboxNativeTextureCodec.Decode(asset);
            }
            if (asset.ColorFormat == TextureAssetColorFormat.Xbox360Native) {
                return Xbox360NativeTextureCodec.Decode(asset);
            }
            if (asset.ColorFormat == TextureAssetColorFormat.PspNative) {
                return PspTextureCodec.Decode(asset);
            }
            if (asset.ColorFormat == TextureAssetColorFormat.Nintendo3DsNative) {
                return Nintendo3DsTextureCodec.Decode(asset);
            }
            if (asset.ColorFormat == TextureAssetColorFormat.GxNative) {
                return GxNativeTextureCodec.Decode(asset);
            }
            if (asset.ColorFormat == TextureAssetColorFormat.VitaNative) {
                return VitaNativeTextureCodec.Decode(asset);
            }
            if (asset.ColorFormat == TextureAssetColorFormat.WiiUNative) {
                return WiiUNativeTextureCodec.Decode(asset);
            }
            if (asset.ColorFormat == TextureAssetColorFormat.NintendoDsNative) {
                return NintendoDsTextureCodec.Decode(asset);
            }
            int expected = GetPixelByteLength(asset.ColorFormat, asset.Width, asset.Height);
            if (asset.Colors == null || asset.Colors.Length != expected) {
                throw new ArgumentException("Texture pixel payload does not match its dimensions and format.", nameof(asset));
            }
            ValidateAlphaPrecision(asset.ColorFormat, asset.AlphaPrecision);
            if (asset.ColorFormat == TextureAssetColorFormat.Rgba32) {
                byte[] copiedColors = new byte[asset.Colors.Length];
                for (int index = 0; index < copiedColors.Length; index++) {
                    copiedColors[index] = asset.Colors[index];
                }
                return copiedColors;
            }
            int paletteEntries = ValidatePalette(asset);
            byte[] rgba = new byte[GetPixelByteLength(TextureAssetColorFormat.Rgba32, asset.Width, asset.Height)];
            if (asset.ColorFormat == TextureAssetColorFormat.Yuv16) {
                DecodeYuv(asset, rgba);
                return rgba;
            }
            int pixelCount = asset.Width * asset.Height;
            for (int pixel = 0; pixel < pixelCount; pixel++) {
                int target = pixel * 4;
                int word;
                int intensity;
                int nibble;
                switch (asset.ColorFormat) {
                    case TextureAssetColorFormat.Rgba4444:
                        word = ReadWord(asset.Colors, pixel * 2);
                        SetPixel(rgba, target, ExpandBits(word & 15, 15), ExpandBits((word >> 4) & 15, 15),
                            ExpandBits((word >> 8) & 15, 15), ExpandBits(word >> 12, 15));
                        break;
                    case TextureAssetColorFormat.Rgba5551:
                        word = ReadWord(asset.Colors, pixel * 2);
                        SetPixel(rgba, target, ExpandBits(word >> 11, 31), ExpandBits((word >> 6) & 31, 31),
                            ExpandBits((word >> 1) & 31, 31), (word & 1) * 255);
                        break;
                    case TextureAssetColorFormat.Indexed4:
                    case TextureAssetColorFormat.Indexed8:
                        int index = asset.ColorFormat == TextureAssetColorFormat.Indexed4
                            ? (asset.Colors[pixel / 2] >> ((pixel % 2) * 4)) & 15 : asset.Colors[pixel];
                        if (index >= paletteEntries) {
                            throw new ArgumentException("Texture contains an index outside its palette.", nameof(asset));
                        }
                        int entry = index * 4;
                        SetPixel(rgba, target, asset.PaletteColors[entry], asset.PaletteColors[entry + 1],
                            asset.PaletteColors[entry + 2], asset.PaletteColors[entry + 3]);
                        break;
                    case TextureAssetColorFormat.Ia4:
                        nibble = ReadRowNibble(asset, pixel);
                        intensity = ExpandBits(nibble >> 1, 7);
                        SetPixel(rgba, target, intensity, intensity, intensity, (nibble & 1) * 255);
                        break;
                    case TextureAssetColorFormat.Ia8:
                        intensity = ExpandBits(asset.Colors[pixel] >> 4, 15);
                        SetPixel(rgba, target, intensity, intensity, intensity, ExpandBits(asset.Colors[pixel] & 15, 15));
                        break;
                    case TextureAssetColorFormat.Ia16:
                        intensity = asset.Colors[pixel * 2 + 1];
                        SetPixel(rgba, target, intensity, intensity, intensity, asset.Colors[pixel * 2]);
                        break;
                    case TextureAssetColorFormat.I4:
                        intensity = ExpandBits(ReadRowNibble(asset, pixel), 15);
                        SetPixel(rgba, target, intensity, intensity, intensity, intensity);
                        break;
                    case TextureAssetColorFormat.I8:
                        intensity = asset.Colors[pixel];
                        SetPixel(rgba, target, intensity, intensity, intensity, intensity);
                        break;
                }
            }
            return rgba;
        }

        /// <summary>
        /// Encodes validated RGBA32 source pixels into one of the seven linear RGB/IA/I/YUV formats.
        /// RGB channels truncate to their stored bit depth; independent alpha is quantized by the requested policy.
        /// I4/I8 store Rec.601 intensity in every decoded channel and intentionally do not preserve source alpha.
        /// Indexed quantization and legacy RGBA4444 encoding remain with their existing editor processors.
        /// </summary>
        /// <param name="source">RGBA32 source, retained unchanged including its identity metadata.</param>
        /// <param name="format">New packed format to produce.</param>
        /// <param name="alpha">Compatible stored-alpha policy; incompatible requests are rejected.</param>
        /// <returns>New texture and pixel buffer, preserving the source's stable identities and ownership flag.</returns>
        public static TextureAsset EncodeFromRgba32(TextureAsset source, TextureAssetColorFormat format, TextureAssetAlphaPrecision alpha) {
            if (source == null) {
                throw new ArgumentNullException(nameof(source));
            }
            if (source.ColorFormat != TextureAssetColorFormat.Rgba32) {
                throw new ArgumentException("Encoding requires an RGBA32 source texture.", nameof(source));
            }
            byte[] rgba = DecodeToRgba32(source);
            if (format < TextureAssetColorFormat.Rgba5551 || format > TextureAssetColorFormat.Yuv16) {
                throw new NotSupportedException("This encoder only produces the new linear RGB, intensity and YUV formats.");
            }
            ValidateAlphaPrecision(format, alpha);
            byte[] colors = new byte[GetPixelByteLength(format, source.Width, source.Height)];
            if (format == TextureAssetColorFormat.Yuv16) {
                EncodeYuv(source.Width, source.Height, rgba, colors);
            } else {
                int pixelCount = source.Width * source.Height;
                for (int pixel = 0; pixel < pixelCount; pixel++) {
                    int offset = pixel * 4;
                    int intensity = GetIntensity(rgba, offset);
                    int storedAlpha = QuantizeAlpha(rgba[offset + 3], alpha);
                    switch (format) {
                        case TextureAssetColorFormat.Rgba5551:
                            WriteWord(colors, pixel * 2, ((rgba[offset] >> 3) << 11) | ((rgba[offset + 1] >> 3) << 6)
                                | ((rgba[offset + 2] >> 3) << 1) | (storedAlpha >> 7));
                            break;
                        case TextureAssetColorFormat.Ia4:
                            WriteRowNibble(colors, source.Width, pixel, ((intensity >> 5) << 1) | (storedAlpha >> 7));
                            break;
                        case TextureAssetColorFormat.Ia8:
                            colors[pixel] = (byte)((intensity & 240) | (storedAlpha >> 4));
                            break;
                        case TextureAssetColorFormat.Ia16:
                            WriteWord(colors, pixel * 2, (intensity << 8) | storedAlpha);
                            break;
                        case TextureAssetColorFormat.I4:
                            WriteRowNibble(colors, source.Width, pixel, intensity >> 4);
                            break;
                        case TextureAssetColorFormat.I8:
                            colors[pixel] = (byte)intensity;
                            break;
                    }
                }
            }
            return new TextureAsset {
                Id = source.Id,
                RuntimeAssetId = source.RuntimeAssetId,
                AuthoringAssetId = source.AuthoringAssetId,
                FormerAuthoringAssetIds = CopyFormerAuthoringAssetIds(source.FormerAuthoringAssetIds),
                IsEngineOwned = source.IsEngineOwned,
                Width = source.Width,
                Height = source.Height,
                ColorFormat = format,
                AlphaPrecision = alpha,
                Colors = colors
            };
        }

        /// <summary>Copies the former-identity container without sharing native array ownership; immutable string values are retained.</summary>
        /// <param name="source">Former identities, or null when the source asset has no container.</param>
        /// <returns>New independently owned container, or null when no source container exists.</returns>
        [NativeOwnedReturn]
        static string[] CopyFormerAuthoringAssetIds(string[] source) {
            if (source == null) {
                return null;
            }
            string[] copy = new string[source.Length];
            for (int index = 0; index < source.Length; index++) {
                copy[index] = source[index];
            }
            return copy;
        }

        /// <summary>
        /// Tests whether a format can honor the requested alpha policy. Legacy formats retain their existing
        /// accepted policies; intensity formats require their intrinsic precision instead of independent alpha.
        /// Unsupported or unknown formats and policies return false, including platform-specific GX storage.
        /// </summary>
        /// <param name="format">Texture storage format whose policy is being selected.</param>
        /// <param name="alpha">Requested stored-alpha policy.</param>
        /// <returns>Whether the generic codec recognizes both values and supports their combination.</returns>
        public static bool IsAlphaPrecisionSupported(TextureAssetColorFormat format, TextureAssetAlphaPrecision alpha) {
            if (alpha < TextureAssetAlphaPrecision.Opaque || alpha > TextureAssetAlphaPrecision.A8) {
                return false;
            }
            switch (format) {
                case TextureAssetColorFormat.Rgba32:
                case TextureAssetColorFormat.Rgba4444:
                case TextureAssetColorFormat.Indexed4:
                case TextureAssetColorFormat.Indexed8:
                case TextureAssetColorFormat.Ia16:
                    return true;
                case TextureAssetColorFormat.Rgba5551:
                case TextureAssetColorFormat.Ia4:
                    return alpha == TextureAssetAlphaPrecision.Opaque || alpha == TextureAssetAlphaPrecision.Binary;
                case TextureAssetColorFormat.Ia8:
                    return alpha != TextureAssetAlphaPrecision.A8;
                case TextureAssetColorFormat.I4:
                    return alpha == TextureAssetAlphaPrecision.A4;
                case TextureAssetColorFormat.I8:
                    return alpha == TextureAssetAlphaPrecision.A8;
                case TextureAssetColorFormat.Yuv16:
                    return alpha == TextureAssetAlphaPrecision.Opaque;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Gets the format's greatest stored alpha precision for explicit UI selections. This does not repair
        /// persisted settings or imply all lower policies are supported: I4/I8 have only intrinsic alpha.
        /// </summary>
        /// <param name="format">Generic texture format to describe.</param>
        /// <returns>Maximum stored precision, rejecting unknown or platform-specific formats.</returns>
        public static TextureAssetAlphaPrecision GetMaximumAlphaPrecision(TextureAssetColorFormat format) {
            switch (format) {
                case TextureAssetColorFormat.Rgba5551:
                case TextureAssetColorFormat.Ia4:
                    return TextureAssetAlphaPrecision.Binary;
                case TextureAssetColorFormat.Rgba4444:
                case TextureAssetColorFormat.Ia8:
                case TextureAssetColorFormat.I4:
                    return TextureAssetAlphaPrecision.A4;
                case TextureAssetColorFormat.Yuv16:
                    return TextureAssetAlphaPrecision.Opaque;
                case TextureAssetColorFormat.Rgba32:
                case TextureAssetColorFormat.Indexed4:
                case TextureAssetColorFormat.Indexed8:
                case TextureAssetColorFormat.Ia16:
                case TextureAssetColorFormat.I8:
                    return TextureAssetAlphaPrecision.A8;
                default:
                    throw new NotSupportedException("This texture format has no generic alpha-precision policy.");
            }
        }

        /// <summary>Rejects policies whose independent or intrinsic alpha precision cannot be represented by the format.</summary>
        static void ValidateAlphaPrecision(TextureAssetColorFormat format, TextureAssetAlphaPrecision alpha) {
            if (!IsAlphaPrecisionSupported(format, alpha)) {
                throw new ArgumentException("Alpha precision is incompatible with the texture format.", nameof(alpha));
            }
        }

        /// <summary>Validates RGBA32 palette length for indexed formats, returning its bounded number of usable entries.</summary>
        static int ValidatePalette(TextureAsset asset) {
            int maximum = asset.ColorFormat == TextureAssetColorFormat.Indexed4 ? 16
                : asset.ColorFormat == TextureAssetColorFormat.Indexed8 ? 256 : 0;
            if (maximum == 0) {
                return 0;
            }
            if (asset.PaletteColors == null || asset.PaletteColors.Length == 0
                || asset.PaletteColors.Length % 4 != 0 || asset.PaletteColors.Length / 4 > maximum) {
                throw new ArgumentException("Indexed texture requires a bounded RGBA32 palette.", nameof(asset));
            }
            return asset.PaletteColors.Length / 4;
        }

        /// <summary>Reads one little-endian word without relying on host CPU byte order.</summary>
        static int ReadWord(byte[] colors, int offset) {
            return colors[offset] | (colors[offset + 1] << 8);
        }

        /// <summary>Writes one packed word in generic little-endian byte order.</summary>
        static void WriteWord(byte[] colors, int offset, int value) {
            colors[offset] = (byte)value;
            colors[offset + 1] = (byte)(value >> 8);
        }

        /// <summary>Expands a stored unsigned channel to eight bits with nearest-integer rounding.</summary>
        static int ExpandBits(int value, int maximum) {
            return (value * 255 + maximum / 2) / maximum;
        }

        /// <summary>Reads a high-first nibble from a new byte-aligned I/IA row, skipping any odd-width row padding.</summary>
        static int ReadRowNibble(TextureAsset asset, int pixel) {
            int row = pixel / asset.Width;
            int column = pixel % asset.Width;
            return (asset.Colors[row * ((asset.Width + 1) / 2) + column / 2] >> ((column % 2 == 0) ? 4 : 0)) & 15;
        }

        /// <summary>Stores a high-first nibble in a new byte-aligned I/IA row; unused low nibbles remain zero.</summary>
        static void WriteRowNibble(byte[] colors, int width, int pixel, int value) {
            int row = pixel / width;
            int column = pixel % width;
            colors[row * ((width + 1) / 2) + column / 2] |= (byte)(value << ((column % 2 == 0) ? 4 : 0));
        }

        /// <summary>Writes one straight RGBA pixel after the format-specific channels have been expanded.</summary>
        static void SetPixel(byte[] rgba, int offset, int red, int green, int blue, int alpha) {
            rgba[offset] = (byte)red;
            rgba[offset + 1] = (byte)green;
            rgba[offset + 2] = (byte)blue;
            rgba[offset + 3] = (byte)alpha;
        }

        /// <summary>Computes rounded Rec.601 full-range luma from an RGBA pixel's RGB channels.</summary>
        static int GetIntensity(byte[] rgba, int offset) {
            return RoundByte(0.299 * rgba[offset] + 0.587 * rgba[offset + 1] + 0.114 * rgba[offset + 2]);
        }

        /// <summary>Quantizes independent source alpha by the declared precision without changing color channels.</summary>
        static int QuantizeAlpha(byte alpha, TextureAssetAlphaPrecision precision) {
            switch (precision) {
                case TextureAssetAlphaPrecision.Opaque:
                    return 255;
                case TextureAssetAlphaPrecision.Binary:
                    return alpha >= 128 ? 255 : 0;
                case TextureAssetAlphaPrecision.A4:
                    return (alpha >> 4) * 17;
                default:
                    return alpha;
            }
        }

        /// <summary>Rounds a double channel to the nearest byte, clamping conversion overshoot at the RGB/YUV gamut boundary.</summary>
        static int RoundByte(double value) {
            return (int)Math.Clamp(Math.Floor(value + 0.5), 0.0, 255.0);
        }

        /// <summary>Encodes BT.601 limited-range YUYV pairs with averaged chroma; an odd final pixel is repeated within its row.</summary>
        static void EncodeYuv(int width, int height, byte[] rgba, byte[] colors) {
            int stride = ((width + 1) / 2) * 4;
            for (int row = 0; row < height; row++) {
                for (int column = 0; column < width; column += 2) {
                    int first = (row * width + column) * 4;
                    int second = (row * width + Math.Min(column + 1, width - 1)) * 4;
                    int target = row * stride + (column / 2) * 4;
                    colors[target] = (byte)GetLimitedY(rgba, first);
                    colors[target + 2] = (byte)GetLimitedY(rgba, second);
                    colors[target + 1] = (byte)RoundByte((GetLimitedU(rgba, first) + GetLimitedU(rgba, second)) * 0.5);
                    colors[target + 3] = (byte)RoundByte((GetLimitedV(rgba, first) + GetLimitedV(rgba, second)) * 0.5);
                }
            }
        }

        /// <summary>Computes the limited-range luma channel, mapping RGB black to 16 and white to 235.</summary>
        static int GetLimitedY(byte[] rgba, int offset) {
            return RoundByte(16.0 + (65.481 * rgba[offset] + 128.553 * rgba[offset + 1] + 24.966 * rgba[offset + 2]) / 255.0);
        }

        /// <summary>Computes unrounded limited-range blue-difference chroma so pairs can average before quantization.</summary>
        static double GetLimitedU(byte[] rgba, int offset) {
            return 128.0 + (-37.797 * rgba[offset] - 74.203 * rgba[offset + 1] + 112.0 * rgba[offset + 2]) / 255.0;
        }

        /// <summary>Computes unrounded limited-range red-difference chroma so pairs can average before quantization.</summary>
        static double GetLimitedV(byte[] rgba, int offset) {
            return 128.0 + (112.0 * rgba[offset] - 93.786 * rgba[offset + 1] - 18.214 * rgba[offset + 2]) / 255.0;
        }

        /// <summary>Decodes BT.601 limited-range YUYV pairs, dropping the padded second pixel at odd row ends.</summary>
        static void DecodeYuv(TextureAsset asset, byte[] rgba) {
            int stride = ((asset.Width + 1) / 2) * 4;
            for (int row = 0; row < asset.Height; row++) {
                for (int column = 0; column < asset.Width; column++) {
                    int source = row * stride + (column / 2) * 4;
                    double y = asset.Colors[source + ((column % 2) * 2)] - 16.0;
                    double u = asset.Colors[source + 1] - 128.0;
                    double v = asset.Colors[source + 3] - 128.0;
                    SetPixel(rgba, (row * asset.Width + column) * 4,
                        RoundByte(1.1643835616438356 * y + 1.5960267857142858 * v),
                        RoundByte(1.1643835616438356 * y - 0.39176229009491365 * u - 0.8129676472377708 * v),
                        RoundByte(1.1643835616438356 * y + 2.017232142857143 * u), 255);
                }
            }
        }
    }
}
