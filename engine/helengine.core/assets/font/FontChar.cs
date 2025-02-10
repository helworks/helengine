namespace helengine {
    public struct FontChar {
        public float4 SourceRect;
        public float OffsetY;

        public FontChar(float4 r, float offsetY) {
            SourceRect = r;
            OffsetY = offsetY;
        }
    }
}

