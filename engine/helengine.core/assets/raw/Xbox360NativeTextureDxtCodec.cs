namespace helengine {
    /// <summary>Encodes and decodes linear Xenos DXT1, DXT3 and DXT5 blocks without decompressing cooked GPU storage.</summary>
    public static class Xbox360NativeTextureDxtCodec {
        /// <summary>Encodes edge-replicated 4x4 blocks; endpoint pairs are chosen from the most distant actual block colors.</summary>
        public static void Encode(byte[] rgba, int width, int height, Xbox360NativeTextureLayout layout,
            TextureAssetAlphaPrecision alpha, byte[] output, int offset) {
            byte[] block = new byte[64];
            byte[] palette = new byte[16];
            int[] alphaPalette = new int[8];
            int code = Xbox360NativeTexturePixelCodec.BaseCode(layout.Format.HardwareFormat);
            code = code == 18 ? 0x0c : code == 19 ? 0x0e : 0x0f;
            int blockBytes = code == 0x0c ? 8 : 16;
            for (int blockY = 0; blockY < layout.StorageHeight; blockY += 4) {
                for (int blockX = 0; blockX < layout.StorageWidth; blockX += 4) {
                    for (int pixel = 0; pixel < 16; pixel++) {
                        int source = (Math.Min(blockY + pixel / 4, height - 1) * width + Math.Min(blockX + pixel % 4, width - 1)) * 4;
                        int target = pixel * 4;
                        XboxNativeTexturePixelCodec.SetPixel(block, target, rgba[source], rgba[source + 1], rgba[source + 2],
                            XboxNativeTexturePixelCodec.QuantizeAlpha(rgba[source + 3], alpha));
                    }
                    int targetBlock = offset + (blockY / 4) * layout.PitchBytes + (blockX / 4) * blockBytes;
                    if (code == 0x0e) EncodeExplicitAlpha(block, output, targetBlock);
                    else if (code == 0x0f) EncodeInterpolatedAlpha(block, output, targetBlock, alphaPalette);
                    EncodeColors(block, code == 0x0c, output, targetBlock + (code == 0x0c ? 0 : 8), palette);
                }
            }
        }

        /// <summary>Decodes native blocks to the authored image bounds, ignoring replicated storage padding.</summary>
        public static void Decode(byte[] input, int offset, Xbox360NativeTextureLayout layout, byte[] rgba) {
            byte[] palette = new byte[16];
            int[] alphaPalette = new int[8];
            int code = Xbox360NativeTexturePixelCodec.BaseCode(layout.Format.HardwareFormat);
            code = code == 18 ? 0x0c : code == 19 ? 0x0e : 0x0f;
            int blockBytes = code == 0x0c ? 8 : 16;
            for (int blockY = 0; blockY < layout.StorageHeight; blockY += 4) {
                for (int blockX = 0; blockX < layout.StorageWidth; blockX += 4) {
                    int block = offset + (blockY / 4) * layout.PitchBytes + (blockX / 4) * blockBytes;
                    int colors = block + (code == 0x0c ? 0 : 8);
                    int first = XboxNativeTexturePixelCodec.ReadWord(input, colors);
                    int second = XboxNativeTexturePixelCodec.ReadWord(input, colors + 2);
                    BuildColorPalette(first, second, code == 0x0c && first <= second, palette);
                    uint colorIndices = XboxNativeTexturePixelCodec.ReadUInt32(input, colors + 4);
                    ulong alphaIndices = 0;
                    if (code == 0x0f) {
                        BuildAlphaPalette(input[block], input[block + 1], alphaPalette);
                        for (int index = 0; index < 6; index++) alphaIndices |= (ulong)input[block + 2 + index] << (index * 8);
                    }
                    for (int pixel = 0; pixel < 16; pixel++) {
                        int x = blockX + pixel % 4;
                        int y = blockY + pixel / 4;
                        if (x >= layout.RealWidth || y >= layout.RealHeight) continue;
                        int selected = (int)((colorIndices >> (pixel * 2)) & 3) * 4;
                        int alpha = palette[selected + 3];
                        if (code == 0x0e) alpha = ((input[block + pixel / 2] >> ((pixel % 2) * 4)) & 15) * 17;
                        else if (code == 0x0f) alpha = alphaPalette[(int)((alphaIndices >> (pixel * 3)) & 7)];
                        XboxNativeTexturePixelCodec.SetPixel(rgba, (y * layout.RealWidth + x) * 4,
                            palette[selected], palette[selected + 1], palette[selected + 2], alpha);
                    }
                }
            }
        }

        /// <summary>Selects distant RGB endpoints, orders them for the DXT1 coverage mode and chooses the nearest representable color.</summary>
        static void EncodeColors(byte[] block, bool dxt1, byte[] output, int target, byte[] palette) {
            int firstPixel = 0;
            int secondPixel = 0;
            int greatestDistance = -1;
            bool transparent = false;
            for (int first = 0; first < 16; first++) {
                if (dxt1 && block[first * 4 + 3] < 128) {
                    transparent = true;
                    continue;
                }
                for (int second = first; second < 16; second++) {
                    if (dxt1 && block[second * 4 + 3] < 128) continue;
                    int distance = GetColorDistance(block, first * 4, block, second * 4);
                    if (distance > greatestDistance) {
                        greatestDistance = distance;
                        firstPixel = first;
                        secondPixel = second;
                    }
                }
            }
            int firstWord = XboxNativeTexturePixelCodec.PackRgb565(block[firstPixel * 4], block[firstPixel * 4 + 1], block[firstPixel * 4 + 2]);
            int secondWord = XboxNativeTexturePixelCodec.PackRgb565(block[secondPixel * 4], block[secondPixel * 4 + 1], block[secondPixel * 4 + 2]);
            int low = Math.Min(firstWord, secondWord);
            int high = Math.Max(firstWord, secondWord);
            int firstEndpoint = transparent ? low : high;
            int secondEndpoint = transparent ? high : low;
            bool threeColor = dxt1 && firstEndpoint <= secondEndpoint;
            BuildColorPalette(firstEndpoint, secondEndpoint, threeColor, palette);
            XboxNativeTexturePixelCodec.WriteWord(output, target, firstEndpoint);
            XboxNativeTexturePixelCodec.WriteWord(output, target + 2, secondEndpoint);
            uint indices = 0;
            for (int pixel = 0; pixel < 16; pixel++) {
                int selected = 0;
                if (dxt1 && block[pixel * 4 + 3] < 128) {
                    selected = 3;
                } else {
                    int closest = int.MaxValue;
                    for (int entry = 0; entry < (threeColor ? 3 : 4); entry++) {
                        int distance = GetColorDistance(block, pixel * 4, palette, entry * 4);
                        if (distance < closest) {
                            closest = distance;
                            selected = entry;
                        }
                    }
                }
                indices |= (uint)selected << (pixel * 2);
            }
            XboxNativeTexturePixelCodec.WriteUInt32(output, target + 4, indices);
        }

        /// <summary>Builds the BC1 RGB palette with bit-replicated endpoints and integer interpolation.</summary>
        static void BuildColorPalette(int first, int second, bool threeColor, byte[] palette) {
            XboxNativeTexturePixelCodec.DecodeRgb565(first, palette, 0);
            XboxNativeTexturePixelCodec.DecodeRgb565(second, palette, 4);
            for (int channel = 0; channel < 3; channel++) {
                palette[8 + channel] = (byte)(threeColor ? (palette[channel] + palette[4 + channel]) / 2
                    : (2 * palette[channel] + palette[4 + channel]) / 3);
                palette[12 + channel] = threeColor ? (byte)0 : (byte)((palette[channel] + 2 * palette[4 + channel]) / 3);
            }
            palette[11] = 255;
            palette[15] = threeColor ? (byte)0 : (byte)255;
        }

        /// <summary>Writes each DXT3 alpha nibble low-first within its byte.</summary>
        static void EncodeExplicitAlpha(byte[] block, byte[] output, int target) {
            for (int pair = 0; pair < 8; pair++) {
                output[target + pair] = (byte)((block[pair * 8 + 3] >> 4) | (block[pair * 8 + 7] & 0xf0));
            }
        }

        /// <summary>Selects the closest DXT5 interpolated alpha entry using the block's alpha extrema.</summary>
        static void EncodeInterpolatedAlpha(byte[] block, byte[] output, int target, int[] palette) {
            int low = 255;
            int high = 0;
            for (int pixel = 0; pixel < 16; pixel++) {
                low = Math.Min(low, block[pixel * 4 + 3]);
                high = Math.Max(high, block[pixel * 4 + 3]);
            }
            output[target] = (byte)high;
            output[target + 1] = (byte)low;
            BuildAlphaPalette(high, low, palette);
            ulong indices = 0;
            for (int pixel = 0; pixel < 16; pixel++) {
                int best = 0;
                int error = int.MaxValue;
                for (int entry = 0; entry < 8; entry++) {
                    int distance = Math.Abs(block[pixel * 4 + 3] - palette[entry]);
                    if (distance < error) {
                        best = entry;
                        error = distance;
                    }
                }
                indices |= (ulong)best << (pixel * 3);
            }
            for (int index = 0; index < 6; index++) output[target + index + 2] = (byte)(indices >> (index * 8));
        }

        /// <summary>Builds the BC3 alpha table including the explicit zero and one entries for ascending endpoints.</summary>
        static void BuildAlphaPalette(int first, int second, int[] palette) {
            palette[0] = first;
            palette[1] = second;
            if (first > second) {
                for (int index = 2; index < 8; index++) palette[index] = ((8 - index) * first + (index - 1) * second) / 7;
            } else {
                for (int index = 2; index < 6; index++) palette[index] = ((6 - index) * first + (index - 1) * second) / 5;
                palette[6] = 0;
                palette[7] = 255;
            }
        }

        /// <summary>Measures squared RGB error without giving alpha a second influence on color endpoints.</summary>
        static int GetColorDistance(byte[] first, int firstOffset, byte[] second, int secondOffset) {
            int red = first[firstOffset] - second[secondOffset];
            int green = first[firstOffset + 1] - second[secondOffset + 1];
            int blue = first[firstOffset + 2] - second[secondOffset + 2];
            return red * red + green * green + blue * blue;
        }
    }
}
