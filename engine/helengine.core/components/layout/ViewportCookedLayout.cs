namespace helengine {
    /// <summary>Describes one prepared viewport without retaining any participant's original values.</summary>
    public sealed class ViewportCookedLayout {
        /// <summary>Version shared by cook identity and both runtime layout owners.</summary>
        public const int CurrentAlgorithmVersion = 2;
        /// <summary>Identifies the layout algorithm that prepared the packaged scene.</summary>
        public int AlgorithmVersion { get; set; }
        /// <summary>Gets or sets the effective target width in framebuffer pixels.</summary>
        public int TargetWidth { get; set; }
        /// <summary>Gets or sets the effective target height in framebuffer pixels.</summary>
        public int TargetHeight { get; set; }
        /// <summary>Gets or sets the displayed width-to-height ratio of one pixel.</summary>
        public double PixelAspect { get; set; }
        /// <summary>Indicates whether the backend resolved a physical display density.</summary>
        public bool HasDpi { get; set; }
        /// <summary>Gets or sets the resolved DPI when physical density is known.</summary>
        public double Dpi { get; set; }
        /// <summary>Gets or sets the effective user UI multiplier.</summary>
        public double UiScale { get; set; }
        /// <summary>Gets or sets the horizontal pixels per authored unit.</summary>
        public double ScaleX { get; set; }
        /// <summary>Gets or sets the vertical pixels per authored unit.</summary>
        public double ScaleY { get; set; }
        /// <summary>Gets or sets the uniform glyph and decoration multiplier.</summary>
        public double GlyphScale { get; set; }
        /// <summary>Gets or sets the fitted canvas displacement within the target.</summary>
        public float2 Origin { get; set; }
        /// <summary>Gets or sets the prepared anchor canvas size.</summary>
        public int2 AnchorSize { get; set; }
    }
}
