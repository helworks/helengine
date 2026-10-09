namespace helengine {
    /// <summary>Publishes all eleven GU encodings, four CLUT encodings and verified storage layouts.</summary>
    public static class PspTextureFormatCatalog {
        /// <summary>Stores the exact format combinations supported by the cooker and runtime.</summary>
        static readonly PspTextureFormat[] FormatValues = BuildFormats();
        /// <summary>Gets the 43 native combinations: eight direct, 32 indexed and three compressed.</summary>
        public static PspTextureFormat[] Formats => FormatValues;
        /// <summary>Finds an exact native setting identifier; unknown aliases are rejected.</summary>
        public static bool TryGetFormat(string id, out PspTextureFormat format) {
            for (int index = 0; index < FormatValues.Length; index++) {
                if (FormatValues[index].Id == id) { format = FormatValues[index]; return true; }
            }
            format = null;
            return false;
        }
        /// <summary>Returns borrowed catalog metadata for exact native controls without allocating a new format object.</summary>
        [NativeBorrowedReturn]
        public static PspTextureFormat GetFormat(int code, bool swizzled, int paletteCode) {
            for (int index = 0; index < FormatValues.Length; index++) {
                PspTextureFormat format = FormatValues[index];
                if (format.Code == code && format.Swizzled == swizzled && format.PaletteCode == paletteCode) return format;
            }
            throw new ArgumentException("Unsupported PSP hardware controls.");
        }
        /// <summary>Builds the finite catalog without exposing swizzling for DXT, which the GE handles as blocks.</summary>
        static PspTextureFormat[] BuildFormats() {
            string[] names = new string[] { "Rgb565", "Rgba5551", "Rgba4444", "Rgba8888", "T4", "T8", "T16", "T32", "Dxt1", "Dxt3", "Dxt5" };
            PspTextureFormat[] formats = new PspTextureFormat[43];
            int index = 0;
            for (int code = 0; code <= 10; code++) {
                int layouts = code >= 8 ? 1 : 2;
                int palettes = code >= 4 && code <= 7 ? 4 : 1;
                for (int order = 0; order < layouts; order++) {
                    for (int palette = 0; palette < palettes; palette++) {
                        string id = "Psp." + (order == 0 ? "Linear." : "Swizzled.") + names[code];
                        if (palettes > 1) id += ".Clut" + names[palette];
                        formats[index++] = new PspTextureFormat(id, code, order != 0, palette);
                    }
                }
            }
            return formats;
        }
    }
}
