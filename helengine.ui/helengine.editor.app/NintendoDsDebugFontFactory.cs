using System.Drawing;
using FontStyle = System.Drawing.FontStyle;
using GraphicsUnit = System.Drawing.GraphicsUnit;

namespace helengine.editor.app {
    /// <summary>
    /// Creates the dedicated Nintendo DS debug font used by generated City companion scenes.
    /// </summary>
    public static class NintendoDsDebugFontFactory {
        /// <summary>
        /// Pixel size used by the Nintendo DS debug glyph source before the runtime tile packer clips each glyph into one 8x8 BG0 cell.
        /// </summary>
        const float BottomOverlayFontPixelSize = 8f;

        /// <summary>
        /// Creates the Nintendo DS bottom-overlay debug font.
        /// </summary>
        /// <param name="renderManager2D">Renderer owned by the caller's session, or null for headless generation.</param>
        /// <returns>Generated Nintendo DS debug font asset.</returns>
        public static FontAsset CreateBottomOverlayFont(RenderManager2D renderManager2D) {
            using Font overlayFont = new Font("Consolas", BottomOverlayFontPixelSize, FontStyle.Regular, GraphicsUnit.Pixel);
            return GDIFontProcessor.ImportFont(overlayFont, renderManager2D);
        }
    }
}
