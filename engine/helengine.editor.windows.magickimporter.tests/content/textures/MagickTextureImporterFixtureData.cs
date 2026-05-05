namespace helengine.editor.windows.magickimporter.tests.content.textures {
    /// <summary>
    /// Provides tiny encoded image fixtures used by the Magick importer tests.
    /// </summary>
    public static class MagickTextureImporterFixtureData {
        /// <summary>
        /// Creates a 1x1 PNG file whose single pixel is encoded as RGBA 9,8,7,6.
        /// </summary>
        /// <returns>Encoded PNG file bytes.</returns>
        public static byte[] CreateSinglePixelPngFile() {
            return Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAADsMAAA7DAcdvqGQAAAANSURBVBhXY+DkYGcDAABVAB+CD+SxAAAAAElFTkSuQmCC");
        }

        /// <summary>
        /// Creates a 1x1 opaque PNG file whose single pixel is encoded as RGBA 9,8,7,255.
        /// </summary>
        /// <returns>Encoded PNG file bytes.</returns>
        public static byte[] CreateSinglePixelOpaquePngFile() {
            return Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAADsMAAA7DAcdvqGQAAAANSURBVBhXY+DkYP8PAAFOARgGWpOHAAAAAElFTkSuQmCC");
        }

        /// <summary>
        /// Creates a 1x1 PSD file whose single pixel is encoded as RGBA 9,8,7,255.
        /// </summary>
        /// <returns>Encoded PSD file bytes.</returns>
        public static byte[] CreateSinglePixelPsdFile() {
            return MagickFixtureEncoder.EncodeToFormat(CreateSinglePixelOpaquePngFile(), "Psd");
        }
    }
}
