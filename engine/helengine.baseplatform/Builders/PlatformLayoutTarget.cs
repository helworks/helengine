namespace helengine.baseplatform.Builders {
    /// <summary>Describes known backend presentation geometry without inventing physical density.</summary>
    public sealed class PlatformLayoutTarget {
        /// <summary>Creates a resolved presentation target from backend-owned output metrics.</summary>
        public PlatformLayoutTarget(int width, int height, double pixelAspect, bool hasDpi, double dpi, double uiScale) {
            if (width <= 0) { throw new ArgumentOutOfRangeException(nameof(width)); }
            if (height <= 0) { throw new ArgumentOutOfRangeException(nameof(height)); }
            if (!(pixelAspect > 0d) || double.IsInfinity(pixelAspect)) { throw new ArgumentOutOfRangeException(nameof(pixelAspect)); }
            if (hasDpi && (!(dpi > 0d) || double.IsInfinity(dpi))) { throw new ArgumentOutOfRangeException(nameof(dpi)); }
            if (!(uiScale > 0d) || double.IsInfinity(uiScale)) { throw new ArgumentOutOfRangeException(nameof(uiScale)); }
            Width = width; Height = height; PixelAspect = pixelAspect; HasDpi = hasDpi; Dpi = dpi; UiScale = uiScale;
        }
        /// <summary>Gets the actual target width in framebuffer pixels.</summary>
        public int Width { get; }
        /// <summary>Gets the actual target height in framebuffer pixels.</summary>
        public int Height { get; }
        /// <summary>Gets displayed pixel width divided by displayed pixel height.</summary>
        public double PixelAspect { get; }
        /// <summary>Gets whether the backend can resolve physical density before launch.</summary>
        public bool HasDpi { get; }
        /// <summary>Gets known physical density; callers must check HasDpi.</summary>
        public double Dpi { get; }
        /// <summary>Gets the effective presentation UI multiplier.</summary>
        public double UiScale { get; }
    }
}
