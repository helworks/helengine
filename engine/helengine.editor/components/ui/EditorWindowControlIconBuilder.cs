using System.Runtime.Versioning;

namespace helengine.editor {
    /// <summary>Generates window-control SVG documents in memory and rasterizes white coverage masks at their final pixel size.</summary>
    public static class EditorWindowControlIconBuilder {
        /// <summary>Creates a self-contained SVG with no font, image, or file dependencies.</summary>
        /// <param name="kind">Window control whose geometry is requested.</param>
        /// <param name="pixelSize">Final square canvas size in physical pixels.</param>
        /// <returns>SVG source ready for in-memory rasterization.</returns>
        public static string BuildSvg(EditorWindowControlIconKind kind, int pixelSize) {
            if (pixelSize <= 0 || pixelSize > ushort.MaxValue) {
                throw new ArgumentOutOfRangeException(nameof(pixelSize));
            }
            string geometry = kind switch {
                EditorWindowControlIconKind.Minimize => "<path d=\"M1 8.5H11\"/>",
                EditorWindowControlIconKind.Maximize => "<rect x=\"1.5\" y=\"1.5\" width=\"9\" height=\"9\"/>",
                EditorWindowControlIconKind.Restore => "<path d=\"M4.5 3.5V1.5H10.5V7.5H8.5 M1.5 4.5H7.5V10.5H1.5Z\"/>",
                EditorWindowControlIconKind.Close => "<path d=\"M2 2L10 10M10 2L2 10\"/>",
                _ => throw new ArgumentOutOfRangeException(nameof(kind))
            };
            return $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{pixelSize}\" height=\"{pixelSize}\" viewBox=\"0 0 12 12\"><g fill=\"none\" stroke=\"white\" stroke-width=\"1\" stroke-linecap=\"square\" stroke-linejoin=\"miter\">{geometry}</g></svg>";
        }

        /// <summary>Rasterizes the generated SVG once at the display size and converts its coverage into straight RGBA.</summary>
        /// <param name="kind">Window control whose geometry is requested.</param>
        /// <param name="pixelSize">Final square canvas size in physical pixels.</param>
        /// <returns>White RGBA mask with transparent background, suitable for theme tinting.</returns>
        [SupportedOSPlatform("windows")]
        public static TextureAsset CreateTextureAsset(EditorWindowControlIconKind kind, int pixelSize) {
            return EditorSvgIconRasterizer.CreateWhiteMask(BuildSvg(kind, pixelSize), pixelSize);
        }
    }
}
