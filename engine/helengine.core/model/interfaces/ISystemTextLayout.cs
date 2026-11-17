namespace helengine {
    /// <summary>Borrowed immutable layout metrics in resolved drawable pixels; callers must consume them before requesting another layout.</summary>
    public interface ISystemTextLayout {
        /// <summary>Gets the backend-owned raster texture, or null when the layout has no visible ink.</summary>
        RuntimeTexture Texture { get; }
        /// <summary>Gets the conservative pixel-aligned raster enclosure relative to the complete text layout origin, including visible overhang after any requested window clip.</summary>
        float4 RasterBounds { get; }
        /// <summary>Gets complete typographic width and total line height, including empty and clipped-out lines, independently of the optional raster window.</summary>
        float2 Size { get; }
        /// <summary>Gets the number of shaped lines.</summary>
        int LineCount { get; }
        /// <summary>Gets the original UTF-16 code-unit count, preserving supplementary characters and combining sequences.</summary>
        int TextLength { get; }
        /// <summary>Gets first-line ascent in drawable pixels.</summary>
        float Ascent { get; }
        /// <summary>Gets first-line descent in drawable pixels.</summary>
        float Descent { get; }
        /// <summary>Returns primary X, line top Y, line height and secondary bidi X for a valid UTF-16 boundary; invalid or interior composed-character indices fail explicitly.</summary>
        float4 GetCaret(int utf16Index);
        /// <summary>Reports whether an index is a composed-character boundary accepted by GetCaret; includes zero and TextLength.</summary>
        bool IsCaretBoundary(int utf16Index);
        /// <summary>Returns the nearest composed-character UTF-16 boundary for a layout-local point using shaped visual positions.</summary>
        int HitTest(float x, float y);
    }
}
