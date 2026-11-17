namespace helengine {
    /// <summary>Host-owned shaping and bounded raster cache used by the existing 2D command builder.</summary>
    public interface ISystemTextBackend {
        /// <summary>Gets the maximum accepted UTF-16 code-unit count; oversize requests fail without truncation.</summary>
        int MaximumTextLength { get; }
        /// <summary>Shapes and rasterizes on a cache miss. The result is borrowed until the next layout request or Invalidate call; copy metrics immediately and never retain it in a control.</summary>
        /// <remarks>Position and tint are excluded. Pixel scale already includes viewport and drawable scaling. The backend owns returned textures and must defer their release until submitted commands are safe.</remarks>
        [NativeBorrowedReturn]
        ISystemTextLayout Layout(string text, SystemTextStyle style, float pixelScale, int width, bool wrap, TextAlignment alignment);
        /// <summary>Shapes the complete bounded input while rasterizing only its intersection with a finite top-down layout-local window. Full size, line and UTF-16 caret metrics remain available outside the window.</summary>
        /// <remarks>Window X/Y may be negative or shifted for scrolling; width/height must be nonnegative and all coordinates/edges finite. Zero area or empty ink intersection returns no texture. The result has the same immediate borrowed lifetime as Layout.</remarks>
        [NativeBorrowedReturn]
        ISystemTextLayout LayoutClipped(string text, SystemTextStyle style, float pixelScale, int width, bool wrap, TextAlignment alignment, float4 rasterWindow);
        /// <summary>Invalidates cached environment-dependent layouts and safely retires their textures.</summary>
        void Invalidate();
    }
}
