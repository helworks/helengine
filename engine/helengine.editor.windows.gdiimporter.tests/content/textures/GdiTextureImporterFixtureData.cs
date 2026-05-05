namespace helengine.editor.windows.gdiimporter.tests.content.textures {
    /// <summary>
    /// Provides tiny encoded image fixtures used by the GDI importer tests.
    /// </summary>
    public static class GdiTextureImporterFixtureData {
        /// <summary>
        /// Creates a 1x1 PNG file whose single pixel is encoded as RGBA 9,8,7,6.
        /// </summary>
        /// <returns>Encoded PNG file bytes.</returns>
        public static byte[] CreateSinglePixelPngFile() {
            return Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAADsMAAA7DAcdvqGQAAAANSURBVBhXY+DkYGcDAABVAB+CD+SxAAAAAElFTkSuQmCC");
        }
    }
}
