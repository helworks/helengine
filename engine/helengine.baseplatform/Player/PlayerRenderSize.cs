namespace helengine.baseplatform.Player {
    /// <summary>
    /// The size, in pixels, of the frame a platform renders during Play. The player renders into an offscreen target of
    /// exactly this size and magnifies it into the window by an integer scale.
    /// </summary>
    public sealed class PlayerRenderSize {
        /// <summary>Largest accepted width or height.</summary>
        public const int MaximumDimension = 8192;

        /// <summary>
        /// Initializes one render size.
        /// </summary>
        /// <param name="width">Width in pixels, 1 to <see cref="MaximumDimension"/>.</param>
        /// <param name="height">Height in pixels, 1 to <see cref="MaximumDimension"/>.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when either dimension is outside the accepted range.</exception>
        public PlayerRenderSize(int width, int height) {
            if (width < 1 || width > MaximumDimension) {
                throw new ArgumentOutOfRangeException(nameof(width), width, "The Play render width must be between 1 and 8192 pixels.");
            } else if (height < 1 || height > MaximumDimension) {
                throw new ArgumentOutOfRangeException(nameof(height), height, "The Play render height must be between 1 and 8192 pixels.");
            }

            Width = width;
            Height = height;
        }

        /// <summary>Gets the render width in pixels.</summary>
        public int Width { get; }

        /// <summary>Gets the render height in pixels.</summary>
        public int Height { get; }
    }
}
