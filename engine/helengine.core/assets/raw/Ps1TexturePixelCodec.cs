namespace helengine {
    /// <summary>Converts between straight RGBA pixels and the PS1's little-endian BGR555 texture words.</summary>
    public static class Ps1TexturePixelCodec {
        /// <summary>Packs a visible texel with the STP bit set so opaque black remains distinct from transparent zero.</summary>
        /// <param name="red">Eight-bit red channel.</param>
        /// <param name="green">Eight-bit green channel.</param>
        /// <param name="blue">Eight-bit blue channel.</param>
        /// <param name="alpha">Zero discards the texel; visible texels participate in primitive blending.</param>
        /// <returns>A native PS1 texture word.</returns>
        public static ushort Pack(byte red, byte green, byte blue, byte alpha) {
            return alpha == 0 ? (ushort)0 : (ushort)((red >> 3) | ((green >> 3) << 5) | ((blue >> 3) << 10) | 0x8000);
        }

        /// <summary>Expands a five-bit color channel using bit replication.</summary>
        /// <param name="channel">Five-bit channel value.</param>
        /// <returns>The corresponding eight-bit channel.</returns>
        public static byte ExpandChannel(int channel) {
            return (byte)((channel << 3) | (channel >> 2));
        }

        /// <summary>Decodes a native word; only zero is transparent, while STP controls GPU blending separately.</summary>
        /// <param name="word">Native little-endian BGR555 word.</param>
        /// <param name="destination">RGBA buffer receiving the decoded texel.</param>
        /// <param name="offset">Start of the four destination bytes.</param>
        public static void DecodePixel(ushort word, byte[] destination, int offset) {
            destination[offset] = ExpandChannel(word & 31);
            destination[offset + 1] = ExpandChannel((word >> 5) & 31);
            destination[offset + 2] = ExpandChannel((word >> 10) & 31);
            destination[offset + 3] = word == 0 ? (byte)0 : byte.MaxValue;
        }

        /// <summary>Encodes a validated RGBA image to row-major native PS1 words with binary or opaque coverage.</summary>
        /// <param name="asset">Source RGBA32 image.</param>
        /// <param name="alphaPrecision">Binary coverage or forced opaque coverage.</param>
        /// <returns>A new asset retaining source identity and dimensions.</returns>
        [NativeOwnedReturn]
        public static TextureAsset Encode(TextureAsset asset, TextureAssetAlphaPrecision alphaPrecision) {
            if (asset == null) {
                throw new ArgumentNullException(nameof(asset));
            } else if (asset.ColorFormat != TextureAssetColorFormat.Rgba32 || asset.Width == 0 || asset.Height == 0
                || asset.Colors == null || asset.Colors.Length != checked(asset.Width * asset.Height * 4)) {
                throw new InvalidOperationException("PS1 direct-color encoding requires a complete positive-sized RGBA32 image.");
            } else if (alphaPrecision != TextureAssetAlphaPrecision.Binary && alphaPrecision != TextureAssetAlphaPrecision.Opaque) {
                throw new InvalidOperationException("PS1 textures support binary or opaque coverage.");
            }

            byte[] words = new byte[checked(asset.Width * asset.Height * 2)];
            for (int offset = 0; offset < words.Length; offset += 2) {
                int sourceOffset = offset * 2;
                byte alpha = alphaPrecision == TextureAssetAlphaPrecision.Opaque || asset.Colors[sourceOffset + 3] >= 128
                    ? byte.MaxValue : (byte)0;
                ushort word = Pack(asset.Colors[sourceOffset], asset.Colors[sourceOffset + 1], asset.Colors[sourceOffset + 2], alpha);
                words[offset] = (byte)word;
                words[offset + 1] = (byte)(word >> 8);
            }
            return new TextureAsset {
                Id = asset.Id,
                RuntimeAssetId = asset.RuntimeAssetId,
                AuthoringAssetId = asset.AuthoringAssetId,
                IsEngineOwned = asset.IsEngineOwned,
                Width = asset.Width,
                Height = asset.Height,
                ColorFormat = TextureAssetColorFormat.Ps1Bgr555,
                AlphaPrecision = alphaPrecision,
                Colors = words,
                PaletteColors = new byte[0]
            };
        }

        /// <summary>Expands a native PS1 direct-color asset for editor preview or further processing.</summary>
        /// <param name="asset">Positive-sized asset with exactly two bytes per texel.</param>
        /// <returns>Straight RGBA32 bytes with binary coverage.</returns>
        [NativeOwnedReturn]
        public static byte[] Decode(TextureAsset asset) {
            if (asset == null) {
                throw new ArgumentNullException(nameof(asset));
            } else if (asset.ColorFormat != TextureAssetColorFormat.Ps1Bgr555 || asset.Width == 0 || asset.Height == 0
                || asset.Colors == null || asset.Colors.Length != checked(asset.Width * asset.Height * 2)) {
                throw new InvalidOperationException("PS1 direct-color decoding requires exactly two bytes per texel.");
            }
            byte[] rgba = new byte[checked(asset.Width * asset.Height * 4)];
            for (int offset = 0; offset < asset.Colors.Length; offset += 2) {
                DecodePixel((ushort)(asset.Colors[offset] | (asset.Colors[offset + 1] << 8)), rgba, offset * 2);
            }
            return rgba;
        }
    }
}
