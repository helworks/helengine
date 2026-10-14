namespace helengine {
    /// <summary>
    /// Fits an authored reference canvas into a live surface in display space. A surface's pixels may not be square (a
    /// console mode shown 4:3), so the fit compares displayed widths, <c>framebuffer width * pixel aspect</c>, with heights;
    /// the canvas then keeps its authored shape on the display while its framebuffer size scales differently on each
    /// axis. With a pixel aspect of 1 every expression reduces exactly to the square-pixel letterbox.
    /// </summary>
    public static class DisplayAspectFitUtils {
        /// <summary>
        /// Resolves the framebuffer size of the reference canvas fitted, centred and letterboxed, into a live surface.
        /// </summary>
        /// <param name="liveWidth">Live surface width in framebuffer pixels.</param>
        /// <param name="liveHeight">Live surface height in framebuffer pixels.</param>
        /// <param name="referenceWidth">Authored reference canvas width.</param>
        /// <param name="referenceHeight">Authored reference canvas height.</param>
        /// <param name="pixelAspect">Displayed width of one framebuffer pixel divided by its displayed height.</param>
        /// <returns>The fitted canvas size in framebuffer pixels.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown for an empty reference canvas or a non-finite or non-positive pixel aspect.</exception>
        public static int2 ResolveFittedSize(double liveWidth, double liveHeight, int referenceWidth, int referenceHeight, double pixelAspect) {
            if (referenceWidth < 1) {
                throw new ArgumentOutOfRangeException(nameof(referenceWidth), "The reference canvas width must be at least one.");
            } else if (referenceHeight < 1) {
                throw new ArgumentOutOfRangeException(nameof(referenceHeight), "The reference canvas height must be at least one.");
            }
            ValidatePixelAspect(pixelAspect);

            double expectedWidth = liveHeight * referenceWidth / (referenceHeight * pixelAspect);
            double expectedHeight = liveWidth * pixelAspect * referenceHeight / referenceWidth;
            if (Math.Abs(liveWidth - expectedWidth) <= 0.5d || Math.Abs(liveHeight - expectedHeight) <= 0.5d) {
                return new int2((int)Math.Round(liveWidth), (int)Math.Round(liveHeight));
            }

            double widthScale = liveWidth * pixelAspect / referenceWidth;
            double heightScale = liveHeight / referenceHeight;
            double scale = Math.Min(widthScale, heightScale);
            if (scale <= 0d) {
                return new int2(referenceWidth, referenceHeight);
            }

            int fittedWidth = Math.Max(1, (int)Math.Round(referenceWidth * scale / pixelAspect));
            int fittedHeight = Math.Max(1, (int)Math.Round(referenceHeight * scale));
            return new int2(fittedWidth, fittedHeight);
        }

        /// <summary>
        /// Resolves the uniform scale for glyphs, radii, outlines and other single-number layout values: the smaller of the
        /// two display-axis scales, so they never overflow their fitted boxes on the display.
        /// </summary>
        /// <param name="widthScale">Horizontal framebuffer pixels per authored pixel.</param>
        /// <param name="heightScale">Vertical framebuffer pixels per authored pixel.</param>
        /// <param name="pixelAspect">Displayed width of one framebuffer pixel divided by its displayed height.</param>
        /// <returns>The uniform scale.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown for a non-finite or non-positive pixel aspect.</exception>
        public static double ResolveGlyphScale(double widthScale, double heightScale, double pixelAspect) {
            ValidatePixelAspect(pixelAspect);
            return Math.Min(widthScale * pixelAspect, heightScale);
        }

        /// <summary>
        /// Validates a pixel aspect.
        /// </summary>
        /// <param name="pixelAspect">Displayed width of one framebuffer pixel divided by its displayed height.</param>
        static void ValidatePixelAspect(double pixelAspect) {
            if (!double.IsFinite(pixelAspect) || pixelAspect <= 0d) {
                throw new ArgumentOutOfRangeException(nameof(pixelAspect), "The pixel aspect must be finite and positive.");
            }
        }
    }
}
