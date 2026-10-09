namespace helengine {
    /// <summary>Publishes every DS 3D texture encoding plus native 4/8-bit OBJ/BG tile storage.</summary>
    public static class NintendoDsTextureFormatCatalog {
        /// <summary>Owns the ordered immutable format definitions; hardware code zero means no texture.</summary>
        public static readonly NintendoDsTextureFormat[] Formats = new NintendoDsTextureFormat[] {
            new NintendoDsTextureFormat(1, "NintendoDs.A3I5", 8, 32, TextureAssetAlphaPrecision.A3),
            new NintendoDsTextureFormat(2, "NintendoDs.Indexed2", 2, 4, TextureAssetAlphaPrecision.Binary),
            new NintendoDsTextureFormat(3, "NintendoDs.Indexed4", 4, 16, TextureAssetAlphaPrecision.Binary),
            new NintendoDsTextureFormat(4, "NintendoDs.Indexed8", 8, 256, TextureAssetAlphaPrecision.Binary),
            new NintendoDsTextureFormat(5, "NintendoDs.Compressed4x4", 2, 0, TextureAssetAlphaPrecision.Binary),
            new NintendoDsTextureFormat(6, "NintendoDs.A5I3", 8, 8, TextureAssetAlphaPrecision.A5),
            new NintendoDsTextureFormat(7, "NintendoDs.Bgr5551", 16, 0, TextureAssetAlphaPrecision.Binary),
            new NintendoDsTextureFormat(8, "NintendoDs.Tiled.Indexed4", 4, 16, TextureAssetAlphaPrecision.Binary),
            new NintendoDsTextureFormat(9, "NintendoDs.Tiled.Indexed8", 8, 256, TextureAssetAlphaPrecision.Binary)
        };
        /// <summary>Finds a borrowed definition by its exact platform override ID.</summary>
        public static bool TryGetFormat(string id, out NintendoDsTextureFormat format) {
            for (int index = 0; index < Formats.Length; index++) if (Formats[index].Id == id) { format = Formats[index]; return true; }
            format = null; return false;
        }
        /// <summary>Gets a borrowed definition for a checked wire code.</summary>
        [NativeBorrowedReturn]
        public static NintendoDsTextureFormat GetFormat(int code) {
            if (code < 1 || code > 9) throw new ArgumentException("Unknown Nintendo DS native texture code.");
            return Formats[code - 1];
        }
    }
}
