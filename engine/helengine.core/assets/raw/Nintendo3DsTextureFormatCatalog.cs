namespace helengine {
    /// <summary>Publishes all fourteen PICA200 encodings in their native tiled storage order.</summary>
    public static class Nintendo3DsTextureFormatCatalog {
        /// <summary>Stores the finite catalog used by previews, cooking, VRAM estimates and runtime descriptors.</summary>
        static readonly Nintendo3DsTextureFormat[] FormatValues = BuildFormats();
        /// <summary>Gets the fourteen native setting definitions in hardware-code order.</summary>
        public static Nintendo3DsTextureFormat[] Formats => FormatValues;
        /// <summary>Resolves an exact published identifier without accepting unsupported layout aliases.</summary>
        public static bool TryGetFormat(string id, out Nintendo3DsTextureFormat format) {
            for (int index = 0; index < FormatValues.Length; index++) {
                if (FormatValues[index].Id == id) { format = FormatValues[index]; return true; }
            }
            format = null; return false;
        }
        /// <summary>Returns borrowed catalog metadata for a checked hardware code.</summary>
        [NativeBorrowedReturn]
        public static Nintendo3DsTextureFormat GetFormat(int code) {
            if (code < 0 || code >= FormatValues.Length) throw new ArgumentOutOfRangeException(nameof(code));
            return FormatValues[code];
        }
        /// <summary>Creates format IDs matching the documented libctru encodings.</summary>
        static Nintendo3DsTextureFormat[] BuildFormats() {
            string[] names = new string[] { "Rgba8", "Rgb8", "Rgba5551", "Rgb565", "Rgba4", "La8", "Hilo8", "L8", "A8", "La4", "L4", "A4", "Etc1", "Etc1A4" };
            Nintendo3DsTextureFormat[] result = new Nintendo3DsTextureFormat[14];
            for (int index = 0; index < result.Length; index++) result[index] = new Nintendo3DsTextureFormat("Nintendo3Ds." + names[index], index);
            return result;
        }
    }
}
