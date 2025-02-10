using System.Drawing;

namespace helengine.editor {
    public struct TempFontChar {
        public Bitmap Bitmap;
        public int4 SourceRect;
        public float OffsetY;

        public TempFontChar(int4 r, Bitmap bmp, float offsetY) {
            SourceRect = r;
            Bitmap = bmp;
            OffsetY = offsetY;
        }
    }
}

