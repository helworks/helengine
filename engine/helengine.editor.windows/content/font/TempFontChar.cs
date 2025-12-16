using System.Drawing;

namespace helengine.editor {
    /// <summary>
    /// Represents a temporary glyph definition produced during font import.
    /// </summary>
    public struct TempFontChar {
        /// <summary>
        /// Bitmap data for the glyph.
        /// </summary>
        public Bitmap Bitmap;

        /// <summary>
        /// Source rectangle within the atlas for this glyph.
        /// </summary>
        public int4 SourceRect;

        /// <summary>
        /// Baseline offset applied vertically.
        /// </summary>
        public float OffsetY;

        /// <summary>
        /// Horizontal advance width for layout.
        /// </summary>
        public float AdvanceWidth;

        /// <summary>
        /// Horizontal bearing from the origin to the glyph start.
        /// </summary>
        public float BearingX;

        /// <summary>
        /// Vertical bearing from the baseline to the glyph top.
        /// </summary>
        public float BearingY;

        /// <summary>
        /// Initializes a glyph with full metric details.
        /// </summary>
        /// <param name="r">Source rectangle for the glyph.</param>
        /// <param name="bmp">Bitmap containing the glyph pixels.</param>
        /// <param name="offsetY">Vertical baseline offset.</param>
        /// <param name="advanceWidth">Advance width used for layout.</param>
        /// <param name="bearingX">Horizontal bearing relative to origin.</param>
        /// <param name="bearingY">Vertical bearing relative to baseline.</param>
        public TempFontChar(int4 r, Bitmap bmp, float offsetY, float advanceWidth, float bearingX, float bearingY) {
            SourceRect = r;
            Bitmap = bmp;
            OffsetY = offsetY;
            AdvanceWidth = advanceWidth;
            BearingX = bearingX;
            BearingY = bearingY;
        }

        /// <summary>
        /// Initializes a glyph using only offset and source rectangle, deriving other metrics from the rectangle.
        /// </summary>
        /// <param name="r">Source rectangle for the glyph.</param>
        /// <param name="bmp">Bitmap containing the glyph pixels.</param>
        /// <param name="offsetY">Vertical baseline offset.</param>
        public TempFontChar(int4 r, Bitmap bmp, float offsetY) {
            SourceRect = r;
            Bitmap = bmp;
            OffsetY = offsetY;
            AdvanceWidth = r.Z; // Use width as advance width by default
            BearingX = 0;
            BearingY = 0;
        }
    }
}
