namespace helengine.editor {
    /// <summary>Owns one cropped RGBA glyph and its line-relative metrics before atlas packing.</summary>
    internal sealed class PortableFontGlyph {
        /// <summary>Character represented by the raster.</summary>
        public char Character;
        /// <summary>Width of the cropped bitmap, including its transparent border.</summary>
        public int Width;
        /// <summary>Height of the cropped bitmap, including its transparent border.</summary>
        public int Height;
        /// <summary>Straight-alpha white RGBA pixels.</summary>
        public byte[] Pixels;
        /// <summary>Horizontal glyph origin relative to the text pen.</summary>
        public float BearingX;
        /// <summary>Glyph top relative to the line top.</summary>
        public float OffsetY;
        /// <summary>Distance to the next pen position in pixels.</summary>
        public float Advance;
    }
}
