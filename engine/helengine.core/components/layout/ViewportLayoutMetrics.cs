namespace helengine {
    /// <summary>Contains the shared resolved factors for one layout owner.</summary>
    public struct ViewportLayoutMetrics {
        /// <summary>Gets or sets horizontal pixels per authored unit.</summary>
        public double ScaleX { get; set; }
        /// <summary>Gets or sets vertical pixels per authored unit.</summary>
        public double ScaleY { get; set; }
        /// <summary>Gets or sets the uniform multiplier for glyphs and decorations.</summary>
        public double GlyphScale { get; set; }
        /// <summary>Gets or sets the root displacement within the framebuffer.</summary>
        public float2 Origin { get; set; }
        /// <summary>Gets or sets the available resolved anchor size.</summary>
        public int2 AnchorSize { get; set; }
        /// <summary>Gets or sets displayed pixel width divided by displayed pixel height.</summary>
        public double PixelAspect { get; set; }
    }
}
