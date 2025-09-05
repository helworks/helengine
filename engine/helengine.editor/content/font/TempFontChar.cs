using System.Drawing;

namespace helengine.editor {
    public struct TempFontChar {
        public Bitmap Bitmap;
        public int4 SourceRect;
        public float OffsetY;
        public float AdvanceWidth;
        public float BearingX;
        public float BearingY;

        public TempFontChar(int4 r, Bitmap bmp, float offsetY, float advanceWidth, float bearingX, float bearingY) {
            SourceRect = r;
            Bitmap = bmp;
            OffsetY = offsetY;
            AdvanceWidth = advanceWidth;
            BearingX = bearingX;
            BearingY = bearingY;
        }

        // Legacy constructor for backward compatibility
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

