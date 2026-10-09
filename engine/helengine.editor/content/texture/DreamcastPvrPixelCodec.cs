namespace helengine.editor {
    /// <summary>Converts Dreamcast PVR words and addresses independently of host image libraries.</summary>
    public static class DreamcastPvrPixelCodec {
        /// <summary>Estimates GPU texel and palette storage for a published Dreamcast native format after source resizing.</summary>
        public static bool TryCalculateMemory(string formatId, int width, int height, out long bytes) {
            bytes = 0;
            if (string.IsNullOrEmpty(formatId) || width < 1 || height < 1 || width > 512 || height > 512) return false;
            int separator = formatId.IndexOf('_');
            string name = separator < 0 ? formatId : formatId.Substring(0, separator);
            string layout = separator < 0 ? string.Empty : formatId.Substring(separator + 1);
            int format = name switch { "DcArgb1555" => 0, "DcRgb565" => 1, "DcArgb4444" => 2, "DcYuv422" => 3, "DcBump" => 4, "DcPal4" => 5, "DcPal8" => 6, _ => -1 };
            if (format < 0 || layout != string.Empty && layout != "Mipmapped" && layout != "Vq" && layout != "VqMipmapped" && layout != "Linear") return false;
            bool vq = layout == "Vq" || layout == "VqMipmapped";
            bool mipmaps = layout == "Mipmapped" || layout == "VqMipmapped";
            if (format >= 5 && (vq || layout == "Linear")) return false;
            int side = 8;
            while (side < Math.Max(width, height)) side *= 2;
            bytes = ((PayloadBytes(side, side, format, mipmaps, vq) + 31) & ~31) + (format == 5 ? 64 : format == 6 ? 1024 : 0);
            return true;
        }

        /// <summary>Returns a rectangular Morton address; the smaller dimension defines each square block.</summary>
        public static int TwiddledIndex(int x, int y, int width, int height) {
            int side = Math.Min(width, height);
            int address = 0;
            for (int bit = 0; (1 << bit) < side; bit++) {
                address |= ((y >> bit) & 1) << (bit * 2);
                address |= ((x >> bit) & 1) << (bit * 2 + 1);
            }
            return address + ((x / side) + (y / side)) * side * side;
        }

        /// <summary>Returns the hardware byte offset of a mip level, excluding its VQ codebook.</summary>
        public static int MipOffset(int side, int format, bool vq) {
            int offset = side == 1 ? 6 : (side * side * 2 + 16) / 3;
            return vq ? offset / 8 : format == 5 ? offset / 4 : format == 6 ? offset / 2 : offset;
        }

        /// <summary>Returns the complete hardware payload size, before 32-byte transfer alignment.</summary>
        public static int PayloadBytes(int width, int height, int format, bool mipmaps, bool vq, int codebookEntries = 256) {
            int pixels = width * height;
            if (mipmaps) pixels = pixels * 4 / 3 + 3;
            int bytes = format == 5 ? pixels / 2 : format == 6 ? pixels : pixels * 2;
            return vq ? (bytes + 7) / 8 + codebookEntries * 8 : bytes;
        }

        /// <summary>Packs RGBA or an RGB normal into ARGB1555, RGB565, ARGB4444 or spherical bump data.</summary>
        public static ushort EncodeWord(byte[] rgba, int offset, int format) {
            int r = rgba[offset], g = rgba[offset + 1], b = rgba[offset + 2], a = rgba[offset + 3];
            if (format == 0) return (ushort)((a >> 7) << 15 | (r >> 3) << 10 | (g >> 3) << 5 | b >> 3);
            if (format == 1) return (ushort)((r >> 3) << 11 | (g >> 2) << 5 | b >> 3);
            if (format == 2) return (ushort)((a >> 4) << 12 | (r >> 4) << 8 | (g >> 4) << 4 | b >> 4);
            if (format != 4) throw new ArgumentOutOfRangeException(nameof(format));
            double nx = (r - 128) / 127d, ny = (g - 128) / 127d, nz = (b - 128) / 127d;
            double length = Math.Sqrt(nx * nx + ny * ny + nz * nz);
            if (length == 0) { nz = 1; length = 1; }
            double azimuth = Math.Atan2(ny, nx);
            if (azimuth < 0) azimuth += 2 * Math.PI;
            int rotation = (int)(azimuth / (2 * Math.PI) * 255 + 0.5);
            int elevation = 255 - (int)(Math.Clamp(Math.Acos(Math.Clamp(nz / length, -1, 1)), 0, Math.PI / 2) / (Math.PI / 2) * 255 + 0.5);
            return (ushort)(elevation << 8 | rotation);
        }

        /// <summary>Expands one native word into a portable RGBA pixel; bump data becomes an RGB normal preview.</summary>
        public static void DecodeWord(ushort word, int format, byte[] rgba, int offset) {
            int r, g, b, a = 255;
            if (format == 0) {
                r = Expand(word >> 10 & 31, 5); g = Expand(word >> 5 & 31, 5); b = Expand(word & 31, 5); a = (word & 32768) == 0 ? 0 : 255;
            } else if (format == 1) {
                r = Expand(word >> 11, 5); g = Expand(word >> 5 & 63, 6); b = Expand(word & 31, 5);
            } else if (format == 2) {
                r = (word >> 8 & 15) * 17; g = (word >> 4 & 15) * 17; b = (word & 15) * 17; a = (word >> 12) * 17;
            } else if (format == 4) {
                double rotation = (word & 255) / 256d * 2 * Math.PI;
                double altitude = (255 - (word >> 8)) / 255d * Math.PI / 2;
                r = ClampByte(Math.Sin(altitude) * Math.Cos(rotation) * 127 + 128);
                g = ClampByte(Math.Sin(altitude) * Math.Sin(rotation) * 127 + 128);
                b = ClampByte(Math.Cos(altitude) * 127 + 128);
            } else {
                throw new ArgumentOutOfRangeException(nameof(format));
            }
            rgba[offset] = (byte)r; rgba[offset + 1] = (byte)g; rgba[offset + 2] = (byte)b; rgba[offset + 3] = (byte)a;
        }

        /// <summary>Encodes a horizontal pixel pair as UY0 and VY1 words in PVR byte order.</summary>
        public static uint EncodeYuvPair(byte[] rgba, int first, int second) {
            double r = (rgba[first] + rgba[second]) / 2d, g = (rgba[first + 1] + rgba[second + 1]) / 2d, b = (rgba[first + 2] + rgba[second + 2]) / 2d;
            int y0 = ClampByte(.299 * rgba[first] + .587 * rgba[first + 1] + .114 * rgba[first + 2]);
            int y1 = ClampByte(.299 * rgba[second] + .587 * rgba[second + 1] + .114 * rgba[second + 2]);
            int u = ClampByte(-.169 * r - .331 * g + .499 * b + 128);
            int v = ClampByte(.499 * r - .418 * g - .0813 * b + 128);
            return (uint)(y0 << 8 | u) | (uint)(y1 << 8 | v) << 16;
        }

        /// <summary>Expands one YUV word using chroma shared with its horizontal partner.</summary>
        public static void DecodeYuvWord(ushort word, int u, int v, byte[] rgba, int offset) {
            int y = word >> 8;
            rgba[offset] = (byte)ClampByte(y + 1.375 * v);
            rgba[offset + 1] = (byte)ClampByte(y - .34375 * u - .6875 * v);
            rgba[offset + 2] = (byte)ClampByte(y + 1.71875 * u);
            rgba[offset + 3] = 255;
        }

        /// <summary>Expands hardware color bits by replicating the high bits into the low positions.</summary>
        static int Expand(int value, int bits) { int expanded = value << (8 - bits); return expanded | expanded >> bits; }

        /// <summary>Clamps and truncates the SDK's color-conversion arithmetic to one byte.</summary>
        static int ClampByte(double value) { return (int)Math.Clamp(value, 0, 255); }
    }
}
