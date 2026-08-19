namespace helengine.editor {
    /// <summary>
    /// Estimates the processed texture payload size the texture asset processor produces for one settings
    /// configuration, mirroring its resize and per-format packing rules.
    /// </summary>
    public static class TextureVramUsageCalculator {
        /// <summary>
        /// Number of palette entries stored by the Indexed4 format.
        /// </summary>
        const int Indexed4PaletteEntries = 16;

        /// <summary>
        /// Number of palette entries stored by the Indexed8 format.
        /// </summary>
        const int Indexed8PaletteEntries = 256;

        /// <summary>
        /// Bytes stored per palette entry.
        /// </summary>
        const int PaletteEntryBytes = 4;

        /// <summary>
        /// Attempts to estimate the processed texture payload size for one source image and settings pair.
        /// </summary>
        /// <param name="sourceWidth">Source image width in pixels.</param>
        /// <param name="sourceHeight">Source image height in pixels.</param>
        /// <param name="settings">Texture processor settings describing the output format.</param>
        /// <param name="bytes">Receives the estimated payload size in bytes.</param>
        /// <returns>True when the configured color format maps to a known payload layout.</returns>
        public static bool TryCalculateBytes(int sourceWidth, int sourceHeight, TextureAssetProcessorSettings settings, out long bytes) {
            bytes = 0;
            if (sourceWidth < 1 || sourceHeight < 1 || settings == null) {
                return false;
            }
            if (!Enum.TryParse(settings.ColorFormatId, true, out TextureAssetColorFormat colorFormat)) {
                return false;
            }

            int width = sourceWidth;
            int height = sourceHeight;
            if (settings.MaxResolution > 0 && (width > settings.MaxResolution || height > settings.MaxResolution)) {
                double largestDimension = Math.Max(width, height);
                double scale = settings.MaxResolution / largestDimension;
                width = Math.Max(1, (int)Math.Round(sourceWidth * scale));
                height = Math.Max(1, (int)Math.Round(sourceHeight * scale));
            }

            long pixelCount = (long)width * height;
            switch (colorFormat) {
                case TextureAssetColorFormat.Rgba32:
                    bytes = pixelCount * 4;
                    return true;
                case TextureAssetColorFormat.Rgba4444:
                case TextureAssetColorFormat.GxRgb5A3:
                    bytes = pixelCount * 2;
                    return true;
                case TextureAssetColorFormat.Indexed4:
                    bytes = ((pixelCount + 1) / 2) + (Indexed4PaletteEntries * PaletteEntryBytes);
                    return true;
                case TextureAssetColorFormat.Indexed8:
                    bytes = pixelCount + (Indexed8PaletteEntries * PaletteEntryBytes);
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Formats one byte count as a compact human-readable size.
        /// </summary>
        /// <param name="bytes">Byte count to format.</param>
        /// <returns>Formatted size text.</returns>
        public static string FormatBytes(long bytes) {
            if (bytes < 0) {
                throw new ArgumentOutOfRangeException(nameof(bytes), "Byte counts must not be negative.");
            }

            const double kilobyte = 1024d;
            const double megabyte = 1024d * 1024d;
            if (bytes >= megabyte) {
                return $"{(bytes / megabyte).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)} MB";
            }
            if (bytes >= kilobyte) {
                return $"{(bytes / kilobyte).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)} KB";
            }

            return $"{bytes} B";
        }
    }
}
