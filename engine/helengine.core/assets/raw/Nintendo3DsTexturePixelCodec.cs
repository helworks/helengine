namespace helengine {
    /// <summary>Packs PICA component words and expands native luminance, alpha and HILO channel semantics.</summary>
    public static class Nintendo3DsTexturePixelCodec {
        /// <summary>Packs one RGBA source into a native little-endian word; red occupies the high color bits.</summary>
        public static uint Pack(byte[] rgba, int offset, int code, TextureAssetAlphaPrecision alpha) {
            int r = rgba[offset], g = rgba[offset + 1], b = rgba[offset + 2];
            int a = alpha == TextureAssetAlphaPrecision.Opaque ? 255 : rgba[offset + 3];
            int luminance = (r * 299 + g * 587 + b * 114 + 500) / 1000;
            if (code == 0) return (uint)r << 24 | (uint)g << 16 | (uint)b << 8 | (uint)a;
            if (code == 1) return (uint)(r << 16 | g << 8 | b);
            if (code == 2) return (uint)(Quantize(r, 31) << 11 | Quantize(g, 31) << 6 | Quantize(b, 31) << 1 | (a >= 128 ? 1 : 0));
            if (code == 3) return (uint)(Quantize(r, 31) << 11 | Quantize(g, 63) << 5 | Quantize(b, 31));
            if (code == 4) return (uint)(Quantize(r, 15) << 12 | Quantize(g, 15) << 8 | Quantize(b, 15) << 4 | Quantize(a, 15));
            if (code == 5) return (uint)(luminance << 8 | a);
            if (code == 6) return (uint)(r << 8 | g);
            if (code == 7) return (uint)luminance;
            if (code == 8) return (uint)a;
            if (code == 9) return (uint)(Quantize(luminance, 15) << 4 | Quantize(a, 15));
            return (uint)Quantize(code == 10 ? luminance : a, 15);
        }
        /// <summary>Expands native channels to RGBA; alpha-only hardware textures have zero RGB.</summary>
        public static void Unpack(uint value, int code, byte[] rgba, int offset) {
            int r, g, b, a = 255;
            if (code == 0) { r = (int)(value >> 24); g = (int)(value >> 16 & 255); b = (int)(value >> 8 & 255); a = (int)(value & 255); }
            else if (code == 1) { r = (int)(value >> 16); g = (int)(value >> 8 & 255); b = (int)(value & 255); }
            else if (code == 2) { r = Expand(value >> 11, 5); g = Expand(value >> 6, 5); b = Expand(value >> 1, 5); a = (int)(value & 1) * 255; }
            else if (code == 3) { r = Expand(value >> 11, 5); g = Expand(value >> 5, 6); b = Expand(value, 5); }
            else if (code == 4) { r = Expand(value >> 12, 4); g = Expand(value >> 8, 4); b = Expand(value >> 4, 4); a = Expand(value, 4); }
            else if (code == 6) { r = (int)(value >> 8); g = (int)(value & 255); b = 0; }
            else if (code == 8 || code == 11) { r = 0; g = 0; b = 0; a = code == 8 ? (int)value : Expand(value, 4); }
            else {
                r = code == 5 ? (int)(value >> 8) : code == 9 ? Expand(value >> 4, 4) : code == 10 ? Expand(value, 4) : (int)value;
                g = r; b = r;
                if (code == 5) a = (int)(value & 255);
                else if (code == 9) a = Expand(value, 4);
            }
            rgba[offset] = (byte)r; rgba[offset + 1] = (byte)g; rgba[offset + 2] = (byte)b; rgba[offset + 3] = (byte)a;
        }
        /// <summary>Quantizes a source channel to the nearest representable unsigned component.</summary>
        static int Quantize(int value, int maximum) => (value * maximum + 127) / 255;
        /// <summary>Replicates high component bits exactly as native texture sampling does.</summary>
        public static int Expand(uint value, int bits) {
            int channel = (int)(value & ((1u << bits) - 1));
            return bits == 5 ? channel << 3 | channel >> 2 : bits == 6 ? channel << 2 | channel >> 4 : channel * 17;
        }
    }
}
