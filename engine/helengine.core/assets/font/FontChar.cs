namespace helengine {
    public struct FontChar {
        public float4 SourceRect;  // UV coordinates in texture atlas (x, y, width, height)
        public float OffsetY;      // Vertical offset from baseline
        public float AdvanceWidth; // Horizontal advance to next character
        public float BearingX;     // Horizontal bearing (left side bearing)
        public float BearingY;     // Vertical bearing (top side bearing)

        public FontChar(float4 sourceRect, float offsetY, float advanceWidth, float bearingX, float bearingY) {
            SourceRect = sourceRect;
            OffsetY = offsetY;
            AdvanceWidth = advanceWidth;
            BearingX = bearingX;
            BearingY = bearingY;
        }

        // Legacy constructor for backward compatibility
        public FontChar(float4 r, float offsetY) {
            SourceRect = r;
            OffsetY = offsetY;
            AdvanceWidth = r.Z; // Use width as advance width by default
            BearingX = 0;
            BearingY = 0;
        }
    }
}

