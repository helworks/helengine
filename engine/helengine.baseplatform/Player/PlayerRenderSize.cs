namespace helengine.baseplatform.Player {
    /// <summary>
    /// The size, in pixels, of the frame a platform renders during Play, and the display pixel aspect it is shown at. The
    /// player renders into an offscreen target of exactly this size and magnifies it into the window: rows by an integer
    /// multiple, columns by that multiple times the pixel aspect.
    /// </summary>
    public sealed class PlayerRenderSize {
        /// <summary>Largest accepted width or height.</summary>
        public const int MaximumDimension = 8192;

        /// <summary>
        /// Initializes one render size.
        /// </summary>
        /// <param name="width">Width in pixels, 1 to <see cref="MaximumDimension"/>.</param>
        /// <param name="height">Height in pixels, 1 to <see cref="MaximumDimension"/>.</param>
        /// <param name="pixelAspect">Displayed width of one rendered pixel divided by its displayed height; 1 for square pixels.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when a dimension is outside the accepted range or the pixel aspect is not finite and positive.</exception>
        public PlayerRenderSize(int width, int height, double pixelAspect) {
            if (width < 1 || width > MaximumDimension) {
                throw new ArgumentOutOfRangeException(nameof(width), width, "The Play render width must be between 1 and 8192 pixels.");
            } else if (height < 1 || height > MaximumDimension) {
                throw new ArgumentOutOfRangeException(nameof(height), height, "The Play render height must be between 1 and 8192 pixels.");
            } else if (!double.IsFinite(pixelAspect) || pixelAspect <= 0d) {
                throw new ArgumentOutOfRangeException(nameof(pixelAspect), pixelAspect, "The Play pixel aspect must be finite and positive.");
            }

            Width = width;
            Height = height;
            PixelAspect = pixelAspect;
        }

        /// <summary>Gets the render width in pixels.</summary>
        public int Width { get; }

        /// <summary>Gets the render height in pixels.</summary>
        public int Height { get; }

        /// <summary>Gets the displayed width of one rendered pixel divided by its displayed height.</summary>
        public double PixelAspect { get; }
    }
}
