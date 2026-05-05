namespace helengine.editor {
    /// <summary>
    /// Centralizes the texture file extensions supported by the editor import pipeline.
    /// </summary>
    public static class TextureImportFormatCatalog {
        /// <summary>
        /// Extensions handled by the existing GDI-backed importer.
        /// </summary>
        static readonly string[] gdiTextureExtensions = new[] {
            ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tiff", ".tif"
        };

        /// <summary>
        /// Extensions handled by the Pfim importer.
        /// </summary>
        static readonly string[] pfimTextureExtensions = new[] {
            ".dds", ".tga", ".targa"
        };

        /// <summary>
        /// Extensions routed through Magick.NET for broad Windows image coverage.
        /// </summary>
        static readonly string[] magickTextureExtensions = new[] {
            ".apng", ".avif", ".avs", ".bmp", ".cin", ".cr2", ".cr3", ".cur", ".cut", ".dcm", ".dds", ".dib", ".djvu",
            ".dng", ".dpx", ".emf", ".exr", ".fax", ".fits", ".gif", ".gray", ".hdr", ".heic", ".heif", ".ico",
            ".icon", ".j2c", ".j2k", ".jng", ".jp2", ".jpc", ".jpeg", ".jpf", ".jpg", ".jpm", ".jps", ".jpt",
            ".jpx", ".jxl", ".miff", ".mng", ".mono", ".mrw", ".nef", ".orf", ".pam", ".pbm", ".pcd", ".pcds",
            ".pcx", ".pdb", ".pef", ".pgm", ".picon", ".pict", ".pix", ".png", ".pnm", ".ppm", ".psb", ".psd",
            ".ptif", ".qoi", ".raf", ".ras", ".rgb", ".rgba", ".rw2", ".sgi", ".sun", ".tga", ".targa", ".tif",
            ".tiff", ".viff", ".wbmp", ".webp", ".wmf", ".wpg", ".x3f", ".xbm", ".xpm", ".xwd"
        };

        /// <summary>
        /// Union of every known texture extension supported by the editor host.
        /// </summary>
        static readonly string[] allTextureExtensions = BuildAllTextureExtensions();

        /// <summary>
        /// Gets the extensions handled by the GDI-backed importer.
        /// </summary>
        public static IReadOnlyList<string> GdiTextureExtensions => gdiTextureExtensions;

        /// <summary>
        /// Gets the extensions handled by the Pfim importer.
        /// </summary>
        public static IReadOnlyList<string> PfimTextureExtensions => pfimTextureExtensions;

        /// <summary>
        /// Gets the extensions routed through Magick.NET.
        /// </summary>
        public static IReadOnlyList<string> MagickTextureExtensions => magickTextureExtensions;

        /// <summary>
        /// Gets the de-duplicated union of all supported texture extensions.
        /// </summary>
        public static IReadOnlyList<string> AllTextureExtensions => allTextureExtensions;

        /// <summary>
        /// Builds the de-duplicated union of all texture extensions.
        /// </summary>
        /// <returns>Sorted texture extension array.</returns>
        static string[] BuildAllTextureExtensions() {
            HashSet<string> uniqueExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            AddExtensions(uniqueExtensions, gdiTextureExtensions);
            AddExtensions(uniqueExtensions, pfimTextureExtensions);
            AddExtensions(uniqueExtensions, magickTextureExtensions);

            List<string> extensions = new List<string>(uniqueExtensions);
            extensions.Sort(StringComparer.OrdinalIgnoreCase);
            return extensions.ToArray();
        }

        /// <summary>
        /// Adds one extension sequence into the provided set.
        /// </summary>
        /// <param name="target">Set receiving extension values.</param>
        /// <param name="extensions">Extensions to add.</param>
        static void AddExtensions(HashSet<string> target, IReadOnlyList<string> extensions) {
            if (target == null) {
                throw new ArgumentNullException(nameof(target));
            }

            if (extensions == null) {
                throw new ArgumentNullException(nameof(extensions));
            }

            for (int index = 0; index < extensions.Count; index++) {
                target.Add(extensions[index]);
            }
        }
    }
}
