namespace helengine {
    /// <summary>Implements Xenos two-channel and alpha-only block formats using their actual compact GPU representation.</summary>
    public static class Xbox360NativeTextureBlockCodec {
        /// <summary>Encodes complete edge-replicated blocks while leaving canonical row padding zero.</summary>
        public static void Encode(byte[] rgba, Xbox360NativeTextureLayout layout, TextureAssetAlphaPrecision alpha, byte[] output, int offset) {
            byte[] block = new byte[64]; int[] palette = new int[8];
            int code = layout.Format.HardwareFormat;
            for (int blockY = 0; blockY < layout.StorageHeight; blockY += 4) {
                for (int blockX = 0; blockX < (layout.RealWidth + 3) / 4 * 4; blockX += 4) {
                    for (int pixel = 0; pixel < 16; pixel++) {
                        int source = (Math.Min(blockY + pixel / 4, layout.RealHeight - 1) * layout.RealWidth + Math.Min(blockX + pixel % 4, layout.RealWidth - 1)) * 4;
                        XboxNativeTexturePixelCodec.SetPixel(block, pixel * 4, rgba[source], rgba[source + 1], rgba[source + 2], XboxNativeTexturePixelCodec.QuantizeAlpha(rgba[source + 3], alpha));
                    }
                    int target = offset + blockY / 4 * layout.PitchBytes + blockX / 4 * layout.Format.BytesPerBlock;
                    if (code == 49) {
                        EncodeChannel(block, 0, output, target, palette); EncodeChannel(block, 1, output, target + 8, palette);
                    } else if (code == 59) EncodeChannel(block, 3, output, target, palette);
                    else if (code == 60) EncodeCtx(block, output, target);
                    else {
                        for (int pixel = 0; pixel < 16; pixel++) {
                            int source = pixel * 4;
                            int nibble = code == 58 ? block[source + 3] >> 4 :
                                (block[source] >= 128 ? 8 : 0) | (block[source + 1] >= 128 ? 4 : 0) | (block[source + 2] >= 128 ? 2 : 0) | (block[source + 3] >= 128 ? 1 : 0);
                            output[target + pixel / 2] |= (byte)(nibble << (pixel % 2 * 4));
                        }
                    }
                }
            }
        }
        /// <summary>Decodes only authored bounds with canonical RG01, white-alpha or RGBA sampling semantics.</summary>
        public static void Decode(byte[] input, int offset, Xbox360NativeTextureLayout layout, byte[] rgba) {
            int[] firstPalette = new int[8]; int[] secondPalette = new int[8];
            int code = layout.Format.HardwareFormat;
            for (int y = 0; y < layout.RealHeight; y++) {
                for (int x = 0; x < layout.RealWidth; x++) {
                    int block = offset + y / 4 * layout.PitchBytes + x / 4 * layout.Format.BytesPerBlock;
                    int pixel = y % 4 * 4 + x % 4; int target = (y * layout.RealWidth + x) * 4;
                    if (code == 49 || code == 59) {
                        int first = DecodeChannel(input, block, pixel, firstPalette);
                        int second = code == 49 ? DecodeChannel(input, block + 8, pixel, secondPalette) : 0;
                        XboxNativeTexturePixelCodec.SetPixel(rgba, target, code == 49 ? first : 255, code == 49 ? second : 255, code == 49 ? 0 : 255, code == 49 ? 255 : first);
                    } else if (code == 60) {
                        uint indices = XboxNativeTexturePixelCodec.ReadUInt32(input, block + 4);
                        int selected = (int)((indices >> (pixel * 2)) & 3);
                        int red = Interpolate(input[block + 1], input[block + 3], selected);
                        int green = Interpolate(input[block], input[block + 2], selected);
                        XboxNativeTexturePixelCodec.SetPixel(rgba, target, red, green, 0, 255);
                    } else {
                        int nibble = (input[block + pixel / 2] >> (pixel % 2 * 4)) & 15;
                        if (code == 58) XboxNativeTexturePixelCodec.SetPixel(rgba, target, 255, 255, 255, nibble * 17);
                        else XboxNativeTexturePixelCodec.SetPixel(rgba, target, (nibble & 8) != 0 ? 255 : 0, (nibble & 4) != 0 ? 255 : 0, (nibble & 2) != 0 ? 255 : 0, (nibble & 1) != 0 ? 255 : 0);
                    }
                }
            }
        }
        /// <summary>Builds the BC4/BC5 unsigned endpoint interpolation palette.</summary>
        static void BuildPalette(int first, int second, int[] palette) {
            palette[0] = first; palette[1] = second;
            if (first > second) {
                for (int index = 1; index <= 6; index++) palette[index + 1] = ((7 - index) * first + index * second) / 7;
            } else {
                for (int index = 1; index <= 4; index++) palette[index + 1] = ((5 - index) * first + index * second) / 5;
                palette[6] = 0; palette[7] = 255;
            }
        }
        /// <summary>Encodes one BC4 channel using extrema and nearest interpolated samples.</summary>
        static void EncodeChannel(byte[] block, int channel, byte[] output, int target, int[] palette) {
            int minimum = 255; int maximum = 0;
            for (int pixel = 0; pixel < 16; pixel++) { minimum = Math.Min(minimum, block[pixel * 4 + channel]); maximum = Math.Max(maximum, block[pixel * 4 + channel]); }
            output[target] = (byte)maximum; output[target + 1] = (byte)minimum; BuildPalette(maximum, minimum, palette);
            ulong indices = 0;
            for (int pixel = 0; pixel < 16; pixel++) {
                int selected = 0; int distance = int.MaxValue;
                for (int index = 0; index < 8; index++) {
                    int candidate = Math.Abs(block[pixel * 4 + channel] - palette[index]);
                    if (candidate < distance) { selected = index; distance = candidate; }
                }
                indices |= (ulong)selected << (pixel * 3);
            }
            for (int index = 0; index < 6; index++) output[target + 2 + index] = (byte)(indices >> (index * 8));
        }
        /// <summary>Reads one BC4 sample from compact six-byte index storage.</summary>
        static int DecodeChannel(byte[] input, int source, int pixel, int[] palette) {
            BuildPalette(input[source], input[source + 1], palette); ulong indices = 0;
            for (int index = 0; index < 6; index++) indices |= (ulong)input[source + 2 + index] << (index * 8);
            return palette[(int)((indices >> (pixel * 3)) & 7)];
        }
        /// <summary>Interpolates a CTX1 component with the four-color DXT weighting.</summary>
        static int Interpolate(int first, int second, int index) { return index == 0 ? first : index == 1 ? second : index == 2 ? (first * 2 + second) / 3 : (first + second * 2) / 3; }
        /// <summary>Encodes CTX1 using the farthest actual red/green pair and nearest two-component interpolation.</summary>
        static void EncodeCtx(byte[] block, byte[] output, int target) {
            int first = 0; int second = 0; int greatest = -1;
            for (int left = 0; left < 16; left++) {
                for (int right = left; right < 16; right++) {
                    int red = block[left * 4] - block[right * 4]; int green = block[left * 4 + 1] - block[right * 4 + 1];
                    int distance = red * red + green * green;
                    if (distance > greatest) { greatest = distance; first = left * 4; second = right * 4; }
                }
            }
            output[target] = block[first + 1]; output[target + 1] = block[first]; output[target + 2] = block[second + 1]; output[target + 3] = block[second];
            uint indices = 0;
            for (int pixel = 0; pixel < 16; pixel++) {
                int selected = 0; int distance = int.MaxValue;
                for (int entry = 0; entry < 4; entry++) {
                    int red = block[pixel * 4] - Interpolate(block[first], block[second], entry);
                    int green = block[pixel * 4 + 1] - Interpolate(block[first + 1], block[second + 1], entry);
                    int candidate = red * red + green * green;
                    if (candidate < distance) { distance = candidate; selected = entry; }
                }
                indices |= (uint)selected << (pixel * 2);
            }
            XboxNativeTexturePixelCodec.WriteUInt32(output, target + 4, indices);
        }
    }
}
