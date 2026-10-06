using System.Runtime.Versioning;

namespace helengine.editor {
    /// <summary>Generates the snap toolbar's filled up/down arrows as SVG geometry without external assets.</summary>
    public static class EditorSnapArrowIconBuilder {
        /// <summary>Creates a white arrow SVG with transparent padding at the requested display size.</summary>
        /// <param name="isIncreaseButton">True for the upward increase arrow; false for the downward decrease arrow.</param>
        /// <param name="pixelSize">Final square canvas size in physical pixels.</param>
        /// <returns>SVG source containing only the arrow path.</returns>
        public static string BuildSvg(bool isIncreaseButton, int pixelSize) {
            if (pixelSize <= 0 || pixelSize > ushort.MaxValue) {
                throw new ArgumentOutOfRangeException(nameof(pixelSize));
            }
            string geometry = isIncreaseButton
                ? "M8 2L15 10H11V14H5V10H1Z"
                : "M8 14L15 6H11V2H5V6H1Z";
            return $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{pixelSize}\" height=\"{pixelSize}\" viewBox=\"0 0 16 16\"><path fill=\"white\" d=\"{geometry}\"/></svg>";
        }

        /// <summary>Rasterizes the arrow once at its displayed dimensions so the toolbar draws the mask at 1:1.</summary>
        /// <param name="isIncreaseButton">True for the upward increase arrow; false for the downward decrease arrow.</param>
        /// <param name="pixelSize">Final square canvas size in physical pixels.</param>
        /// <returns>White RGBA mask whose alpha defines the arrow.</returns>
        [SupportedOSPlatform("windows")]
        public static TextureAsset CreateTextureAsset(bool isIncreaseButton, int pixelSize) {
            return EditorSvgIconRasterizer.CreateWhiteMask(BuildSvg(isIncreaseButton, pixelSize), pixelSize);
        }
    }
}
