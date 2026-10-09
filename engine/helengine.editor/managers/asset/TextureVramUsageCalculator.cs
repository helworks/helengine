namespace helengine.editor {
    /// <summary>
    /// Estimates generic texture payloads or native console GPU storage for one import configuration,
    /// mirroring resize, packing, pitch, storage padding and palette budgeting.
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
        /// <returns>True when the configured format has a known representable layout.
        /// Native Xbox estimates include storage padding and a full P8 palette budget, excluding the file header;
        /// Xbox 360 estimates include aligned linear block rows and exclude unsupported catalog entries;
        /// PS2 estimates include complete GS texture pages and an indexed texture's separate CLUT page;
        /// dimensions remaining outside the native extent limit return false.</returns>
        public static bool TryCalculateBytes(int sourceWidth, int sourceHeight, TextureAssetProcessorSettings settings, out long bytes) {
            bytes = 0;
            if (sourceWidth < 1 || sourceHeight < 1 || settings == null) {
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

            if (DreamcastPvrPixelCodec.TryCalculateMemory(settings.ColorFormatId, width, height, out bytes)) return true;
            if (SwitchTextureFormatCatalog.TryGetFormat(settings.ColorFormatId, out SwitchTextureFormat switchFormat)) {
                if (width > 16384 || height > 16384 || !switchFormat.SupportsAlpha(settings.AlphaPrecision)) return false;
                try { SwitchTextureLayout layout = new SwitchTextureLayout(switchFormat, width, height); bytes = layout.TexelBytes; return true; }
                catch (ArgumentException) { return false; }
            }
            if (PlayStation3TextureFormatCatalog.TryCalculateMemory(settings.ColorFormatId, width, height, out bytes)) return true;

            if (GxNativeTextureFormatCatalog.TryGetFormat(settings.ColorFormatId, out GxNativeTextureFormatDefinition gxFormat)) {
                if (width > 1024 || height > 1024) return false;
                bytes = gxFormat.GetVramByteLength(width, height);
                return true;
            }
            if (VitaNativeTextureFormatCatalog.TryGetFormat(settings.ColorFormatId, out VitaNativeTextureFormatDefinition vitaFormat)) {
                if (width > 4096 || height > 4096) return false;
                bytes = vitaFormat.GetVramByteLength(width, height);
                return true;
            }
            if (WiiUNativeTextureFormatCatalog.TryGetFormat(settings.ColorFormatId, out WiiUNativeTextureFormatDefinition wiiuFormat)) {
                if (width > 8192 || height > 8192) return false;
                bytes = wiiuFormat.GetVramByteLength(width, height);
                return true;
            }

            if (Ps2GsTextureFormatCatalog.TryGetFormat(settings.ColorFormatId, out Ps2GsTextureFormatDefinition ps2Format)) {
                if (width > 1024 || height > 1024) return false;
                bytes = ps2Format.GetVramByteLength(width, height);
                return true;
            }
            if (PspTextureFormatCatalog.TryGetFormat(settings.ColorFormatId, out PspTextureFormat pspFormat)) {
                if (width > 512 || height > 512) return false;
                PspTextureLayout pspLayout = new PspTextureLayout(pspFormat, width, height);
                bytes = (long)pspLayout.TexelBytes + pspLayout.PaletteBytes;
                return true;
            }
            if (Nintendo3DsTextureFormatCatalog.TryGetFormat(settings.ColorFormatId, out Nintendo3DsTextureFormat ctrFormat)) {
                if (width > 1024 || height > 1024) return false;
                Nintendo3DsTextureLayout ctrLayout = new Nintendo3DsTextureLayout(ctrFormat, width, height);
                bytes = ctrLayout.TexelBytes;
                return true;
            }
            if (NintendoDsTextureFormatCatalog.TryGetFormat(settings.ColorFormatId, out NintendoDsTextureFormat dsFormat)) {
                if (width > 1024 || height > 1024 || !dsFormat.SupportsAlpha(settings.AlphaPrecision)) return false;
                long pixels = (long)NintendoDsTextureLayout.GetStorageExtent(width) * NintendoDsTextureLayout.GetStorageExtent(height);
                long texels = pixels * dsFormat.BitsPerPixel / 8;
                if ((dsFormat.Code == 5 && texels > 128 * 1024) || (dsFormat.Code <= 7 && texels > 512 * 1024)) return false;
                bytes = texels + (dsFormat.Code == 5 ? pixels / 8 + Math.Min(65536, pixels / 2) : dsFormat.PaletteEntries * 2);
                return true;
            }

            if (Xbox360NativeTextureFormatCatalog.TryGetFormat(settings.ColorFormatId, out Xbox360NativeTextureFormatDefinition nativeXbox360Format)) {
                if (!nativeXbox360Format.SupportsCooking || width > 8192 || height > 8192) {
                    return false;
                }
                Xbox360NativeTextureLayout nativeXbox360Layout = new Xbox360NativeTextureLayout(nativeXbox360Format, width, height);
                bytes = nativeXbox360Layout.TexelLength;
                return true;
            }

            bool isNativeXbox = XboxNativeTextureFormatCatalog.TryGetFormat(settings.ColorFormatId, out XboxNativeTextureFormatDefinition nativeFormat);
            if (!Enum.TryParse(settings.ColorFormatId, true, out TextureAssetColorFormat colorFormat) && !isNativeXbox) return false;

            if (isNativeXbox) {
                if (width > 4096 || height > 4096) {
                    return false;
                }
                // P8 reserves the largest possible palette because its actual unique-color
                // count is unavailable until cooking. The serialized header never reaches GPU memory.
                XboxNativeTextureLayout nativeLayout = new XboxNativeTextureLayout(nativeFormat, width, height,
                    nativeFormat.IsPaletted ? Indexed8PaletteEntries : 0);
                bytes = (long)nativeLayout.TexelLength + nativeLayout.PaletteLength;
                return true;
            }

            long pixelCount = (long)width * height;
            switch (colorFormat) {
                case TextureAssetColorFormat.Rgba32:
                    bytes = pixelCount * 4;
                    return true;
                case TextureAssetColorFormat.Rgba4444:
                case TextureAssetColorFormat.Ps1Bgr555:
                    bytes = pixelCount * 2;
                    return true;
                case TextureAssetColorFormat.GxRgb5A3:
                    if (width > 1024 || height > 1024) return false;
                    bytes = ((width + 3L) / 4) * ((height + 3L) / 4) * 32;
                    return true;
                case TextureAssetColorFormat.Indexed4:
                    bytes = ((pixelCount + 1) / 2) + (Indexed4PaletteEntries * PaletteEntryBytes);
                    return true;
                case TextureAssetColorFormat.Indexed8:
                    bytes = pixelCount + (Indexed8PaletteEntries * PaletteEntryBytes);
                    return true;
                case TextureAssetColorFormat.Rgba5551:
                case TextureAssetColorFormat.Ia4:
                case TextureAssetColorFormat.Ia8:
                case TextureAssetColorFormat.Ia16:
                case TextureAssetColorFormat.I4:
                case TextureAssetColorFormat.I8:
                case TextureAssetColorFormat.Yuv16:
                    try {
                        bytes = TextureAssetPixelCodec.GetPixelByteLength(colorFormat, width, height);
                        return true;
                    } catch (ArgumentOutOfRangeException) {
                        return false;
                    } catch (OverflowException) {
                        return false;
                    }
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
