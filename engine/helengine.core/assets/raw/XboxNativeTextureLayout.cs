namespace helengine {
    /// <summary>Computes the canonical single-level storage required by a version-one native Xbox texture.</summary>
    public sealed class XboxNativeTextureLayout {
        /// <summary>Constructs checked row, block and palette sizes; all native GPU textures are limited to 4096 pixels per axis.</summary>
        public XboxNativeTextureLayout(XboxNativeTextureFormatDefinition format, int width, int height, int paletteEntries) {
            if (format == null) throw new ArgumentNullException(nameof(format));
            if (width <= 0 || height <= 0 || width > 4096 || height > 4096) {
                throw new ArgumentOutOfRangeException(nameof(width), "Xbox textures require positive dimensions no greater than 4096.");
            }
            Format = format;
            RealWidth = width;
            RealHeight = height;
            StorageWidth = format.IsLinear ? width : GetPowerOfTwo(width);
            StorageHeight = format.IsLinear ? height : GetPowerOfTwo(height);
            if (format.IsCompressed) {
                StorageWidth = Math.Max(StorageWidth, 4);
                StorageHeight = Math.Max(StorageHeight, 4);
                TexelLength = checked(StorageWidth / 4 * (StorageHeight / 4) * (format.HardwareFormat == 0x0c ? 8 : 16));
            } else if (format.IsLinear) {
                int rowBytes = format.IsYuv ? checked((width + 1) / 2 * 4) : checked(width * format.BytesPerTexel);
                PitchBytes = (rowBytes + 63) & ~63;
                TexelLength = checked(PitchBytes * height);
            } else {
                TexelLength = checked(StorageWidth * StorageHeight * format.BytesPerTexel);
            }
            if (format.IsPaletted) {
                if (paletteEntries < 1 || paletteEntries > 256) throw new ArgumentException("Xbox P8 requires between one and 256 palette entries.");
                PaletteEntryCount = paletteEntries <= 32 ? 32 : paletteEntries <= 64 ? 64 : paletteEntries <= 128 ? 128 : 256;
                PaletteLength = PaletteEntryCount * 4;
            } else if (paletteEntries != 0) {
                throw new ArgumentException("Only Xbox P8 textures may carry a palette.");
            }
        }

        /// <summary>Gets the hardware storage and sampling description.</summary>
        public XboxNativeTextureFormatDefinition Format { get; }
        /// <summary>Gets the authored width retained outside storage padding.</summary>
        public int RealWidth { get; }
        /// <summary>Gets the authored height retained outside storage padding.</summary>
        public int RealHeight { get; }
        /// <summary>Gets the padded power-of-two width, or exact linear width.</summary>
        public int StorageWidth { get; }
        /// <summary>Gets the padded power-of-two height, or exact linear height.</summary>
        public int StorageHeight { get; }
        /// <summary>Gets the 64-byte-aligned linear row pitch, or zero for Morton texels and DXT blocks.</summary>
        public int PitchBytes { get; }
        /// <summary>Gets the native texel or block byte count, excluding the header and palette.</summary>
        public int TexelLength { get; }
        /// <summary>Gets the hardware palette size: zero, 32, 64, 128 or 256.</summary>
        public int PaletteEntryCount { get; }
        /// <summary>Gets the byte count of the separate BGRA8888 palette.</summary>
        public int PaletteLength { get; }

        /// <summary>Computes rectangular Morton addressing by interleaving only the coordinate bits present on each axis.</summary>
        public int GetTexelOffset(int x, int y) {
            if (Format.IsLinear) return y * PitchBytes + x * Format.BytesPerTexel;
            int result = 0;
            int destinationBit = 1;
            for (int sourceBit = 1; sourceBit < StorageWidth || sourceBit < StorageHeight; sourceBit <<= 1) {
                if (sourceBit < StorageWidth) {
                    if ((x & sourceBit) != 0) result |= destinationBit;
                    destinationBit <<= 1;
                }
                if (sourceBit < StorageHeight) {
                    if ((y & sourceBit) != 0) result |= destinationBit;
                    destinationBit <<= 1;
                }
            }
            return result * Format.BytesPerTexel;
        }

        /// <summary>Rounds one positive checked dimension up to the next representable storage power of two.</summary>
        static int GetPowerOfTwo(int dimension) {
            int result = 1;
            while (result < dimension) result <<= 1;
            return result;
        }
    }
}
