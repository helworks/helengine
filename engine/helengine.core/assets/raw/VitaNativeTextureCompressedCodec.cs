namespace helengine {
    /// <summary>Routes native GXM compression families without expanding their serialized GPU storage.</summary>
    public static class VitaNativeTextureCompressedCodec {
        /// <summary>Encodes borrowed source pixels into the selected native compression family.</summary>
        public static void Encode(byte[] rgba, VitaNativeTextureLayout layout, TextureAssetAlphaPrecision alpha, byte[] output, int offset) {
            if (layout.Format.IsPvrtc) VitaNativeTexturePvrtcCodec.Encode(rgba, layout, alpha, output, offset);
            else if (layout.Format.BaseFormat == 0x84) VitaNativeTextureEtcCodec.Encode(rgba, layout, output, offset);
            else VitaNativeTextureBcCodec.Encode(rgba, layout, alpha, output, offset);
        }
        /// <summary>Decodes native compressed bytes using their real GPU interpolation and channel swizzle.</summary>
        public static void Decode(byte[] input, int offset, VitaNativeTextureLayout layout, byte[] rgba) {
            if (layout.Format.IsPvrtc) VitaNativeTexturePvrtcCodec.Decode(input, offset, layout, rgba);
            else if (layout.Format.BaseFormat == 0x84) VitaNativeTextureEtcCodec.Decode(input, offset, layout, rgba);
            else VitaNativeTextureBcCodec.Decode(input, offset, layout, rgba);
        }
    }
}
