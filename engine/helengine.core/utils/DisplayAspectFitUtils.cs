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
        public static int2 ResolveFittedSize(RealEvaluation liveWidth, RealEvaluation liveHeight, int referenceWidth, int referenceHeight, RealEvaluation pixelAspect) {
            return LayoutNumericKernel<RealEvaluation, RuntimeNumericOperations>.Fit(liveWidth, liveHeight, referenceWidth, referenceHeight, pixelAspect, new RuntimeNumericOperations());
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
        public static RealEvaluation ResolveGlyphScale(RealEvaluation widthScale, RealEvaluation heightScale, RealEvaluation pixelAspect) {
            ValidatePixelAspect(pixelAspect);
            return RealMath.Min(widthScale * pixelAspect, heightScale);
        }

        /// <summary>
        /// Validates a pixel aspect.
        /// </summary>
        /// <param name="pixelAspect">Displayed width of one framebuffer pixel divided by its displayed height.</param>
        static void ValidatePixelAspect(RealEvaluation pixelAspect) {
            if (!RealEvaluation.IsFinite(pixelAspect) || pixelAspect <= 0d) {
                throw new ArgumentOutOfRangeException(nameof(pixelAspect), "The pixel aspect must be finite and positive.");
            }
        }
    }
}
