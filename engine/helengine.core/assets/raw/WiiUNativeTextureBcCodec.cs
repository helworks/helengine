namespace helengine {
    /// <summary>Reuses the engine's validated standard BC codec while adapting compact GX2 row pitch and sRGB sampling.</summary>
    public static class WiiUNativeTextureBcCodec {
        /// <summary>Fits BC endpoints from borrowed source RGBA while preserving native GPU compression.</summary>
        public static void Encode(byte[] rgba, WiiUNativeTextureLayout layout, TextureAssetAlphaPrecision alpha, byte[] output, int start) {
            VitaNativeTextureLayout blockLayout = CreateBlockLayout(layout);
            try { EncodeWithLayout(rgba, layout, alpha, output, start, blockLayout); }
            finally { NativeOwnership.Release(ref blockLayout); }
        }
        /// <summary>Owns temporary physical pixels independently from compressed output storage.</summary>
        static void EncodeWithLayout(byte[] rgba, WiiUNativeTextureLayout layout, TextureAssetAlphaPrecision alpha, byte[] output, int start, VitaNativeTextureLayout blockLayout) {
            byte[] physical = new byte[64];
            try { EncodeBlocks(rgba, layout, alpha, output, start, blockLayout, physical); }
            finally { NativeOwnership.Release(ref physical); }
        }
        /// <summary>Copies compact block rows from the existing validated scanline BC encoder.</summary>
        static void EncodeBlocks(byte[] rgba, WiiUNativeTextureLayout layout, TextureAssetAlphaPrecision alpha, byte[] output, int start, VitaNativeTextureLayout blockLayout, byte[] physical) {
            byte[] blocks = new byte[blockLayout.TexelLength];
            try {
                for (int y = 0; y < layout.StorageHeight; y += 4) for (int x = 0; x < layout.StorageWidth; x += 4) {
                    for (int pixel = 0; pixel < 16; pixel++) {
                        int source = (Math.Min(y + pixel / 4, layout.RealHeight - 1) * layout.RealWidth + Math.Min(x + pixel % 4, layout.RealWidth - 1)) * 4;
                        for (int channel = 0; channel < 4; channel++) physical[pixel * 4 + channel] = layout.Format.NumericKind == WiiUNativeTextureNumericKind.Srgb && channel < 3 ? WiiUNativeTextureSrgbCodec.Encode(rgba[source + channel]) : rgba[source + channel];
                    }
                    VitaNativeTextureBcCodec.Encode(physical, blockLayout, alpha, blocks, 0); int target = start + y / 4 * layout.PitchBytes + x / 4 * layout.Format.BytesPerBlock;
                    for (int index = 0; index < layout.Format.BytesPerBlock; index++) output[target + index] = blocks[index];
                }
            }
            finally { NativeOwnership.Release(ref blocks); }
        }
        /// <summary>Reconstructs BC samples and applies EOTF to RGB only for sRGB GX2 surfaces.</summary>
        public static void Decode(byte[] input, int start, WiiUNativeTextureLayout layout, byte[] rgba) {
            VitaNativeTextureLayout blockLayout = CreateBlockLayout(layout);
            try { DecodeWithLayout(input, start, layout, rgba, blockLayout); }
            finally { NativeOwnership.Release(ref blockLayout); }
        }
        /// <summary>Owns one sampled block separately from compressed temporary storage.</summary>
        static void DecodeWithLayout(byte[] input, int start, WiiUNativeTextureLayout layout, byte[] rgba, VitaNativeTextureLayout blockLayout) {
            byte[] decoded = new byte[64];
            try { DecodeBlocks(input, start, layout, rgba, blockLayout, decoded); }
            finally { NativeOwnership.Release(ref decoded); }
        }
        /// <summary>Adapts compact block rows without imposing another platform's image dimension limit.</summary>
        static void DecodeBlocks(byte[] input, int start, WiiUNativeTextureLayout layout, byte[] rgba, VitaNativeTextureLayout blockLayout, byte[] decoded) {
            byte[] blocks = new byte[blockLayout.TexelLength];
            try {
                for (int y = 0; y < layout.StorageHeight; y += 4) for (int x = 0; x < layout.StorageWidth; x += 4) {
                    int source = start + y / 4 * layout.PitchBytes + x / 4 * layout.Format.BytesPerBlock;
                    for (int index = 0; index < layout.Format.BytesPerBlock; index++) blocks[index] = input[source + index];
                    VitaNativeTextureBcCodec.Decode(blocks, 0, blockLayout, decoded);
                    for (int pixel = 0; pixel < 16; pixel++) {
                        int tx = x + pixel % 4; int ty = y + pixel / 4; if (tx >= layout.RealWidth || ty >= layout.RealHeight) continue;
                        int target = (ty * layout.RealWidth + tx) * 4;
                        for (int channel = 0; channel < 4; channel++) rgba[target + channel] = layout.Format.NumericKind == WiiUNativeTextureNumericKind.Srgb && channel < 3 ? WiiUNativeTextureSrgbCodec.Decode(decoded[pixel * 4 + channel]) : decoded[pixel * 4 + channel];
                    }
                }
            } finally { NativeOwnership.Release(ref blocks); }
        }
        /// <summary>Creates an adapter over the existing exact BC family; no Vita container or channel alias is serialized.</summary>
        [NativeOwnedReturn]
        static VitaNativeTextureLayout CreateBlockLayout(WiiUNativeTextureLayout layout) {
            int code = layout.Format.BaseFormat; uint hardware = (uint)(code == 49 ? 0x85 : code == 50 ? 0x86 : code == 51 ? 0x87 : code == 52 ? layout.Format.NumericKind == WiiUNativeTextureNumericKind.SignedNormalized ? 0x89 : 0x88 : layout.Format.NumericKind == WiiUNativeTextureNumericKind.SignedNormalized ? 0x8b : 0x8a) << 24;
            if (!VitaNativeTextureFormatCatalog.TryGetHardwareFormat(hardware, out VitaNativeTextureFormatDefinition format)) throw new InvalidOperationException("Required standard BC block codec is unavailable.");
            return new VitaNativeTextureLayout(format, 4, 4, VitaNativeTextureLayoutType.Linear);
        }
    }
}
